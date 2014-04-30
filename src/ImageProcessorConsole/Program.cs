using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImageProcessorConsole
{
    using System.Drawing;
    using System.Drawing.Imaging;
    using System.IO;

    using ImageProcessor;

    class Program
    {
        static void Main(string[] args)
        {
            string path = new Uri(System.Reflection.Assembly.GetExecutingAssembly().CodeBase).LocalPath;
            // ReSharper disable once AssignNullToNotNullAttribute
            string resolvedPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path), @"..\..\images\input"));
            DirectoryInfo di = new DirectoryInfo(resolvedPath);
            if (!di.Exists)
            {
                di.Create();
            }

            FileInfo[] files = di.GetFiles("*.gif");

            foreach (FileInfo fileInfo in files)
            {
                byte[] photoBytes = File.ReadAllBytes(fileInfo.FullName);

                // ImageProcessor
                using (MemoryStream inStream = new MemoryStream(photoBytes))
                {
                    using (ImageFactory imageFactory = new ImageFactory())
                    {
                        Size size = new Size(200, 200);
                        ImageFormat format = ImageFormat.Gif;

                        // Load, resize, set the format and quality and save an image.
                        imageFactory.Load(inStream)
                            .Constrain(size)
                            .Format(format)
                            .Save(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path), @"..\..\images\output", fileInfo.Name)));
                    }
                }
            }
        }
    }
}
