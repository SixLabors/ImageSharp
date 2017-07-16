

namespace AvatarWithRoundedCorner
{
    using System;
    using System.Numerics;
    using ImageSharp;
    using SixLabors.Primitives;
    using SixLabors.Shapes;

    static class Program
    {
        static void Main(string[] args)
        {
            System.IO.Directory.CreateDirectory("output");
            using (var img = Image.Load("fb.jpg"))
            {
                // as generate returns a new IImage make sure we dispose of it
                using (Image<Rgba32> dest = img.Clone(x => x.ConvertToAvatar(new Size(200, 200), 20)))
                {
                    dest.Save("output/fb.png");
                }

                using (Image<Rgba32> destRound = img.Clone(x => x.ConvertToAvatar(new Size(200, 200), 100)))
                {
                    destRound.Save("output/fb-round.png");
                }

                using (Image<Rgba32> destRound = img.Clone(x => x.ConvertToAvatar(new Size(200, 200), 150)))
                {
                    destRound.Save("output/fb-rounder.png");
                }
                
                // the original `img` object has not been altered at all.
            }
        }

        // lets create our custom image mutating pipeline
        private static IImageProcessingContext<Rgba32> ConvertToAvatar(this IImageProcessingContext<Rgba32> operations, Size size, float cornerRadius)
        {
            return operations.Resize(new ImageSharp.Processing.ResizeOptions
            {
                Size = size,
                Mode = ImageSharp.Processing.ResizeMode.Crop
            }).Run(i => ApplyRoundedCourners(i, cornerRadius));
        }

        // the combination of `IImageOperations.Run()` + this could be replaced with an `IImageProcessor`
        public static void ApplyRoundedCourners(Image<Rgba32> img, float cornerRadius)
        {
            var corners = BuildCorners(img.Width, img.Height, cornerRadius);

            // mutating in here as we already have a cloned original
            img.Mutate(x => x.Fill(Rgba32.Transparent, corners, new GraphicsOptions(true)
            {
                BlenderMode = ImageSharp.PixelFormats.PixelBlenderMode.Src // enforces that any part of this shape that has color is punched out of the background
            }));
        }

        public static IPathCollection BuildCorners(int imageWidth, int imageHeight, float cornerRadius)
        {
            // first create a square
            var rect = new SixLabors.Shapes.RectangularePolygon(-0.5f, -0.5f, cornerRadius, cornerRadius);

            // then cut out of the square a circle so we are left with a corner
            var cornerToptLeft = rect.Clip(new SixLabors.Shapes.EllipsePolygon(cornerRadius - 0.5f, cornerRadius - 0.5f, cornerRadius));

            // corner is now a corner shape positions top left
            //lets make 3 more positioned correctly, we cando that by translating the orgional artound the center of the image
            var center = new Vector2(imageWidth / 2, imageHeight / 2);

            float rightPos = imageWidth - cornerToptLeft.Bounds.Width + 1;
            float bottomPos = imageHeight - cornerToptLeft.Bounds.Height + 1;

            // move it across the widthof the image - the width of the shape
            var cornerTopRight = cornerToptLeft.RotateDegree(90).Translate(rightPos, 0);
            var cornerBottomLeft = cornerToptLeft.RotateDegree(-90).Translate(0, bottomPos);
            var cornerBottomRight = cornerToptLeft.RotateDegree(180).Translate(rightPos, bottomPos);

            return new PathCollection(cornerToptLeft, cornerBottomLeft, cornerTopRight, cornerBottomRight);
        }
    }
}