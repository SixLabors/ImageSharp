// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl;

/// <summary>
/// Provides options for decoding JPEG XL images.
/// </summary>
public sealed class JxlDecoderOptions : ISpecializedDecoderOptions
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JxlDecoderOptions"/> class.
    /// </summary>
    /// <param name="generalOptions">The general options used to decode and identify images.</param>
    /// <exception cref="ArgumentNullException">The general options are null.</exception>
    public JxlDecoderOptions(DecoderOptions generalOptions)
    {
        Guard.NotNull(generalOptions, nameof(generalOptions));

        this.GeneralOptions = generalOptions;
    }

    /// <inheritdoc/>
    public DecoderOptions GeneralOptions { get; }
}
