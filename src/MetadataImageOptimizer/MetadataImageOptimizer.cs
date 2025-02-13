using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Controls;
using MetadataImageOptimizer.Addons;
using MetadataImageOptimizer.Addons.BackgroundChanger;
using MetadataImageOptimizer.Models;
using MetadataImageOptimizer.Settings;
using MetadataImageOptimizer.Views;
using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;

namespace MetadataImageOptimizer
{
    public class MetadataImageOptimizer : GenericPlugin
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        private readonly IPlayniteAPI api;
        private readonly List<SupportedAddonBase> supportedAddons;

        private MetadataImageOptimizerSettingsViewModel settingsVm { get; set; }
        private Guid inProcessGameId;

        public override Guid Id { get; } = Guid.Parse("17b571ff-6ffe-4bea-ad25-32e52b54f9d3");

        public MetadataImageOptimizer(IPlayniteAPI api) : base(api)
        {
            this.api = api;
            settingsVm = new MetadataImageOptimizerSettingsViewModel(this);
            supportedAddons = new List<SupportedAddonBase> { new BackgroundChangerOptimizer(api, settingsVm) };
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
                    OptimizeGames(actionArgs.Games, settingsVm.Settings);
                }
            };
        }

        public override IEnumerable<MainMenuItem> GetMainMenuItems(GetMainMenuItemsArgs args)
        {
            // SOME kind of UI to be shown in the main menu if we're optimizing in the background
            // Gives users a way to kill it
            if (settingsVm.Settings.RunInBackground &&
                backgroundOptimizeQueue.Count > 0)
            {
                yield return new MainMenuItem()
                {
                    MenuSection = $"@MetadataImageOptimizer|Optimizing {backgroundOptimizeQueue.Count} games",
                    Description = "Cancel",
                    Action = (a) =>
                    {
                        ClearBackgroundOptimizeQueue();
                    }
                };
            }
        }

        public override ISettings GetSettings(bool firstRunSettings)
        {
            return settingsVm;
        }

        public override UserControl GetSettingsView(bool firstRunSettings)
        {
            return new MetadataImageOptimizerSettingsView();
        }


        private void OnGameUpdated(object sender, ItemUpdatedEventArgs<Game> e)
        {
            var optimizerSettings = settingsVm.Settings;
            var gamesToUpdate = e.UpdatedItems;

            // Ignore games where the playing/installing status was updated
            gamesToUpdate = gamesToUpdate.Where(
                    change => change.OldData.IsInstalled == change.NewData.IsInstalled
                        && change.OldData.IsInstalling == change.NewData.IsInstalling
                        && change.OldData.IsLaunching == change.NewData.IsLaunching
                        && change.OldData.IsRunning == change.NewData.IsRunning
                        && change.OldData.IsUninstalling == change.NewData.IsUninstalling)
                .ToList();

            // Ignore the game we are currently updating
            gamesToUpdate = gamesToUpdate.Where(change => change.OldData.Id != inProcessGameId)
                .ToList();

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

            if (optimizerSettings.RunInBackground)
            {
                foreach (var change in gamesToUpdate)
                {
                    OptimizeGame(change, optimizerSettings);
                }
            }
            else
            {
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
        }

        private void OptimizeGames(List<Game> games, MetadataImageOptimizerSettings optimizerSettings)
        {
            if (games.Count == 0)
            {
                return;
            }


            if (optimizerSettings.RunInBackground)
            {
                foreach (var game in games)
                {
                    QueueOptimizeGame(
                        game.Id
                        , optimizerSettings.Background.Optimize
                        , optimizerSettings.Cover.Optimize
                        , optimizerSettings.Icon.Optimize);
                }

                api.Dialogs.ShowMessage($"Queued up {games.Count} games to optimize. Optimizing in the background.", "Queued up optimization");
            }
            else
            {
                api.Dialogs.ActivateGlobalProgress(
                    globalProgress =>
                    {
                        globalProgress.ProgressMaxValue = games.Count;

                        foreach (var game in games)
                        {
                            OptimizeGame(
                                game.Id
                                , optimizerSettings.Background.Optimize
                                , optimizerSettings.Cover.Optimize
                                , optimizerSettings.Icon.Optimize);

                            globalProgress.CurrentProgressValue += 1;
                        }
                    }
                    , new GlobalProgressOptions("Optimizing game images...", false) { IsIndeterminate = games.Count == 1 });
            }
        }

        private void OptimizeGame(ItemUpdateEvent<Game> change, MetadataImageOptimizerSettings optimizerSettings)
        {
            var game = api.Database.Games[change.NewData.Id];

            var backgroundChanged = optimizerSettings.AlwaysOptimizeOnSave || change.OldData.BackgroundImage != change.NewData.BackgroundImage;
            var coverChanged = optimizerSettings.AlwaysOptimizeOnSave || change.OldData.CoverImage != change.NewData.CoverImage;
            var iconChanged = optimizerSettings.AlwaysOptimizeOnSave || change.OldData.Icon != change.NewData.Icon;

            if (backgroundChanged || coverChanged || iconChanged)
            {
                if (optimizerSettings.RunInBackground)
                {
                    QueueOptimizeGame(game.Id, backgroundChanged, coverChanged, iconChanged);
                }
                else
                {
                    OptimizeGame(game.Id, backgroundChanged, coverChanged, iconChanged);
                }
            }
        }

        private void OptimizeGame(Guid gameId, bool optimizeBackground, bool optimizeCover, bool optimizeIcon)
        {
            var modified = false;
            var optimizerSettings = settingsVm.Settings;

            // Ensure we have the latest copy of {game}
            var game = api.Database.Games.Get(gameId);
            if (game == null)
            {
                return;
            }

            string newBackgroundPath = null;
            string newCoverPath = null;
            string newIconPath = null;

            if (optimizeBackground && !string.IsNullOrWhiteSpace(game.BackgroundImage))
            {
                try
                {
                    var backgroundPath = api.Database.GetFullFilePath(game.BackgroundImage);
                    newBackgroundPath = ImageOptimizer.Optimize(backgroundPath, optimizerSettings.Background, optimizerSettings.Quality);
                }
                catch (Exception ex)
                {
                    logger.Error(ex, $"Error while optimizing background image for '{game.Name}'.");
                }
            }

            if (optimizeCover && !string.IsNullOrWhiteSpace(game.CoverImage))
            {
                try
                {
                    var coverPath = api.Database.GetFullFilePath(game.CoverImage);
                    newCoverPath = ImageOptimizer.Optimize(coverPath, optimizerSettings.Cover, optimizerSettings.Quality);
                }
                catch (Exception ex)
                {
                    logger.Error(ex, $"Error while optimizing cover image for '{game.Name}'.");
                }
            }

            if (optimizeIcon && !string.IsNullOrWhiteSpace(game.Icon))
            {
                try
                {
                    var iconPath = api.Database.GetFullFilePath(game.Icon);
                    newIconPath = ImageOptimizer.Optimize(iconPath, optimizerSettings.Icon, optimizerSettings.Quality);
                }
                catch (Exception ex)
                {
                    logger.Error(ex, $"Error while optimizing icon for '{game.Name}'.");
                }
            }

            // Ensure we have the latest copy of {game} again in case it changed during the optimization
            game = api.Database.Games.Get(gameId) ?? game;

            if (newBackgroundPath != null && !string.Equals(newBackgroundPath, api.Database.GetFullFilePath(game.BackgroundImage), StringComparison.OrdinalIgnoreCase))
            {
                PlayniteApi.Database.RemoveFile(game.BackgroundImage);
                game.BackgroundImage = api.Database.AddFile(newBackgroundPath, game.Id);
                File.Delete(newBackgroundPath);
                modified = true;
            }

            if (newCoverPath != null && !string.Equals(newCoverPath, api.Database.GetFullFilePath(game.CoverImage), StringComparison.OrdinalIgnoreCase))
            {
                PlayniteApi.Database.RemoveFile(game.CoverImage);
                game.CoverImage = api.Database.AddFile(newCoverPath, game.Id);
                File.Delete(newCoverPath);
                modified = true;
            }

            if (newIconPath != null && !string.Equals(newIconPath, api.Database.GetFullFilePath(game.Icon), StringComparison.OrdinalIgnoreCase))
            {
                PlayniteApi.Database.RemoveFile(game.Icon);
                game.Icon = api.Database.AddFile(newIconPath, game.Id);
                File.Delete(newIconPath);
                modified = true;
            }

            // Process images from supported addons
            supportedAddons.Where(x => x.IsInstalled).ForEach(addon => addon.OptimizeImages(game.Id));

            if (modified)
            {
                api.Database.Games.Update(game);
            }
        }

        #region Background processing

        private readonly ConcurrentQueue<OptimizeQueueItem> backgroundOptimizeQueue = new ConcurrentQueue<OptimizeQueueItem>();
        private Thread backgroundOptimizeThread;
        private string BackgroundOptimizeQueueFilePath => Path.Combine(GetPluginUserDataPath(), "optimize-queue");

        public override void OnApplicationStarted(OnApplicationStartedEventArgs args)
        {
            var queueFileExists = File.Exists(BackgroundOptimizeQueueFilePath);
            if (queueFileExists)
            {
                if (settingsVm.Settings.RunInBackground)
                {
                    LoadBackgroundOptimizeQueueFromFile();
                }
                else
                {
                    File.Delete(BackgroundOptimizeQueueFilePath);
                }
            }
        }

        private void QueueOptimizeGame(Guid gameId, bool optimizeBackground, bool optimizeCover, bool optimizeIcon)
        {
            var queueItem = new OptimizeQueueItem(gameId, optimizeBackground, optimizeCover, optimizeIcon);

            var existingItem = backgroundOptimizeQueue.FirstOrDefault(x => x.GameId == gameId);
            if (existingItem != null)
            {
                existingItem.OptimizeBackground |= optimizeBackground;
                existingItem.OptimizeCover |= optimizeCover;
                existingItem.OptimizeIcon |= optimizeIcon;
            }
            else
            {
                backgroundOptimizeQueue.Enqueue(queueItem);
            }

            EnsureBackgroundThreadRunning();
        }

        private void EnsureBackgroundThreadRunning(bool force = false)
        {
            if (backgroundOptimizeThread != null && backgroundOptimizeThread.IsAlive)
            {
                if (force)
                {
                    try
                    {
                        backgroundOptimizeThread.Abort();
                    }
                    catch { }
                }
                else
                {
                    return;
                }
            }

            backgroundOptimizeThread = new Thread(BackgroundOptimizeThread_Run)
            { IsBackground = true, Name = nameof(MetadataImageOptimizer) };

            backgroundOptimizeThread.Start();
        }

        private void BackgroundOptimizeThread_Run()
        {
            var handled = 0u;

            while (!Environment.HasShutdownStarted && backgroundOptimizeQueue.TryDequeue(out var item))
            {
                inProcessGameId = item.GameId;
                OptimizeGame(item.GameId, item.OptimizeBackground, item.OptimizeCover, item.OptimizeIcon);

                if (++handled % 100u == 0u)
                {
                    // Every {X} items we update the queue file to remove old items
                    // We don't do it EVERY file because then we'd be potentially hammering the disk
                    SaveBackgroundOptimizeQueueToFile();
                }

                if (!settingsVm.Settings.RunInBackground)
                {
                    // Clear remaining queue if background processing is turned off
                    logger.Info($"Background processing was turned off. Cleared queue of {backgroundOptimizeQueue.Count} items.");
                    api.Dialogs.ShowMessage(
                        $"Background processing was turned off. Cleared queue of {backgroundOptimizeQueue.Count} items."
                        , "MetadataImageOptimizer - Stopped background processing");
                    ClearBackgroundOptimizeQueue();
                    inProcessGameId = Guid.Empty;
                    return;
                }
            }

            inProcessGameId = Guid.Empty;
            SaveBackgroundOptimizeQueueToFile();
        }

        private void ClearBackgroundOptimizeQueue()
        {
            lock (backgroundOptimizeQueue)
            {
                while (backgroundOptimizeQueue.Count > 0)
                {
                    backgroundOptimizeQueue.TryDequeue(out _);
                }
            }

            SaveBackgroundOptimizeQueueToFile();
        }

        private void LoadBackgroundOptimizeQueueFromFile()
        {
            var lines = File.ReadAllLines(BackgroundOptimizeQueueFilePath);

            foreach (var line in lines)
            {
                var parts = line.Split(',');

                if (parts.Length == 4 &&
                    Guid.TryParse(parts[0], out var gameId) &&
                    bool.TryParse(parts[1], out var optimizeBackground) &&
                    bool.TryParse(parts[2], out var optimizeCover) &&
                    bool.TryParse(parts[3], out var optimizeIcon))
                {
                    backgroundOptimizeQueue.Enqueue(new OptimizeQueueItem(gameId, optimizeBackground, optimizeCover, optimizeIcon));
                }
                else
                {
                    logger.Error($"Failed to parse optimize queue line: {line}");
                }
            }

            EnsureBackgroundThreadRunning();
        }

        private void SaveBackgroundOptimizeQueueToFile()
        {
            lock (backgroundOptimizeQueue)
            {
                File.WriteAllLines(
                    BackgroundOptimizeQueueFilePath
                    , backgroundOptimizeQueue.Select(
                        queueItem => $"{queueItem.GameId},{queueItem.OptimizeBackground},{queueItem.OptimizeCover},{queueItem.OptimizeIcon}"));
            }
        }
        #endregion
    }
}
