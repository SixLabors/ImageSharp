using System;
using System.IO;
using System.Linq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing.Text;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Fonts;
using SixLabors.ImageSharp.Processing.Transforms;
using SixLabors.Primitives;

namespace scratchpad
{
    class Program
    {
        static void Main(string[] args)
        {
            var files = Directory.EnumerateFiles(@"../../../../Images/Input", "*.jpg", SearchOption.AllDirectories);

            foreach (var f in files)
            {
                try
                {
                    using (var image = Image.Load<Rgba32>(f))
                    {
                        using (var cloned = image.Clone(x => x.DrawText("Foo", SystemFonts.CreateFont("Arial", 10), Rgba32.Black, new SixLabors.Primitives.PointF(10, 10))))
                        {
                            cloned.Mutate(x => x.Resize(new Size(10, 10)));
                        }

                        image.Save(Guid.NewGuid().ToString() + ".png");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"{f}>{ex.Message}");
                }
            }
        }
    }
}
