using System;
using System.IO;
using System.Linq;
using System.Reflection;
using MetadataImageOptimizer.Addons.BackgroundChanger.Model;
using MetadataImageOptimizer.Settings;
using MetadataImageOptimizer.Views;
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
        private readonly MetadataImageOptimizerSettingsViewModel settingsVm;

        private object backgroundChangerDb;
        private Func<Guid, bool, bool, object> getGameConfigFunc;
        private MethodInfo updateMethodInfo;

        public BackgroundChangerOptimizer(IPlayniteAPI api, MetadataImageOptimizerSettingsViewModel settingsVm)
        {
            this.api = api;
            imageBasePath = api.Paths.ExtensionsDataPath + $@"\{ExtensionId}\Images";
            this.settingsVm = settingsVm;
        }

        public override Guid ExtensionId { get; } = new Guid("3afdd02b-db6c-4b60-8faa-2971d6dfad2a");
        public override bool IsInstalled => api.Addons.Plugins.Any(x => x.Id  == ExtensionId);

        public override void OptimizeImages(Guid gameId)
        {
            if (!settingsVm.Settings.AddonSettings.BackgroundChangerOptimize)
            {
                return;
            }

            var gameConfigPath = api.Paths.ExtensionsDataPath + $@"\{ExtensionId}\BackgroundChanger\{gameId}.json";
            if (!File.Exists(gameConfigPath))
            {
                return;
            }

            // Ensure required reflection objects exist
            if (!FindBackgroundChangerDb())
            {
                DisableBackgroundChangerOptimize();
                return;
            }

            var gameConfig = Serialization.FromJsonFile<GameConfig>(gameConfigPath);
            foreach (var image in gameConfig.Items.Where(x => !string.IsNullOrWhiteSpace(x.FolderName)))
            {
                if (!image.IsCover && settingsVm.Settings.Background.Optimize)
                {
                    OptimizeImage(image, settingsVm.Settings.Background);
                }
                else if (image.IsCover && settingsVm.Settings.Cover.Optimize)
                {
                    OptimizeImage(image, settingsVm.Settings.Cover);
                }
            }

            if (!RefreshBackgroundChangerDB(gameId, gameConfig))
            {
                DisableBackgroundChangerOptimize();
            }
        }

        private void DisableBackgroundChangerOptimize()
        {
            // Current version of background changer is not compatible!
            api.Notifications.Add(
                "metadataimageoptimizer-backgroundchanger-incompatible"
                , "This version of MetadataImageOptimizer is not compatible with your version of BackgroundChanger!\nThe 'Optimize' setting for BackgroundChanger has been turned off."
                , NotificationType.Error);
            settingsVm.Settings.AddonSettings.BackgroundChangerOptimize = false;
            settingsVm.EndEdit();
        }

        private void OptimizeImage(GameImage image, ImageTypeSettings imageSettings)
        {
            var imagePath = Path.Combine(imageBasePath, image.FolderName, image.Name);
            var tmpPath = ImageOptimizer.Optimize(imagePath, imageSettings, settingsVm.Settings.Quality);
            if (tmpPath == imagePath)
            {
                return;
            }

            var optimizedPath = Path.Combine(imageBasePath, image.FolderName, Path.GetFileName(tmpPath));
            File.Delete(imagePath);
            File.Move(tmpPath, optimizedPath);
            image.Name = Path.GetFileName(optimizedPath);
        }

        private bool FindBackgroundChangerDb()
        {
            if (backgroundChangerDb != null)
            {
                return true;
            }

            var bcType = Type.GetType($"BackgroundChanger.BackgroundChanger, {BackgroundChangerAssemblyName}");
            if (bcType == null)
            {
                logger.Warn("Failed to find BackgroundChanger through reflection. Most likely this indicates a change in the addon.");
                return false;
            }

            var pluginType = Type.GetType($"BackgroundChanger.Controls.PluginBackgroundImage, {BackgroundChangerAssemblyName}");
            if (pluginType == null)
            {
                logger.Warn("Failed to find PluginBackgroundImage through reflection. Most likely this indicates a change in the addon.");
                return false;
            }

            var pluginDbProp = pluginType.GetProperty("PluginDatabase", BindingFlags.Static | BindingFlags.NonPublic);
            if (pluginDbProp == null)
            {
                logger.Warn("Failed to find PluginDatabase through reflection. Most likely this indicates a change in the addon.");
                return false;
            }

            backgroundChangerDb =  pluginDbProp.GetValue(null);

            var getMethod = backgroundChangerDb.GetType().GetMethod("Get", new[] { typeof(Guid), typeof(bool), typeof(bool) });
            if (getMethod == null)
            {
                logger.Warn("Failed to find PluginDatabase.Get through reflection. Most likely this indicates a change in the addon.");
                return false;
            }

            var updateMethod = backgroundChangerDb.GetType().GetMethod("Update", BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy);
            if (updateMethod == null)
            {
                logger.Warn("Failed to find PluginDatabase.Update through reflection. Most likely this indicates a change in the addon.");
                return false;
            }

            getGameConfigFunc = (Func<Guid, bool, bool, object>)Delegate.CreateDelegate(typeof(Func<Guid, bool, bool, object>), backgroundChangerDb, getMethod);
            updateMethodInfo = updateMethod;
            return true;
        }

        private bool RefreshBackgroundChangerDB(Guid gameId, GameConfig config)
        {
            var itemImageType = Type.GetType($"BackgroundChanger.Models.ItemImage, {BackgroundChangerAssemblyName}");
            if (itemImageType == null)
            {
                logger.Warn("Failed to find ItemImageType through reflection. Most likely this indicates a change in the addon.");
                return false;
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
            return true;
        }
    }
}
