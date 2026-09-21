// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

[Trait("Format", "Avif")]
public class Av1CornerDetectorTests
{
    /// <summary>
    /// The configuration set the other AV1 vector tests use, so every supported width and the
    /// scalar remainder are all exercised.
    /// </summary>
    private const HwIntrinsics DetectorConfigurations =
        HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic;

    [Theory]
    [InlineData(64, 48, 0)]
    [InlineData(37, 29, 1)]
    [InlineData(96, 96, 2)]
    [InlineData(19, 11, 3)]
    public void DetectMatchesTheReferenceDetector(int width, int height, int pattern)
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            static parameters =>
            {
                string[] values = parameters.Split(',');
                ValidateDetect(int.Parse(values[0]), int.Parse(values[1]), int.Parse(values[2]));
            },
            FormattableString.Invariant($"{width},{height},{pattern}"),
            DetectorConfigurations);

    [Fact]
    public void DetectReturnsNothingForAPlaneTooSmallToHoldACircle()
    {
        const int Width = 6;
        const int Height = 6;
        byte[] plane = new byte[Width * Height];
        int[] corners = new int[2 * Av1CornerDetector.MaximumCorners];

        Assert.Equal(
            0,
            Av1CornerDetector.Detect(Configuration.Default.MemoryAllocator, plane, 0, Width, Height, Width, corners));
    }

    /// <summary>
    /// Compares the detected corners with the reference for one plane.
    /// </summary>
    /// <param name="width">The plane width.</param>
    /// <param name="height">The plane height.</param>
    /// <param name="pattern">The content pattern.</param>
    private static void ValidateDetect(int width, int height, int pattern)
    {
        const int Padding = 7;
        int stride = width + (2 * Padding);
        int origin = (Padding * stride) + Padding;
        byte[] plane = new byte[stride * (height + (2 * Padding))];

        for (int y = -Padding; y < height + Padding; y++)
        {
            for (int x = -Padding; x < width + Padding; x++)
            {
                plane[origin + (y * stride) + x] = Sample(x, y, pattern);
            }
        }

        int[] expected = Av1CornerDetectorOracle.Detect(plane, origin, width, height, stride);

        int[] corners = new int[2 * Av1CornerDetector.MaximumCorners];
        int count = Av1CornerDetector.Detect(
            Configuration.Default.MemoryAllocator, plane, origin, width, height, stride, corners);

        Assert.Equal(expected.Length / 2, count);
        Assert.Equal(expected, corners.AsSpan(0, 2 * count).ToArray());
    }

    /// <summary>
    /// Builds one sample of a content pattern.
    /// </summary>
    /// <param name="x">The column.</param>
    /// <param name="y">The row.</param>
    /// <param name="pattern">The content pattern.</param>
    /// <returns>The sample.</returns>
    /// <remarks>
    /// The patterns cover a flat plane, which has no corner at all, a field of isolated bright
    /// points, which has a corner at every point, a checkerboard, whose corners form plateaus of
    /// equal score that suppression must collapse, and a noisy gradient.
    /// </remarks>
    private static byte Sample(int x, int y, int pattern)
        => pattern switch
        {
            0 => 128,
            1 => (x % 7 == 3 && y % 5 == 2) ? (byte)250 : (byte)20,
            2 => ((x / 4) + (y / 4)) % 2 == 0 ? (byte)230 : (byte)25,
            _ => (byte)(((x * 37) + (y * 91) + ((x * y) / 5)) & byte.MaxValue),
        };
}
