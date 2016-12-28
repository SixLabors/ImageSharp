using System.Collections.Generic;

namespace ImageSharp.Benchmarks.Image
{
    using System.Drawing;
    using System.IO;
    using BenchmarkDotNet.Attributes;

    using Image = ImageSharp.Image;
    using ImageSharpSize = ImageSharp.Size;

    public class DecodeJpegMultiple : MultiImageBenchmarkBase
    {
        protected override IEnumerable<string> InputImageSubfolders => new[]
        {
            "Formats/Jpg/"
        };

        protected override IEnumerable<string> FileFilters => new[] { "*.jpg" };

        [Benchmark(Description = "DecodeJpegMultiple - ImageSharp")]
        public void DecodeJpegImageSharp()
        {
            this.ForEachStream(
                ms => new ImageSharp.Image(ms)
                );  
        }

        [Benchmark(Baseline = true, Description = "DecodeJpegMultiple - System.Drawing")]
        public void DecodeJpegSystemDrawing()
        {
            this.ForEachStream(
                System.Drawing.Image.FromStream
                );
        }

    }
}