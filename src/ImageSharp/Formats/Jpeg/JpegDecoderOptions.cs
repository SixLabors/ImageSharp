// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jpeg;

/// <summary>
/// Configuration options for decoding Jpeg images.
/// </summary>
public sealed class JpegDecoderOptions : ISpecializedDecoderOptions
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JpegDecoderOptions"/> class with the specified general options.
    /// </summary>
    /// <param name="generalOptions">The general options used to decode and identify images.</param>
    /// <exception cref="ArgumentNullException">The general options are null.</exception>
    public JpegDecoderOptions(DecoderOptions generalOptions)
    {
        Guard.NotNull(generalOptions, nameof(generalOptions));

        this.GeneralOptions = generalOptions;
    }

    /// <inheritdoc/>
    public DecoderOptions GeneralOptions { get; }

    /// <summary>
    /// Gets the resize mode.
    /// </summary>
    public JpegDecoderResizeMode ResizeMode { get; init; }
}
