using System.Collections.Generic;

namespace MetadataImageOptimizer.Settings
{
    public class AddonSettings : ObservableObject
    {
        private bool backgroundChangerOptimize;

        public bool BackgroundChangerOptimize { get => backgroundChangerOptimize; set => SetValue(ref backgroundChangerOptimize, value); }
    }
}
