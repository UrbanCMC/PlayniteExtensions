using System;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

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
        /// <param name="preferredFormat">The name of the format the image should be saved in (e.g. jpeg, png)</param>
        /// <returns></returns>
        public static string Optimize(string imagePath, int maxWidth, int maxHeight, string preferredFormat)
        {
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

                var imageExtension = Path.GetExtension(imagePath).Substring(1);
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
                        case "JPEG":
                            image.SaveAsJpeg(newPath);
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
    }
}
