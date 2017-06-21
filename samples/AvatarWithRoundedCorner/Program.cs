

namespace AvatarWithRoundedCorner
{
    using System;
    using System.Numerics;
    using ImageSharp;
    using SixLabors.Shapes;

    class Program
    {
        static void Main(string[] args)
        {
            System.IO.Directory.CreateDirectory("output");

            GenerateAvatar("fb.jpg", "output/fb.png", new ImageSharp.Size(200, 200), 20);
            GenerateAvatar("fb.jpg", "output/fb-round.png", new ImageSharp.Size(200, 200), 100);
            GenerateAvatar("fb.jpg", "output/fb-rounder.png", new ImageSharp.Size(200, 200), 150);
        }

        private static void GenerateAvatar(string source, string destination, ImageSharp.Size size, float cornerRadius)
        {
            using (var image = Image.Load(source))
            {
                image.Resize(new ImageSharp.Processing.ResizeOptions
                {
                    Size = size,
                    Mode = ImageSharp.Processing.ResizeMode.Crop
                });

                ApplyRoundedCourners(image, cornerRadius);
                image.Save(destination);
            }
        }

        public static void ApplyRoundedCourners(Image<Rgba32> img, float cornerRadius)
        {
            var corners = BuildCorners(img.Width, img.Height, cornerRadius);
            // now we have our corners time to draw them
            img.Fill(Rgba32.Transparent, corners, new GraphicsOptions(true)
            {
                BlenderMode = ImageSharp.PixelFormats.PixelBlenderMode.Src // enforces that any part of this shape that has color is punched out of the background
            });
        }

        public static  IPathCollection BuildCorners(int imageWidth, int imageHeight, float cornerRadius)
        {
            // first create a square
            var rect = new SixLabors.Shapes.Rectangle(-0.5f, -0.5f, cornerRadius, cornerRadius);

            // then cut out of the square a circle so we are left with a corner
            var cornerToptLeft = rect.Clip(new SixLabors.Shapes.Ellipse(cornerRadius-0.5f, cornerRadius - 0.5f, cornerRadius));

            // corner is now a corner shape positions top left
            //lets make 3 more positioned correctly, we cando that by translating the orgional artound the center of the image
            var center = new Vector2(imageWidth / 2, imageHeight / 2);
            var angle = Math.PI / 2f;

            float rightPos = imageWidth - cornerToptLeft.Bounds.Width  +1;
            float bottomPos = imageHeight - cornerToptLeft.Bounds.Height + 1;

            // move it across the widthof the image - the width of the shape
            var cornerTopRight = cornerToptLeft.RotateDegree(90).Translate(rightPos, 0);
            var cornerBottomLeft = cornerToptLeft.RotateDegree(-90).Translate(0, bottomPos);
            var cornerBottomRight = cornerToptLeft.RotateDegree(180).Translate(rightPos, bottomPos);

            return new PathCollection(cornerToptLeft, cornerBottomLeft, cornerTopRight, cornerBottomRight);
        }
    }
}