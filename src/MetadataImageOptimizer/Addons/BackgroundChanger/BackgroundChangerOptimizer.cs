using System;
using System.IO;
using System.Linq;
using System.Reflection;
using MetadataImageOptimizer.Addons.BackgroundChanger.Model;
using MetadataImageOptimizer.Settings;
using Playnite.SDK;
using Playnite.SDK.Data;

namespace MetadataImageOptimizer.Addons.BackgroundChanger
{
    public sealed class BackgroundChangerOptimizer : SupportedAddonBase
    {
        private const string BackgroundChangerAssemblyName = "BackgroundChanger, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null";

        private static readonly ILogger logger = LogManager.GetLogger();

        private readonly IPlayniteAPI api;
        private readonly string imageBasePath;
        private readonly MetadataImageOptimizerSettings settings;

        private object backgroundChangerDb;
        private Func<Guid, bool, bool, object> getGameConfigFunc;
        private MethodInfo updateMethodInfo;

        public BackgroundChangerOptimizer(IPlayniteAPI api, MetadataImageOptimizerSettings settings)
        {
            this.api = api;
            imageBasePath = api.Paths.ExtensionsDataPath + $@"\{ExtensionId}\Images";
            this.settings = settings;
        }

        public override Guid ExtensionId { get; } = new Guid("3afdd02b-db6c-4b60-8faa-2971d6dfad2a");
        public override bool IsInstalled => api.Addons.Plugins.Any(x => x.Id  == ExtensionId);

        public override void OptimizeImages(Guid gameId)
        {
            var gameConfigPath = api.Paths.ExtensionsDataPath + $@"\{ExtensionId}\BackgroundChanger\{gameId}.json";
            if (!File.Exists(gameConfigPath))
            {
                return;
            }

            var gameConfig = Serialization.FromJsonFile<GameConfig>(gameConfigPath);
            foreach (var image in gameConfig.Items.Where(x => !string.IsNullOrWhiteSpace(x.FolderName)))
            {
                if (!image.IsCover && settings.Background.Optimize)
                {
                    OptimizeImage(image, settings.Background);
                }
                else if (image.IsCover && settings.Cover.Optimize)
                {
                    OptimizeImage(image, settings.Cover);
                }
            }

            RefreshBackgroundChangerDB(gameId, gameConfig);
        }

        private void OptimizeImage(GameImage image, ImageTypeSettings imageSettings)
        {
            var imagePath = Path.Combine(imageBasePath, image.FolderName, image.Name);
            var tmpPath = ImageOptimizer.Optimize(imagePath, imageSettings, settings.Quality);
            if (tmpPath == imagePath)
            {
                return;
            }

            var optimizedPath = Path.Combine(imageBasePath, image.FolderName, Path.GetFileName(tmpPath));
            File.Delete(imagePath);
            File.Move(tmpPath, optimizedPath);
            image.Name = Path.GetFileName(optimizedPath);
        }

        private void FindBackgroundChangerDb()
        {
            if (backgroundChangerDb != null)
            {
                return;
            }

            var bcType = Type.GetType($"BackgroundChanger.BackgroundChanger, {BackgroundChangerAssemblyName}");
            if (bcType == null)
            {
                logger.Warn("Failed to find BackgroundChanger through reflection. Most likely this indicates a change in the addon.");
                return;
            }

            var pluginType = Type.GetType($"BackgroundChanger.Controls.PluginBackgroundImage, {BackgroundChangerAssemblyName}");
            if (pluginType == null)
            {
                logger.Warn("Failed to find PluginBackgroundImage through reflection. Most likely this indicates a change in the addon.");
                return;
            }

            var pluginDbProp = pluginType.GetProperty("PluginDatabase", BindingFlags.Static | BindingFlags.NonPublic);
            if (pluginDbProp == null)
            {
                logger.Warn("Failed to find PluginDatabase through reflection. Most likely this indicates a change in the addon.");
                return;
            }

            backgroundChangerDb =  pluginDbProp.GetValue(null);

            var getMethod = backgroundChangerDb.GetType().GetMethod("Get", new[] { typeof(Guid), typeof(bool), typeof(bool) });
            if (getMethod == null)
            {
                logger.Warn("Failed to find PluginDatabase.Get through reflection. Most likely this indicates a change in the addon.");
                return;
            }

            var updateMethod = backgroundChangerDb.GetType().GetMethod("Update", BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy);
            if (updateMethod == null)
            {
                logger.Warn("Failed to find PluginDatabase.Update through reflection. Most likely this indicates a change in the addon.");
                return;
            }

            getGameConfigFunc = (Func<Guid, bool, bool, object>)Delegate.CreateDelegate(typeof(Func<Guid, bool, bool, object>), backgroundChangerDb, getMethod);
            updateMethodInfo = updateMethod;
        }

        private void RefreshBackgroundChangerDB(Guid gameId, GameConfig config)
        {
            // Ensure required reflection objects exist
            FindBackgroundChangerDb();

            var itemImageType = Type.GetType($"BackgroundChanger.Models.ItemImage, {BackgroundChangerAssemblyName}");
            if (itemImageType == null)
            {
                logger.Warn("Failed to find ItemImageType through reflection. Most likely this indicates a change in the addon.");
                return;
            }

            dynamic dbConfig = getGameConfigFunc(gameId, false, false);
            dbConfig.Items.Clear();

            foreach (var image in config.Items)
            {
                dynamic itemImage = Activator.CreateInstance(itemImageType);
                itemImage.Name = image.Name;
                itemImage.FolderName = image.FolderName;
                itemImage.IsCover = image.IsCover;
                itemImage.IsDefault = image.IsDefault;
                itemImage.IsFavorite = image.IsFavorite;

                dbConfig.Items.Add(itemImage);
            }

            updateMethodInfo.Invoke(backgroundChangerDb, new[] { dbConfig });
        }
    }
}
