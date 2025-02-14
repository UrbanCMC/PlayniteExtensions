using System;

namespace MetadataImageOptimizer.Models
{
    /// <summary>
    /// Stores information about the last time a game's images were checked with the current settings
    /// <para/>
    /// Used to avoid unnecessary IO for already verified images
    /// </summary>
    public class OptimizationCacheItem
    {
        public OptimizationCacheItem(Guid gameId, long backgroundFilesize, long coverFileSize, long iconFileSize)
        {
            GameId = gameId;
            BackgroundFileSize = backgroundFilesize;
            CoverFileSize = coverFileSize;
            IconFileSize = iconFileSize;
        }

        public Guid GameId { get; set; }

        public long BackgroundFileSize { get; set; }

        public long CoverFileSize { get; set; }

        public long IconFileSize { get; set; }
    }
}
