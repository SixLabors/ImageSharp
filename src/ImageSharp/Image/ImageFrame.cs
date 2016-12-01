// <copyright file="ImageFrame.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;
    using System.Numerics;
    using System.Threading.Tasks;

    /// <summary>
    /// Represents a single frame in a animation.
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
    public class ImageFrame<TColor, TPacked> : ImageBase<TColor, TPacked>
        where TColor : struct, IPackedPixel<TPacked>
        where TPacked : struct
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ImageFrame{TColor, TPacked}"/> class.
        /// </summary>
        public ImageFrame()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageFrame{TColor, TPacked}"/> class.
        /// </summary>
        /// <param name="image">The image to create the frame from.</param>
        public ImageFrame(ImageBase<TColor, TPacked> image)
            : base(image)
        {
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"ImageFrame: {this.Width}x{this.Height}";
        }

        /// <summary>
        /// Returns a copy of the image frame in the given pixel format.
        /// </summary>
        /// <param name="scaleFunc">A function that allows for the correction of vector scaling between unknown color formats.</param>
        /// <typeparam name="TColor2">The pixel format.</typeparam>
        /// <typeparam name="TPacked2">The packed format. <example>uint, long, float.</example></typeparam>
        /// <returns>The <see cref="ImageFrame{TColor2, TPacked2}"/></returns>
        public ImageFrame<TColor2, TPacked2> To<TColor2, TPacked2>(Func<Vector4, Vector4> scaleFunc = null)
            where TColor2 : struct, IPackedPixel<TPacked2>
            where TPacked2 : struct
        {
            scaleFunc = PackedPixelConverterHelper.ComputeScaleFunction<TColor, TColor2>(scaleFunc);

            ImageFrame<TColor2, TPacked2> target = new ImageFrame<TColor2, TPacked2>
            {
                Quality = this.Quality,
                FrameDelay = this.FrameDelay
            };

            target.InitPixels(this.Width, this.Height);

            using (PixelAccessor<TColor, TPacked> pixels = this.Lock())
            using (PixelAccessor<TColor2, TPacked2> targetPixels = target.Lock())
            {
                Parallel.For(
                    0,
                    target.Height,
                    Bootstrapper.Instance.ParallelOptions,
                    y =>
                    {
                        for (int x = 0; x < target.Width; x++)
                        {
                            TColor2 color = default(TColor2);
                            color.PackFromVector4(scaleFunc(pixels[x, y].ToVector4()));
                            targetPixels[x, y] = color;
                        }
                    });
            }

            return target;
        }

        /// <summary>
        /// Clones the current instance.
        /// </summary>
        /// <returns>The <see cref="ImageFrame{TColor, TPacked}"/></returns>
        internal virtual ImageFrame<TColor, TPacked> Clone()
        {
            return new ImageFrame<TColor, TPacked>(this);
        }
    }
}