// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Quantization;

/// <summary>
/// Allows the quantization of images pixels using color palettes.
/// </summary>
public class PaletteQuantizer : IQuantizer
{
    private readonly ReadOnlyMemory<Color> colorPalette;
    private readonly int transparentIndex;

    /// <summary>
    /// Initializes a new instance of the <see cref="PaletteQuantizer"/> class.
    /// </summary>
    /// <param name="palette">The color palette.</param>
    public PaletteQuantizer(ReadOnlyMemory<Color> palette)
        : this(palette, new())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PaletteQuantizer"/> class.
    /// </summary>
    /// <param name="palette">The color palette.</param>
    /// <param name="options">The quantizer options defining quantization rules.</param>
    public PaletteQuantizer(ReadOnlyMemory<Color> palette, QuantizerOptions options)
        : this(palette, options, -1)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PaletteQuantizer"/> class.
    /// </summary>
    /// <param name="palette">The color palette.</param>
    /// <param name="options">The quantizer options defining quantization rules.</param>
    /// <param name="transparentIndex">An explicit index at which to match transparent pixels.</param>
    internal PaletteQuantizer(ReadOnlyMemory<Color> palette, QuantizerOptions options, int transparentIndex)
    {
        Guard.MustBeGreaterThan(palette.Length, 0, nameof(palette));
        Guard.NotNull(options, nameof(options));

        this.colorPalette = palette;
        this.Options = options;
        this.transparentIndex = transparentIndex;
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
    {
        Guard.NotNull(options, nameof(options));

        // If the palette is larger than the max colors then we need to trim it down.
        // treat the buffer as FILO.
        TPixel[] palette = new TPixel[Math.Min(options.MaxColors, this.colorPalette.Length)];
        Color.ToPixel(this.colorPalette.Span[..palette.Length], palette.AsSpan());
        return new PaletteQuantizer<TPixel>(configuration, options, palette, this.transparentIndex);
    }
}
