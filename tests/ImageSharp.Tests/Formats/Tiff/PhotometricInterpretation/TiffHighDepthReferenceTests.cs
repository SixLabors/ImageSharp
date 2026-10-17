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
/// Compares complete high-depth TIFF decodes with eight-bit RGB references exported from their libtiff scanlines.
/// </summary>
[Trait("Format", "Tiff")]
public class TiffHighDepthReferenceTests
{
    /// <summary>
    /// Checks packed and planar RGB/RGBA with 24 or 32 bits per channel in both byte orders.
    /// </summary>
    /// <param name="sourcePath">The existing high-depth TIFF sample.</param>
    /// <param name="referencePath">The independent libtiff RGB8 reference.</param>
    [Theory]
    [InlineData(FlowerRgb242424Contiguous, FlowerRgb24ContiguousReference)]
    [InlineData(FlowerRgb242424ContiguousLittleEndian, FlowerRgb24ContiguousLittleEndianReference)]
    [InlineData(FlowerRgb242424Planar, FlowerRgb24PlanarReference)]
    [InlineData(FlowerRgb242424PlanarLittleEndian, FlowerRgb24PlanarLittleEndianReference)]
    [InlineData(FlowerRgb323232Contiguous, FlowerRgb32ContiguousReference)]
    [InlineData(FlowerRgb323232ContiguousLittleEndian, FlowerRgb32ContiguousLittleEndianReference)]
    [InlineData(FlowerRgb323232Planar, FlowerRgb32PlanarReference)]
    [InlineData(FlowerRgb323232PlanarLittleEndian, FlowerRgb32PlanarLittleEndianReference)]
    [InlineData(Rgba24BitUnassociatedAlphaBigEndian, Rgba24BigEndianReference)]
    [InlineData(Rgba24BitUnassociatedAlphaLittleEndian, Rgba24LittleEndianReference)]
    [InlineData(Rgba24BitPlanarUnassociatedAlphaBigEndian, Rgba24PlanarBigEndianReference)]
    [InlineData(Rgba24BitPlanarUnassociatedAlphaLittleEndian, Rgba24PlanarLittleEndianReference)]
    [InlineData(Rgba32BitUnassociatedAlphaBigEndian, Rgba32BigEndianReference)]
    [InlineData(Rgba32BitUnassociatedAlphaLittleEndian, Rgba32LittleEndianReference)]
    [InlineData(Rgba32BitPlanarUnassociatedAlphaBigEndian, Rgba32PlanarBigEndianReference)]
    [InlineData(Rgba32BitPlanarUnassociatedAlphaLittleEndian, Rgba32PlanarLittleEndianReference)]
    public void Decode_MatchesLibtiffReference(string sourcePath, string referencePath) => FeatureTestRunner.RunWithHwIntrinsicsFeature(
        AssertReference,
        sourcePath,
        referencePath,
        HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Compares all decoded pixels with libtiff's independently rounded output.
    /// </summary>
    /// <param name="sourcePath">The existing high-depth TIFF sample.</param>
    /// <param name="referencePath">The independent libtiff RGB8 reference.</param>
    private static void AssertReference(string sourcePath, string referencePath)
    {
        using Image<Rgba32> image = TestFile.Create(sourcePath).CreateRgba32Image(TiffDecoder.Instance);
        using Image<Rgba32> reference = TestFile.Create(referencePath).CreateRgba32Image(MagickReferenceDecoder.Tiff);

        ImageComparer.Exact.VerifySimilarity(reference, image);
    }
}
