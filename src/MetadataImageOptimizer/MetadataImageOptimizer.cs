using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Controls;
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

        public override IEnumerable<MainMenuItem> GetMainMenuItems(GetMainMenuItemsArgs args)
        {
            // SOME kind of UI to be shown in the main menu if we're optimizing in the background
            // Gives users a way to kill it
            if (settings.Settings.RunInBackground &&
                _backgroundOptimizeQueue.Count > 0)
            {
                yield return new MainMenuItem()
                {
                    MenuSection = $"@MetadataImageOptimizer|Optimizing {_backgroundOptimizeQueue.Count} games",
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
            return settings;
        }

        public override UserControl GetSettingsView(bool firstRunSettings)
        {
            return new MetadataImageOptimizerSettingsView();
        }

        public override void OnApplicationStarted(OnApplicationStartedEventArgs args)
        {
            var queueFileExists = File.Exists(_backgroundOptimizeQueueFilePath);

            if (queueFileExists)
            {
                if (settings.Settings.RunInBackground)
                {
                    LoadBackgroundOptimizeQueueFromFile();
                }
                else
                {
                    File.Delete(_backgroundOptimizeQueueFilePath);
                }
            }
        }

        private void OnGameUpdated(object sender, ItemUpdatedEventArgs<Game> e)
        {
            var optimizerSettings = settings.Settings;
            var gamesToUpdate = e.UpdatedItems;

            // Ignore games where the playing/installing status was updated
            gamesToUpdate = gamesToUpdate.Where(
                    change => change.OldData.IsInstalled == change.NewData.IsInstalled
                        && change.OldData.IsInstalling == change.NewData.IsInstalling
                        && change.OldData.IsLaunching == change.NewData.IsLaunching
                        && change.OldData.IsRunning == change.NewData.IsRunning
                        && change.OldData.IsUninstalling == change.NewData.IsUninstalling)
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
            var optimizerSettings = settings.Settings;

            // Ensure we have the latest copy of {game}
            var game = this.api.Database.Games.Get(gameId);

            if (game == null)
            {
                return;
            }

            string newBackgroundPath = null;
            string newCoverPath = null;
            string newIconPath = null;

            if (optimizeBackground)
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

            if (optimizeCover)
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

            if (optimizeIcon)
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
            game = this.api.Database.Games.Get(gameId) ?? game;

            if (newBackgroundPath != null && !string.Equals(newBackgroundPath, api.Database.GetFullFilePath(game.BackgroundImage), StringComparison.OrdinalIgnoreCase))
            {
                api.Database.RemoveFile(game.BackgroundImage);
                game.BackgroundImage = api.Database.AddFile(newBackgroundPath, game.Id);
                File.Delete(newBackgroundPath);
                modified = true;
            }

            if (newCoverPath != null && !string.Equals(newCoverPath, api.Database.GetFullFilePath(game.CoverImage), StringComparison.OrdinalIgnoreCase))
            {
                api.Database.RemoveFile(game.CoverImage);
                game.CoverImage = api.Database.AddFile(newCoverPath, game.Id);
                File.Delete(newCoverPath);
                modified = true;
            }

            if (newIconPath != null && !string.Equals(newIconPath, api.Database.GetFullFilePath(game.Icon), StringComparison.OrdinalIgnoreCase))
            {
                api.Database.RemoveFile(game.Icon);
                game.Icon = api.Database.AddFile(newIconPath, game.Id);
                File.Delete(newIconPath);
                modified = true;
            }

            if (modified)
            {
                api.Database.Games.Update(game);
            }
        }

        #region Background processing
        private readonly System.Collections.Concurrent.ConcurrentQueue<Tuple<Guid, bool, bool, bool>> _backgroundOptimizeQueue =
            new System.Collections.Concurrent.ConcurrentQueue<Tuple<Guid, bool, bool, bool>>();
        private System.Threading.Thread _backgroundOptimizeThread = null;
        private string _backgroundOptimizeQueueFilePath => Path.Combine(this.GetPluginUserDataPath(), "optimize-queue");

        private void QueueOptimizeGame(Guid gameId, bool optimizeBackground, bool optimizeCover, bool optimizeIcon)
        {
            var queueItem = Tuple.Create(gameId, optimizeBackground, optimizeCover, optimizeIcon);

            File.AppendAllLines(_backgroundOptimizeQueueFilePath, new[] { $"{queueItem.Item1},{queueItem.Item2},{queueItem.Item3},{queueItem.Item4}" });
            _backgroundOptimizeQueue.Enqueue(queueItem);

            EnsureBackgroundThreadRunning();
        }

        private void EnsureBackgroundThreadRunning(bool force = false)
        {
            if (_backgroundOptimizeThread != null && _backgroundOptimizeThread.IsAlive)
            {
                if (force)
                {
                    try
                    {
                        _backgroundOptimizeThread.Abort();
                    }
                    catch (Exception) { }
                }
                else
                {
                    return;
                }
            }

            _backgroundOptimizeThread = new System.Threading.Thread(BackgroundOptimizeThread_Run) 
            { IsBackground = true, Name = nameof(MetadataImageOptimizer) };

            _backgroundOptimizeThread.Start();
        }

        private void BackgroundOptimizeThread_Run()
        {
            uint handled = 0u;

            while (!Environment.HasShutdownStarted && _backgroundOptimizeQueue.TryDequeue(out var item))
            {
                OptimizeGame(item.Item1, item.Item2, item.Item3, item.Item4);

                if (++handled % 100u == 0u)
                {
                    // Every {X} items we update the queue file to remove old items
                    // We don't do it EVERY file because then we'd be potentially hammering the disk
                    SaveBackgroundOptimizeQueueToFile();
                }
            }

            SaveBackgroundOptimizeQueueToFile();
        }

        private void ClearBackgroundOptimizeQueue()
        {
            lock (_backgroundOptimizeQueue)
            {
                while (_backgroundOptimizeQueue.Count > 0)
                {
                    _backgroundOptimizeQueue.TryDequeue(out _);
                }
            }

            SaveBackgroundOptimizeQueueToFile();
        }

        private void LoadBackgroundOptimizeQueueFromFile()
        {
            var lines = File.ReadAllLines(_backgroundOptimizeQueueFilePath);

            foreach (var line in lines)
            {
                var parts = line.Split(',');

                if (parts.Length == 4 &&
                    Guid.TryParse(parts[0], out var gameId) &&
                    bool.TryParse(parts[1], out var optimizeBackground) &&
                    bool.TryParse(parts[2], out var optimizeCover) &&
                    bool.TryParse(parts[3], out var optimizeIcon))
                {
                    _backgroundOptimizeQueue.Enqueue(Tuple.Create(gameId, optimizeBackground, optimizeCover, optimizeIcon));
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
            lock (_backgroundOptimizeQueue)
            {
                File.WriteAllLines(
                    _backgroundOptimizeQueueFilePath,
                    _backgroundOptimizeQueue.Select(queueItem => $"{queueItem.Item1},{queueItem.Item2},{queueItem.Item3},{queueItem.Item4}"));
            }
        }
        #endregion
    }
}
