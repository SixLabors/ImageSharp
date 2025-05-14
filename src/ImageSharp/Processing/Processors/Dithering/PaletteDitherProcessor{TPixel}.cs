// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Quantization;

namespace SixLabors.ImageSharp.Processing.Processors.Dithering;

/// <summary>
/// Allows the consumption a palette to dither an image.
/// </summary>
/// <typeparam name="TPixel">The pixel format.</typeparam>
internal sealed class PaletteDitherProcessor<TPixel> : ImageProcessor<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    private readonly DitherProcessor ditherProcessor;
    private readonly IDither dither;
    private IMemoryOwner<TPixel>? paletteOwner;
    private bool isDisposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="PaletteDitherProcessor{TPixel}"/> class.
    /// </summary>
    /// <param name="configuration">The configuration which allows altering default behaviour or extending the library.</param>
    /// <param name="definition">The <see cref="PaletteDitherProcessor"/> defining the processor parameters.</param>
    /// <param name="source">The source <see cref="Image{TPixel}"/> for the current processor instance.</param>
    /// <param name="sourceRectangle">The source area to process for the current processor instance.</param>
    public PaletteDitherProcessor(Configuration configuration, PaletteDitherProcessor definition, Image<TPixel> source, Rectangle sourceRectangle)
        : base(configuration, source, sourceRectangle)
    {
        this.dither = definition.Dither;

        ReadOnlySpan<Color> sourcePalette = definition.Palette.Span;
        this.paletteOwner = this.Configuration.MemoryAllocator.Allocate<TPixel>(sourcePalette.Length);
        Color.ToPixel(sourcePalette, this.paletteOwner.Memory.Span);

        this.ditherProcessor = new DitherProcessor(
            this.Configuration,
            this.paletteOwner.Memory,
            definition.DitherScale);
    }

    /// <inheritdoc/>
    protected override void OnFrameApply(ImageFrame<TPixel> source)
    {
        Rectangle interest = Rectangle.Intersect(this.SourceRectangle, source.Bounds);
        this.dither.ApplyPaletteDither(in this.ditherProcessor, source, interest);
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (this.isDisposed)
        {
            return;
        }

        this.isDisposed = true;
        if (disposing)
        {
            this.paletteOwner?.Dispose();
            this.ditherProcessor.Dispose();
        }

        this.paletteOwner = null;
        base.Dispose(disposing);
    }

    /// <summary>
    /// Used to allow inlining of calls to
    /// <see cref="IPaletteDitherImageProcessor{TPixel}.GetPaletteColor(TPixel)"/>.
    /// </summary>
    /// <remarks>Internal for AOT</remarks>
    [SuppressMessage(
        "Design",
        "CA1001:Types that own disposable fields should be disposable",
        Justification = "https://github.com/dotnet/roslyn-analyzers/issues/6151")]
    internal readonly struct DitherProcessor : IPaletteDitherImageProcessor<TPixel>, IDisposable
    {
        private readonly PixelMap<TPixel> pixelMap;

        [MethodImpl(InliningOptions.ShortMethod)]
        public DitherProcessor(
            Configuration configuration,
            ReadOnlyMemory<TPixel> palette,
            float ditherScale)
        {
            this.Configuration = configuration;
            this.pixelMap = PixelMapFactory.Create(configuration, palette, ColorMatchingMode.Coarse);
            this.Palette = palette;
            this.DitherScale = ditherScale;
        }

        public Configuration Configuration { get; }

        public ReadOnlyMemory<TPixel> Palette { get; }

        public float DitherScale { get; }

        [MethodImpl(InliningOptions.ShortMethod)]
        public TPixel GetPaletteColor(TPixel color)
        {
            this.pixelMap.GetClosestColor(color, out TPixel match);
            return match;
        }

        public void Dispose() => this.pixelMap.Dispose();
    }
}
