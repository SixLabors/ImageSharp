// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Drawing.Imaging;
using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.Formats.Bmp;

namespace SixLabors.ImageSharp.Benchmarks.Codecs;

[Config(typeof(Config.Short))]
public class EncodeBmpMultiple : MultiImageBenchmarkBase.WithImagesPreloaded
{
    protected override IEnumerable<string> InputImageSubfoldersOrFiles => ["Bmp/", "Jpg/baseline"];

    [Benchmark(Description = "EncodeBmpMultiple - ImageSharp")]
    public void EncodeBmpImageSharp()
        => this.ForEachImageSharpImage((img, ms) =>
        {
            img.Save(ms, new BmpEncoder());
            return null;
        });

    [Benchmark(Baseline = true, Description = "EncodeBmpMultiple - System.Drawing")]
    public void EncodeBmpSystemDrawing()
        => this.ForEachSystemDrawingImage((img, ms) =>
        {
            img.Save(ms, ImageFormat.Bmp);
            return null;
        });
}
