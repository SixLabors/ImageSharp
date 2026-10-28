// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Webp;

/// <summary>
/// Configuration options for decoding webp images.
/// </summary>
public sealed class WebpDecoderOptions : ISpecializedDecoderOptions
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WebpDecoderOptions"/> class with the specified general options.
    /// </summary>
    /// <param name="generalOptions">The general options used to decode and identify images.</param>
    /// <exception cref="ArgumentNullException">The general options are null.</exception>
    public WebpDecoderOptions(DecoderOptions generalOptions)
    {
        Guard.NotNull(generalOptions, nameof(generalOptions));

        this.GeneralOptions = generalOptions;
    }

    /// <inheritdoc/>
    public DecoderOptions GeneralOptions { get; }

    /// <summary>
    /// Gets the flag to decide how to handle the background color Animation Chunk.
    /// The specification is vague on how to handle the background color of the animation chunk.
    /// This option let's the user choose how to deal with it.
    /// </summary>
    /// <see href="https://developers.google.com/speed/webp/docs/riff_container#animation"/>
    public BackgroundColorHandling BackgroundColorHandling { get; init; } = BackgroundColorHandling.Standard;
}
