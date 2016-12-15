// <copyright file="ImageBrush.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Drawing.Brushes
{
    using System;
    using System.Collections.Generic;
    using System.Numerics;
    using System.Threading.Tasks;
    using Processors;

    /// <summary>
    /// Provides an implementaion of a solid brush for painting with repeating images.
    /// </summary>
    public class ImageBrush : ImageBrush<Color, uint>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ImageBrush" /> class.
        /// </summary>
        /// <param name="color">The color.</param>
        public ImageBrush(IImageBase<Color, uint> image)
            : base(image)
        {
        }
    }

    /// <summary>
    /// Provides an implementaion of a solid brush for painting solid color areas.
    /// </summary>
    /// <typeparam name="TColor">The type of the color.</typeparam>
    /// <typeparam name="TPacked">The type of the packed.</typeparam>
    public class ImageBrush<TColor, TPacked> : IBrush<TColor, TPacked>
            where TColor : struct, IPackedPixel<TPacked>
            where TPacked : struct
    {
        private readonly IImageBase<TColor, TPacked> image;

        /// <summary>
        /// Initializes a new instance of the <see cref="SolidBrush{TColor, TPacked}"/> class.
        /// </summary>
        /// <param name="color">The color.</param>
        public ImageBrush(IImageBase<TColor, TPacked> image)
        {
            this.image = image;
        }

        /// <summary>
        /// Creates the applicator for this brush.
        /// </summary>
        /// <param name="region">The region the brush will be applied to.</param>
        /// <returns>
        /// The brush applicator for this brush
        /// </returns>
        /// <remarks>
        /// The <paramref name="region" /> when being applied to things like shapes would ussually be the
        /// bounding box of the shape not necessarily the bounds of the whole image
        /// </remarks>
        public IBrushApplicator<TColor, TPacked> CreateApplicator(RectangleF region)
        {
            return new ImageBrushApplicator(this.image, region);
        }

        private class ImageBrushApplicator : IBrushApplicator<TColor, TPacked>
        {
            private readonly PixelAccessor<TColor, TPacked> source;
            private readonly int yLength;
            private readonly int xLength;
            private readonly Vector2 offset;
            private readonly float YOffset;

            public ImageBrushApplicator(IImageBase<TColor, TPacked> image, RectangleF region)
            {
                this.source = image.Lock();
                this.xLength = image.Width;
                this.yLength = image.Height;
                this.offset = new Vector2((float)Math.Max(Math.Floor(region.Top), 0),
                                          (float)Math.Max(Math.Floor(region.Left), 0));
            }

            /// <summary>
            /// Gets the color for a single pixel.
            /// </summary>
            /// <param name="point">The point.</param>
            /// <returns>
            /// The color
            /// </returns>
            public TColor GetColor(Vector2 point)
            {
                //offset the requested pixel by the value in the rectangle (the shapes position)
                point = point - offset;
                var x = (int)point.X % this.xLength;
                var y = (int)point.Y % this.yLength;

                return source[x, y];
            }

            /// <summary>
            /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
            /// </summary>
            public void Dispose()
            {
                source.Dispose();
            }
        }
    }
}
