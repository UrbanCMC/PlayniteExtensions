using System.Collections.Generic;

namespace MetadataImageOptimizer.Settings
{
    public class ImageTypeSettings : ObservableObject
    {
        private string format;
        private int maxHeight;
        private int maxWidth;
        private bool optimize;

        public string Format { get => format; set => SetValue(ref format, value); }
        public int MaxHeight { get => maxHeight; set => SetValue(ref maxHeight, value); }
        public int MaxWidth { get => maxWidth; set => SetValue(ref maxWidth, value); }
        public bool Optimize { get => optimize; set => SetValue(ref optimize, value); }
    }
}
