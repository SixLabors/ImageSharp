// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats;

/// <summary>
/// Provides a way to specify the type of encoding to be used.
/// </summary>
public enum EncodingType
{
    /// <summary>
    /// Lossless encoding, which compresses data without any loss of information.
    /// </summary>
    Lossless,

    /// <summary>
    /// Lossy encoding, which compresses data by discarding some of it.
    /// </summary>
    Lossy
}
