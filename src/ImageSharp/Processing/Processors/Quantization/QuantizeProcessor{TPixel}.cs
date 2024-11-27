// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Quantization;

/// <summary>
/// Enables the quantization of images to reduce the number of colors used in the image palette.
/// </summary>
/// <typeparam name="TPixel">The pixel format.</typeparam>
internal class QuantizeProcessor<TPixel> : ImageProcessor<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    private readonly IQuantizer quantizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="QuantizeProcessor{TPixel}"/> class.
    /// </summary>
    /// <param name="configuration">The configuration which allows altering default behaviour or extending the library.</param>
    /// <param name="quantizer">The quantizer used to reduce the color palette.</param>
    /// <param name="source">The source <see cref="Image{TPixel}"/> for the current processor instance.</param>
    /// <param name="sourceRectangle">The source area to process for the current processor instance.</param>
    public QuantizeProcessor(Configuration configuration, IQuantizer quantizer, Image<TPixel> source, Rectangle sourceRectangle)
        : base(configuration, source, sourceRectangle)
    {
        Guard.NotNull(quantizer, nameof(quantizer));
        this.quantizer = quantizer;
    }

    /// <inheritdoc />
    protected override void OnFrameApply(ImageFrame<TPixel> source)
    {
        Rectangle interest = Rectangle.Intersect(source.Bounds, this.SourceRectangle);

        Configuration configuration = this.Configuration;
        using IQuantizer<TPixel> frameQuantizer = this.quantizer.CreatePixelSpecificQuantizer<TPixel>(configuration);
        using IndexedImageFrame<TPixel> quantized = frameQuantizer.BuildPaletteAndQuantizeFrame(source, interest);

        ReadOnlySpan<TPixel> paletteSpan = quantized.Palette.Span;
        int offsetY = interest.Top;
        int offsetX = interest.Left;
        Buffer2D<TPixel> sourceBuffer = source.PixelBuffer;

        for (int y = 0; y < quantized.Height; y++)
        {
            ReadOnlySpan<byte> quantizedRow = quantized.DangerousGetRowSpan(y);
            Span<TPixel> row = sourceBuffer.DangerousGetRowSpan(y + offsetY);

            for (int x = 0; x < quantized.Width; x++)
            {
                row[x + offsetX] = paletteSpan[quantizedRow[x]];
            }
        }
    }
}
