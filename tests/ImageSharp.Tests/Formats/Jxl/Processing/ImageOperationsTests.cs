// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Memory;
using SixLabors.ImageSharp.Formats.Jxl.Processing.AcStrategy;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder.Group;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Image;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Tests.Memory;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Formats.Jxl.Processing;

public class ImageOperationsTests
{
    /// <summary>
    /// Checks that range scans exclude a prefix, padding and retained rows at every SIMD width and in the scalar path.
    /// </summary>
    [Fact]
    public void PlaneMinMax_UsesOnlyLogicalSamples()
    {
        static void RunTest()
        {
            using JxlPlane<int> plane = new(TestEnvironment.Configuration, 33, 3, 2);
            Span<int> allocation = plane.GetRowMinus(0, 2);
            Span<int> samples = plane.GetRow(0);
            ReadOnlySpan<int> widths = [0, 3, 17, 33];

            foreach (int width in widths)
            {
                Assert.True(plane.ShrinkTo(width, 2));

                // These extremes belong only to the prefix, padding and unused third row. Logical samples lie between -width and width.
                allocation.Fill(int.MaxValue);
                allocation[0] = int.MinValue;
                for (int x = 0; x < width; x++)
                {
                    samples[x] = x + 1;
                    samples[plane.PixelsPerRow + x] = -x - 1;
                }

                JxlImageOperations.ImageMinMax(plane, out int minimum, out int maximum);
                Assert.Equal(width == 0 ? int.MaxValue : -width, minimum);
                Assert.Equal(width == 0 ? int.MinValue : width, maximum);
            }

            // A logical shrink retains the allocation. Neither the other columns nor the earlier two-row extent belongs to this scan.
            Assert.True(plane.ShrinkTo(1, 1));
            JxlImageOperations.ImageMinMax(plane, out int finalMinimum, out int finalMaximum);
            Assert.Equal(1, finalMinimum);
            Assert.Equal(1, finalMaximum);
        }

        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunTest,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);
    }

    /// <summary>
    /// Checks that groups reuse coefficient scratch and release replaced buffers when it grows.
    /// </summary>
    [Fact]
    public void GroupScratch_ReusesAndReleasesAllocations()
    {
        TestMemoryAllocator allocator = new();
        allocator.EnableNonThreadSafeLogging();
        Configuration configuration = TestEnvironment.Configuration.Clone();
        configuration.MemoryAllocator = allocator;

        using (JxlGroupDecoderCache cache = new(configuration))
        {
            int smallTransform = JxlAcStrategy.GetTypeBit(JxlAcStrategyType.DCT);
            cache.InitializeOnce(0, smallTransform);
            Assert.Equal(3, allocator.AllocationLog.Count);

            cache.InitializeOnce(0, smallTransform);
            Assert.Equal(3, allocator.AllocationLog.Count);
            Assert.Empty(allocator.ReturnLog);

            // A larger transform replaces the three coefficient buffers. The previous owners
            // must be returned immediately rather than retained until the cache is disposed.
            cache.InitializeOnce(0, JxlAcStrategy.GetTypeBit(JxlAcStrategyType.DCT32X32));
            Assert.Equal(6, allocator.AllocationLog.Count);
            Assert.Equal(3, allocator.ReturnLog.Count);
        }

        Assert.Equal(6, allocator.ReturnLog.Count);
        Assert.Equal(0, allocator.LiveBufferBytes);
    }

    /// <summary>
    /// Checks typed allocation, distinct rows, sample offsets, and initialized vector padding.
    /// </summary>
    /// <param name="width">The logical sample width.</param>
    [Theory]
    [InlineData(1)]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(17)]
    [InlineData(33)]
    public void PlaneRows_PreserveLayoutAndPadding(int width)
    {
        TestMemoryAllocator allocator = new(byte.MaxValue);
        allocator.EnableNonThreadSafeLogging();
        Configuration configuration = TestEnvironment.Configuration.Clone();
        configuration.MemoryAllocator = allocator;

        using JxlPlane<ushort> plane = new(configuration, width, 3, 2);
        Assert.Equal(typeof(ushort), Assert.Single(allocator.AllocationLog).ElementType);

        for (int y = 0; y < 3; y++)
        {
            plane.GetRow(y)[..width].Fill((ushort)(y + 1));
        }

        // Prefix and padding do not contain samples. Dirty allocator storage must not leak into
        // unused vector lanes, and writing one row must not overwrite the next row.
        Assert.Equal((ushort)0, plane.GetRowMinus(0, 2)[0]);
        Assert.Equal((ushort)0, plane.GetRowMinus(0, 2)[1]);

        for (int y = 0; y < 3; y++)
        {
            Span<ushort> row = plane.GetRow(y);
            for (int x = 0; x < width; x++)
            {
                Assert.Equal((ushort)(y + 1), row[x]);
            }

            for (int x = width; x < plane.PixelsPerRow; x++)
            {
                Assert.Equal((ushort)0, row[x]);
            }
        }

        plane.GetRowMemory(1).Span[0] = 42;
        Assert.Equal((ushort)42, plane.GetRow(1)[0]);
        Assert.Equal((ushort)1, plane.GetRowPlus(1, -plane.PixelsPerRow)[0]);
    }

    /// <summary>
    /// Checks that logical resizing preserves allocation layout and rejects invalid dimensions.
    /// </summary>
    [Fact]
    public void PlaneShrink_PreservesStorage()
    {
        using JxlPlane<int> plane = new(TestEnvironment.Configuration, 33, 3);
        plane.GetRow(2)[32] = 123;
        int stride = plane.PixelsPerRow;

        Assert.True(plane.ShrinkTo(1, 1));
        Assert.True(plane.ShrinkTo(33, 3));
        Assert.Equal(stride, plane.PixelsPerRow);
        Assert.Equal(123, plane.GetRow(2)[32]);

        Assert.False(plane.ShrinkTo(-1, 1));
        Assert.False(plane.ShrinkTo(34, 1));
        Assert.False(plane.ShrinkTo(1, 4));
        Assert.Equal(new Rectangle(0, 0, 33, 3), plane.GetRectangle());
    }

    /// <summary>
    /// Checks that rectangle operations do not modify other samples or later rows.
    /// </summary>
    [Fact]
    public void PlaneRectangleOperations_RespectLogicalBounds()
    {
        using JxlPlane<int> source = new(TestEnvironment.Configuration, 5, 4);
        using JxlPlane<int> destination = new(TestEnvironment.Configuration, 7, 5);

        for (int y = 0; y < source.YSize; y++)
        {
            for (int x = 0; x < source.XSize; x++)
            {
                source.GetRow(y)[x] = (y * 10) + x;
            }
        }

        JxlImageOperations.FillImage(-1, destination);
        Rectangle sourceRectangle = new(1, 1, 2, 2);
        Rectangle destinationRectangle = new(3, 2, 2, 2);
        Assert.True(JxlImageOperations.CopyImageTo(sourceRectangle, source, destinationRectangle, destination));

        for (int y = 0; y < destination.YSize; y++)
        {
            Span<int> row = destination.GetRow(y);
            for (int x = 0; x < destination.XSize; x++)
            {
                int expected = destinationRectangle.Contains(x, y) ? ((y - 1) * 10) + x - 2 : -1;
                Assert.Equal(expected, row[x]);
            }
        }

        JxlImageOperations.FillPlane(99, destination, new Rectangle(1, 0, 1, 1));
        Assert.Equal(-1, destination.GetRow(0)[0]);
        Assert.Equal(99, destination.GetRow(0)[1]);
        Assert.Equal(-1, destination.GetRow(0)[2]);
        Assert.Equal(-1, destination.GetRow(1)[1]);
    }

    /// <summary>
    /// Checks that exchanging planes transfers both their owners and their row layouts.
    /// </summary>
    [Fact]
    public void PlaneSwap_TransfersOwnership()
    {
        TestMemoryAllocator allocator = new();
        allocator.EnableNonThreadSafeLogging();
        Configuration configuration = TestEnvironment.Configuration.Clone();
        configuration.MemoryAllocator = allocator;

        using (JxlPlane<ushort> first = new(configuration, 3, 2, 1))
        using (JxlPlane<ushort> second = new(configuration, 17, 3, 5))
        {
            first.GetRow(1)[2] = 12;
            second.GetRow(2)[16] = 99;

            first.Swap(second);
            Assert.Equal((ushort)99, first.GetRow(2)[16]);
            Assert.Equal((ushort)12, second.GetRow(1)[2]);
            Assert.Equal(new Rectangle(0, 0, 17, 3), first.GetRectangle());
            Assert.Equal(new Rectangle(0, 0, 3, 2), second.GetRectangle());
        }

        Assert.Equal(2, allocator.ReturnLog.Count);
        Assert.Equal(0, allocator.LiveBufferBytes);
    }

    /// <summary>
    /// Checks that a failed third-plane allocation releases the first two planes.
    /// </summary>
    [Fact]
    public void Image3Construction_ReleasesStorageOnFailure()
    {
        Configuration configuration = TestEnvironment.Configuration.Clone();
        configuration.MemoryAllocator = MemoryAllocator.Create(new MemoryAllocatorOptions { AccumulativeAllocationLimitMegabytes = 1 });
        using MemoryAllocatorValidator.TestMemoryDiagnostics allocations = MemoryAllocatorValidator.MonitorAllocations();

        // Each float plane uses half a MiB. The third plane exceeds the allocator's one-MiB
        // budget. Both earlier allocations must be released when construction fails.
        Assert.Throws<InvalidMemoryOperationException>(() => new JxlImage3<float>(configuration, 512, 256));
        Assert.Equal(2, allocations.TotalAllocated);
        Assert.Equal(0, allocations.TotalRemainingAllocated);

        using JxlImage3<byte> smaller = new(configuration, 16, 16);
        Assert.Equal(3, allocations.TotalRemainingAllocated);
    }

    /// <summary>
    /// Checks the stored transform type and first-block flag for rectangular transforms.
    /// </summary>
    /// <param name="type">The transform type.</param>
    /// <param name="width">The covered block width.</param>
    /// <param name="height">The covered block height.</param>
    [Theory]
    [InlineData(JxlAcStrategyType.DCT16X8, 1, 2)]
    [InlineData(JxlAcStrategyType.DCT8X16, 2, 1)]
    public void AcStrategyRows_PreserveRectangularExtent(JxlAcStrategyType type, int width, int height)
    {
        using JxlAcStrategyImage image = JxlAcStrategyImage.Create(TestEnvironment.Configuration, width, height);
        image.FillInvalid();
        Assert.True(image.Set(0, 0, type));

        // Each eight-pixel block stores the same transform type. Only the first block has
        // the low bit set. The expected extents come from libjxl's transform-selection table.
        for (int y = 0; y < height; y++)
        {
            JxlAcStrategyRow row = image.GetRow(y);
            for (int x = 0; x < width; x++)
            {
                Assert.Equal(type, row[x].Strategy);
                Assert.Equal(x == 0 && y == 0, row[x].IsFirstBlock);
            }
        }

        Assert.Equal(1, image.CountBlocks(type));
    }

    private static void TestFillImpl<T>(JxlImage3<T> image, string layout)
        where T : unmanaged
    {
        JxlImageOperations.FillImage((T)Convert.ChangeType(1, typeof(T), null), image);

        for (int y = 0; y < image.YSize; y++)
        {
            for (int c = 0; c < 3; c++)
            {
                Span<T> row = image.PlaneRow(c, y);

                for (int x = 0; x < image.XSize; x++)
                {
                    if (!EqualityComparer<T>.Default.Equals(
                        row[x],
                        (T)Convert.ChangeType(1, typeof(T), null)))
                    {
                        Assert.Fail(
                            $"Not 1 at c={c} {x}, {y} " +
                            $"({image.XSize} x {image.YSize}) ({layout})");
                    }

                    row[x] = (T)Convert.ChangeType(2, typeof(T), null);
                }
            }
        }

        JxlImageOperations.ZeroFillImage(image);

        for (int c = 0; c < 3; c++)
        {
            for (int y = 0; y < image.YSize; y++)
            {
                Span<T> row = image.PlaneRow(c, y);

                for (int x = 0; x < image.XSize; x++)
                {
                    if (!EqualityComparer<T>.Default.Equals(
                        row[x],
                        default))
                    {
                        Assert.Fail(
                            $"Not 0 at c={c} {x}, {y} " +
                            $"({image.XSize} x {image.YSize}) ({layout})");
                    }

                    row[x] = (T)Convert.ChangeType(3, typeof(T), null);
                }
            }
        }
    }

    private static void TestFillT<T>()
        where T : unmanaged
    {
        foreach (uint xSize in new uint[] { 0, 1, 15, 16, 31, 32 })
        {
            foreach (uint ySize in new uint[] { 0, 1, 15, 16, 31, 32 })
            {
                JxlImage3<T> image = JxlImage3<T>.Create(TestEnvironment.Configuration, (int)xSize, (int)ySize);

                TestFillImpl(image, "size ctor");
            }
        }
    }

    [Fact]
    public void TestFill()
    {
        TestFillT<byte>();
        TestFillT<short>();
        TestFillT<float>();
    }

    [Fact]
    public void CopyImageToWithPaddingTest()
    {
        JxlPlane<uint> src = JxlPlane<uint>.Create(TestEnvironment.Configuration, 100, 61);

        for (int y = 0; y < src.YSize; y++)
        {
            Span<uint> row = src.GetRow(y);

            for (int x = 0; x < src.XSize; x++)
            {
                row[x] = (uint)((x * 1000) + y);
            }
        }

        Rectangle srcRect = new(10, 20, 30, 40);
        Assert.True(srcRect.IsInside(src.GetRectangle()));

        JxlPlane<uint> dst = JxlPlane<uint>.Create(TestEnvironment.Configuration, 60, 50);

        JxlImageOperations.FillImage(0u, dst);

        Rectangle dstRect = new(20, 5, 30, 40);
        Assert.True(dstRect.IsInside(dst.GetRectangle()));

        Assert.True(
            JxlImageOperations.CopyImageToWithPadding(
                srcRect,
                src,
                padding: 2,
                dstRect,
                dst));

        Rectangle paddedDstRect = new(
            20 - 2,
            5 - 2,
            30 + 4,
            40 + 3);

        for (int y = 0; y < dst.YSize; y++)
        {
            Span<uint> row = dst.GetRow(y);

            for (int x = 0; x < dst.XSize; x++)
            {
                if (new Rectangle(x, y, 1, 1).IsInside(paddedDstRect))
                {
                    Assert.Equal(
                        (uint)(
                            ((x - dstRect.X0() + srcRect.X0()) * 1000) +
                            (y - dstRect.Y0() + srcRect.Y0())),
                        row[x]);
                }
                else
                {
                    Assert.Equal(0u, row[x]);
                }
            }
        }
    }
}
