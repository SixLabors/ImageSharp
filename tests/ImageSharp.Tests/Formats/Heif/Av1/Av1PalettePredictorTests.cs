// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

/// <summary>
/// Verifies AV1 palette reconstruction across every supported sample precision and intrinsic tier.
/// </summary>
[Trait("Format", "Avif")]
public class Av1PalettePredictorTests
{
    /// <summary>
    /// The hardware configurations required to exercise each packed width and the scalar fallback.
    /// </summary>
    private const HwIntrinsics Configurations =
        HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic;

    /// <summary>
    /// Verifies exact indexed reconstruction and destination-padding preservation for every palette size.
    /// </summary>
    [Fact]
    public void PredictMatchesIndependentDefinitionAcrossIntrinsicWidths()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(ValidatePredictors, Configurations);

    /// <summary>
    /// Exercises all palette sizes and transform widths at 8, 10, and 12 bits.
    /// </summary>
    private static void ValidatePredictors()
    {
        int[] widths = [4, 8, 16, 32, 64];
        foreach (int paletteSize in Enumerable.Range(2, Av1Constants.PaletteMaxSize - 1))
        {
            foreach (int width in widths)
            {
                int height = width == 64 ? 16 : width;
                int mapStride = width + 5;
                int destinationStride = width + 9;
                byte[] colorIndexMap = CreateColorIndexMap(mapStride, height, width, paletteSize);
                using Buffer2D<byte> colorIndexMapBuffer = Configuration.Default.MemoryAllocator.Allocate2D<byte>(mapStride, height);
                for (int row = 0; row < height; row++)
                {
                    colorIndexMap.AsSpan(row * mapStride, mapStride).CopyTo(colorIndexMapBuffer.DangerousGetRowSpan(row));
                }

                Buffer2DRegion<byte> colorIndexMapRegion = new(colorIndexMapBuffer);
                ushort[] bytePalette = CreatePalette(paletteSize, 8);
                byte[] expectedBytes = Enumerable.Repeat((byte)251, destinationStride * height).ToArray();
                byte[] actualBytes = (byte[])expectedBytes.Clone();

                ApplyReference(bytePalette, colorIndexMap, mapStride, expectedBytes, destinationStride, width, height);
                Av1PalettePredictor.Predict(bytePalette, colorIndexMapRegion, actualBytes, destinationStride, width, height);
                Assert.Equal(expectedBytes, actualBytes);

                foreach (int bitDepth in new[] { 10, 12 })
                {
                    ushort[] palette = CreatePalette(paletteSize, bitDepth);
                    short[] expected = Enumerable.Repeat((short)-1, destinationStride * height).ToArray();
                    short[] actual = (short[])expected.Clone();

                    ApplyReference(palette, colorIndexMap, mapStride, expected, destinationStride, width, height);
                    Av1PalettePredictor.Predict(palette, colorIndexMapRegion, actual, destinationStride, width, height);
                    Assert.Equal(expected, actual);
                }
            }
        }
    }

    /// <summary>
    /// Creates a deterministic palette spanning the legal range for the requested bit depth.
    /// </summary>
    private static ushort[] CreatePalette(int paletteSize, int bitDepth)
    {
        ushort[] result = new ushort[paletteSize];
        int maximum = (1 << bitDepth) - 1;
        for (int index = 0; index < result.Length; index++)
        {
            result[index] = (ushort)(((index * 977) + 37) & maximum);
        }

        return result;
    }

    /// <summary>
    /// Creates deterministic active indices and invalid padding indices for each map row.
    /// </summary>
    private static byte[] CreateColorIndexMap(int stride, int height, int width, int paletteSize)
    {
        byte[] result = Enumerable.Repeat((byte)Av1Constants.PaletteMaxSize, stride * height).ToArray();
        for (int row = 0; row < height; row++)
        {
            for (int column = 0; column < width; column++)
            {
                result[(row * stride) + column] = (byte)(((row * 5) + (column * 3)) % paletteSize);
            }
        }

        return result;
    }

    /// <summary>
    /// Applies independent scalar palette lookup to an 8-bit destination.
    /// </summary>
    private static void ApplyReference(
        ReadOnlySpan<ushort> palette,
        ReadOnlySpan<byte> colorIndexMap,
        int mapStride,
        Span<byte> destination,
        int destinationStride,
        int width,
        int height)
    {
        for (int row = 0; row < height; row++)
        {
            for (int column = 0; column < width; column++)
            {
                destination[(row * destinationStride) + column] = (byte)palette[colorIndexMap[(row * mapStride) + column]];
            }
        }
    }

    /// <summary>
    /// Applies independent scalar palette lookup to a high-bit-depth destination.
    /// </summary>
    private static void ApplyReference(
        ReadOnlySpan<ushort> palette,
        ReadOnlySpan<byte> colorIndexMap,
        int mapStride,
        Span<short> destination,
        int destinationStride,
        int width,
        int height)
    {
        for (int row = 0; row < height; row++)
        {
            for (int column = 0; column < width; column++)
            {
                destination[(row * destinationStride) + column] = (short)palette[colorIndexMap[(row * mapStride) + column]];
            }
        }
    }
}
