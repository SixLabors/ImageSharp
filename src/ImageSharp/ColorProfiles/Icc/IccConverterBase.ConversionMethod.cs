// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.ColorProfiles.Conversion.Icc;

/// <summary>
/// Color converter for ICC profiles
/// </summary>
internal abstract partial class IccConverterBase
{
    /// <summary>
    /// Conversion methods with ICC profiles
    /// </summary>
    private enum ConversionMethod
    {
        /// <summary>
        /// Conversion using anything but Multi Process Elements with perceptual rendering intent
        /// </summary>
        A0,

        /// <summary>
        /// Conversion using anything but Multi Process Elements with relative colorimetric rendering intent
        /// </summary>
        A1,

        /// <summary>
        /// Conversion using anything but Multi Process Elements with saturation rendering intent
        /// </summary>
        A2,

        /// <summary>
        /// Conversion using Multi Process Elements with perceptual rendering intent
        /// </summary>
        D0,

        /// <summary>
        /// Conversion using Multi Process Elements with relative colorimetric rendering intent
        /// </summary>
        D1,

        /// <summary>
        /// Conversion using Multi Process Elements with saturation rendering intent
        /// </summary>
        D2,

        /// <summary>
        /// Conversion using Multi Process Elements with absolute colorimetric rendering intent
        /// </summary>
        D3,

        /// <summary>
        /// Conversion of more than one channel using tone reproduction curves
        /// </summary>
        ColorTrc,

        /// <summary>
        /// Conversion of exactly one channel using a tone reproduction curve
        /// </summary>
        GrayTrc,

        /// <summary>
        /// No valid conversion method available or found
        /// </summary>
        Invalid,
    }
}
