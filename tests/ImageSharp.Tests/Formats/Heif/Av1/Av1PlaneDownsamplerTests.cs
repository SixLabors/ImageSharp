// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using SixLabors.ImageSharp.Tests.TestUtilities;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

[Trait("Format", "Avif")]
public class Av1PlaneDownsamplerTests
{
    /// <summary>
    /// The configuration set the other AV1 vector tests use, so every supported width and the
    /// scalar remainder are all exercised.
    /// </summary>
    private const HwIntrinsics DownsamplerConfigurations =
        HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic;

    [Fact]
    public void HalveMatchesTheReferenceKernel()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(ValidateHalveGeometries, DownsamplerConfigurations);

    /// <summary>
    /// Compares the halved plane with the reference kernel for a vector-remainder and a full-width geometry.
    /// </summary>
    private static void ValidateHalveGeometries()
    {
        ValidateHalve(48, 24);
        ValidateHalve(128, 96);
    }

    /// <summary>
    /// Compares the halved plane with the reference kernel for one geometry.
    /// </summary>
    /// <param name="width">The source width.</param>
    /// <param name="height">The source height.</param>
    private static void ValidateHalve(int width, int height)
    {
        const int Padding = 5;
        int sourceStride = width + Padding;
        int halvedWidth = width >> 1;
        int halvedHeight = height >> 1;
        int destinationStride = halvedWidth + Padding;

        byte[] source = new byte[sourceStride * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < sourceStride; x++)
            {
                // A pattern with strong local structure exercises every tap, including the negative ones.
                source[(y * sourceStride) + x] = (byte)(((x * 29) + (y * 71) + ((x * y) * 13)) & byte.MaxValue);
            }
        }

        byte[] expected = new byte[destinationStride * halvedHeight];
        byte[] actual = new byte[destinationStride * halvedHeight];
        Av1PlaneDownsamplerOracle.Halve(source, sourceStride, width, height, expected, destinationStride);

        Av1PlaneDownsampler.Halve(
            Configuration.Default.MemoryAllocator,
            source,
            sourceStride,
            width,
            height,
            actual,
            destinationStride);

        for (int y = 0; y < halvedHeight; y++)
        {
            Assert.Equal(
                expected.AsSpan(y * destinationStride, halvedWidth).ToArray(),
                actual.AsSpan(y * destinationStride, halvedWidth).ToArray());
        }
    }
}
