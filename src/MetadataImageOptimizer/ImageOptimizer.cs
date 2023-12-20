using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Media.Imaging;
using MetadataImageOptimizer.Settings;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;
using Image = SixLabors.ImageSharp.Image;

namespace MetadataImageOptimizer
{
    public static class ImageOptimizer
    {
        /// <summary>
        /// Optimizes an image with the specified requirements
        /// </summary>
        /// <param name="imagePath">The full path to the image that should be optimized</param>
        /// <param name="imageSettings">The settings to use for optimizing the image</param>
        /// <param name="qualitySettings">The quality settings for optimizing the image</param>
        /// <returns>The path of the optimized image</returns>
        public static string Optimize(string imagePath, ImageTypeSettings imageSettings, QualitySettings qualitySettings)
        {
            var imageExtension = Path.GetExtension(imagePath);
            if (!string.IsNullOrWhiteSpace(imageExtension))
            {
                imageExtension = imageExtension.Substring(1);
            }

            if (imageExtension == "ico")
            {
                imagePath = IcoToHighQualityImage(imagePath);
            }

            using (var image = Image.Load(imagePath))
            {
                var modified = false;

                var heightMult = (double)imageSettings.MaxHeight / image.Height;
                var widthMult = (double)imageSettings.MaxWidth / image.Width;
                if (image.Height > imageSettings.MaxHeight && heightMult < widthMult)
                {
                    image.Mutate(x => x.Resize(0, imageSettings.MaxHeight));
                    modified = true;
                }
                else if (image.Width > imageSettings.MaxWidth)
                {
                    image.Mutate(x => x.Resize(imageSettings.MaxWidth, 0));
                    modified = true;
                }

                if (imageExtension != imageSettings.Format)
                {
                    var filename = Path.ChangeExtension(Guid.NewGuid().ToString(), imageSettings.Format.ToLower());
                    var newPath = Path.Combine(Path.GetTempPath(), filename);
                    switch (imageSettings.Format.ToUpper())
                    {
                        case "BMP":
                            image.SaveAsBmp(newPath);
                            break;
                        case "JPG":
                            image.SaveAsJpeg(newPath, new JpegEncoder { Quality = qualitySettings.JpgQuality });
                            break;
                        case "PNG":
                            image.SaveAsPng(newPath, new PngEncoder { CompressionLevel = qualitySettings.PngCompressionLevel });
                            break;
                        case "WEBP":
                            image.SaveAsWebp(newPath, new WebpEncoder { Method = qualitySettings.WebpEncodingMethod, Quality = qualitySettings.WebpQuality });
                            break;
                    }

                    return newPath;
                }

                if (modified)
                {
                    image.Save(imagePath);
                }

                return imagePath;
            }
        }

        private static string IcoToHighQualityImage(string imagePath)
        {
            using (var fs = File.OpenRead(imagePath))
            {
                var decoder = new IconBitmapDecoder(fs, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.None);
                using (var pngStream = new MemoryStream())
                {
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(decoder.Frames.OrderByDescending(x => x.Height).First());
                    encoder.Save(pngStream);

                    var filename = Path.ChangeExtension(Guid.NewGuid().ToString(), "png");
                    var newPath = Path.Combine(Path.GetTempPath(), filename);

                    var bmp = new Bitmap(pngStream);
                    bmp.Save(newPath);

                    return newPath;
                }
            }
        }
    }
}
