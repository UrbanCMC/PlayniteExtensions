using System;
using System.IO;
using System.Linq;
using System.Windows.Controls;
using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;

namespace MetadataImageOptimizer
{
    public class MetadataImageOptimizer : GenericPlugin
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        private readonly IPlayniteAPI api;

        private MetadataImageOptimizerSettingsViewModel settings { get; set; }

        public override Guid Id { get; } = Guid.Parse("17b571ff-6ffe-4bea-ad25-32e52b54f9d3");

        public MetadataImageOptimizer(IPlayniteAPI api) : base(api)
        {
            this.api = api;
            settings = new MetadataImageOptimizerSettingsViewModel(this);
            Properties = new GenericPluginProperties { HasSettings = true };

            api.Database.Games.ItemUpdated += OnGameUpdated;
        }

        public override ISettings GetSettings(bool firstRunSettings)
        {
            return settings;
        }

        public override UserControl GetSettingsView(bool firstRunSettings)
        {
            return new MetadataImageOptimizerSettingsView();
        }

        private void OnGameUpdated(object sender, ItemUpdatedEventArgs<Game> e)
        {
            var optimizerSettings = settings.Settings;
            var gamesToUpdate = e.UpdatedItems;
            if (!optimizerSettings.AlwaysOptimizeOnSave)
            {
                gamesToUpdate = gamesToUpdate.Where(
                        change => change.OldData.BackgroundImage != change.NewData.BackgroundImage
                            || change.OldData.CoverImage != change.NewData.CoverImage
                            || change.OldData.Icon != change.NewData.Icon)
                    .ToList();
            }

            if (gamesToUpdate.Count == 0)
            {
                return;
            }

            foreach (var change in gamesToUpdate)
            {
                OptimizeGame(change, optimizerSettings);
            }
        }

        private void OptimizeGame(ItemUpdateEvent<Game> change, MetadataImageOptimizerSettings optimizerSettings)
        {
            var game = api.Database.Games[change.NewData.Id];

            var backgroundChanged = optimizerSettings.AlwaysOptimizeOnSave || change.OldData.BackgroundImage != change.NewData.BackgroundImage;
            var coverChanged = optimizerSettings.AlwaysOptimizeOnSave || change.OldData.CoverImage != change.NewData.CoverImage;
            var iconChanged = optimizerSettings.AlwaysOptimizeOnSave || change.OldData.Icon != change.NewData.Icon;

            var modified = false;
            if (backgroundChanged && optimizerSettings.UpdateBackground)
            {
                try
                {
                    var backgroundPath = api.Database.GetFullFilePath(game.BackgroundImage);
                    var newBackgroundPath = ImageOptimizer.Optimize(
                        backgroundPath
                        , optimizerSettings.BackgroundMaxWidth
                        , optimizerSettings.BackgroundMaxHeight
                        , optimizerSettings.PreferredFormat);
                    if (!string.Equals(newBackgroundPath, backgroundPath, StringComparison.OrdinalIgnoreCase))
                    {
                        api.Database.RemoveFile(game.BackgroundImage);
                        game.BackgroundImage = api.Database.AddFile(newBackgroundPath, game.Id);
                        File.Delete(newBackgroundPath);
                        modified = true;
                    }
                }
                catch (Exception ex)
                {
                    logger.Error(ex, $"Error while optimizing background image for '{game.Name}'.");
                }
            }

            if (coverChanged && optimizerSettings.UpdateCover)
            {
                try
                {
                    var coverPath = api.Database.GetFullFilePath(game.CoverImage);
                    var newCoverPath = ImageOptimizer.Optimize(
                        coverPath
                        , optimizerSettings.CoverMaxWidth
                        , optimizerSettings.CoverMaxHeight
                        , optimizerSettings.PreferredFormat);
                    if (!string.Equals(newCoverPath, coverPath, StringComparison.OrdinalIgnoreCase))
                    {
                        api.Database.RemoveFile(game.CoverImage);
                        game.CoverImage = api.Database.AddFile(newCoverPath, game.Id);
                        File.Delete(newCoverPath);
                        modified = true;
                    }
                }
                catch (Exception ex)
                {
                    logger.Error(ex, $"Error while optimizing cover image for '{game.Name}'.");
                }
            }

            if (iconChanged && optimizerSettings.UpdateIcon)
            {
                try
                {
                    var iconPath = api.Database.GetFullFilePath(game.Icon);
                    var newIconPath = ImageOptimizer.Optimize(
                        iconPath
                        , optimizerSettings.IconMaxWidth
                        , optimizerSettings.IconMaxHeight
                        , optimizerSettings.PreferredFormat);
                    if (!string.Equals(newIconPath, iconPath, StringComparison.OrdinalIgnoreCase))
                    {
                        api.Database.RemoveFile(game.Icon);
                        game.Icon = api.Database.AddFile(newIconPath, game.Id);
                        File.Delete(newIconPath);
                        modified = true;
                    }
                }
                catch (Exception ex)
                {
                    logger.Error(ex, $"Error while optimizing icon for '{game.Name}'.");
                }
            }

            if (modified)
            {
                api.Database.Games.Update(game);
            }
        }
    }
}
