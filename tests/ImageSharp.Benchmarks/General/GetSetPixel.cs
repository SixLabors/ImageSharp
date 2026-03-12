// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

#if OS_WINDOWS
using System.Drawing;
using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Benchmarks;

public class GetSetPixel
{
    [Benchmark(Baseline = true, Description = "System.Drawing GetSet pixel")]
    public System.Drawing.Color GetSetSystemDrawing()
    {
        using Bitmap source = new(400, 400);
        source.SetPixel(200, 200, System.Drawing.Color.White);
        return source.GetPixel(200, 200);
    }

    [Benchmark(Description = "ImageSharp GetSet pixel")]
    public Rgba32 GetSetImageSharp()
    {
        using Image<Rgba32> image = new(400, 400);
        image[200, 200] = Color.White.ToPixel<Rgba32>();
        return image[200, 200];
    }
}
#endif
