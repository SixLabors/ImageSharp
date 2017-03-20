// <copyright file="Image{TColor}.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;
    using System.Buffers;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Numerics;
    using System.Text;
    using System.Threading.Tasks;

    using Formats;
    using Processing;

    /// <summary>
    /// Encapsulates an image, which consists of the pixel data for a graphics image and its attributes.
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    [DebuggerDisplay("Image: {Width}x{Height}")]
    public class Image<TColor> : ImageBase<TColor>, IImage
        where TColor : struct, IPixel<TColor>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Image{TColor}"/> class
        /// with the height and the width of the image.
        /// </summary>
        /// <param name="width">The width of the image in pixels.</param>
        /// <param name="height">The height of the image in pixels.</param>
        /// <param name="configuration">
        /// The configuration providing initialization code which allows extending the library.
        /// </param>
        public Image(int width, int height, Configuration configuration)
            : base(width, height, configuration)
        {
            if (!this.Configuration.ImageFormats.Any())
            {
                throw new InvalidOperationException("No image formats have been configured.");
            }

            this.MetaData = new ImageMetaData();
            this.CurrentImageFormat = this.Configuration.ImageFormats.First();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Image{TColor}"/> class
        /// with the height and the width of the image.
        /// </summary>
        /// <param name="width">The width of the image in pixels.</param>
        /// <param name="height">The height of the image in pixels.</param>
        public Image(int width, int height)
            : this(width, height, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Image{TColor}"/> class
        /// by making a copy from another image.
        /// </summary>
        /// <param name="other">The other image, where the clone should be made from.</param>
        /// <exception cref="System.ArgumentNullException"><paramref name="other"/> is null.</exception>
        public Image(Image<TColor> other)
            : base(other)
        {
            foreach (ImageFrame<TColor> frame in other.Frames)
            {
                if (frame != null)
                {
                    this.Frames.Add(new ImageFrame<TColor>(frame));
                }
            }

            this.CopyProperties(other);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Image{TColor}"/> class
        /// by making a copy from another image.
        /// </summary>
        /// <param name="other">The other image, where the clone should be made from.</param>
        /// <exception cref="System.ArgumentNullException"><paramref name="other"/> is null.</exception>
        public Image(ImageBase<TColor> other)
            : base(other)
        {
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
        public IList<ImageFrame<TColor>> Frames { get; } = new List<ImageFrame<TColor>>();

        /// <summary>
        /// Gets the currently loaded image format.
        /// </summary>
        public IImageFormat CurrentImageFormat { get; internal set; }

        /// <summary>
        /// Applies the processor to the image.
        /// </summary>
        /// <param name="processor">The processor to apply to the image.</param>
        /// <param name="rectangle">The <see cref="Rectangle" /> structure that specifies the portion of the image object to draw.</param>
        public override void ApplyProcessor(IImageProcessor<TColor> processor, Rectangle rectangle)
        {
            // we want to put this on on here as it gives us a really go place to test/verify processor settings
            base.ApplyProcessor(processor, rectangle);
            foreach (ImageFrame<TColor> sourceFrame in this.Frames)
            {
                sourceFrame.ApplyProcessor(processor, rectangle);
            }
        }

        /// <summary>
        /// Saves the image to the given stream using the currently loaded image format.
        /// </summary>
        /// <param name="stream">The stream to save the image to.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if the stream is null.</exception>
        /// <returns>The <see cref="Image{TColor}"/></returns>
        public Image<TColor> Save(Stream stream)
        {
            return this.Save(stream, (IEncoderOptions)null);
        }

        /// <summary>
        /// Saves the image to the given stream using the currently loaded image format.
        /// </summary>
        /// <param name="stream">The stream to save the image to.</param>
        /// <param name="options">The options for the encoder.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if the stream is null.</exception>
        /// <returns>The <see cref="Image{TColor}"/></returns>
        public Image<TColor> Save(Stream stream, IEncoderOptions options)
        {
            return this.Save(stream, this.CurrentImageFormat?.Encoder, options);
        }

        /// <summary>
        /// Saves the image to the given stream using the given image format.
        /// </summary>
        /// <param name="stream">The stream to save the image to.</param>
        /// <param name="format">The format to save the image as.</param>
        /// <returns>The <see cref="Image{TColor}"/></returns>
        public Image<TColor> Save(Stream stream, IImageFormat format)
        {
            return this.Save(stream, format, null);
        }

        /// <summary>
        /// Saves the image to the given stream using the given image format.
        /// </summary>
        /// <param name="stream">The stream to save the image to.</param>
        /// <param name="format">The format to save the image as.</param>
        /// <param name="options">The options for the encoder.</param>
        /// <returns>The <see cref="Image{TColor}"/></returns>
        public Image<TColor> Save(Stream stream, IImageFormat format, IEncoderOptions options)
        {
            Guard.NotNull(format, nameof(format));

            return this.Save(stream, format.Encoder, options);
        }

        /// <summary>
        /// Saves the image to the given stream using the given image encoder.
        /// </summary>
        /// <param name="stream">The stream to save the image to.</param>
        /// <param name="encoder">The encoder to save the image with.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if the stream or encoder is null.</exception>
        /// <returns>
        /// The <see cref="Image{TColor}"/>.
        /// </returns>
        public Image<TColor> Save(Stream stream, IImageEncoder encoder)
        {
            return this.Save(stream, encoder, null);
        }

        /// <summary>
        /// Saves the image to the given stream using the given image encoder.
        /// </summary>
        /// <param name="stream">The stream to save the image to.</param>
        /// <param name="encoder">The encoder to save the image with.</param>
        /// <param name="options">The options for the encoder.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if the stream or encoder is null.</exception>
        /// <returns>
        /// The <see cref="Image{TColor}"/>.
        /// </returns>
        public Image<TColor> Save(Stream stream, IImageEncoder encoder, IEncoderOptions options)
        {
            Guard.NotNull(stream, nameof(stream));
            Guard.NotNull(encoder, nameof(encoder));

            encoder.Encode(this, stream, options);

            return this;
        }

#if !NETSTANDARD1_1
        /// <summary>
        /// Saves the image to the given stream using the currently loaded image format.
        /// </summary>
        /// <param name="filePath">The file path to save the image to.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if the stream is null.</exception>
        /// <returns>The <see cref="Image{TColor}"/></returns>
        public Image<TColor> Save(string filePath)
        {
            return this.Save(filePath, (IEncoderOptions)null);
        }

        /// <summary>
        /// Saves the image to the given stream using the currently loaded image format.
        /// </summary>
        /// <param name="filePath">The file path to save the image to.</param>
        /// <param name="options">The options for the encoder.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if the stream is null.</exception>
        /// <returns>The <see cref="Image{TColor}"/></returns>
        public Image<TColor> Save(string filePath, IEncoderOptions options)
        {
            string ext = Path.GetExtension(filePath).Trim('.');
            IImageFormat format = this.Configuration.ImageFormats.SingleOrDefault(f => f.SupportedExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase));
            if (format == null)
            {
                throw new InvalidOperationException($"No image formats have been registered for the file extension '{ext}'.");
            }

            return this.Save(filePath, format, options);
        }

        /// <summary>
        /// Saves the image to the given stream using the currently loaded image format.
        /// </summary>
        /// <param name="filePath">The file path to save the image to.</param>
        /// <param name="format">The format to save the image as.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if the format is null.</exception>
        /// <returns>The <see cref="Image{TColor}"/></returns>
        public Image<TColor> Save(string filePath, IImageFormat format)
        {
            return this.Save(filePath, format, null);
        }

        /// <summary>
        /// Saves the image to the given stream using the currently loaded image format.
        /// </summary>
        /// <param name="filePath">The file path to save the image to.</param>
        /// <param name="format">The format to save the image as.</param>
        /// <param name="options">The options for the encoder.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if the format is null.</exception>
        /// <returns>The <see cref="Image{TColor}"/></returns>
        public Image<TColor> Save(string filePath, IImageFormat format, IEncoderOptions options)
        {
            Guard.NotNull(format, nameof(format));
            return this.Save(filePath, format.Encoder, options);
        }

        /// <summary>
        /// Saves the image to the given stream using the currently loaded image format.
        /// </summary>
        /// <param name="filePath">The file path to save the image to.</param>
        /// <param name="encoder">The encoder to save the image with.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if the encoder is null.</exception>
        /// <returns>The <see cref="Image{TColor}"/></returns>
        public Image<TColor> Save(string filePath, IImageEncoder encoder)
        {
            return this.Save(filePath, encoder, null);
        }

        /// <summary>
        /// Saves the image to the given stream using the currently loaded image format.
        /// </summary>
        /// <param name="filePath">The file path to save the image to.</param>
        /// <param name="encoder">The encoder to save the image with.</param>
        /// <param name="options">The options for the encoder.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if the encoder is null.</exception>
        /// <returns>The <see cref="Image{TColor}"/></returns>
        public Image<TColor> Save(string filePath, IImageEncoder encoder, IEncoderOptions options)
        {
            Guard.NotNull(encoder, nameof(encoder));
            using (Stream fs = this.Configuration.FileSystem.Create(filePath))
            {
                return this.Save(fs, encoder, options);
            }
        }
#endif

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"Image: {this.Width}x{this.Height}";
        }

        /// <summary>
        /// Returns a Base64 encoded string from the given image.
        /// </summary>
        /// <example><see href="data:image/gif;base64,R0lGODlhAQABAIABAEdJRgAAACwAAAAAAQABAAACAkQBAA=="/></example>
        /// <returns>The <see cref="string"/></returns>
        public string ToBase64String()
        {
            using (MemoryStream stream = new MemoryStream())
            {
                this.Save(stream);
                stream.Flush();
                return $"data:{this.CurrentImageFormat.MimeType};base64,{Convert.ToBase64String(stream.ToArray())}";
            }
        }

        /// <summary>
        /// Returns a copy of the image in the given pixel format.
        /// </summary>
        /// <param name="scaleFunc">A function that allows for the correction of vector scaling between unknown color formats.</param>
        /// <typeparam name="TColor2">The pixel format.</typeparam>
        /// <returns>The <see cref="Image{TColor2}"/></returns>
        public Image<TColor2> To<TColor2>(Func<Vector4, Vector4> scaleFunc = null)
            where TColor2 : struct, IPixel<TColor2>
        {
            scaleFunc = PackedPixelConverterHelper.ComputeScaleFunction<TColor, TColor2>(scaleFunc);

            Image<TColor2> target = new Image<TColor2>(this.Width, this.Height, this.Configuration);
            target.CopyProperties(this);

            using (PixelAccessor<TColor> pixels = this.Lock())
            using (PixelAccessor<TColor2> targetPixels = target.Lock())
            {
                Parallel.For(
                    0,
                    target.Height,
                    this.Configuration.ParallelOptions,
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

            for (int i = 0; i < this.Frames.Count; i++)
            {
                target.Frames.Add(this.Frames[i].To<TColor2>());
            }

            return target;
        }

        /// <summary>
        /// Creates a new <see cref="ImageFrame{TColor}"/> from this instance
        /// </summary>
        /// <returns>The <see cref="ImageFrame{TColor}"/></returns>
        internal virtual ImageFrame<TColor> ToFrame()
        {
            return new ImageFrame<TColor>(this);
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
            this.CurrentImageFormat = other.CurrentImageFormat;
            this.MetaData = new ImageMetaData(other.MetaData);
        }
    }
}