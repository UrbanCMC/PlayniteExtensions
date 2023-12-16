using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Media.Imaging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
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
        /// <param name="maxWidth">The maximum width allowed for the image</param>
        /// <param name="maxHeight">The maximum height allowed for the image</param>
        /// <param name="preferredFormat">The name of the format the image should be saved in (e.g. jpg, png)</param>
        /// <returns>The path of the optimized image</returns>
        public static string Optimize(string imagePath, int maxWidth, int maxHeight, string preferredFormat)
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

                var heightMult = (double)maxHeight / image.Height;
                var widthMult = (double)maxWidth / image.Width;
                if (image.Height > maxHeight && heightMult < widthMult)
                {
                    image.Mutate(x => x.Resize(0, maxHeight));
                    modified = true;
                }
                else if (image.Width > maxWidth)
                {
                    image.Mutate(x => x.Resize(maxWidth, 0));
                    modified = true;
                }

                if (imageExtension != preferredFormat)
                {
                    var filename = Path.ChangeExtension(Guid.NewGuid().ToString(), preferredFormat.ToLower());
                    var newPath = Path.Combine(Path.GetTempPath(), filename);
                    switch (preferredFormat.ToUpper())
                    {
                        case "BMP":
                            image.SaveAsBmp(newPath);
                            break;
                        case "JPG":
                            image.SaveAsJpeg(newPath, new JpegEncoder { Quality = 90 });
                            break;
                        case "PNG":
                            image.SaveAsPng(newPath);
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
