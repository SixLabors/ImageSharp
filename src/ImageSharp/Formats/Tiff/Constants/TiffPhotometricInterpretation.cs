// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Tiff.Constants
{
    /// <summary>
    /// Enumeration representing the photometric interpretation formats defined by the Tiff file-format.
    /// </summary>
    public enum TiffPhotometricInterpretation : ushort
    {
        /// <summary>
        /// Bilevel and grayscale: 0 is imaged as white. The maximum value is imaged as black.
        ///
        /// Not supported by the TiffEncoder.
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
        /// A transparency mask.
        ///
        /// Not supported by the TiffEncoder.
        /// </summary>
        TransparencyMask = 4,

        /// <summary>
        /// Separated: usually CMYK (see Section 16 of the TIFF 6.0 specification).
        ///
        /// Not supported by the TiffEncoder.
        /// </summary>
        Separated = 5,

        /// <summary>
        /// YCbCr (see Section 21 of the TIFF 6.0 specification).
        ///
        /// Not supported by the TiffEncoder.
        /// </summary>
        YCbCr = 6,

        /// <summary>
        /// 1976 CIE L*a*b* (see Section 23 of the TIFF 6.0 specification).
        ///
        /// Not supported by the TiffEncoder.
        /// </summary>
        CieLab = 8,

        /// <summary>
        /// ICC L*a*b* (see TIFF Specification, supplement 1).
        ///
        /// Not supported by the TiffEncoder.
        /// </summary>
        IccLab = 9,

        /// <summary>
        /// ITU L*a*b* (see RFC2301).
        ///
        /// Not supported by the TiffEncoder.
        /// </summary>
        ItuLab = 10,

        /// <summary>
        /// Color Filter Array (see the DNG specification).
        ///
        /// Not supported by the TiffEncoder.
        /// </summary>
        ColorFilterArray = 32803,

        /// <summary>
        /// Linear Raw (see the DNG specification).
        ///
        /// Not supported by the TiffEncoder.
        /// </summary>
        LinearRaw = 34892
    }
}
