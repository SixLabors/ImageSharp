// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats;

/// <summary>
/// Provides specialized configuration options for decoding image formats.
/// </summary>
public interface ISpecializedDecoderOptions
{
    /// <summary>
    /// Gets or sets the general decoder options.
    /// </summary>
    DecoderOptions GeneralOptions { get; set; }
}
