// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Tiff;

/// <summary>
/// Gives the result of the checks of an IFD before the decoder allocates its frame.
/// </summary>
internal enum TiffDirectoryStatus
{
    /// <summary>
    /// The decoder can decode the IFD.
    /// </summary>
    Valid,

    /// <summary>
    /// The IFD is not valid TIFF data.
    /// </summary>
    Damaged,

    /// <summary>
    /// The IFD is valid TIFF data, but it uses a feature that the decoder does not support.
    /// </summary>
    Unsupported
}
