// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Hevc;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Hevc;

/// <summary>
/// Verifies HEVC intra-reference collection, substitution, and filtering decisions.
/// </summary>
[Trait("Format", "Heic")]
public class HevcIntraReferenceSamplesTests
{
    /// <summary>
    /// Verifies that a fully available reference border is copied directly from the reconstructed plane.
    /// </summary>
    [Fact]
    public void PrepareReferenceSamplesCopiesCompleteBorder()
    {
        const int blockX = 8;
        const int blockY = 8;
        const int log2Size = 2;
        const int unitSize = 2;
        const int referenceLength = 9;
        using HevcPictureBuffer picture = CreatePicture(32, 32, 10, 1);
        bool[] availableUnits = new bool[9];
        availableUnits.AsSpan().Fill(true);
        ushort[] top = new ushort[referenceLength];
        ushort[] left = new ushort[referenceLength];
        ushort[] scratch = new ushort[HevcIntraPredictor.GetReferenceScratchLength(log2Size, unitSize)];

        HevcIntraPredictor.PrepareReferenceSamples(
            picture,
            HevcPlane.Y,
            blockX,
            blockY,
            log2Size,
            unitSize,
            unitSize,
            availableUnits,
            top,
            left,
            scratch);

        for (int i = 0; i < referenceLength; i++)
        {
            Assert.Equal(GetSample(blockX + i - 1, blockY - 1), top[i]);
            Assert.Equal(GetSample(blockX - 1, blockY + i - 1), left[i]);
        }
    }

    /// <summary>
    /// Verifies that a block without available neighbors receives the component midpoint.
    /// </summary>
    [Fact]
    public void PrepareReferenceSamplesUsesMidpointWithoutNeighbors()
    {
        const int log2Size = 2;
        const int unitSize = 2;
        const ushort midpoint = 512;
        using HevcPictureBuffer picture = CreatePicture(16, 16, 10, 1);
        bool[] availableUnits = new bool[9];
        ushort[] top = new ushort[9];
        ushort[] left = new ushort[9];
        ushort[] scratch = new ushort[HevcIntraPredictor.GetReferenceScratchLength(log2Size, unitSize)];

        HevcIntraPredictor.PrepareReferenceSamples(
            picture,
            HevcPlane.Y,
            0,
            0,
            log2Size,
            unitSize,
            unitSize,
            availableUnits,
            top,
            left,
            scratch);

        Assert.All(top, sample => Assert.Equal(midpoint, sample));
        Assert.All(left, sample => Assert.Equal(midpoint, sample));
    }

    /// <summary>
    /// Verifies forward substitution across unavailable below-left, left, corner, top, and above-right units.
    /// </summary>
    [Fact]
    public void PrepareReferenceSamplesSubstitutesPartialBorderInNormativeOrder()
    {
        const int log2Size = 2;
        const int unitSize = 2;
        using HevcPictureBuffer picture = CreatePicture(32, 32, 10, 1);
        bool[] availableUnits = [false, true, false, true, false, false, true, false, false];
        ushort[] top = new ushort[9];
        ushort[] left = new ushort[9];
        ushort[] scratch = new ushort[HevcIntraPredictor.GetReferenceScratchLength(log2Size, unitSize)];

        HevcIntraPredictor.PrepareReferenceSamples(
            picture,
            HevcPlane.Y,
            8,
            8,
            log2Size,
            unitSize,
            unitSize,
            availableUnits,
            top,
            left,
            scratch);

        ushort[] expectedTop = [807, 807, 807, 710, 711, 711, 711, 711, 711];
        ushort[] expectedLeft = [807, 807, 907, 1207, 1207, 1207, 1307, 1307, 1307];
        Assert.True(expectedTop.AsSpan().SequenceEqual(top));
        Assert.True(expectedLeft.AsSpan().SequenceEqual(left));
    }

    /// <summary>
    /// Verifies asymmetric availability units used by horizontally subsampled 4:2:2 chroma planes.
    /// </summary>
    [Fact]
    public void PrepareReferenceSamplesSupportsAsymmetricChromaUnits()
    {
        const int blockX = 16;
        const int blockY = 16;
        const int log2Size = 3;
        const int unitWidth = 2;
        const int unitHeight = 4;
        const int referenceLength = 17;
        using HevcPictureBuffer picture = CreatePicture(48, 48, 10, 2);
        bool[] availableUnits = [true, false, false, false, false, true, false, false, false, false, false, false, false];
        ushort[] top = new ushort[referenceLength];
        ushort[] left = new ushort[referenceLength];
        ushort[] scratch = new ushort[HevcIntraPredictor.GetReferenceScratchLength(log2Size, unitWidth)];

        HevcIntraPredictor.PrepareReferenceSamples(
            picture,
            HevcPlane.Cb,
            blockX,
            blockY,
            log2Size,
            unitWidth,
            unitHeight,
            availableUnits,
            top,
            left,
            scratch);

        ushort substitutedLeft = GetSample(blockX - 1, blockY + 12);
        Assert.Equal(substitutedLeft, top[0]);
        Assert.Equal(GetSample(blockX, blockY - 1), top[1]);
        Assert.Equal(GetSample(blockX + 1, blockY - 1), top[2]);
        for (int i = 3; i < referenceLength; i++)
        {
            Assert.Equal(top[2], top[i]);
        }

        for (int i = 0; i <= 13; i++)
        {
            Assert.Equal(substitutedLeft, left[i]);
        }

        Assert.Equal(GetSample(blockX - 1, blockY + 13), left[14]);
        Assert.Equal(GetSample(blockX - 1, blockY + 14), left[15]);
        Assert.Equal(GetSample(blockX - 1, blockY + 15), left[16]);
    }

    /// <summary>
    /// Verifies the mode, size, component, chroma-format, and sequence controls for reference filtering.
    /// </summary>
    [Fact]
    public void ShouldFilterReferenceSamplesUsesHevcThresholds()
    {
        Assert.False(HevcIntraPredictor.ShouldFilterReferenceSamples(HevcPlane.Y, 0, 2, 1, false));
        Assert.True(HevcIntraPredictor.ShouldFilterReferenceSamples(HevcPlane.Y, 0, 3, 1, false));
        Assert.False(HevcIntraPredictor.ShouldFilterReferenceSamples(HevcPlane.Y, 1, 5, 1, false));
        Assert.False(HevcIntraPredictor.ShouldFilterReferenceSamples(HevcPlane.Y, 26, 5, 1, false));
        Assert.True(HevcIntraPredictor.ShouldFilterReferenceSamples(HevcPlane.Y, 2, 3, 1, false));
        Assert.False(HevcIntraPredictor.ShouldFilterReferenceSamples(HevcPlane.Cb, 2, 3, 1, false));
        Assert.True(HevcIntraPredictor.ShouldFilterReferenceSamples(HevcPlane.Cb, 2, 3, 3, false));
        Assert.False(HevcIntraPredictor.ShouldFilterReferenceSamples(HevcPlane.Y, 2, 3, 1, true));
    }

    /// <summary>
    /// Creates a picture whose luma and chroma samples encode their source coordinates.
    /// </summary>
    /// <param name="width">The coded luma width.</param>
    /// <param name="height">The coded luma height.</param>
    /// <param name="bitDepth">The component precision.</param>
    /// <param name="chromaFormat">The HEVC chroma-format identifier.</param>
    /// <returns>The initialized picture.</returns>
    private static HevcPictureBuffer CreatePicture(int width, int height, int bitDepth, byte chromaFormat)
    {
        HevcPictureBuffer picture = new(Configuration.Default, width, height, bitDepth, bitDepth, chromaFormat, false);
        FillPlane(picture, HevcPlane.Y);
        if (chromaFormat != 0)
        {
            FillPlane(picture, HevcPlane.Cb);
            FillPlane(picture, HevcPlane.Cr);
        }

        return picture;
    }

    /// <summary>
    /// Fills one picture plane with coordinate-derived samples.
    /// </summary>
    /// <param name="picture">The destination picture.</param>
    /// <param name="plane">The destination plane.</param>
    private static void FillPlane(HevcPictureBuffer picture, HevcPlane plane)
    {
        for (int y = 0; y < picture.GetHeight(plane); y++)
        {
            Span<ushort> row = picture.GetRowSpan(plane, y);
            for (int x = 0; x < row.Length; x++)
            {
                row[x] = GetSample(x, y);
            }
        }
    }

    /// <summary>
    /// Gets the deterministic sample value for one plane coordinate.
    /// </summary>
    /// <param name="x">The sample X coordinate.</param>
    /// <param name="y">The sample Y coordinate.</param>
    /// <returns>The coordinate-derived sample.</returns>
    private static ushort GetSample(int x, int y) => (ushort)((y * 100) + x);
}
