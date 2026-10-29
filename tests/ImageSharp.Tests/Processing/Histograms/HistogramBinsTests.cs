// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Histograms;
using SixLabors.ImageSharp.Processing.Processors.Normalization;
using SixLabors.ImageSharp.Tests.Memory;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Processing.Histograms;

/// <summary>
/// Checks count removal, implicit zero bins, clipping and page reuse independently of image processing.
/// </summary>
public class HistogramBinsTests
{
    /// <summary>
    /// Checks page rounding without allocating storage for large bin counts.
    /// </summary>
    /// <param name="binCount">The logical bin count.</param>
    /// <param name="expectedPages">The number of 128-bin pages needed to contain the bins.</param>
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(127, 1)]
    [InlineData(128, 1)]
    [InlineData(129, 2)]
    [InlineData(int.MaxValue - 127, 16_777_215)]
    [InlineData(int.MaxValue, 16_777_216)]
    public void GetPageCount_RoundsUpWithoutOverflow(int binCount, int expectedPages)
    {
        // int.MaxValue is one bin short of 16,777,216 full pages. Subtracting another 127 bins
        // gives exactly 16,777,215 pages. Neither case needs a large allocation to test the calculation.
        Assert.Equal(expectedPages, Histogram<Histogram.CountBin>.Data.GetPageCount(binCount));
    }

    /// <summary>
    /// Checks cumulative counts and redistribution into previously empty pages at every SIMD setting.
    /// </summary>
    [Fact]
    public void Counts_ClippingCdfAndReuseHaveIdenticalSemantics() => FeatureTestRunner.RunWithHwIntrinsicsFeature(
        CheckCounts,
        HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Checks that the actual histogram runner releases its storage after normal and exceptional callbacks.
    /// </summary>
    /// <param name="throwFromOperation">Whether the consumer calculation throws.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OperationStorage_IsReleased(bool throwFromOperation)
    {
        TestMemoryAllocator allocator = new();
        allocator.EnableNonThreadSafeLogging();
        Configuration configuration = Configuration.Default.Clone();
        configuration.MemoryAllocator = allocator;
        configuration.MaxDegreeOfParallelism = 1;

        // Decode a real fixture with the default allocator so only the runner's working memory
        // is tracked below. Source pixels remain alive while its temporary buffers are released.
        using Image<Rgba32> image = Image.Load<Rgba32>(TestFile.Create(TestImages.Png.Gray4Bpp).Bytes);
        CleanupOperation operation = new(throwFromOperation);
        if (throwFromOperation)
        {
            Assert.Throws<InvalidOperationException>(() => Histogram.Apply<Rgba32, Histogram.CountBin, Histogram.Cdf, CleanupOperation>(
                configuration, image.Frames.RootFrame, image.Bounds, 256, new Vector3(1F / 3F), false, 0, Histogram.Region.Global, in operation));
        }
        else
        {
            Histogram.Apply<Rgba32, Histogram.CountBin, Histogram.Cdf, CleanupOperation>(
                configuration, image.Frames.RootFrame, image.Bounds, 256, new Vector3(1F / 3F), false, 0, Histogram.Region.Global, in operation);
        }

        Assert.Equal(0, allocator.LiveBufferBytes);
    }

    /// <summary>
    /// Checks independently calculated cumulative values, source-count retention and page reuse.
    /// </summary>
    private static void CheckCounts()
    {
        TestMemoryAllocator allocator = new();
        allocator.EnableNonThreadSafeLogging();
        Configuration configuration = Configuration.Default.Clone();
        configuration.MemoryAllocator = allocator;
        HistogramIndexMapping mapping = new(1024, -1);

        using (Histogram<Histogram.CountBin> histogram = new(configuration, mapping, 0))
        {
            Histogram<Histogram.CountBin>.Data data = histogram.GetData();
            Assert.Equal(0, data.GetCdf(0).Cumulative);
            Assert.Equal(0, data.GetCdf(0).Minimum);

            // Repeated samples return to earlier pages. Removing one sample must retain the other occurrences.
            ReadOnlySpan<int> samples = [128, 128, 768, 768, 128, 768];
            data.Add(samples, ReadOnlySpan<Vector4>.Empty, 1);
            data.Add(768, default, -1);
            Assert.Equal(0, data.ReadBin(512).Count);
            Assert.Equal(2, data.GetCdf(768).Cumulative);
            Assert.Equal(3, data.GetCdf(768).Minimum);

            // Count-only bins skip maximum tracking during updates. A mode query must still inspect the current counts.
            Assert.Equal(3, data.GetMostFrequent().Count);

            // Capping counts 3 and 2 at one releases three samples. Redistribution places them
            // at 0, 341 and 682, including pages that did not previously store any counts.
            data.Clip(1);
            ReadOnlySpan<int> occupied = [0, 128, 341, 682, 768];
            foreach (int index in occupied)
            {
                Assert.Equal(1, data.ReadBin(index).Count);
            }

            Assert.Equal(4, data.GetCdf(768).Cumulative);
            Assert.Equal(1, data.GetCdf(768).Minimum);

            data.PrepareCdf();
            for (int i = 0; i < occupied.Length; i++)
            {
                Assert.Equal(i, data.ReadBin(occupied[i]).Count);
            }

            int rents = allocator.AllocationLog.Count;
            data.Clear();
            Assert.Equal(0, data.ReadBin(768).Count);
            data.Add(768, default, 1);
            Assert.Equal(rents, allocator.AllocationLog.Count);
        }

        Assert.Equal(0, allocator.LiveBufferBytes);

        using (Histogram<Histogram.CountBin> original = new(configuration, mapping, 0))
        using (Histogram<Histogram.CountBin> clipped = new(configuration, mapping, 0))
        {
            Histogram<Histogram.CountBin>.Query query = new(original.GetData());
            query.SetClippedStorage(clipped.GetData());
            query.Current.Add(128, default, 3);
            query.Current.Add(768, default, 2);
            query.PrepareClippedQuery(1);

            Assert.Equal(4, query.GetCdf(768).Cumulative);
            Assert.Equal(1, query.GetCdf(768).Minimum);
            Assert.Equal(3, query.Current.ReadBin(128).Count);
            Assert.Equal(2, query.Current.ReadBin(768).Count);

            // Removing one sample produces four occupied bins after redistribution. Repeated
            // queries must reuse the copy without accumulating its preceding query's counts.
            query.Current.Add(128, default, -1);
            query.PrepareClippedQuery(1);
            Assert.Equal(3, query.GetCdf(768).Cumulative);
            query.PrepareClippedQuery(1);
            Assert.Equal(3, query.GetCdf(768).Cumulative);

            query.Current.Clear();
            query.PrepareClippedQuery(1);
            Assert.Equal(0, query.GetCdf(768).Cumulative);
        }

        Assert.Equal(0, allocator.LiveBufferBytes);
    }

    /// <summary>
    /// Throws from the consumer callback when requested; its output calculation otherwise leaves pixels unchanged.
    /// </summary>
    /// <param name="throwFromOperation">Whether to check exceptional cleanup.</param>
    private readonly struct CleanupOperation(bool throwFromOperation) : IHistogramOperation<Histogram.CountBin, Histogram.Cdf>
    {
        /// <inheritdoc />
        public Histogram.Cdf GetResult(ref Histogram<Histogram.CountBin>.Query histogram, Vector4 source, int index)
        {
            if (throwFromOperation)
            {
                throw new InvalidOperationException("Check histogram cleanup after a calculation throws.");
            }

            return histogram.GetCdf(index);
        }

        /// <inheritdoc />
        public Vector4 Invoke(Vector4 source, in Histogram.Cdf data) => source;

        /// <inheritdoc />
        public Vector128<float> Invoke(Vector128<float> source, ReadOnlySpan<Histogram.Cdf> data) => source;

        /// <inheritdoc />
        public Vector256<float> Invoke(Vector256<float> source, ReadOnlySpan<Histogram.Cdf> data) => source;

        /// <inheritdoc />
        public Vector512<float> Invoke(Vector512<float> source, ReadOnlySpan<Histogram.Cdf> data) => source;
    }
}
