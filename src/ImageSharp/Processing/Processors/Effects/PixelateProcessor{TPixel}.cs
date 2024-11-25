// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Effects;

/// <summary>
/// Applies a pixelation effect processing to the image.
/// </summary>
/// <typeparam name="TPixel">The pixel format.</typeparam>
internal class PixelateProcessor<TPixel> : ImageProcessor<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    private readonly PixelateProcessor definition;

    /// <summary>
    /// Initializes a new instance of the <see cref="PixelateProcessor{TPixel}"/> class.
    /// </summary>
    /// <param name="configuration">The configuration which allows altering default behaviour or extending the library.</param>
    /// <param name="definition">The <see cref="PixelateProcessor"/>.</param>
    /// <param name="source">The source <see cref="Image{TPixel}"/> for the current processor instance.</param>
    /// <param name="sourceRectangle">The source area to process for the current processor instance.</param>
    public PixelateProcessor(Configuration configuration, PixelateProcessor definition, Image<TPixel> source, Rectangle sourceRectangle)
        : base(configuration, source, sourceRectangle)
        => this.definition = definition;

    private int Size => this.definition.Size;

    /// <inheritdoc/>
    protected override void OnFrameApply(ImageFrame<TPixel> source)
    {
        Rectangle interest = Rectangle.Intersect(this.SourceRectangle, source.Bounds);
        int size = this.Size;

        Guard.MustBeBetweenOrEqualTo(size, 0, interest.Width, nameof(size));
        Guard.MustBeBetweenOrEqualTo(size, 0, interest.Height, nameof(size));

        // Get the range on the y-plane to choose from.
        // TODO: It would be nice to be able to pool this somehow but neither Memory<T> nor Span<T>
        // implement IEnumerable<T>.
        IEnumerable<int> range = EnumerableExtensions.SteppedRange(interest.Y, i => i < interest.Bottom, size);
        Parallel.ForEach(
        range,
        this.Configuration.GetParallelOptions(),
        new RowOperation(interest, size, source.PixelBuffer).Invoke);
    }

    private readonly struct RowOperation
    {
        private readonly int minX;
        private readonly int maxX;
        private readonly int maxXIndex;
        private readonly int maxY;
        private readonly int maxYIndex;
        private readonly int size;
        private readonly int radius;
        private readonly Buffer2D<TPixel> source;

        [MethodImpl(InliningOptions.ShortMethod)]
        public RowOperation(
            Rectangle bounds,
            int size,
            Buffer2D<TPixel> source)
        {
            this.minX = bounds.X;
            this.maxX = bounds.Right;
            this.maxXIndex = bounds.Right - 1;
            this.maxY = bounds.Bottom;
            this.maxYIndex = bounds.Bottom - 1;
            this.size = size;
            this.radius = size >> 1;
            this.source = source;
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        public void Invoke(int y)
        {
            Span<TPixel> rowSpan = this.source.DangerousGetRowSpan(Math.Min(y + this.radius, this.maxYIndex));

            for (int x = this.minX; x < this.maxX; x += this.size)
            {
                // Get the pixel color in the centre of the soon to be pixelated area.
                TPixel pixel = rowSpan[Math.Min(x + this.radius, this.maxXIndex)];

                // For each pixel in the pixelate size, set it to the centre color.
                for (int oY = y; oY < y + this.size && oY < this.maxY; oY++)
                {
                    for (int oX = x; oX < x + this.size && oX < this.maxX; oX++)
                    {
                        this.source[oX, oY] = pixel;
                    }
                }
            }
        }
    }
}
