// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Hevc;
using SixLabors.ImageSharp.Tests.Memory;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Hevc;

/// <summary>
/// Verifies HEVC coefficient scan selection, grouped ordering, and CABAC level reconstruction.
/// </summary>
public class HevcCoefficientDecoderTests
{
    /// <summary>
    /// Verifies the three normative four-by-four coefficient scans.
    /// </summary>
    /// <param name="scanType">The scan direction under test.</param>
    /// <param name="expected">The expected raster indices in scan order.</param>
    [Theory]
    [MemberData(nameof(GetFourByFourScans))]
    public void WritesFourByFourScan(int scanType, int[] expected)
    {
        int[] actual = new int[16];

        int lastScanPosition = HevcCoefficientScanOrder.Write(actual, 4, 4, (HevcCoefficientScanType)scanType, expected[^1]);

        Assert.Equal(expected, actual);
        Assert.Equal(15, lastScanPosition);
    }

    /// <summary>
    /// Verifies that grouped scans visit each coefficient exactly once for every supported transform geometry.
    /// </summary>
    /// <param name="width">The transform-block width.</param>
    /// <param name="height">The transform-block height.</param>
    /// <param name="scanType">The scan direction under test.</param>
    [Theory]
    [InlineData(4, 4, HevcCoefficientScanType.Diagonal)]
    [InlineData(8, 8, HevcCoefficientScanType.Diagonal)]
    [InlineData(8, 4, HevcCoefficientScanType.Horizontal)]
    [InlineData(4, 8, HevcCoefficientScanType.Vertical)]
    [InlineData(16, 32, HevcCoefficientScanType.Diagonal)]
    [InlineData(32, 16, HevcCoefficientScanType.Horizontal)]
    [InlineData(32, 32, HevcCoefficientScanType.Vertical)]
    public void GroupedScanVisitsEveryCoefficient(int width, int height, int scanType)
    {
        int coefficientCount = width * height;
        int[] scan = new int[coefficientCount];
        bool[] visited = new bool[coefficientCount];

        int lastScanPosition = HevcCoefficientScanOrder.Write(scan, width, height, (HevcCoefficientScanType)scanType, coefficientCount - 1);

        Assert.InRange(lastScanPosition, 0, coefficientCount - 1);
        foreach (int rasterPosition in scan)
        {
            Assert.InRange(rasterPosition, 0, coefficientCount - 1);
            Assert.False(visited[rasterPosition]);
            visited[rasterPosition] = true;
        }

        Assert.All(visited, Assert.True);
    }

    /// <summary>
    /// Verifies that an eight-by-eight diagonal scan groups coefficients in the normative group order.
    /// </summary>
    [Fact]
    public void DiagonalEightByEightScanUsesGroupedOrder()
    {
        int[] scan = new int[64];

        HevcCoefficientScanOrder.Write(scan, 8, 8, HevcCoefficientScanType.Diagonal, 63);

        Assert.Equal(0, scan[0]);
        Assert.Equal(32, scan[16]);
        Assert.Equal(4, scan[32]);
        Assert.Equal(36, scan[48]);
    }

    /// <summary>
    /// Verifies transform geometry and intra direction select horizontal, vertical, or diagonal scans.
    /// </summary>
    /// <param name="width">The transform-block width.</param>
    /// <param name="height">The transform-block height.</param>
    /// <param name="plane">The reconstructed component.</param>
    /// <param name="isIntra">Whether the containing coding unit uses intra prediction.</param>
    /// <param name="mode">The effective intra mode.</param>
    /// <param name="chromaFormat">The sequence chroma format.</param>
    /// <param name="expected">The expected coefficient scan.</param>
    [Theory]
    [InlineData(8, 8, HevcPlane.Y, false, 26, 3, HevcCoefficientScanType.Diagonal)]
    [InlineData(8, 8, HevcPlane.Y, true, 26, 3, HevcCoefficientScanType.Horizontal)]
    [InlineData(8, 8, HevcPlane.Y, true, 10, 3, HevcCoefficientScanType.Vertical)]
    [InlineData(8, 8, HevcPlane.Y, true, 18, 3, HevcCoefficientScanType.Diagonal)]
    [InlineData(16, 16, HevcPlane.Y, true, 26, 3, HevcCoefficientScanType.Diagonal)]
    [InlineData(8, 8, HevcPlane.Cb, true, 26, 1, HevcCoefficientScanType.Diagonal)]
    [InlineData(4, 4, HevcPlane.Cb, true, 26, 1, HevcCoefficientScanType.Horizontal)]
    public void SelectsScanType(
        int width,
        int height,
        int plane,
        bool isIntra,
        int mode,
        byte chromaFormat,
        int expected)
    {
        HevcCoefficientScanType actual = HevcCoefficientCodingParameters.SelectScanType(
            width,
            height,
            (HevcPlane)plane,
            isIntra,
            mode,
            chromaFormat,
            false);

        Assert.Equal((HevcCoefficientScanType)expected, actual);
    }

    /// <summary>
    /// Verifies the significance-map context bases selected by transform size, scan, channel, and Range Extensions.
    /// </summary>
    /// <param name="width">The transform-block width.</param>
    /// <param name="height">The transform-block height.</param>
    /// <param name="plane">The reconstructed component.</param>
    /// <param name="scanType">The selected coefficient scan.</param>
    /// <param name="singleContext">Whether the Range Extensions single-context mode applies.</param>
    /// <param name="expected">The expected first significance-map context.</param>
    [Theory]
    [InlineData(4, 4, HevcPlane.Y, HevcCoefficientScanType.Diagonal, false, 0)]
    [InlineData(8, 8, HevcPlane.Y, HevcCoefficientScanType.Diagonal, false, 9)]
    [InlineData(8, 8, HevcPlane.Y, HevcCoefficientScanType.Horizontal, false, 15)]
    [InlineData(16, 16, HevcPlane.Y, HevcCoefficientScanType.Diagonal, false, 21)]
    [InlineData(8, 8, HevcPlane.Cb, HevcCoefficientScanType.Vertical, false, 9)]
    [InlineData(16, 16, HevcPlane.Cr, HevcCoefficientScanType.Diagonal, false, 12)]
    [InlineData(4, 4, HevcPlane.Y, HevcCoefficientScanType.Diagonal, true, 27)]
    [InlineData(4, 4, HevcPlane.Cb, HevcCoefficientScanType.Diagonal, true, 15)]
    public void SelectsFirstSignificanceContext(
        int width,
        int height,
        int plane,
        int scanType,
        bool singleContext,
        int expected)
    {
        HevcCoefficientCodingParameters parameters = CreateParameters(
            width,
            height,
            (HevcPlane)plane,
            (HevcCoefficientScanType)scanType,
            singleContext);

        Assert.Equal(expected, parameters.FirstSignificanceMapContext);
    }

    /// <summary>
    /// Verifies the complete four-by-four raster-position context mapping.
    /// </summary>
    [Fact]
    public void FourByFourSignificanceContextsMatchNormativeMap()
    {
        int[] expected = [0, 1, 4, 5, 2, 3, 4, 5, 6, 6, 8, 8, 7, 7, 8, 8];
        HevcCoefficientCodingParameters parameters = CreateParameters(4, 4, HevcPlane.Y, HevcCoefficientScanType.Diagonal, false);

        for (int rasterPosition = 0; rasterPosition < expected.Length; rasterPosition++)
        {
            Assert.Equal(expected[rasterPosition], parameters.GetSignificantCoefficientContext(rasterPosition, 0));
        }
    }

    /// <summary>
    /// Verifies significant-group and coefficient contexts use already decoded right and lower groups.
    /// </summary>
    [Fact]
    public void SignificanceContextsUseRightAndLowerGroups()
    {
        int[] groupFlags = [0, 1, 1, 0];
        HevcCoefficientCodingParameters luma = CreateParameters(8, 8, HevcPlane.Y, HevcCoefficientScanType.Diagonal, false);
        HevcCoefficientCodingParameters chroma = CreateParameters(8, 8, HevcPlane.Cb, HevcCoefficientScanType.Diagonal, false);

        Assert.Equal(1, luma.GetSignificantGroupContext(groupFlags, 0, 0));
        Assert.Equal(3, luma.GetSignificancePattern(groupFlags, 0, 0));
        Assert.Equal(0, luma.GetSignificantGroupContext(groupFlags, 1, 0));
        Assert.Equal(0, luma.GetSignificancePattern(groupFlags, 1, 0));
        Assert.Equal(0, luma.GetSignificantCoefficientContext(0, 3));
        Assert.Equal(11, luma.GetSignificantCoefficientContext(1, 3));
        Assert.Equal(14, luma.GetSignificantCoefficientContext(4, 0));
        Assert.Equal(11, chroma.GetSignificantCoefficientContext(4, 0));
    }

    /// <summary>
    /// Verifies luma and chroma level-context sets track subset position and preceding greater-than-one state.
    /// </summary>
    [Fact]
    public void SelectsLevelContextSets()
    {
        HevcCoefficientCodingParameters luma = CreateParameters(8, 8, HevcPlane.Y, HevcCoefficientScanType.Diagonal, false);
        HevcCoefficientCodingParameters chroma = CreateParameters(8, 8, HevcPlane.Cb, HevcCoefficientScanType.Diagonal, false);

        Assert.Equal(0, luma.GetLevelContextSet(0, false));
        Assert.Equal(1, luma.GetLevelContextSet(0, true));
        Assert.Equal(2, luma.GetLevelContextSet(1, false));
        Assert.Equal(3, luma.GetLevelContextSet(1, true));
        Assert.Equal(0, chroma.GetLevelContextSet(0, false));
        Assert.Equal(1, chroma.GetLevelContextSet(3, true));
    }

    /// <summary>
    /// Verifies a fixed CABAC substream reconstructs one positive DC coefficient without allocating per block.
    /// </summary>
    [Fact]
    public void DecodesPositiveDcCoefficientWithReusableScratch()
    {
        TestMemoryAllocator allocator = new();
        allocator.EnableNonThreadSafeLogging();
        Configuration configuration = new() { MemoryAllocator = allocator };
        using HevcCoefficientDecoder decoder = new(configuration);
        int[] coefficients = new int[16];
        HevcCoefficientCodingParameters parameters = CreateParameters(4, 4, HevcPlane.Y, HevcCoefficientScanType.Diagonal, false);

        HevcCabacSyntaxReader firstReader = new([0xEE, 0x48], 22);
        int firstNonZeroCount = decoder.Decode(ref firstReader, coefficients, in parameters);
        HevcCabacSyntaxReader secondReader = new([0xEE, 0x48], 22);
        int secondNonZeroCount = decoder.Decode(ref secondReader, coefficients, in parameters);

        Assert.Equal(1, firstNonZeroCount);
        Assert.Equal(1, secondNonZeroCount);
        Assert.Equal(1, coefficients[0]);
        Assert.All(coefficients[1..], value => Assert.Equal(0, value));
        Assert.Single(allocator.AllocationLog);
    }

    /// <summary>
    /// Verifies the bypass-coded sign is applied to a fixed single-coefficient CABAC substream.
    /// </summary>
    [Fact]
    public void DecodesNegativeDcCoefficient()
    {
        using HevcCoefficientDecoder decoder = new(Configuration.Default);
        HevcCabacSyntaxReader reader = new([0xF4, 0x24], 22);
        int[] coefficients = new int[16];
        HevcCoefficientCodingParameters parameters = CreateParameters(4, 4, HevcPlane.Y, HevcCoefficientScanType.Diagonal, false);

        int nonZeroCount = decoder.Decode(ref reader, coefficients, in parameters);

        Assert.Equal(1, nonZeroCount);
        Assert.Equal(-1, coefficients[0]);
        Assert.All(coefficients[1..], value => Assert.Equal(0, value));
    }

    /// <summary>
    /// Gets the exact raster order for each four-by-four scan direction.
    /// </summary>
    /// <returns>The scan direction and expected raster positions.</returns>
    public static TheoryData<int, int[]> GetFourByFourScans() =>
        new()
        {
            {
                (int)HevcCoefficientScanType.Diagonal,
                [0, 4, 1, 8, 5, 2, 12, 9, 6, 3, 13, 10, 7, 14, 11, 15]
            },
            {
                (int)HevcCoefficientScanType.Horizontal,
                [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15]
            },
            {
                (int)HevcCoefficientScanType.Vertical,
                [0, 4, 8, 12, 1, 5, 9, 13, 2, 6, 10, 14, 3, 7, 11, 15]
            },
        };

    /// <summary>
    /// Creates explicit coefficient parameters for scan and entropy tests without requiring a parsed parameter set.
    /// </summary>
    /// <param name="width">The transform-block width.</param>
    /// <param name="height">The transform-block height.</param>
    /// <param name="plane">The reconstructed component.</param>
    /// <param name="scanType">The coefficient scan.</param>
    /// <param name="singleContext">Whether the Range Extensions single significance context applies.</param>
    /// <returns>The coefficient coding parameters.</returns>
    private static HevcCoefficientCodingParameters CreateParameters(
        int width,
        int height,
        HevcPlane plane,
        HevcCoefficientScanType scanType,
        bool singleContext)
        => new(width, height, plane, scanType, singleContext, false, false, false, false, 15, plane == HevcPlane.Y ? 0 : 2);
}
