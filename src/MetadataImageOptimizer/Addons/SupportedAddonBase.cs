using System;

namespace MetadataImageOptimizer.Addons
{
    public abstract class SupportedAddonBase
    {
        public abstract Guid ExtensionId { get; }

        public abstract bool IsInstalled { get; }

        public abstract void OptimizeImages(Guid gameId);
    }
}
