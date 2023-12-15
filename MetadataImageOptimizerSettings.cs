using System.Collections.Generic;

namespace MetadataImageOptimizer
{
    public class MetadataImageOptimizerSettings : ObservableObject
    {
        private bool alwaysOptimizeOnSave;
        private int backgroundMaxHeight = 1080;
        private int backgroundMaxWidth = 1920;
        private int coverMaxHeight = 900;
        private int coverMaxWidth = 600;
        private int iconMaxHeight = 256;
        private int iconMaxWidth = 256;
        private bool optimizeBackground;
        private bool optimizeCover;
        private bool optimizeIcon;
        private string preferredFormat = "png";

        public List<string> AvailableImageFormats { get; } = new List<string> { "bmp", "jpg", "png" };

        public bool AlwaysOptimizeOnSave { get => alwaysOptimizeOnSave; set => SetValue(ref alwaysOptimizeOnSave, value); }
        public int BackgroundMaxHeight { get => backgroundMaxHeight; set => SetValue(ref backgroundMaxHeight, value); }
        public int BackgroundMaxWidth { get => backgroundMaxWidth; set => SetValue(ref backgroundMaxWidth, value); }
        public int CoverMaxHeight { get => coverMaxHeight; set => SetValue(ref coverMaxHeight, value); }
        public int CoverMaxWidth { get => coverMaxWidth; set => SetValue(ref coverMaxWidth, value); }
        public int IconMaxHeight { get => iconMaxHeight; set => SetValue(ref iconMaxHeight, value); }
        public int IconMaxWidth { get => iconMaxWidth; set => SetValue(ref iconMaxWidth, value); }
        public bool OptimizeBackground { get => optimizeBackground; set => SetValue(ref optimizeBackground, value); }
        public bool OptimizeCover { get => optimizeCover; set => SetValue(ref optimizeCover, value); }
        public bool OptimizeIcon { get => optimizeIcon; set => SetValue(ref optimizeIcon, value); }
        public string PreferredFormat { get => preferredFormat; set => SetValue(ref preferredFormat, value); }
    }
}
