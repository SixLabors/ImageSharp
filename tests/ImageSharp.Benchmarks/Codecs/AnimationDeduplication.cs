// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests;

namespace SixLabors.ImageSharp.Benchmarks.Codecs;

// Measured 2026-10-09: BenchmarkDotNet 0.15.8, .NET 10.0.12, AMD Ryzen AI MAX+ 395.
// | Method  | Categories | FileName             | Blend | Mean      | Error     | StdDev    | Ratio | Gen0   | Allocated | Alloc Ratio |
// |-------- |----------- |--------------------- |------ |----------:|----------:|----------:|------:|-------:|----------:|------------:|
// | Bytes   | Rgba32     | Png/a(...)g.png [21] | False |  3.236 us | 0.0231 us | 0.0153 us |  1.00 | 0.0076 |     144 B |        1.00 |
// | Vectors | RgbaVector | Png/a(...)g.png [21] | False |  3.776 us | 0.0300 us | 0.0198 us |  1.17 | 0.0076 |     144 B |        1.00 |
// |         |            |                      |       |           |           |           |       |        |           |             |
// | Bytes   | Rgba32     | Png/a(...)g.png [21] | True  |  3.258 us | 0.0299 us | 0.0198 us |  1.00 | 0.0076 |     144 B |        1.00 |
// | Vectors | RgbaVector | Png/a(...)g.png [21] | True  |  3.986 us | 0.0064 us | 0.0034 us |  1.22 | 0.0076 |     144 B |        1.00 |
// |         |            |                      |       |           |           |           |       |        |           |             |
// | Bytes   | Rgba32     | Png/a(...)t.png [29] | False | 12.917 us | 0.0334 us | 0.0221 us |  1.00 |      - |     144 B |        1.00 |
// | Vectors | RgbaVector | Png/a(...)t.png [29] | False | 28.496 us | 0.2192 us | 0.1450 us |  2.21 |      - |     144 B |        1.00 |
// |         |            |                      |       |           |           |           |       |        |           |             |
// | Bytes   | Rgba32     | Png/a(...)t.png [29] | True  | 13.189 us | 0.0552 us | 0.0365 us |  1.00 |      - |     144 B |        1.00 |
// | Vectors | RgbaVector | Png/a(...)t.png [29] | True  | 29.440 us | 0.1351 us | 0.0804 us |  2.23 |      - |     144 B |        1.00 |

/// <summary>
/// Compares byte and Vector4 deduplication on the same animation fixtures.
/// </summary>
[MemoryDiagnoser]
[DisassemblyDiagnoser(maxDepth: 3, printSource: true, exportCombinedDisassemblyReport: true)]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByParams)]
[CategoriesColumn]
public class AnimationDeduplication
{
    private Image<Rgba32> source;
    private Image<Rgba32> destination;
    private ImageFrame<Rgba32> nextFrame;
    private Image<RgbaVector> vectorSource;
    private Image<RgbaVector> vectorDestination;
    private ImageFrame<RgbaVector> nextVectorFrame;

    /// <summary>
    /// Gets or sets the existing animation fixture.
    /// </summary>
    [Params(TestImages.Png.APng, TestImages.Png.FrameOffset)]
    public string FileName { get; set; }

    /// <summary>
    /// Gets or sets whether duplicate pixels are replaced with transparent pixels.
    /// </summary>
    [Params(false, true)]
    public bool Blend { get; set; }

    /// <summary>
    /// Loads byte and floating-point frames and checks output pixels and bounds outside the measured invocations.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        this.source = Image.Load<Rgba32>(Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, this.FileName));
        this.destination = new(this.source.Configuration, this.source.Width, this.source.Height);
        this.vectorSource = this.source.CloneAs<RgbaVector>();
        this.vectorDestination = new(this.source.Configuration, this.source.Width, this.source.Height);

        // Both paths receive the same colors. RgbaVector selects the floating-point path through PixelTypeInfo.
        // Cloning occurs only during setup. Validate every transition before measuring the first transition.
        for (int frame = 1; frame < this.source.Frames.Count; frame++)
        {
            ImageFrame<Rgba32> previous = this.source.Frames[frame - 1];
            ImageFrame<Rgba32> current = this.source.Frames[frame];
            ImageFrame<Rgba32> next = frame + 1 < this.source.Frames.Count ? this.source.Frames[frame + 1] : null;
            (bool Difference, Rectangle Bounds) bytes = AnimationUtilities.DeDuplicatePixels(
                this.source.Configuration, previous, current, next, this.destination.Frames.RootFrame, Color.Transparent, this.Blend, ClampingMode.None);

            ImageFrame<RgbaVector> vectorPrevious = this.vectorSource.Frames[frame - 1];
            ImageFrame<RgbaVector> vectorCurrent = this.vectorSource.Frames[frame];
            ImageFrame<RgbaVector> vectorNext = frame + 1 < this.vectorSource.Frames.Count ? this.vectorSource.Frames[frame + 1] : null;
            ImageFrame<RgbaVector> vectorResult = this.vectorDestination.Frames.RootFrame;
            (bool Difference, Rectangle Bounds) vectors = AnimationUtilities.DeDuplicatePixels(
                this.source.Configuration, vectorPrevious, vectorCurrent, vectorNext, vectorResult, Color.Transparent, this.Blend, ClampingMode.None);

            if (bytes != vectors)
            {
                throw new InvalidOperationException($"Vector4 deduplication bounds differ for {this.FileName}, frame {frame}, blend {this.Blend}.");
            }

            for (int y = 0; y < this.source.Height; y++)
            {
                ReadOnlySpan<Rgba32> expected = this.destination.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(y);

                // These floating frames contain the same byte-origin colors. Only their storage and comparison path differ.
                ReadOnlySpan<RgbaVector> actualVectors = this.vectorDestination.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(y);
                for (int x = 0; x < expected.Length; x++)
                {
                    if (!actualVectors[x].Equals(RgbaVector.FromRgba32(expected[x])))
                    {
                        throw new InvalidOperationException(
                            $"Vector4 deduplication pixels differ for {this.FileName}, frame {frame}, row {y}, blend {this.Blend}.");
                    }
                }
            }
        }

        this.nextFrame = this.source.Frames.Count > 2 ? this.source.Frames[2] : null;
        this.nextVectorFrame = this.vectorSource.Frames.Count > 2 ? this.vectorSource.Frames[2] : null;
    }

    /// <summary>
    /// Releases source and destination images after measurement.
    /// </summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        this.source.Dispose();
        this.destination.Dispose();
        this.vectorSource.Dispose();
        this.vectorDestination.Dispose();
    }

    /// <summary>
    /// Measures byte deduplication, including row conversion and result packing, on Rgba32 frames.
    /// </summary>
    /// <returns>The difference flag and changed bounds.</returns>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Rgba32")]
    public (bool Difference, Rectangle Bounds) Bytes()
    {
        ImageFrame<Rgba32> previous = this.source.Frames[0];
        ImageFrame<Rgba32> current = this.source.Frames[1];
        ImageFrame<Rgba32> result = this.destination.Frames.RootFrame;
        return AnimationUtilities.DeDuplicatePixels(
            this.source.Configuration, previous, current, this.nextFrame, result, Color.Transparent, this.Blend, ClampingMode.None);
    }

    /// <summary>
    /// Measures Vector4 deduplication, including native row conversion and result packing, on RgbaVector frames.
    /// </summary>
    /// <returns>The difference flag and changed bounds.</returns>
    [Benchmark]
    [BenchmarkCategory("RgbaVector")]
    public (bool Difference, Rectangle Bounds) Vectors()
    {
        ImageFrame<RgbaVector> previous = this.vectorSource.Frames[0];
        ImageFrame<RgbaVector> current = this.vectorSource.Frames[1];
        ImageFrame<RgbaVector> result = this.vectorDestination.Frames.RootFrame;
        return AnimationUtilities.DeDuplicatePixels(
            this.source.Configuration, previous, current, this.nextVectorFrame, result, Color.Transparent, this.Blend, ClampingMode.None);
    }
}
