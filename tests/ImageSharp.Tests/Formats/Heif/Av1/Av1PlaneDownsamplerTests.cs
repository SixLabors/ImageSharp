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
        HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic;

    [Theory]
    [InlineData(16, 16)]
    [InlineData(64, 64)]
    [InlineData(48, 24)]
    [InlineData(8, 32)]
    [InlineData(128, 96)]
    public void HalveMatchesTheReferenceKernel(int width, int height)
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            static parameters =>
            {
                string[] values = parameters.Split(',');
                ValidateHalve(int.Parse(values[0]), int.Parse(values[1]));
            },
            FormattableString.Invariant($"{width},{height}"),
            DownsamplerConfigurations);

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

    [Theory]
    [InlineData(16, 8, true)]
    [InlineData(17, 9, true)]
    [InlineData(16, 7, false)]
    [InlineData(1, 1, true)]
    public void IsHalvedFollowsTheReferenceLength(int length, int halvedLength, bool expected)
        => Assert.Equal(expected, Av1PlaneDownsampler.IsHalved(length, halvedLength));
}
