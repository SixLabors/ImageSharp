// <copyright file="ImageFrame{TPixel}.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;
    using System.Numerics;
    using System.Threading.Tasks;
    using ImageSharp.PixelFormats;

    /// <summary>
    /// Represents a single frame in a animation.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    public class ImageFrame<TPixel> : ImageBase<TPixel>, IImageFrame
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ImageFrame{TPixel}"/> class.
        /// </summary>
        /// <param name="width">The width of the image in pixels.</param>
        /// <param name="height">The height of the image in pixels.</param>
        /// <param name="configuration">
        /// The configuration providing initialization code which allows extending the library.
        /// </param>
        public ImageFrame(int width, int height, Configuration configuration = null)
            : base(configuration, width, height)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageFrame{TPixel}"/> class.
        /// </summary>
        /// <param name="image">The image to create the frame from.</param>
        public ImageFrame(ImageFrame<TPixel> image)
            : base(image)
        {
            this.CopyProperties(image);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageFrame{TPixel}"/> class.
        /// </summary>
        /// <param name="image">The image to create the frame from.</param>
        public ImageFrame(ImageBase<TPixel> image)
            : base(image)
        {
        }

        /// <summary>
        /// Gets the meta data of the frame.
        /// </summary>
        public ImageFrameMetaData MetaData { get; private set; } = new ImageFrameMetaData();

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"ImageFrame: {this.Width}x{this.Height}";
        }

        /// <summary>
        /// Returns a copy of the image frame in the given pixel format.
        /// </summary>
        /// <param name="scaleFunc">A function that allows for the correction of vector scaling between unknown color formats.</param>
        /// <typeparam name="TPixel2">The pixel format.</typeparam>
        /// <returns>The <see cref="ImageFrame{TPixel2}"/></returns>
        public ImageFrame<TPixel2> To<TPixel2>(Func<Vector4, Vector4> scaleFunc = null)
            where TPixel2 : struct, IPixel<TPixel2>
        {
            scaleFunc = PackedPixelConverterHelper.ComputeScaleFunction<TPixel, TPixel2>(scaleFunc);

            ImageFrame<TPixel2> target = new ImageFrame<TPixel2>(this.Width, this.Height, this.Configuration);
            target.CopyProperties(this);

            using (PixelAccessor<TPixel> pixels = this.Lock())
            using (PixelAccessor<TPixel2> targetPixels = target.Lock())
            {
                Parallel.For(
                    0,
                    target.Height,
                    this.Configuration.ParallelOptions,
                    y =>
                    {
                        for (int x = 0; x < target.Width; x++)
                        {
                            TPixel2 color = default(TPixel2);
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
        /// <returns>The <see cref="ImageFrame{TPixel}"/></returns>
        internal virtual ImageFrame<TPixel> Clone()
        {
            return new ImageFrame<TPixel>(this);
        }

        /// <summary>
        /// Copies the properties from the other <see cref="IImageFrame"/>.
        /// </summary>
        /// <param name="other">
        /// The other <see cref="IImageFrame"/> to copy the properties from.
        /// </param>
        private void CopyProperties(IImageFrame other)
        {
            base.CopyProperties(other);

            this.MetaData = new ImageFrameMetaData(other.MetaData);
        }
    }
}