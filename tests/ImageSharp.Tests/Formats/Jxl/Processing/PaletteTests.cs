// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Processing.Modular;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Modular.Encoding.ContextPrediction;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Modular.Transforms;
using SixLabors.ImageSharp.Tests.Memory;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Formats.Jxl.Processing;

/// <summary>
/// Tests inverse palette sample reconstruction and its channel ownership.
/// </summary>
public class PaletteTests
{
    /// <summary>
    /// Checks component selection, retained index strides, channel placement and allocation ownership at every SIMD width.
    /// </summary>
    [Fact]
    public void InversePalette_RestoresComponentsAndRetainsIndexStorage()
    {
        static void RunTest()
        {
            ReadOnlySpan<int> componentCounts = [1, 3, 6];
            foreach (int components in componentCounts)
            {
                TestMemoryAllocator allocator = new(173);
                allocator.EnableNonThreadSafeLogging();
                Configuration configuration = TestEnvironment.Configuration.Clone();
                configuration.MemoryAllocator = allocator;

                using (JxlModularImage image = JxlModularImage.Create(configuration, 65, 2, 8, components + 2))
                {
                    // A channel before and after the selected range must survive. Retained indices keep an 80-sample stride;
                    // newly restored 33-sample channels use a 48-sample stride. Every component must read the same logical index row.
                    image.Channels[0].GetRow(0).Fill(-777);
                    JxlModularChannel prefix = image.Channels[0];
                    JxlModularChannel suffix = image.Channels[^1];
                    for (int component = 0; component < components; component++)
                    {
                        image.Channels[1 + component].Shrink(33, 2);
                    }

                    JxlPalette.MetaPalette(configuration, image, 1, components, 3, 0);
                    JxlModularChannel indices = image.Channels[2];
                    int indexStride = indices.Plane.PixelsPerRow;
                    Span<int> indexSamples = indices.GetRow(0);
                    for (int y = 0; y < 2; y++)
                    {
                        for (int x = 0; x < 33; x++)
                        {
                            indexSamples[(y * indexStride) + x] = (x + y) % 3;
                        }

                        indexSamples.Slice((y * indexStride) + 33, indexStride - 33).Fill(int.MinValue);
                    }

                    JxlModularChannel palette = image.Channels[0];
                    for (int component = 0; component < components; component++)
                    {
                        Span<int> entries = palette.GetRow(component);
                        entries[0] = (component * 100) + 11;
                        entries[1] = (component * 100) + 22;
                        entries[2] = (component * 100) + 33;
                    }

                    int allocationCount = allocator.AllocationLog.Count;
                    JxlPalette.InversePalette(configuration, image, 1, 3, 0, JxlPredictor.Zero, new JxlModularHeader());

                    // Only the missing output components need allocations. A second full index plane would add another allocation here.
                    Assert.Equal(allocationCount + components - 1, allocator.AllocationLog.Count);
                    Assert.Equal(components + 2, image.Channels.Count);
                    Assert.Equal(0, image.MetaChannels);
                    Assert.Same(prefix, image.Channels[0]);
                    Assert.Same(indices, image.Channels[1]);
                    Assert.Same(suffix, image.Channels[^1]);
                    Assert.Equal(indexStride, indices.Plane.PixelsPerRow);
                    Assert.Equal(-777, prefix.GetRow(0)[0]);

                    for (int component = 0; component < components; component++)
                    {
                        JxlModularChannel restored = image.Channels[1 + component];
                        Span<int> samples = restored.GetRow(0);
                        int stride = restored.Plane.PixelsPerRow;
                        for (int y = 0; y < 2; y++)
                        {
                            for (int x = 0; x < 33; x++)
                            {
                                int expected = (component * 100) + (11 * (((x + y) % 3) + 1));
                                Assert.Equal(expected, samples[(y * stride) + x]);
                            }
                        }
                    }

                    // No output tail may overwrite retained index padding, even when its original stride is wider than other outputs.
                    for (int y = 0; y < 2; y++)
                    {
                        ReadOnlySpan<int> padding = indexSamples.Slice((y * indexStride) + 33, indexStride - 33);
                        for (int x = 0; x < padding.Length; x++)
                        {
                            Assert.Equal(int.MinValue, padding[x]);
                        }
                    }
                }

                Assert.Equal(0, allocator.LiveBufferBytes);
            }

            // Preserve the native complete-table shortcut for one component with no prediction.
            using JxlModularImage clamped = JxlModularImage.Create(TestEnvironment.Configuration, 33, 1, 8, 1);
            JxlPalette.MetaPalette(TestEnvironment.Configuration, clamped, 0, 0, 2, 0);
            clamped.Channels[0].GetRow(0)[0] = 11;
            clamped.Channels[0].GetRow(0)[1] = 22;
            ReadOnlySpan<int> codes = [-4, 0, 1, 2, int.MaxValue];
            Span<int> clampedIndices = clamped.Channels[1].GetRow(0);
            for (int x = 0; x < 33; x++)
            {
                clampedIndices[x] = codes[x % codes.Length];
            }

            JxlPalette.InversePalette(TestEnvironment.Configuration, clamped, 0, 2, 0, JxlPredictor.Zero, new JxlModularHeader());
            ReadOnlySpan<int> expectedClamped = [11, 11, 22, 22, 22];
            ReadOnlySpan<int> clampedSamples = clamped.Channels[0].GetRow(0);
            for (int x = 0; x < 33; x++)
            {
                Assert.Equal(expectedClamped[x % expectedClamped.Length], clampedSamples[x]);
            }
        }

        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunTest,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);
    }

    /// <summary>
    /// Checks negative deltas, both implicit cubes, signed index extremes and extra components against fixed integer expectations.
    /// </summary>
    [Fact]
    public void InversePalette_PreservesImplicitIndices()
    {
        static void RunTest()
        {
            ReadOnlySpan<int> precisions = [8, 24];
            ReadOnlySpan<int> codes = [-1, -2, -3, -144, int.MinValue, 2, 65, 66, 190, 191, int.MaxValue, 0, 1];
            foreach (int precision in precisions)
            {
                using JxlModularImage image = JxlModularImage.Create(TestEnvironment.Configuration, 33, 1, precision, 6);
                JxlPalette.MetaPalette(TestEnvironment.Configuration, image, 0, 5, 2, 0);
                for (int component = 0; component < 6; component++)
                {
                    Span<int> entries = image.Channels[0].GetRow(component);
                    entries[0] = (component * 100) + 10;
                    entries[1] = (component * 100) + 20;
                }

                Span<int> indices = image.Channels[1].GetRow(0);
                for (int x = 0; x < 33; x++)
                {
                    indices[x] = codes[x % codes.Length];
                }

                // The negative period has 143 entries: Int32.MinValue selects signed delta [0,-32,-32].
                // Int32.MaxValue selects large-cube digits [1,1,3]. At 24 bits, scale those digits with the format's integer rounding.
                int[][] expected = precision == 8
                    ?
                    [
                        [0, 4, -4, 0, 0, 32, 223, 0, 255, 0, 63, 10, 20],
                        [0, 4, -4, 0, -32, 32, 223, 0, 255, 0, 63, 110, 120],
                        [0, 4, -4, 0, -32, 32, 223, 0, 255, 0, 191, 210, 220]
                    ]
                    :
                    [
                        [0, 262144, -262144, 0, 0, 2097152, 14680063, 0, 16777215, 0, 4194303, 10, 20],
                        [0, 262144, -262144, 0, -2097152, 2097152, 14680063, 0, 16777215, 0, 4194303, 110, 120],
                        [0, 262144, -262144, 0, -2097152, 2097152, 14680063, 0, 16777215, 0, 12582911, 210, 220]
                    ];

                JxlPalette.InversePalette(TestEnvironment.Configuration, image, 0, 2, 0, JxlPredictor.Zero, new JxlModularHeader());
                for (int component = 0; component < 6; component++)
                {
                    ReadOnlySpan<int> samples = image.Channels[component].GetRow(0);
                    for (int x = 0; x < 33; x++)
                    {
                        int slot = x % codes.Length;
                        int value = component < 3 ? expected[component][slot]
                            : codes[slot] == 0 ? (component * 100) + 10
                            : codes[slot] == 1 ? (component * 100) + 20
                            : 0;

                        Assert.Equal(value, samples[x]);
                    }
                }
            }

            // A zero-width palette is valid metadata. No explicit entry exists, so even one-component expansion uses implicit colors.
            using JxlModularImage emptyTable = JxlModularImage.Create(TestEnvironment.Configuration, 33, 1, 8, 1);
            JxlPalette.MetaPalette(TestEnvironment.Configuration, emptyTable, 0, 0, 0, 0);
            ReadOnlySpan<int> emptyCodes = [-1, 0, 63, 64, 188, 189];
            ReadOnlySpan<int> emptyExpected = [0, 32, 223, 0, 255, 0];
            Span<int> emptyIndices = emptyTable.Channels[1].GetRow(0);
            for (int x = 0; x < 33; x++)
            {
                emptyIndices[x] = emptyCodes[x % emptyCodes.Length];
            }

            JxlPalette.InversePalette(TestEnvironment.Configuration, emptyTable, 0, 0, 0, JxlPredictor.Zero, new JxlModularHeader());
            ReadOnlySpan<int> emptySamples = emptyTable.Channels[0].GetRow(0);
            for (int x = 0; x < 33; x++)
            {
                Assert.Equal(emptyExpected[x % emptyExpected.Length], emptySamples[x]);
            }
        }

        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunTest,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);
    }

    /// <summary>
    /// Checks causal delta reconstruction and independent weighted histories without allocating a copy of the index image.
    /// </summary>
    [Fact]
    public void InversePalette_ReconstructsPredictedDeltas()
    {
        static void RunTest()
        {
            ReadOnlySpan<JxlPredictor> predictors = [JxlPredictor.Left, JxlPredictor.Weighted];
            foreach (JxlPredictor predictor in predictors)
            {
                TestMemoryAllocator allocator = new(173);
                allocator.EnableNonThreadSafeLogging();
                Configuration configuration = TestEnvironment.Configuration.Clone();
                configuration.MemoryAllocator = allocator;

                using (JxlModularImage image = JxlModularImage.Create(configuration, 33, 2, 8, 6))
                {
                    JxlPalette.MetaPalette(configuration, image, 0, 5, 1, 1);
                    for (int component = 0; component < 6; component++)
                    {
                        Span<int> entries = image.Channels[0].GetRow(component);
                        entries[0] = predictor == JxlPredictor.Left ? (component + 1) * 2 : 0;
                        entries[1] = (component + 1) * 10;
                    }

                    JxlModularChannel indexChannel = image.Channels[1];
                    Span<int> indices = indexChannel.GetRow(0);
                    int indexStride = indexChannel.Plane.PixelsPerRow;
                    ReadOnlySpan<int> leftCodes = [1, 0, -2, 1];
                    for (int y = 0; y < 2; y++)
                    {
                        for (int x = 0; x < 33; x++)
                        {
                            indices[(y * indexStride) + x] = predictor == JxlPredictor.Left ? leftCodes[x % 4] : x == 0 ? 1 : 0;
                        }
                    }

                    int allocations = allocator.AllocationLog.Count;
                    JxlPalette.InversePalette(configuration, image, 0, 1, 1, predictor, new JxlModularHeader());
                    Assert.Same(indexChannel, image.Channels[0]);
                    Assert.Equal(allocations + 5 + (predictor == JxlPredictor.Weighted ? 1 : 0), allocator.AllocationLog.Count);

                    for (int component = 0; component < 6; component++)
                    {
                        JxlModularChannel output = image.Channels[component];
                        ReadOnlySpan<int> samples = output.GetRow(0);
                        int stride = output.Plane.PixelsPerRow;
                        int absolute = (component + 1) * 10;
                        int delta = (component + 1) * 2;

                        for (int y = 0; y < 2; y++)
                        {
                            for (int x = 0; x < 33; x++)
                            {
                                // Left adds each delta to its restored predecessor, then the next absolute entry resets the run.
                                // Weighted prediction of a constant neighborhood retains that constant, including the absolute first sample.
                                int expected = predictor == JxlPredictor.Weighted ? absolute : (x % 4) switch
                                {
                                    1 => absolute + delta,
                                    2 => absolute + delta + (component < 3 ? 4 : 0),
                                    _ => absolute
                                };

                                Assert.Equal(expected, samples[(y * stride) + x]);
                            }
                        }
                    }
                }

                Assert.Equal(0, allocator.LiveBufferBytes);
            }
        }

        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunTest,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);
    }
}
