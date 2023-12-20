using System;
using System.Collections.Generic;
using Playnite.SDK.Data;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;

namespace MetadataImageOptimizer.Settings
{
    public class QualitySettings : ObservableObject
    {
        private int jpgQuality;
        private PngCompressionLevel pngCompressionLevel;
        private WebpEncodingMethod webpEncodingMethod;
        private int webpQuality;

        [DontSerialize]
        public Dictionary<PngCompressionLevel, string> AvailablePngCompressionLevels { get; }
            = new Dictionary<PngCompressionLevel, string>
            {
                { PngCompressionLevel.Level0, "0 (No compression)" }
                , { PngCompressionLevel.Level1, "1" }
                , { PngCompressionLevel.Level2, "2" }
                , { PngCompressionLevel.Level3, "3" }
                , { PngCompressionLevel.Level4, "4" }
                , { PngCompressionLevel.Level5, "5" }
                , { PngCompressionLevel.Level6, "6 (Default)" }
                , { PngCompressionLevel.Level7, "7" }
                , { PngCompressionLevel.Level8, "8" }
                , { PngCompressionLevel.Level9, "9 (Highest compression)" }
            };

        [DontSerialize]
        public Dictionary<WebpEncodingMethod, string> AvailableWebpEncodingMethods { get; }
            = new Dictionary<WebpEncodingMethod, string>
            {
                { WebpEncodingMethod.Level0, "0 (Fastest)" }
                , { WebpEncodingMethod.Level1, "1" }
                , { WebpEncodingMethod.Level2, "2" }
                , { WebpEncodingMethod.Level3, "3" }
                , { WebpEncodingMethod.Level4, "4 (Default)" }
                , { WebpEncodingMethod.Level5, "5" }
                , { WebpEncodingMethod.Level6, "6 (Slowest)" }
            };

        public int JpgQuality
        {
            get => jpgQuality;
            set
            {
                value = Math.Min(100, value);
                value = Math.Max(1, value);

                SetValue(ref jpgQuality, value);
            }
        }

        public PngCompressionLevel PngCompressionLevel { get => pngCompressionLevel; set => SetValue(ref pngCompressionLevel, value); }

        public WebpEncodingMethod WebpEncodingMethod { get => webpEncodingMethod; set => SetValue(ref webpEncodingMethod, value); }

        public int WebpQuality
        {
            get => webpQuality;
            set
            {
                value = Math.Min(100, value);
                value = Math.Max(0, value);

                SetValue(ref webpQuality, value);
            }
        }
    }
}
