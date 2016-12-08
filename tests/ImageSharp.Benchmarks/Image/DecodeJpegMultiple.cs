namespace ImageSharp.Benchmarks.Image
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.IO;
    using System.Linq;

    using BenchmarkDotNet.Attributes;

    using Image = ImageSharp.Image;
    using ImageSharpSize = ImageSharp.Size;

    public class DecodeJpegMultiple
    {
        private Dictionary<string, byte[]> fileNamesToBytes;

        public enum JpegTestingMode
        {
            All,
            SmallImagesOnly,
            LargeImagesOnly,
            CalliphoraOnly,
        }

        [Params(JpegTestingMode.All, JpegTestingMode.SmallImagesOnly,  JpegTestingMode.LargeImagesOnly, JpegTestingMode.CalliphoraOnly)]
        public JpegTestingMode Mode { get; set; }

        private IEnumerable<KeyValuePair<string, byte[]>> RequestedImages
        {
            get
            {
                int thresholdInBytes = 100000;
                
                switch (this.Mode)
                {
                    case JpegTestingMode.All:
                        return this.fileNamesToBytes;
                    case JpegTestingMode.SmallImagesOnly:
                        return this.fileNamesToBytes.Where(kv => kv.Value.Length < thresholdInBytes);
                    case JpegTestingMode.LargeImagesOnly:
                        return this.fileNamesToBytes.Where(kv => kv.Value.Length >= thresholdInBytes);
                    case JpegTestingMode.CalliphoraOnly:
                        return new[] {
                                       this.fileNamesToBytes.First(kv => kv.Key.ToLower().Contains("calliphora"))
                                   };
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }


#if BENCHMARK46
        private const string Folder = "../../../ImageSharp.Tests/TestImages/Formats/Jpg/";
#else
        private const string Folder = "../ImageSharp.Tests/TestImages/Formats/Jpg/";
#endif

        [Setup]
        public void ReadImages()
        {
            if (this.fileNamesToBytes != null) return;

            // Decoder does not work for these images (yet?):
            string[] filterWords = { "testimgari", "corrupted", "gray", "longvertical" };

            var allFiles = Directory.EnumerateFiles(Folder, "*.jpg", SearchOption.AllDirectories)
                    .Where(fn => !filterWords.Any(w => fn.ToLower().Contains(w)))
                    .ToArray();

            this.fileNamesToBytes = allFiles.ToDictionary(fn => fn, File.ReadAllBytes);
        }


        [Benchmark(Description = "DecodeJpegMultiple - ImageSharp")]
        public ImageSharpSize JpegImageSharp()
        {
            ImageSharpSize lastSize = new ImageSharpSize();
            foreach (var kv in this.RequestedImages)
            {
                using (MemoryStream memoryStream = new MemoryStream(kv.Value))
                {
                    Image image = new ImageSharp.Image(memoryStream);
                    lastSize = new ImageSharpSize(image.Width, image.Height);
                }
            }

            return lastSize;
        }

        [Benchmark(Baseline = true, Description = "DecodeJpegMultiple - System.Drawing")]
        public Size JpegSystemDrawing()
        {
            Size lastSize = new System.Drawing.Size();
            foreach (var kv in this.RequestedImages)
            {
                using (MemoryStream memoryStream = new MemoryStream(kv.Value))
                {
                    using (System.Drawing.Image image = System.Drawing.Image.FromStream(memoryStream))
                    {
                        lastSize = image.Size;
                    }
                }
            }

            return lastSize;
        }

    }
}