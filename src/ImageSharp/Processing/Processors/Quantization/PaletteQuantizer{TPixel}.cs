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
internal readonly struct PaletteQuantizer<TPixel> : IQuantizer<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    private readonly EuclideanPixelMap<TPixel> pixelMap;

    /// <summary>
    /// Initializes a new instance of the <see cref="PaletteQuantizer{TPixel}"/> struct.
    /// </summary>
    /// <param name="configuration">The configuration which allows altering default behavior or extending the library.</param>
    /// <param name="options">The quantizer options defining quantization rules.</param>
    /// <param name="palette">The palette to use.</param>
    /// <param name="transparentIndex">An explicit index at which to match transparent pixels.</param>
    [MethodImpl(InliningOptions.ShortMethod)]
    public PaletteQuantizer(
        Configuration configuration,
        QuantizerOptions options,
        ReadOnlyMemory<TPixel> palette,
        int transparentIndex)
    {
        Guard.NotNull(configuration, nameof(configuration));
        Guard.NotNull(options, nameof(options));

        this.Configuration = configuration;
        this.Options = options;
        this.pixelMap = new(configuration, palette, transparentIndex);
    }

    /// <inheritdoc/>
    public Configuration Configuration { get; }

    /// <inheritdoc/>
    public QuantizerOptions Options { get; }

    /// <inheritdoc/>
    public ReadOnlyMemory<TPixel> Palette => this.pixelMap.Palette;

    /// <inheritdoc/>
    [MethodImpl(InliningOptions.ShortMethod)]
    public readonly IndexedImageFrame<TPixel> QuantizeFrame(ImageFrame<TPixel> source, Rectangle bounds)
        => QuantizerUtilities.QuantizeFrame(ref Unsafe.AsRef(in this), source, bounds);

    /// <inheritdoc/>
    [MethodImpl(InliningOptions.ShortMethod)]
    public void AddPaletteColors(Buffer2DRegion<TPixel> pixelRegion)
    {
    }

    /// <summary>
    /// Allows setting the transparent index after construction.
    /// </summary>
    /// <param name="index">An explicit index at which to match transparent pixels.</param>
    public void SetTransparentIndex(int index) => this.pixelMap.SetTransparentIndex(index);

    /// <inheritdoc/>
    [MethodImpl(InliningOptions.ShortMethod)]
    public readonly byte GetQuantizedColor(TPixel color, out TPixel match)
        => (byte)this.pixelMap.GetClosestColor(color, out match);

    /// <inheritdoc/>
    public void Dispose() => this.pixelMap.Dispose();
}
