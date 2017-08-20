// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.MetaData;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// Encapsulates an image, which consists of the pixel data for a graphics image and its attributes.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    public sealed class Image<TPixel> : ImageBase<TPixel>, IImage
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Image{TPixel}"/> class
        /// with the height and the width of the image.
        /// </summary>
        /// <param name="configuration">
        /// The configuration providing initialization code which allows extending the library.
        /// </param>
        /// <param name="width">The width of the image in pixels.</param>
        /// <param name="height">The height of the image in pixels.</param>
        public Image(Configuration configuration, int width, int height)
            : this(configuration, width, height, new ImageMetaData())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Image{TPixel}"/> class
        /// with the height and the width of the image.
        /// </summary>
        /// <param name="width">The width of the image in pixels.</param>
        /// <param name="height">The height of the image in pixels.</param>
        public Image(int width, int height)
            : this(null, width, height)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Image{TPixel}"/> class
        /// with the height and the width of the image.
        /// </summary>
        /// <param name="configuration">
        /// The configuration providing initialization code which allows extending the library.
        /// </param>
        /// <param name="width">The width of the image in pixels.</param>
        /// <param name="height">The height of the image in pixels.</param>
        /// <param name="metadata">The images metadata.</param>
        internal Image(Configuration configuration, int width, int height, ImageMetaData metadata)
            : base(configuration, width, height)
        {
            this.MetaData = metadata ?? new ImageMetaData();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Image{TPixel}"/> class
        /// by making a copy from another image.
        /// </summary>
        /// <param name="other">The other image, where the clone should be made from.</param>
        /// <exception cref="System.ArgumentNullException"><paramref name="other"/> is null.</exception>
        private Image(Image<TPixel> other)
            : base(other)
        {
            foreach (ImageFrame<TPixel> frame in other.Frames)
            {
                if (frame != null)
                {
                    this.Frames.Add(frame.Clone());
                }
            }

            this.CopyProperties(other);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Image{TPixel}"/> class
        /// by making a copy from another image.
        /// </summary>
        /// <param name="other">The other image, where the clone should be made from.</param>
        /// <exception cref="System.ArgumentNullException"><paramref name="other"/> is null.</exception>
        private Image(ImageBase<TPixel> other)
            : base(other)
        {
        }

        /// <summary>
        /// Gets the meta data of the image.
        /// </summary>
        public ImageMetaData MetaData { get; private set; } = new ImageMetaData();

        /// <summary>
        /// Gets the other frames associated with this image.
        /// </summary>
        /// <value>The list of frame images.</value>
        public IList<ImageFrame<TPixel>> Frames { get; } = new List<ImageFrame<TPixel>>();

        /// <summary>
        /// Saves the image to the given stream using the given image encoder.
        /// </summary>
        /// <param name="stream">The stream to save the image to.</param>
        /// <param name="encoder">The encoder to save the image with.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if the stream or encoder is null.</exception>
        public void Save(Stream stream, IImageEncoder encoder)
        {
            Guard.NotNull(stream, nameof(stream));
            Guard.NotNull(encoder, nameof(encoder));

            encoder.Encode(this, stream);
        }

        /// <summary>
        /// Clones the current image
        /// </summary>
        /// <returns>Returns a new image with all the same metadata as the original.</returns>
        public new Image<TPixel> Clone()
        {
            return new Image<TPixel>(this);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"Image<{typeof(TPixel).Name}>: {this.Width}x{this.Height}";
        }

        /// <summary>
        /// Returns a copy of the image in the given pixel format.
        /// </summary>
        /// <typeparam name="TPixel2">The pixel format.</typeparam>
        /// <returns>The <see cref="Image{TPixel2}"/></returns>
        public Image<TPixel2> CloneAs<TPixel2>()
            where TPixel2 : struct, IPixel<TPixel2>
        {
            if (typeof(TPixel2) == typeof(TPixel))
            {
                // short circuit when same pixel types
                return this.Clone() as Image<TPixel2>;
            }

            Func<Vector4, Vector4> scaleFunc = PackedPixelConverterHelper.ComputeScaleFunction<TPixel, TPixel2>();

            var target = new Image<TPixel2>(this.Configuration, this.Width, this.Height);
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
                                var color = default(TPixel2);
                                color.PackFromVector4(scaleFunc(pixels[x, y].ToVector4()));
                                targetPixels[x, y] = color;
                            }
                        });
            }

            for (int i = 0; i < this.Frames.Count; i++)
            {
                target.Frames.Add(this.Frames[i].CloneAs<TPixel2>());
            }

            return target;
        }

        /// <summary>
        /// Creates a new <see cref="ImageFrame{TPixel}"/> from this instance
        /// </summary>
        /// <returns>The <see cref="ImageFrame{TPixel}"/></returns>
        internal ImageFrame<TPixel> ToFrame()
        {
            return new ImageFrame<TPixel>(this);
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            // ReSharper disable once ForCanBeConvertedToForeach
            for (int i = 0; i < this.Frames.Count; i++)
            {
                this.Frames[i].Dispose();
            }

            base.Dispose(disposing);
        }

        /// <inheritdoc/>
        protected override ImageBase<TPixel> CloneImageBase()
        {
            return this.Clone();
        }

        /// <summary>
        /// Copies the properties from the other <see cref="IImage"/>.
        /// </summary>
        /// <param name="other">
        /// The other <see cref="IImage"/> to copy the properties from.
        /// </param>
        private void CopyProperties(IImage other)
        {
            this.MetaData = new ImageMetaData(other.MetaData);
        }
    }
}