// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Quantization;

/// <summary>
/// Encapsulates methods to create a quantized image based upon the given palette.
/// <see href="http://msdn.microsoft.com/en-us/library/aa479306.aspx"/>
/// </summary>
/// <typeparam name="TPixel">The pixel format.</typeparam>
[SuppressMessage(
    "Design",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "https://github.com/dotnet/roslyn-analyzers/issues/6151")]
internal struct PaletteQuantizer<TPixel> : IQuantizer<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    private readonly PixelMap<TPixel> pixelMap;
    private int transparencyIndex;
    private TPixel transparentColor;

    /// <summary>
    /// Initializes a new instance of the <see cref="PaletteQuantizer{TPixel}"/> struct.
    /// </summary>
    /// <param name="configuration">The configuration which allows altering default behavior or extending the library.</param>
    /// <param name="options">The quantizer options defining quantization rules.</param>
    /// <param name="palette">The palette to use.</param>
    [MethodImpl(InliningOptions.ShortMethod)]
    public PaletteQuantizer(Configuration configuration, QuantizerOptions options, ReadOnlyMemory<TPixel> palette)
        : this(configuration, options, palette, -1, default)
    {
        Guard.NotNull(configuration, nameof(configuration));
        Guard.NotNull(options, nameof(options));
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PaletteQuantizer{TPixel}"/> struct.
    /// </summary>
    /// <param name="configuration">The configuration which allows altering default behavior or extending the library.</param>
    /// <param name="options">The quantizer options defining quantization rules.</param>
    /// <param name="palette">The palette to use.</param>
    /// <param name="transparencyIndex">The index of the color in the palette that should be considered as transparent.</param>
    /// <param name="transparentColor">The color that should be considered as transparent.</param>
    public PaletteQuantizer(
        Configuration configuration,
        QuantizerOptions options,
        ReadOnlyMemory<TPixel> palette,
        int transparencyIndex,
        TPixel transparentColor)
    {
        Guard.NotNull(configuration, nameof(configuration));
        Guard.NotNull(options, nameof(options));

        this.Configuration = configuration;
        this.Options = options;
        this.pixelMap = PixelMapFactory.Create(this.Configuration, palette, options.ColorMatchingMode);
        this.transparencyIndex = transparencyIndex;
        this.transparentColor = transparentColor;
    }

    /// <inheritdoc/>
    public Configuration Configuration { get; }

    /// <inheritdoc/>
    public QuantizerOptions Options { get; }

    /// <inheritdoc/>
    public readonly ReadOnlyMemory<TPixel> Palette => this.pixelMap.Palette;

    /// <inheritdoc/>
    [MethodImpl(InliningOptions.ShortMethod)]
    public readonly void AddPaletteColors(in Buffer2DRegion<TPixel> pixelRegion)
    {
    }

    /// <inheritdoc/>
    [MethodImpl(InliningOptions.ShortMethod)]
    public readonly IndexedImageFrame<TPixel> QuantizeFrame(ImageFrame<TPixel> source, Rectangle bounds)
        => QuantizerUtilities.QuantizeFrame(ref Unsafe.AsRef(in this), source, bounds);

    /// <inheritdoc/>
    [MethodImpl(InliningOptions.ShortMethod)]
    public readonly byte GetQuantizedColor(TPixel color, out TPixel match)
    {
        if (this.transparencyIndex >= 0 && color.Equals(this.transparentColor))
        {
            match = this.transparentColor;
            return (byte)this.transparencyIndex;
        }

        return (byte)this.pixelMap.GetClosestColor(color, out match);
    }

    public void SetTransparencyIndex(int transparencyIndex, TPixel transparentColor)
    {
        this.transparencyIndex = transparencyIndex;
        this.transparentColor = transparentColor;
    }

    /// <inheritdoc/>
    public readonly void Dispose() => this.pixelMap.Dispose();
}
