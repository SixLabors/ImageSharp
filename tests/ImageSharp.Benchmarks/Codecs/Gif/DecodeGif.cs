// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests;
using SDImage = System.Drawing.Image;
using SDSize = System.Drawing.Size;

namespace SixLabors.ImageSharp.Benchmarks.Codecs;

[Config(typeof(Config.Short))]
public class DecodeGif
{
    private byte[] gifBytes;

    private string TestImageFullPath
        => Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, this.TestImage);

    [GlobalSetup]
    public void ReadImages() => this.gifBytes ??= File.ReadAllBytes(this.TestImageFullPath);

    [Params(TestImages.Gif.Cheers)]
    public string TestImage { get; set; }

    [Benchmark(Baseline = true, Description = "System.Drawing Gif")]
    public SDSize GifSystemDrawing()
    {
        using MemoryStream memoryStream = new(this.gifBytes);
        using SDImage image = SDImage.FromStream(memoryStream);
        return image.Size;
    }

    [Benchmark(Description = "ImageSharp Gif")]
    public Size GifImageSharp()
    {
        using MemoryStream memoryStream = new(this.gifBytes);
        using Image<Rgba32> image = Image.Load<Rgba32>(memoryStream);
        return new(image.Width, image.Height);
    }
}
