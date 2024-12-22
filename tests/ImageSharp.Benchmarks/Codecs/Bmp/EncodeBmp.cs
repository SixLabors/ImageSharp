// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Drawing.Imaging;
using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests;
using SDImage = System.Drawing.Image;

namespace SixLabors.ImageSharp.Benchmarks.Codecs;

[Config(typeof(Config.Short))]
public class EncodeBmp
{
    private Stream bmpStream;
    private SDImage bmpDrawing;
    private Image<Rgba32> bmpCore;

    [GlobalSetup]
    public void ReadImages()
    {
        if (this.bmpStream == null)
        {
            this.bmpStream = File.OpenRead(Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, TestImages.Bmp.Car));
            this.bmpCore = Image.Load<Rgba32>(this.bmpStream);
            this.bmpStream.Position = 0;
            this.bmpDrawing = SDImage.FromStream(this.bmpStream);
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.bmpStream.Dispose();
        this.bmpStream = null;
        this.bmpCore.Dispose();
        this.bmpDrawing.Dispose();
    }

    [Benchmark(Baseline = true, Description = "System.Drawing Bmp")]
    public void BmpSystemDrawing()
    {
        using MemoryStream memoryStream = new MemoryStream();
        this.bmpDrawing.Save(memoryStream, ImageFormat.Bmp);
    }

    [Benchmark(Description = "ImageSharp Bmp")]
    public void BmpImageSharp()
    {
        using MemoryStream memoryStream = new MemoryStream();
        this.bmpCore.SaveAsBmp(memoryStream);
    }
}
