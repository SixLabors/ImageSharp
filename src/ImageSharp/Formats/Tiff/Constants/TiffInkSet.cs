// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Metadata.Profiles.Exif;

namespace SixLabors.ImageSharp.Formats.Tiff.Constants;

/// <summary>
/// Enumeration representing the set of inks used in a separated (<see cref="TiffPhotometricInterpretation.Separated"/>) image.
/// </summary>
public enum TiffInkSet : ushort
{
    /// <summary>
    /// CMYK.
    /// The order of the components is cyan, magenta, yellow, black.
    /// Usually, a value of 0 represents 0% ink coverage and a value of 255 represents 100% ink coverage for that component, but see DotRange.
    /// The <see cref="ExifTagValue.InkNames"/> field should not exist when InkSet=1.
    /// </summary>
    Cmyk = 1,

    /// <summary>
    /// Not CMYK.
    /// See the <see cref="ExifTagValue.InkNames"/> field for a description of the inks to be used.
    /// </summary>
    NotCmyk = 2
}
