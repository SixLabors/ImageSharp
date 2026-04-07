// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Quantization;

/// <summary>
/// Quantizes images by grouping colors in an adaptive 16-way tree and reducing those groups into a palette.
/// </summary>
/// <remarks>
/// Each level routes colors using one bit of RGB and, when useful, one bit of alpha. Fully opaque mid-tone colors
/// use RGB-only routing so more branch resolution is spent on visible color detail, while transparent, dark, and
/// light colors use alpha-aware routing so opacity changes can form their own palette buckets.
/// </remarks>
public class HexadecatreeQuantizer : IQuantizer
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HexadecatreeQuantizer"/> class
    /// using the default <see cref="QuantizerOptions"/>.
    /// </summary>
    public HexadecatreeQuantizer()
        : this(new QuantizerOptions())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HexadecatreeQuantizer"/> class.
    /// </summary>
    /// <param name="options">The quantizer options that control palette size, dithering, and transparency behavior.</param>
    public HexadecatreeQuantizer(QuantizerOptions options)
    {
        Guard.NotNull(options, nameof(options));
        this.Options = options;
    }

    /// <inheritdoc />
    public QuantizerOptions Options { get; }

    /// <inheritdoc />
    public IQuantizer<TPixel> CreatePixelSpecificQuantizer<TPixel>(Configuration configuration)
        where TPixel : unmanaged, IPixel<TPixel>
        => this.CreatePixelSpecificQuantizer<TPixel>(configuration, this.Options);

    /// <inheritdoc />
    public IQuantizer<TPixel> CreatePixelSpecificQuantizer<TPixel>(Configuration configuration, QuantizerOptions options)
        where TPixel : unmanaged, IPixel<TPixel>
        => new HexadecatreeQuantizer<TPixel>(configuration, options);
}
