using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MetadataImageOptimizer.Models;
using Playnite.SDK;
using Playnite.SDK.Models;

namespace MetadataImageOptimizer.Helpers
{
    public class OptimizationCacheHelper
    {
        private const string OptimizationCacheFileName = "optimization-cache";

        private static readonly ILogger Logger = LogManager.GetLogger();
        private static readonly List<OptimizationCacheItem> OptimizationCache = new List<OptimizationCacheItem>();

        private static IPlayniteAPI api;
        private static string optimizationCacheFilePath;

        /// <summary>
        /// Adds the specified game to the optimization cache
        /// </summary>
        /// <param name="game">The game to add</param>
        public static void Add( Game game)
        {
            var backgroundPath = api.Database.GetFullFilePath(game.BackgroundImage);
            var backgroundFile = new FileInfo(backgroundPath);

            var coverPath = api.Database.GetFullFilePath(game.CoverImage);
            var coverFile = new FileInfo(coverPath);

            var iconPath = api.Database.GetFullFilePath(game.Icon);
            var iconFile = new FileInfo(iconPath);

            var existingCacheItem = OptimizationCache.FirstOrDefault(x => x.GameId == game.Id);
            if (existingCacheItem != null)
            {
                OptimizationCache.Remove(existingCacheItem);

                existingCacheItem.BackgroundFileSize = backgroundFile.Exists ? backgroundFile.Length : 0;
                existingCacheItem.CoverFileSize = coverFile.Exists ? coverFile.Length : 0;
                existingCacheItem.IconFileSize = iconFile.Exists ? iconFile.Length : 0;

                OptimizationCache.Add(existingCacheItem);
            }
            else
            {
                var cacheItem = new OptimizationCacheItem(
                    game.Id
                    , backgroundFile.Exists ? backgroundFile.Length : 0
                    , coverFile.Exists ? coverFile.Length : 0
                    , iconFile.Exists ? iconFile.Length : 0);
                OptimizationCache.Add(cacheItem);
            }
        }

        /// <summary>
        /// Clears all items from the optimization cache
        /// </summary>
        public static void Clear()
        {
            OptimizationCache.Clear();
            Save();
        }

        /// <summary>
        /// Checks whether it is necessary to check the specified game's images for needed optimization
        /// </summary>
        /// <param name="game">The game to check</param>
        /// <returns><c>true</c> if the game's images should be checked; otherwise <c>false</c></returns>
        public static bool IsCheckNecessary(Game game)
        {
            var cacheItem = OptimizationCache.FirstOrDefault(x => x.GameId == game.Id);
            if (cacheItem == null)
            {
                return true;
            }

            var backgroundPath = api.Database.GetFullFilePath(game.BackgroundImage);
            var backgroundFile = new FileInfo(backgroundPath);
            if (backgroundFile.Exists && backgroundFile.Length != cacheItem.BackgroundFileSize)
            {
                return true;
            }

            var coverPath = api.Database.GetFullFilePath(game.CoverImage);
            var coverFile = new FileInfo(coverPath);
            if (coverFile.Exists && coverFile.Length != cacheItem.CoverFileSize)
            {
                return true;
            }

            var iconPath = api.Database.GetFullFilePath(game.Icon);
            var iconFile = new FileInfo(iconPath);
            if (iconFile.Exists && iconFile.Length != cacheItem.IconFileSize)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Loads an existing optimization cache from disk
        /// </summary>
        /// <param name="api">The Playnite API</param>
        /// <param name="pluginUserDataPath">The path where the plugin's user data is stored</param>
        public static void Load(IPlayniteAPI api, string pluginUserDataPath)
        {
            OptimizationCacheHelper.api = api;

            optimizationCacheFilePath = Path.Combine(pluginUserDataPath, OptimizationCacheFileName);
            OptimizationCache.Clear();
            if (!File.Exists(optimizationCacheFilePath))
            {
                return;
            }

            var lines = File.ReadAllLines(optimizationCacheFilePath);

            foreach (var line in lines)
            {
                var parts = line.Split(',');

                if (parts.Length == 4 &&
                    Guid.TryParse(parts[0], out var gameId) &&
                    long.TryParse(parts[1], out var backgroundFileSize) &&
                    long.TryParse(parts[2], out var coverFileSize) &&
                    long.TryParse(parts[3], out var iconFileSize))
                {
                    OptimizationCache.Add(new OptimizationCacheItem(gameId, backgroundFileSize, coverFileSize, iconFileSize));
                }
                else
                {
                    Logger.Error($"Failed to parse optimize cache line: {line}");
                }
            }
        }

        /// <summary>
        /// Saves the current optimization cache to disk
        /// </summary>
        public static void Save()
        {
            lock (OptimizationCache)
            {
                File.WriteAllLines(
                    optimizationCacheFilePath
                    , OptimizationCache.Select(
                        cacheItem => $"{cacheItem.GameId},{cacheItem.BackgroundFileSize},{cacheItem.CoverFileSize},{cacheItem.IconFileSize}"));
            }
        }
    }
}
