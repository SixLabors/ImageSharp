namespace ImageSharp.Benchmarks.Image
{
    using System.Collections.Generic;
    using System.Drawing.Imaging;

    using BenchmarkDotNet.Attributes;
    using BenchmarkDotNet.Jobs;

    using ImageSharp.Formats;

    [Config(typeof(SingleRunConfig))]
    public class EncodeGifMultiple : MultiImageBenchmarkBase.WithImagesPreloaded
    {
        public class SingleRunConfig : Config
        {
            public SingleRunConfig()
            {
                this.Add(
                    Job.Default.WithLaunchCount(1)
                        .WithWarmupCount(1)
                        .WithTargetCount(1)
                        );
            }
        }

        [Params(InputImageCategory.AllImages)]
        public override InputImageCategory InputCategory { get; set; }

        protected override IEnumerable<string> InputImageSubfoldersOrFiles => new[] { "Gif/" };

        [Benchmark(Description = "EncodeGifMultiple - ImageSharp")]
        public void EncodeGifImageSharp()
        {
            this.ForEachImageSharpImage(
                (img, ms) =>
                    {
                        img.Save(ms, new GifEncoder());
                        return null;
                    });
        }

        [Benchmark(Baseline = true, Description = "EncodeGifMultiple - System.Drawing")]
        public void EncodeGifSystemDrawing()
        {
            this.ForEachSystemDrawingImage(
                (img, ms) =>
                    {
                        img.Save(ms, ImageFormat.Gif);
                        return null;
                    });
        }
    }
}