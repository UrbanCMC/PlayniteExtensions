using System;

namespace MetadataImageOptimizer.Models
{
    public class OptimizeQueueItem
    {
        public OptimizeQueueItem(Guid gameId, bool optimizeBackground, bool optimizeCover, bool optimizeIcon)
        {
            GameId = gameId;
            OptimizeBackground = optimizeBackground;
            OptimizeCover = optimizeCover;
            OptimizeIcon = optimizeIcon;
        }

        public Guid GameId { get; set; }
        public bool IsRemoved { get; private set; }
        public bool OptimizeBackground { get; set; }
        public bool OptimizeCover { get; set; }
        public bool OptimizeIcon { get; set; }

        public void Remove()
        {
            IsRemoved = true;
        }
    }
}
