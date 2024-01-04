using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Controls;
using MetadataImageOptimizer.Settings;
using MetadataImageOptimizer.Views;
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

        public override IEnumerable<GameMenuItem> GetGameMenuItems(GetGameMenuItemsArgs args)
        {
            yield return new GameMenuItem
            {
                Description = "Optimize images",
                MenuSection = "MetadataImageOptimizer",
                Action = actionArgs =>
                {
                    OptimizeGames(actionArgs.Games, settings.Settings);
                }
            };
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

            api.Dialogs.ActivateGlobalProgress(
                globalProgress =>
                {
                    globalProgress.ProgressMaxValue = gamesToUpdate.Count;

                    foreach (var change in gamesToUpdate)
                    {
                        OptimizeGame(change, optimizerSettings);

                        globalProgress.CurrentProgressValue += 1;
                    }
                }
                , new GlobalProgressOptions("Optimizing game images...", false) { IsIndeterminate = gamesToUpdate.Count == 1 });
        }

        private void OptimizeGames(List<Game> games, MetadataImageOptimizerSettings optimizerSettings)
        {
            if (games.Count == 0)
            {
                return;
            }

            api.Dialogs.ActivateGlobalProgress(
                globalProgress =>
                {
                    globalProgress.ProgressMaxValue = games.Count;

                    foreach (var game in games)
                    {
                        OptimizeGame(
                            game
                            , optimizerSettings
                            , optimizerSettings.Background.Optimize
                            , optimizerSettings.Cover.Optimize
                            , optimizerSettings.Icon.Optimize);

                        globalProgress.CurrentProgressValue += 1;
                    }
                }
                , new GlobalProgressOptions("Optimizing game images...", false) { IsIndeterminate = games.Count == 1 });
        }

        private void OptimizeGame(ItemUpdateEvent<Game> change, MetadataImageOptimizerSettings optimizerSettings)
        {
            var game = api.Database.Games[change.NewData.Id];

            var backgroundChanged = optimizerSettings.AlwaysOptimizeOnSave || change.OldData.BackgroundImage != change.NewData.BackgroundImage;
            var coverChanged = optimizerSettings.AlwaysOptimizeOnSave || change.OldData.CoverImage != change.NewData.CoverImage;
            var iconChanged = optimizerSettings.AlwaysOptimizeOnSave || change.OldData.Icon != change.NewData.Icon;

            OptimizeGame(game, optimizerSettings, backgroundChanged, coverChanged, iconChanged);
        }

        private void OptimizeGame(Game game, MetadataImageOptimizerSettings optimizerSettings, bool optimizeBackground, bool optimizeCover, bool optimizeIcon)
        {
            var modified = false;

            if (optimizeBackground)
            {
                try
                {
                    var backgroundPath = api.Database.GetFullFilePath(game.BackgroundImage);
                    var newBackgroundPath = ImageOptimizer.Optimize(backgroundPath, optimizerSettings.Background, optimizerSettings.Quality);
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

            if (optimizeCover)
            {
                try
                {
                    var coverPath = api.Database.GetFullFilePath(game.CoverImage);
                    var newCoverPath = ImageOptimizer.Optimize(coverPath, optimizerSettings.Cover, optimizerSettings.Quality);
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

            if (optimizeIcon)
            {
                try
                {
                    var iconPath = api.Database.GetFullFilePath(game.Icon);
                    var newIconPath = ImageOptimizer.Optimize(iconPath, optimizerSettings.Icon, optimizerSettings.Quality);
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
