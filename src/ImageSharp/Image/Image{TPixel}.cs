// <copyright file="Image{TPixel}.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Numerics;
    using System.Text;
    using System.Threading.Tasks;

    using Formats;
    using ImageSharp.PixelFormats;
    using ImageSharp.Processing;
    using SixLabors.Primitives;

    /// <summary>
    /// Encapsulates an image, which consists of the pixel data for a graphics image and its attributes.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    [DebuggerDisplay("Image: {Width}x{Height}")]
    public class Image<TPixel> : ImageBase<TPixel>, IImage
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
        /// by making a copy from another image.
        /// </summary>
        /// <param name="other">The other image, where the clone should be made from.</param>
        /// <exception cref="System.ArgumentNullException"><paramref name="other"/> is null.</exception>
        public Image(Image<TPixel> other)
            : base(other)
        {
            foreach (ImageFrame<TPixel> frame in other.Frames)
            {
                if (frame != null)
                {
                    this.Frames.Add(new ImageFrame<TPixel>(frame));
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
        public Image(ImageBase<TPixel> other)
            : base(other)
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
        /// Gets the meta data of the image.
        /// </summary>
        public ImageMetaData MetaData { get; private set; } = new ImageMetaData();

        /// <summary>
        /// Gets the width of the image in inches. It is calculated as the width of the image
        /// in pixels multiplied with the density. When the density is equals or less than zero
        /// the default value is used.
        /// </summary>
        /// <value>The width of the image in inches.</value>
        public double InchWidth => this.Width / this.MetaData.HorizontalResolution;

        /// <summary>
        /// Gets the height of the image in inches. It is calculated as the height of the image
        /// in pixels multiplied with the density. When the density is equals or less than zero
        /// the default value is used.
        /// </summary>
        /// <value>The height of the image in inches.</value>
        public double InchHeight => this.Height / this.MetaData.VerticalResolution;

        /// <summary>
        /// Gets a value indicating whether this image is animated.
        /// </summary>
        /// <value>
        /// <c>True</c> if this image is animated; otherwise, <c>false</c>.
        /// </value>
        public bool IsAnimated => this.Frames.Count > 0;

        /// <summary>
        /// Gets the other frames for the animation.
        /// </summary>
        /// <value>The list of frame images.</value>
        public IList<ImageFrame<TPixel>> Frames { get; } = new List<ImageFrame<TPixel>>();

        /// <summary>
        /// Applies the processor to the image.
        /// </summary>
        /// <param name="processor">The processor to apply to the image.</param>
        /// <param name="rectangle">The <see cref="Rectangle" /> structure that specifies the portion of the image object to draw.</param>
        public virtual void ApplyProcessor(IImageProcessor<TPixel> processor, Rectangle rectangle)
        {
            // we want to put this on on here as it gives us a really go place to test/verify processor settings
            processor.Apply(this, rectangle);
        }

        /// <summary>
        /// Saves the image to the given stream using the currently loaded image format.
        /// </summary>
        /// <param name="stream">The stream to save the image to.</param>
        /// <param name="format">The format to save the image to.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if the stream is null.</exception>
        /// <returns>The <see cref="Image{TPixel}"/></returns>
        public Image<TPixel> Save(Stream stream, IImageFormat format)
        {
            Guard.NotNull(format, nameof(format));
            IImageEncoder encoder = this.Configuration.FindEncoder(format);

            if (encoder == null)
            {
                var stringBuilder = new StringBuilder();
                stringBuilder.AppendLine("Can't find encoder for provided mime type. Available encoded:");

                foreach (KeyValuePair<IImageFormat, IImageEncoder> val in this.Configuration.ImageEncoders)
                {
                    stringBuilder.AppendLine($" - {val.Key.Name} : {val.Value.GetType().Name}");
                }

                throw new NotSupportedException(stringBuilder.ToString());
            }

            return this.Save(stream, encoder);
        }

        /// <summary>
        /// Saves the image to the given stream using the given image encoder.
        /// </summary>
        /// <param name="stream">The stream to save the image to.</param>
        /// <param name="encoder">The encoder to save the image with.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if the stream or encoder is null.</exception>
        /// <returns>
        /// The <see cref="Image{TPixel}"/>.
        /// </returns>
        public Image<TPixel> Save(Stream stream, IImageEncoder encoder)
        {
            Guard.NotNull(stream, nameof(stream));
            Guard.NotNull(encoder, nameof(encoder));

            encoder.Encode(this, stream);

            return this;
        }

#if !NETSTANDARD1_1
        /// <summary>
        /// Saves the image to the given stream using the currently loaded image format.
        /// </summary>
        /// <param name="filePath">The file path to save the image to.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if the stream is null.</exception>
        /// <returns>The <see cref="Image{TPixel}"/></returns>
        public Image<TPixel> Save(string filePath)
        {
            Guard.NotNullOrEmpty(filePath, nameof(filePath));

            string ext = Path.GetExtension(filePath).Trim('.');
            var format = this.Configuration.FindFormatByFileExtensions(ext);
            if (format == null)
            {
                var stringBuilder = new StringBuilder();
                stringBuilder.AppendLine($"Can't find a format that is associated with the file extention '{ext}'. Registered formats with there extensions include:");
                foreach (IImageFormat fmt in this.Configuration.ImageFormats)
                {
                    stringBuilder.AppendLine($" - {fmt.Name} : {string.Join(", ", fmt.FileExtensions)}");
                }

                throw new NotSupportedException(stringBuilder.ToString());
            }

            IImageEncoder encoder = this.Configuration.FindEncoder(format);

            if (encoder == null)
            {
                var stringBuilder = new StringBuilder();
                stringBuilder.AppendLine($"Can't find encoder for file extention '{ext}' using image format '{format.Name}'. Registered encoders include:");
                foreach (KeyValuePair<IImageFormat, IImageEncoder> enc in this.Configuration.ImageEncoders)
                {
                    stringBuilder.AppendLine($" - {enc.Key} : {enc.Value.GetType().Name}");
                }

                throw new NotSupportedException(stringBuilder.ToString());
            }

            return this.Save(filePath, encoder);
        }

        /// <summary>
        /// Saves the image to the given stream using the currently loaded image format.
        /// </summary>
        /// <param name="filePath">The file path to save the image to.</param>
        /// <param name="encoder">The encoder to save the image with.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if the encoder is null.</exception>
        /// <returns>The <see cref="Image{TPixel}"/></returns>
        public Image<TPixel> Save(string filePath, IImageEncoder encoder)
        {
            Guard.NotNull(encoder, nameof(encoder));
            using (Stream fs = this.Configuration.FileSystem.Create(filePath))
            {
                return this.Save(fs, encoder);
            }
        }
#endif

        /// <summary>
        /// Clones the current image
        /// </summary>
        /// <returns>Returns a new image with all the same metadata as the original.</returns>
        public Image<TPixel> Clone()
        {
            return new Image<TPixel>(this);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"Image<{typeof(TPixel).Name}>: {this.Width}x{this.Height}";
        }

        /// <summary>
        /// Returns a Base64 encoded string from the given image.
        /// </summary>
        /// <example><see href="data:image/gif;base64,R0lGODlhAQABAIABAEdJRgAAACwAAAAAAQABAAACAkQBAA=="/></example>
        /// <param name="format">The format.</param>
        /// <returns>The <see cref="string"/></returns>
        public string ToBase64String(IImageFormat format)
        {
            using (var stream = new MemoryStream())
            {
                this.Save(stream, format);
                stream.Flush();
                return $"data:{format.DefaultMimeType};base64,{Convert.ToBase64String(stream.ToArray())}";
            }
        }

        /// <summary>
        /// Returns a copy of the image in the given pixel format.
        /// </summary>
        /// <param name="scaleFunc">A function that allows for the correction of vector scaling between unknown color formats.</param>
        /// <typeparam name="TPixel2">The pixel format.</typeparam>
        /// <returns>The <see cref="Image{TPixel2}"/></returns>
        public Image<TPixel2> To<TPixel2>(Func<Vector4, Vector4> scaleFunc = null)
            where TPixel2 : struct, IPixel<TPixel2>
        {
            scaleFunc = PackedPixelConverterHelper.ComputeScaleFunction<TPixel, TPixel2>(scaleFunc);

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
                target.Frames.Add(this.Frames[i].To<TPixel2>());
            }

            return target;
        }

        /// <summary>
        /// Creates a new <see cref="ImageFrame{TPixel}"/> from this instance
        /// </summary>
        /// <returns>The <see cref="ImageFrame{TPixel}"/></returns>
        internal virtual ImageFrame<TPixel> ToFrame()
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