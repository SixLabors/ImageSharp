// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Threading.Tasks;
using SixLabors.ImageSharp.MetaData;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// Represents a single frame in a animation.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    public sealed class ImageFrame<TPixel> : ImageBase<TPixel>, IImageFrame
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ImageFrame{TPixel}"/> class.
        /// </summary>
        /// <param name="configuration">
        /// The configuration providing initialization code which allows extending the library.
        /// </param>
        /// <param name="width">The width of the image in pixels.</param>
        /// <param name="height">The height of the image in pixels.</param>
        public ImageFrame(Configuration configuration, int width, int height)
            : base(configuration, width, height)
        {
            this.MetaData = new ImageFrameMetaData();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageFrame{TPixel}"/> class.
        /// </summary>
        /// <param name="configuration">
        /// The configuration providing initialization code which allows extending the library.
        /// </param>
        /// <param name="width">The width of the image in pixels.</param>
        /// <param name="height">The height of the image in pixels.</param>
        /// <param name="metadata">The metadata of the frame.</param>
        public ImageFrame(Configuration configuration, int width, int height, ImageFrameMetaData metadata)
            : base(configuration, width, height)
        {
            Guard.NotNull(metadata, nameof(metadata));
            this.MetaData = metadata;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageFrame{TPixel}"/> class.
        /// </summary>
        /// <param name="width">The width of the image in pixels.</param>
        /// <param name="height">The height of the image in pixels.</param>
        public ImageFrame(int width, int height)
            : this(null, width, height)
        {
            this.MetaData = new ImageFrameMetaData();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageFrame{TPixel}"/> class.
        /// </summary>
        /// <param name="image">The image to create the frame from.</param>
        internal ImageFrame(ImageBase<TPixel> image)
            : base(image)
        {
            this.MetaData = new ImageFrameMetaData();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageFrame{TPixel}"/> class.
        /// </summary>
        /// <param name="image">The image to create the frame from.</param>
        private ImageFrame(ImageFrame<TPixel> image)
            : base(image)
        {
            this.CopyProperties(image);
        }

        /// <summary>
        /// Gets the meta data of the frame.
        /// </summary>
        public ImageFrameMetaData MetaData { get; private set; }

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"ImageFrame<{typeof(TPixel).Name}>: {this.Width}x{this.Height}";
        }

        /// <summary>
        /// Returns a copy of the image frame in the given pixel format.
        /// </summary>
        /// <typeparam name="TPixel2">The pixel format.</typeparam>
        /// <returns>The <see cref="ImageFrame{TPixel2}"/></returns>
        public ImageFrame<TPixel2> CloneAs<TPixel2>()
            where TPixel2 : struct, IPixel<TPixel2>
        {
            if (typeof(TPixel2) == typeof(TPixel))
            {
                return this.Clone() as ImageFrame<TPixel2>;
            }

            Func<Vector4, Vector4> scaleFunc = PackedPixelConverterHelper.ComputeScaleFunction<TPixel, TPixel2>();

            var target = new ImageFrame<TPixel2>(this.Configuration, this.Width, this.Height);
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
        public new ImageFrame<TPixel> Clone()
        {
            return new ImageFrame<TPixel>(this);
        }

        /// <inheritdoc/>
        protected override ImageBase<TPixel> CloneImageBase()
        {
            return this.Clone();
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