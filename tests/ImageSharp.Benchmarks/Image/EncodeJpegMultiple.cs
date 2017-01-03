namespace ImageSharp.Benchmarks.Image
{
    using System;
    using System.Collections.Generic;
    using System.Drawing.Imaging;
    using System.IO;
    using System.Linq;

    using BenchmarkDotNet.Attributes;
    using BenchmarkDotNet.Attributes.Jobs;
    using BenchmarkDotNet.Engines;

    using ImageSharp.Formats;

    public class EncodeJpegMultiple : MultiImageBenchmarkBase.WithImagesPreloaded
    {
        protected override IEnumerable<string> InputImageSubfoldersOrFiles => new[]
        {
            "Bmp/",
            "Jpg/baseline"
        };

        protected override IEnumerable<string> FileFilters => new[] { "*.bmp", "*.jpg" };

        [Benchmark(Description = "EncodeJpegMultiple - ImageSharp")]
        public void EncodeJpegImageSharp()
        {
            this.ForEachImageSharpImage(
                img =>
                    {
                        MemoryStream ms = new MemoryStream();
                        img.Save(ms, new JpegEncoder());
                        return ms;
                    });
        }

        [Benchmark(Baseline = true, Description = "EncodeJpegMultiple - System.Drawing")]
        public void EncodeJpegSystemDrawing()
        {
            this.ForEachSystemDrawingImage(
                img =>
                    {
                        MemoryStream ms = new MemoryStream();
                        img.Save(ms, ImageFormat.Jpeg);
                        return ms;
                    });
        }

    }
}