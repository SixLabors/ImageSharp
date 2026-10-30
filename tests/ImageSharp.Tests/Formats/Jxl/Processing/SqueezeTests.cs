// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Processing.Modular;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Modular.Transforms;
using SixLabors.ImageSharp.Tests.Memory;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Formats.Jxl.Processing;

/// <summary>
/// Tests squeeze pair arithmetic, sequential prediction and channel storage at each SIMD width.
/// </summary>
public class SqueezeTests
{
    /// <summary>
    /// Checks independently known linear-ramp averages and residuals before restoring odd and even channel shapes.
    /// </summary>
    [Fact]
    public void Squeeze_LinearRampsPreservePairsAndStrides()
    {
        static void RunTest()
        {
            ReadOnlySpan<int> lengths = [1, 2, 7, 8, 9, 31, 32, 33, 34, 65, 66, 67];
            ReadOnlySpan<bool> orientations = [false, true];
            ReadOnlySpan<int> directions = [-1, 1];
            foreach (int length in lengths)
            {
                foreach (bool horizontal in orientations)
                {
                    foreach (int direction in directions)
                    {
                        TestMemoryAllocator allocator = new(173);
                        allocator.EnableNonThreadSafeLogging();
                        Configuration configuration = TestEnvironment.Configuration.Clone();
                        configuration.MemoryAllocator = allocator;
                        int width = horizontal ? length : 33;
                        int height = horizontal ? 33 : length;
                        using (JxlModularImage image = JxlModularImage.Create(configuration, width + 19, height + 1, 16, 1))
                        {
                            // Shrink retains a wider physical stride. New average, residual and restored planes have their own strides.
                            JxlModularChannel source = image.Channels[0];
                            source.Shrink(width, height);
                            source.Component = 0;
                            Span<int> samples = source.GetRow(0);
                            int stride = source.Plane.PixelsPerRow;
                            for (int y = 0; y < height; y++)
                            {
                                for (int x = 0; x < width; x++)
                                {
                                    samples[(y * stride) + x] = direction * (horizontal ? (100 * y) + x : (100 * x) + y);
                                }
                            }

                            if (horizontal)
                            {
                                JxlSqueeze.ForwardHorizontalSqueeze(configuration, image, 0, 1);
                            }
                            else
                            {
                                JxlSqueeze.ForwardVerticalSqueeze(configuration, image, 0, 1);
                            }

                            JxlModularChannel average = image.Channels[0];
                            JxlModularChannel residual = image.Channels[1];
                            ReadOnlySpan<int> averages = average.GetRow(0);
                            ReadOnlySpan<int> residuals = residual.Width != 0 && residual.Height != 0 ? residual.GetRow(0) : [];
                            for (int y = 0; y < average.Height; y++)
                            {
                                for (int x = 0; x < average.Width; x++)
                                {
                                    int coordinate = horizontal ? x : y;
                                    int other = horizontal ? y : x;
                                    // A decreasing odd sum rounds toward its first sample. An unmatched edge is already a complete sample.
                                    int first = direction * ((100 * other) + (2 * coordinate));
                                    Assert.Equal(first, averages[(y * average.Plane.PixelsPerRow) + x]);
                                }
                            }

                            for (int y = 0; y < residual.Height; y++)
                            {
                                for (int x = 0; x < residual.Width; x++)
                                {
                                    int coordinate = horizontal ? x : y;
                                    // Interior slope prediction exactly removes a unit ramp. At an even final edge the repeated average predicts zero.
                                    int expected = (length & 1) == 0 && coordinate == (length / 2) - 1 ? -direction : 0;
                                    Assert.Equal(expected, residuals[(y * residual.Plane.PixelsPerRow) + x]);
                                }
                            }

                            if (horizontal)
                            {
                                JxlSqueeze.InverseHorizontalSqueeze(configuration, image, 0, 1);
                            }
                            else
                            {
                                JxlSqueeze.InverseVerticalSqueeze(configuration, image, 0, 1);
                            }

                            Assert.Same(source, image.Channels[0]);
                            Assert.Equal(width, source.Width);
                            Assert.Equal(height, source.Height);
                            Assert.Equal(0, source.Component);
                            ReadOnlySpan<int> restored = source.GetRow(0);
                            stride = source.Plane.PixelsPerRow;
                            for (int y = 0; y < height; y++)
                            {
                                for (int x = 0; x < width; x++)
                                {
                                    int expected = direction * (horizontal ? (100 * y) + x : (100 * x) + y);
                                    Assert.Equal(expected, restored[(y * stride) + x]);
                                }
                            }
                        }

                        Assert.Equal(0, allocator.LiveBufferBytes);
                    }
                }
            }
        }

        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunTest,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);
    }

    /// <summary>
    /// Checks native 64-bit reconstruction and narrowing before the next pair consumes its stored predecessor.
    /// </summary>
    [Fact]
    public void InverseSqueeze_WideArithmeticPreservesStoredPredecessor()
    {
        static void RunTest()
        {
            ReadOnlySpan<bool> orientations = [false, true];
            foreach (bool horizontal in orientations)
            {
                Configuration configuration = TestEnvironment.Configuration;
                using JxlModularImage image = new(horizontal ? 66 : 33, horizontal ? 33 : 4, 32);
                JxlModularChannel average = new(configuration, 33, horizontal ? 33 : 2, 1, 1);
                image.Channels.Add(average);
                JxlModularChannel residual = new(configuration, average.Width, average.Height, 1, 1);
                image.Channels.Add(residual);
                for (int y = 0; y < average.Height; y++)
                {
                    Span<int> a = average.GetRow(y);
                    Span<int> r = residual.GetRow(y);
                    for (int x = 0; x < average.Width; x++)
                    {
                        a[x] = (horizontal ? x : y) == 0 ? int.MaxValue : int.MinValue;
                        r[x] = (horizontal ? x : y) < 2 ? a[x] : 0;
                    }
                }

                if (horizontal)
                {
                    JxlSqueeze.InverseHorizontalSqueeze(configuration, image, 0, 1);
                }
                else
                {
                    JxlSqueeze.InverseVerticalSqueeze(configuration, image, 0, 1);
                }

                // The first wide pair is (3221225471, 1073741823). Store its first sample modulo 2^32.
                // The next pair uses the stored odd sample 1073741823, yielding (-3221225472, -1073741824) before narrowing.
                ReadOnlySpan<int> expected = [-1073741825, 1073741823, 1073741824, -1073741824];
                for (int y = 0; y < average.Height; y++)
                {
                    ReadOnlySpan<int> row = average.GetRow(y);
                    for (int x = 0; x < average.Width; x++)
                    {
                        Assert.Equal(horizontal && x >= 4 ? int.MinValue : expected[horizontal ? x : y], row[x]);
                    }
                }
            }
        }

        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunTest,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);
    }

    /// <summary>
    /// Checks signed division and mixed wide/fast lanes at the exact arithmetic boundary without restricting legal stored samples.
    /// </summary>
    [Fact]
    public void InverseSqueeze_MixedMagnitudeLanesTruncateSignedDivision()
    {
        static void RunTest()
        {
            const int fastMaximum = int.MaxValue / 4;
            ReadOnlySpan<int> values = [0, 1, -1, fastMaximum, fastMaximum + 1, int.MinValue, int.MaxValue];
            Configuration configuration = TestEnvironment.Configuration;
            using JxlModularImage image = new(33, 2, 32);
            JxlModularChannel average = new(configuration, 33, 1, 0, 1);
            image.Channels.Add(average);
            JxlModularChannel residual = new(configuration, 33, 1, 0, 1);
            image.Channels.Add(residual);
            Span<int> a = average.GetRow(0);
            residual.GetRow(0)[..33].Fill(-1);
            for (int x = 0; x < 33; x++)
            {
                a[x] = values[x % values.Length];
            }

            JxlSqueeze.InverseVerticalSqueeze(configuration, image, 0, 1);
            ReadOnlySpan<int> even = average.GetRow(0);
            ReadOnlySpan<int> odd = average.GetRow(1);
            for (int x = 0; x < 33; x++)
            {
                // Repeated averages predict zero. Signed -1/2 truncates to zero, so the pair is (average, average+1), with Int32 storage.
                int value = values[x % values.Length];
                Assert.Equal(value, even[x]);
                Assert.Equal(unchecked(value + 1), odd[x]);
            }
        }

        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunTest,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);
    }
}
