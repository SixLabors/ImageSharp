// <copyright file="ImageBrush{TPixel}.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Drawing.Brushes
{
    using System.Numerics;
    using ImageSharp.PixelFormats;
    using Processors;

    /// <summary>
    /// Provides an implementation of an image brush for painting images within areas.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    public class ImageBrush<TPixel> : IBrush<TPixel>
    where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// The image to paint.
        /// </summary>
        private readonly IImageBase<TPixel> image;

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageBrush{TPixel}"/> class.
        /// </summary>
        /// <param name="image">The image.</param>
        public ImageBrush(IImageBase<TPixel> image)
        {
            this.image = image;
        }

        /// <inheritdoc />
        public BrushApplicator<TPixel> CreateApplicator(PixelAccessor<TPixel> sourcePixels, RectangleF region)
        {
            return new ImageBrushApplicator(sourcePixels, this.image, region);
        }

        /// <summary>
        /// The image brush applicator.
        /// </summary>
        private class ImageBrushApplicator : BrushApplicator<TPixel>
        {
            /// <summary>
            /// The source pixel accessor.
            /// </summary>
            private readonly PixelAccessor<TPixel> source;

            /// <summary>
            /// The y-length.
            /// </summary>
            private readonly int yLength;

            /// <summary>
            /// The x-length.
            /// </summary>
            private readonly int xLength;

            /// <summary>
            /// The offset.
            /// </summary>
            private readonly Vector2 offset;

            /// <summary>
            /// Initializes a new instance of the <see cref="ImageBrushApplicator"/> class.
            /// </summary>
            /// <param name="image">
            /// The image.
            /// </param>
            /// <param name="region">
            /// The region.
            /// </param>
            /// <param name="sourcePixels">
            /// The sourcePixels.
            /// </param>
            public ImageBrushApplicator(PixelAccessor<TPixel> sourcePixels, IImageBase<TPixel> image, RectangleF region)
                : base(sourcePixels)
            {
                this.source = image.Lock();
                this.xLength = image.Width;
                this.yLength = image.Height;
                this.offset = new Vector2(MathF.Max(MathF.Floor(region.Top), 0), MathF.Max(MathF.Floor(region.Left), 0));
            }

            /// <summary>
            /// Gets the color for a single pixel.
            /// </summary>
            /// <param name="x">The x.</param>
            /// <param name="y">The y.</param>
            /// <returns>
            /// The color
            /// </returns>
            internal override TPixel this[int x, int y]
            {
                get
                {
                    Vector2 point = new Vector2(x, y);

                    // Offset the requested pixel by the value in the rectangle (the shapes position)
                    point = point - this.offset;
                    int srcX = (int)point.X % this.xLength;
                    int srcY = (int)point.Y % this.yLength;

                    return this.source[srcX, srcY];
                }
            }

            /// <inheritdoc />
            public override void Dispose()
            {
                this.source.Dispose();
            }

            /// <inheritdoc />
            internal override void Apply(float[] scanlineBuffer, int scanlineWidth, int offset, int x, int y)
            {
                Guard.MustBeGreaterThanOrEqualTo(scanlineBuffer.Length, offset + scanlineWidth, nameof(scanlineWidth));

                using (Buffer<float> buffer = new Buffer<float>(scanlineBuffer))
                {
                    BufferSpan<float> slice = buffer.Slice(offset);

                    for (int xPos = 0; xPos < scanlineWidth; xPos++)
                    {
                        int targetX = xPos + x;
                        int targetY = y;

                        float opacity = slice[xPos];
                        if (opacity > Constants.Epsilon)
                        {
                            Vector4 backgroundVector = this.Target[targetX, targetY].ToVector4();

                            Vector4 sourceVector = this[targetX, targetY].ToVector4();

                            Vector4 finalColor = Vector4BlendTransforms.PremultipliedLerp(backgroundVector, sourceVector, opacity);

                            TPixel packed = default(TPixel);
                            packed.PackFromVector4(finalColor);
                            this.Target[targetX, targetY] = packed;
                        }
                    }
                }
            }
        }
    }
}