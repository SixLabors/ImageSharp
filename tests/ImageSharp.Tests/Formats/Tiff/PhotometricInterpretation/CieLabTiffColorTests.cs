// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.Common;
using SixLabors.ImageSharp.Tests.TestUtilities;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;
using SixLabors.ImageSharp.Tests.TestUtilities.ReferenceCodecs;
using static SixLabors.ImageSharp.Tests.TestImages.Tiff;

namespace SixLabors.ImageSharp.Tests.Formats.Tiff.PhotometricInterpretation;

/// <summary>
/// Checks libtiff-produced Lab images against independent Little CMS conversion.
/// </summary>
[Trait("Format", "Tiff")]
public class CieLabTiffColorTests
{
    /// <summary>
    /// Compares packed and planar TIFF pixels across byte orders, strips, and vector tails.
    /// </summary>
    /// <param name="path">The generated Lab TIFF fixture.</param>
    /// <param name="referencePath">The independent RGB reference fixture.</param>
    [Theory]
    [InlineData(Lab8Contiguous65X3, Lab8Contiguous65X3LcmsRgb)]
    [InlineData(Lab8Planar65X3, Lab8Planar65X3LcmsRgb)]
    [InlineData(Lab16LittleEndianContiguous65X3, Lab16LittleEndianContiguous65X3LcmsRgb)]
    [InlineData(Lab16LittleEndianPlanar65X3, Lab16LittleEndianPlanar65X3LcmsRgb)]
    [InlineData(Lab16BigEndianContiguous65X3, Lab16BigEndianContiguous65X3LcmsRgb)]
    [InlineData(Lab16BigEndianPlanar65X3, Lab16BigEndianPlanar65X3LcmsRgb)]
    public void Decode_MatchesLcmsReference(string path, string referencePath) => FeatureTestRunner.RunWithHwIntrinsicsFeature(AssertLcmsReference, path, referencePath, HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Compares every decoded pixel with the Little CMS result from actual libtiff rows.
    /// </summary>
    /// <param name="path">The source TIFF path.</param>
    /// <param name="referencePath">The reference TIFF path.</param>
    private static void AssertLcmsReference(string path, string referencePath)
    {
        using Image<Rgba32> image = TestFile.Create(path).CreateRgba32Image(TiffDecoder.Instance);
        using Image<Rgba32> reference = TestFile.Create(referencePath).CreateRgba32Image(MagickReferenceDecoder.Tiff);

        Assert.Equal(new Size(65, 3), image.Size);

        // Little CMS quantizes RGB to bytes before export; allow one byte code per pixel across all channels.
        ImageComparer.Tolerant(0F, perPixelManhattanThreshold: ushort.MaxValue / byte.MaxValue).VerifySimilarity(reference, image);
    }
}
