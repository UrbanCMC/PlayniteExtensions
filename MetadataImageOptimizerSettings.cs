using System.Collections.Generic;

namespace MetadataImageOptimizer
{
    public class MetadataImageOptimizerSettings : ObservableObject
    {
        private bool alwaysOptimizeOnSave;
        private int backgroundMaxHeight = 1920;
        private int backgroundMaxWidth = 1080;
        private int coverMaxHeight = 600;
        private int coverMaxWidth = 900;
        private int iconMaxHeight = 256;
        private int iconMaxWidth = 256;
        private string preferredFormat;
        private bool updateBackground;
        private bool updateCover;
        private bool updateIcon;

        public bool AlwaysOptimizeOnSave { get => alwaysOptimizeOnSave; set => SetValue(ref alwaysOptimizeOnSave, value); }
        public int BackgroundMaxHeight { get => backgroundMaxHeight; set => SetValue(ref backgroundMaxHeight, value); }
        public int BackgroundMaxWidth { get => backgroundMaxWidth; set => SetValue(ref backgroundMaxWidth, value); }
        public int CoverMaxHeight { get => coverMaxHeight; set => SetValue(ref coverMaxHeight, value); }
        public int CoverMaxWidth { get => coverMaxWidth; set => SetValue(ref coverMaxWidth, value); }
        public int IconMaxHeight { get => iconMaxHeight; set => SetValue(ref iconMaxHeight, value); }
        public int IconMaxWidth { get => iconMaxWidth; set => SetValue(ref iconMaxWidth, value); }
        public string PreferredFormat { get => preferredFormat; set => SetValue(ref preferredFormat, value); }
        public bool UpdateBackground { get => updateBackground; set => SetValue(ref updateBackground, value); }
        public bool UpdateCover { get => updateCover; set => SetValue(ref updateCover, value); }
        public bool UpdateIcon { get => updateIcon; set => SetValue(ref updateIcon, value); }
    }
}
