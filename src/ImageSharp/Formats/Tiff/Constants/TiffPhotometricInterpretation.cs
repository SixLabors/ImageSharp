// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Tiff.Constants;

/// <summary>
/// Enumeration representing the photometric interpretation formats defined by the Tiff file-format.
/// </summary>
public enum TiffPhotometricInterpretation : ushort
{
    /// <summary>
    /// <para>Bilevel and grayscale: 0 is imaged as white. The maximum value is imaged as black.</para>
    /// <para>Not supported by the TiffEncoder.</para>
    /// </summary>
    WhiteIsZero = 0,

    /// <summary>
    /// Bilevel and grayscale: 0 is imaged as black. The maximum value is imaged as white.
    /// </summary>
    BlackIsZero = 1,

    /// <summary>
    /// RGB image.
    /// </summary>
    Rgb = 2,

    /// <summary>
    /// Palette Color.
    /// </summary>
    PaletteColor = 3,

    /// <summary>
    /// <para>A transparency mask.</para>
    /// <para>Not supported by the TiffEncoder.</para>
    /// </summary>
    TransparencyMask = 4,

    /// <summary>
    /// <para>Separated: usually CMYK (see Section 16 of the TIFF 6.0 specification).</para>
    /// <para>Not supported by the TiffEncoder.</para>
    /// </summary>
    Separated = 5,

    /// <summary>
    /// <para>YCbCr (see Section 21 of the TIFF 6.0 specification).</para>
    /// <para>Not supported by the TiffEncoder.</para>
    /// </summary>
    YCbCr = 6,

    /// <summary>
    /// <para>1976 CIE L*a*b* (see Section 23 of the TIFF 6.0 specification).</para>
    /// <para>Not supported by the TiffEncoder.</para>
    /// </summary>
    CieLab = 8,

    /// <summary>
    /// <para>ICC L*a*b* (see TIFF Specification, supplement 1).</para>
    /// <para>Not supported by the TiffEncoder.</para>
    /// </summary>
    IccLab = 9,

    /// <summary>
    /// <para>ITU L*a*b* (see RFC2301).</para>
    /// <para>Not supported by the TiffEncoder.</para>
    /// </summary>
    ItuLab = 10,

    /// <summary>
    /// <para>Color Filter Array (see the DNG specification).</para>
    /// <para>Not supported by the TiffEncoder.</para>
    /// </summary>
    ColorFilterArray = 32803,

    /// <summary>
    /// <para>Linear Raw (see the DNG specification).</para>
    /// <para>Not supported by the TiffEncoder.</para>
    /// </summary>
    LinearRaw = 34892
}
