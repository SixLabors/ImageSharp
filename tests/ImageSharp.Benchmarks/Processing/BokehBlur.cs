// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Tests;

namespace SixLabors.ImageSharp.Benchmarks.Processing;

/* Measured 2026-10-21: .NET 10.0.13, AMD Ryzen AI MAX+ 395, 1024 by 1024 RgbaVectorP inputs.

| Method | FileName                  |     Mean |    Error |   StdDev |     Gen0 |     Gen1 |     Gen2 | Allocated |
| ------ | ------------------------- | -------: | -------: | -------: | -------: | -------: | -------: | --------: |
| Blur   | Exr/openexr_comp_none.exr | 21.05 ms | 0.926 ms | 0.613 ms | 250.0000 | 250.0000 | 250.0000 | 209.88 KB |
| Blur   | Png/CalliphoraPartial.png | 19.44 ms | 0.513 ms | 0.340 ms | 250.0000 | 250.0000 | 250.0000 |  211.9 KB |
*/

/// <summary>
/// Measures Bokeh processing for SDR and HDR input values, excluding decoding and input restoration.
/// </summary>
[Config(typeof(Config.Standard))]
public class BokehBlur
{
    private const int BatchSize = 4;
    private Image<RgbaVectorP> source;
    private Image<RgbaVectorP>[] working;

    /// <summary>
    /// Gets or sets the SDR or HDR source fixture.
    /// </summary>
    [Params(TestImages.Png.CalliphoraPartial, TestImages.Exr.OpenExrHdrHalf)]
    public string FileName { get; set; }

    /// <summary>
    /// Loads and resizes the source to 1024 by 1024 pixels outside measurement.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        this.source = Image.Load<RgbaVectorP>(Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, this.FileName));
        this.source.Mutate(x => x.Resize(1024, 1024));

        this.working = new Image<RgbaVectorP>[BatchSize];
    }

    /// <summary>
    /// Restores the four input images outside measurement. The batch matches the recorded benchmark's pool reuse.
    /// </summary>
    [IterationSetup]
    public void Reset()
    {
        for (int i = 0; i < this.working.Length; i++)
        {
            this.working[i] = this.source.Clone();
        }
    }

    /// <summary>
    /// Disposes the processed images outside measurement.
    /// </summary>
    [IterationCleanup]
    public void Release()
    {
        foreach (Image<RgbaVectorP> image in this.working)
        {
            image.Dispose();
        }
    }

    /// <summary>
    /// Disposes the immutable source after measurement.
    /// </summary>
    [GlobalCleanup]
    public void Cleanup() => this.source.Dispose();

    /// <summary>
    /// Measures default bokeh processing. BenchmarkDotNet reports elapsed time and managed allocations per image.
    /// </summary>
    [Benchmark(OperationsPerInvoke = BatchSize)]
    public void Blur()
    {
        foreach (Image<RgbaVectorP> image in this.working)
        {
            image.Mutate(x => x.BokehBlur());
        }
    }
}
