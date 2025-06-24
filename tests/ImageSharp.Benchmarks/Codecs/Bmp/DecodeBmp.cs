// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests;
using SDImage = System.Drawing.Image;
using SDSize = System.Drawing.Size;

namespace SixLabors.ImageSharp.Benchmarks.Codecs;

[Config(typeof(Config.Short))]
public class DecodeBmp
{
    private byte[] bmpBytes;

    private string TestImageFullPath
        => Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, this.TestImage);

    [GlobalSetup]
    public void ReadImages()
        => this.bmpBytes ??= File.ReadAllBytes(this.TestImageFullPath);

    [Params(TestImages.Bmp.Car)]
    public string TestImage { get; set; }

    [Benchmark(Baseline = true, Description = "System.Drawing Bmp")]
    public SDSize BmpSystemDrawing()
    {
        using MemoryStream memoryStream = new(this.bmpBytes);
        using SDImage image = SDImage.FromStream(memoryStream);
        return image.Size;
    }

    [Benchmark(Description = "ImageSharp Bmp")]
    public Size BmpImageSharp()
    {
        using MemoryStream memoryStream = new(this.bmpBytes);
        using Image<Rgba32> image = Image.Load<Rgba32>(memoryStream);
        return new(image.Width, image.Height);
    }
}
