using System;
using System.IO;
using System.Linq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

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
                        image.Save(Guid.NewGuid().ToString() + ".png");
                    }
                }
                catch(Exception ex)
                {
                    Console.WriteLine($"{f}>{ex.Message}");
                }
            }
        }
    }
}
