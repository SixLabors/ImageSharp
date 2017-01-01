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

    /// <summary>
    /// Encapsulates an image, which consists of the pixel data for a graphics image and its attributes.
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    [DebuggerDisplay("Image: {Width}x{Height}")]
    public class Image<TColor> : ImageBase<TColor>
        where TColor : struct, IPackedPixel, IEquatable<TColor>
    {
        /// <summary>
        /// The default horizontal resolution value (dots per inch) in x direction.
        /// <remarks>The default value is 96 dots per inch.</remarks>
        /// </summary>
        public const double DefaultHorizontalResolution = 96;

        /// <summary>
        /// The default vertical resolution value (dots per inch) in y direction.
        /// <remarks>The default value is 96 dots per inch.</remarks>
        /// </summary>
        public const double DefaultVerticalResolution = 96;

        /// <summary>
        /// Initializes a new instance of the <see cref="Image{TColor}"/> class
        /// with the height and the width of the image.
        /// </summary>
        /// <param name="width">The width of the image in pixels.</param>
        /// <param name="height">The height of the image in pixels.</param>
        /// <param name="configuration">
        /// The configuration providing initialization code which allows extending the library.
        /// </param>
        public Image(int width, int height, Configuration configuration = null)
            : base(width, height, configuration)
        {
            if (!this.Configuration.ImageFormats.Any())
            {
                throw new InvalidOperationException("No image formats have been configured.");
            }

            this.CurrentImageFormat = this.Configuration.ImageFormats.First();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Image{TColor}"/> class.
        /// </summary>
        /// <param name="stream">
        /// The stream containing image information.
        /// </param>
        /// <param name="configuration">
        /// The configuration providing initialization code which allows extending the library.
        /// </param>
        /// <exception cref="System.ArgumentNullException">Thrown if the <paramref name="stream"/> is null.</exception>
        public Image(Stream stream, Configuration configuration = null)
            : base(configuration)
        {
            Guard.NotNull(stream, nameof(stream));
            this.Load(stream);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Image{TColor}"/> class.
        /// </summary>
        /// <param name="bytes">
        /// The byte array containing image information.
        /// </param>
        /// <param name="configuration">
        /// The configuration providing initialization code which allows extending the library.
        /// </param>
        /// <exception cref="System.ArgumentNullException">Thrown if the <paramref name="bytes"/> is null.</exception>
        public Image(byte[] bytes, Configuration configuration = null)
            : base(configuration)
        {
            Guard.NotNull(bytes, nameof(bytes));

            using (MemoryStream stream = new MemoryStream(bytes, false))
            {
                this.Load(stream);
            }
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
            this.CopyProperties(other);
        }

        /// <summary>
        /// Gets or sets the resolution of the image in x- direction. It is defined as
        ///  number of dots per inch and should be an positive value.
        /// </summary>
        /// <value>The density of the image in x- direction.</value>
        public double HorizontalResolution { get; set; } = DefaultHorizontalResolution;

        /// <summary>
        /// Gets or sets the resolution of the image in y- direction. It is defined as
        /// number of dots per inch and should be an positive value.
        /// </summary>
        /// <value>The density of the image in y- direction.</value>
        public double VerticalResolution { get; set; } = DefaultVerticalResolution;

        /// <summary>
        /// Gets the width of the image in inches. It is calculated as the width of the image
        /// in pixels multiplied with the density. When the density is equals or less than zero
        /// the default value is used.
        /// </summary>
        /// <value>The width of the image in inches.</value>
        public double InchWidth
        {
            get
            {
                double resolution = this.HorizontalResolution;

                if (resolution <= 0)
                {
                    resolution = DefaultHorizontalResolution;
                }

                return this.Width / resolution;
            }
        }

        /// <summary>
        /// Gets the height of the image in inches. It is calculated as the height of the image
        /// in pixels multiplied with the density. When the density is equals or less than zero
        /// the default value is used.
        /// </summary>
        /// <value>The height of the image in inches.</value>
        public double InchHeight
        {
            get
            {
                double resolution = this.VerticalResolution;

                if (resolution <= 0)
                {
                    resolution = DefaultVerticalResolution;
                }

                return this.Height / resolution;
            }
        }

        /// <summary>
        /// Gets a value indicating whether this image is animated.
        /// </summary>
        /// <value>
        /// <c>True</c> if this image is animated; otherwise, <c>false</c>.
        /// </value>
        public bool IsAnimated => this.Frames.Count > 0;

        /// <summary>
        /// Gets or sets the number of times any animation is repeated.
        /// <remarks>0 means to repeat indefinitely.</remarks>
        /// </summary>
        public ushort RepeatCount { get; set; }

        /// <summary>
        /// Gets the other frames for the animation.
        /// </summary>
        /// <value>The list of frame images.</value>
        public IList<ImageFrame<TColor>> Frames { get; } = new List<ImageFrame<TColor>>();

        /// <summary>
        /// Gets the list of properties for storing meta information about this image.
        /// </summary>
        /// <value>A list of image properties.</value>
        public IList<ImageProperty> Properties { get; } = new List<ImageProperty>();

        /// <summary>
        /// Gets the currently loaded image format.
        /// </summary>
        public IImageFormat CurrentImageFormat { get; internal set; }

        /// <summary>
        /// Gets or sets the Exif profile.
        /// </summary>
        public ExifProfile ExifProfile { get; set; }

        /// <summary>
        /// Saves the image to the given stream using the currently loaded image format.
        /// </summary>
        /// <param name="stream">The stream to save the image to.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if the stream is null.</exception>
        /// <returns>The <see cref="Image{TColor}"/></returns>
        public Image<TColor> Save(Stream stream)
        {
            Guard.NotNull(stream, nameof(stream));
            this.CurrentImageFormat.Encoder.Encode(this, stream);
            return this;
        }

        /// <summary>
        /// Saves the image to the given stream using the given image format.
        /// </summary>
        /// <param name="stream">The stream to save the image to.</param>
        /// <param name="format">The format to save the image as.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if the stream is null.</exception>
        /// <returns>The <see cref="Image{TColor}"/></returns>
        public Image<TColor> Save(Stream stream, IImageFormat format)
        {
            Guard.NotNull(stream, nameof(stream));
            format.Encoder.Encode(this, stream);
            return this;
        }

        /// <summary>
        /// Saves the image to the given stream using the given image encoder.
        /// </summary>
        /// <param name="stream">The stream to save the image to.</param>
        /// <param name="encoder">The encoder to save the image with.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if the stream is null.</exception>
        /// <returns>
        /// The <see cref="Image{TColor}"/>.
        /// </returns>
        public Image<TColor> Save(Stream stream, IImageEncoder encoder)
        {
            Guard.NotNull(stream, nameof(stream));
            encoder.Encode(this, stream);

            // Reset to the start of the stream.
            if (stream.CanSeek)
            {
                stream.Position = 0;
            }

            return this;
        }

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
            where TColor2 : struct, IPackedPixel, IEquatable<TColor2>
        {
            scaleFunc = PackedPixelConverterHelper.ComputeScaleFunction<TColor, TColor2>(scaleFunc);

            Image<TColor2> target = new Image<TColor2>(this.Width, this.Height, this.Configuration)
            {
                Quality = this.Quality,
                FrameDelay = this.FrameDelay,
                HorizontalResolution = this.HorizontalResolution,
                VerticalResolution = this.VerticalResolution,
                CurrentImageFormat = this.CurrentImageFormat,
                RepeatCount = this.RepeatCount
            };

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

            if (this.ExifProfile != null)
            {
                target.ExifProfile = new ExifProfile(this.ExifProfile);
            }

            foreach (ImageFrame<TColor> frame in this.Frames)
            {
                target.Frames.Add(frame.To<TColor2>());
            }

            return target;
        }

        /// <summary>
        /// Copies the properties from the other <see cref="Image{TColor}"/>.
        /// </summary>
        /// <param name="other">
        /// The other <see cref="Image{TColor}"/> to copy the properties from.
        /// </param>
        internal void CopyProperties(Image<TColor> other)
        {
            base.CopyProperties(other);

            this.HorizontalResolution = other.HorizontalResolution;
            this.VerticalResolution = other.VerticalResolution;
            this.CurrentImageFormat = other.CurrentImageFormat;
            this.RepeatCount = other.RepeatCount;

            if (other.ExifProfile != null)
            {
                this.ExifProfile = new ExifProfile(other.ExifProfile);
            }
        }

        /// <summary>
        /// Creates a new <see cref="ImageFrame{TColor}"/> from this instance
        /// </summary>
        /// <returns>The <see cref="ImageFrame{TColor}"/></returns>
        internal virtual ImageFrame<TColor> ToFrame()
        {
            return new ImageFrame<TColor>(this);
        }

        /// <summary>
        /// Loads the image from the given stream.
        /// </summary>
        /// <param name="stream">The stream containing image information.</param>
        /// <exception cref="NotSupportedException">
        /// Thrown if the stream is not readable nor seekable.
        /// </exception>
        private void Load(Stream stream)
        {
            if (!this.Configuration.ImageFormats.Any())
            {
                throw new InvalidOperationException("No image formats have been configured.");
            }

            if (!stream.CanRead)
            {
                throw new NotSupportedException("Cannot read from the stream.");
            }

            if (stream.CanSeek)
            {
                if (this.Decode(stream))
                {
                    return;
                }
            }
            else
            {
                // We want to be able to load images from things like HttpContext.Request.Body
                using (MemoryStream ms = new MemoryStream())
                {
                    stream.CopyTo(ms);
                    ms.Position = 0;

                    if (this.Decode(ms))
                    {
                        return;
                    }
                }
            }

            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine("Image cannot be loaded. Available formats:");

            foreach (IImageFormat format in this.Configuration.ImageFormats)
            {
                stringBuilder.AppendLine("-" + format);
            }

            throw new NotSupportedException(stringBuilder.ToString());
        }

        /// <summary>
        /// Decodes the image stream to the current image.
        /// </summary>
        /// <param name="stream">The stream.</param>
        /// <returns>
        /// The <see cref="bool"/>.
        /// </returns>
        private bool Decode(Stream stream)
        {
            int maxHeaderSize = this.Configuration.MaxHeaderSize;
            if (maxHeaderSize <= 0)
            {
                return false;
            }

            IImageFormat format;
            byte[] header = ArrayPool<byte>.Shared.Rent(maxHeaderSize);
            try
            {
                long startPosition = stream.Position;
                stream.Read(header, 0, maxHeaderSize);
                stream.Position = startPosition;
                format = this.Configuration.ImageFormats.FirstOrDefault(x => x.IsSupportedFileFormat(header));
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(header);
            }

            if (format == null)
            {
                return false;
            }

            format.Decoder.Decode(this, stream);
            this.CurrentImageFormat = format;
            return true;
        }
    }
}