// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors;
using SixLabors.ImageSharp.Processing.Processors.Effects;
using SixLabors.ImageSharp.Processing.Processors.Normalization;
using SixLabors.ImageSharp.Tests.Memory;
using SixLabors.ImageSharp.Tests.TestUtilities;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;

namespace SixLabors.ImageSharp.Tests.Processing.Normalization;

/// <summary>
/// Checks buffer ownership, window traversal and independent parallel operations on real images.
/// </summary>
/// <param name="output">Receives the requested-buffer memory measurements.</param>
public class HistogramStorageTests(ITestOutputHelper output)
{
    /// <summary>
    /// Checks exact outputs across buffer reuse and traversal choices under each SIMD feature setting.
    /// </summary>
    /// <param name="provider">An existing source photograph.</param>
    [Theory]
    [WithFile(TestImages.Exr.UncompressedFloatRgb, PixelTypes.RgbaVector)]
    [WithFile(TestImages.Png.CalliphoraPartial, PixelTypes.RgbaVector)]
    public void RealCallers_BufferReusePreservesOutput(TestImageProvider<RgbaVector> provider) => FeatureTestRunner.RunWithHwIntrinsicsFeature(
        CheckCallers,
        HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic,
        provider);

    /// <summary>
    /// Reports live allocator-owned buffers separately from managed allocation measurements.
    /// </summary>
    /// <param name="provider">The same source photograph used by the benchmark.</param>
    [Theory]
    [WithFile(TestImages.Exr.UncompressedFloatRgb, PixelTypes.RgbaVector)]
    [WithFile(TestImages.Png.CalliphoraPartial, PixelTypes.RgbaVector)]
    public void RealCallers_ReportBufferMemory(TestImageProvider<RgbaVector> provider)
    {
        using Image<RgbaVector> source = provider.GetImage();
        output.WriteLine("| Size | Caller | Peak buffer bytes | Requested bytes | Buffer rents |");
        output.WriteLine("| ---: | --- | ---: | ---: | ---: |");

        // Measure the benchmark dimensions. The shorter images below check SIMD remainders.
        foreach (int size in new[] { 512, 1024 })
        {
            using Image<RgbaVector> resized = source.Clone();
            resized.Mutate(x => x.Resize(size, size));

            foreach (IImageProcessor processor in CreateProcessors(resized.Width))
            {
                TestMemoryAllocator allocator = new();
                allocator.EnableNonThreadSafeLogging();
                Configuration configuration = Configuration.CreateDefaultInstance();
                configuration.MaxDegreeOfParallelism = 1;
                configuration.MemoryAllocator = allocator;

                using (Image<RgbaVector> image = resized.Clone(configuration))
                {
                    long initialBytes = allocator.LiveBufferBytes;
                    int initialRents = allocator.AllocationLog.Count;
                    image.Mutate(x => x.ApplyProcessor(processor));

                    long peakBuffers = allocator.PeakBufferBytes - initialBytes;
                    long requested = allocator.AllocationLog.Skip(initialRents).Sum(x => (long)x.LengthInBytes);
                    int rents = allocator.AllocationLog.Count - initialRents;
                    output.WriteLine($"| {size} | {processor.GetType().Name} | {peakBuffers} | {requested} | {rents} |");

                    // Temporary buffers must be returned while the result image remains alive.
                    Assert.Equal(initialBytes, allocator.LiveBufferBytes);
                }

                Assert.Equal(0, allocator.LiveBufferBytes);
            }
        }
    }

    /// <summary>
    /// Configures each processor for output and allocator checks.
    /// </summary>
    /// <param name="width">The image width used to select a 64-pixel sliding window.</param>
    /// <returns>One descriptor for each tested configuration.</returns>
    private static IImageProcessor[] CreateProcessors(int width) =>
    [
        new GlobalHistogramEqualizationProcessor(256, false, 350),

        // A brush size of 33 gives 17-sample neighborhood rows, including a SIMD remainder.
        new OilPaintingProcessor(128, 33),
        new AdaptiveHistogramEqualizationSlidingWindowProcessor(256, false, 350, width / 64),

        // Eight tiles match the public default and produce partial tiles on the 129-pixel input.
        new AdaptiveHistogramEqualizationProcessor(256, false, 350, 8),
        new AutoLevelProcessor(256, false, 350, true),
        new AutoLevelProcessor(256, false, 350, false)
    ];

    /// <summary>
    /// Compares cached and streamed windows, row parallelism and independent images using the normal processor lifecycle.
    /// </summary>
    /// <param name="serializedProvider">The provider passed through FeatureTestRunner.</param>
    private static void CheckCallers(string serializedProvider)
    {
        TestImageProvider<RgbaVector> provider = FeatureTestRunner.DeserializeForXunit<TestImageProvider<RgbaVector>>(serializedProvider);
        provider.Configuration.MaxDegreeOfParallelism = 1;
        using Image<RgbaVector> source = provider.GetImage();

        // A 129-pixel row exercises complete SIMD groups followed by a scalar remainder.
        source.Mutate(x => x.Resize(129, 129));

        // Each tile contains one source pixel, so its CDF is zero after subtracting that pixel's count.
        // This checks zero interpolation intervals with a real photograph and a SIMD remainder.
        using (Image<RgbaVector> singlePixelTiles = source.Clone())
        {
            singlePixelTiles.Mutate(x => x.Resize(17, 17));
            using Image<RgbaVector> expected = singlePixelTiles.Clone();
            expected.Mutate(x => x.Brightness(0F));
            singlePixelTiles.Mutate(x => x.HistogramEqualization(new HistogramEqualizationOptions
            {
                Method = HistogramEqualizationMethod.AdaptiveTileInterpolation,
                NumberOfTiles = 17
            }));

            ImageComparer.Exact.VerifySimilarity(expected, singlePixelTiles);
        }

        foreach (IImageProcessor processor in CreateProcessors(source.Width))
        {
            using Image<RgbaVector> serial = source.Clone();
            serial.Mutate(x => x.ApplyProcessor(processor));

            if (processor is AdaptiveHistogramEqualizationSlidingWindowProcessor)
            {
                // This budget cannot hold the mapped-row cache. Streamed edges must produce
                // the same output as cached columns, including reflected border samples.
                Configuration streamedConfiguration = provider.Configuration.Clone();
                streamedConfiguration.WorkingBufferSizeHintInBytes = 16 * 1024;
                streamedConfiguration.MaxDegreeOfParallelism = 4;
                using Image<RgbaVector> streamed = source.Clone(streamedConfiguration);
                streamed.Mutate(x => x.ApplyProcessor(processor));
                ImageComparer.Exact.VerifySimilarity(serial, streamed);
            }
            // A 129-pixel image gives uneven intervals. Sliding-window strips can start on odd
            // columns; both traversal directions must match the single-worker result exactly.
            Configuration parallelConfiguration = provider.Configuration.Clone();
            parallelConfiguration.MaxDegreeOfParallelism = 4;
            using Image<RgbaVector> parallel = source.Clone(parallelConfiguration);
            parallel.Mutate(x => x.ApplyProcessor(processor));
            ImageComparer.Exact.VerifySimilarity(serial, parallel);

            // Independent images use shared pools but must never share active counts or page owners.
            Parallel.For(0, 4, i =>
            {
                using Image<RgbaVector> concurrent = source.Clone();
                concurrent.Mutate(x => x.ApplyProcessor(processor));
                ImageComparer.Exact.VerifySimilarity(serial, concurrent);
            });
        }
    }
}
