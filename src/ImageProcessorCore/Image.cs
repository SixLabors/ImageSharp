// <copyright file="Image.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore
{
    using System.IO;
    using System.Text;

    using System;
    using System.Collections.Generic;
    using System.Linq;

    using Formats;

    /// <summary>
    /// Encapsulates an image, which consists of the pixel data for a graphics image and its attributes.
    /// </summary>
    /// <typeparam name="TPackedVector">
    /// The packed vector containing pixel information.
    /// </typeparam>
    public class Image<TPackedVector> : ImageBase<TPackedVector>
        where TPackedVector : IPackedVector, new()
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
        /// Initializes a new instance of the <see cref="Image{TPackedVector}"/> class.
        /// </summary>
        public Image()
        {
            this.CurrentImageFormat = Bootstrapper.Instance.ImageFormats.First(f => f.GetType() == typeof(BmpFormat));
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Image{TPackedVector}"/> class
        /// with the height and the width of the image.
        /// </summary>
        /// <param name="width">The width of the image in pixels.</param>
        /// <param name="height">The height of the image in pixels.</param>
        public Image(int width, int height)
            : base(width, height)
        {
            // TODO: Change to PNG
            this.CurrentImageFormat = Bootstrapper.Instance.ImageFormats.First(f => f.GetType() == typeof(BmpFormat));
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Image{TPackedVector}"/> class.
        /// </summary>
        /// <param name="stream">
        /// The stream containing image information.
        /// </param>
        /// <exception cref="ArgumentNullException">Thrown if the <paramref name="stream"/> is null.</exception>
        public Image(Stream stream)
        {
            Guard.NotNull(stream, nameof(stream));
            this.Load(stream);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Image{TPackedVector}"/> class
        /// by making a copy from another image.
        /// </summary>
        /// <param name="other">The other image, where the clone should be made from.</param>
        /// <exception cref="ArgumentNullException"><paramref name="other"/> is null.</exception>
        public Image(Image<TPackedVector> other)
        {
            foreach (ImageFrame<TPackedVector> frame in other.Frames)
            {
                if (frame != null)
                {
                    this.Frames.Add(new ImageFrame<TPackedVector>(frame));
                }
            }

            this.RepeatCount = other.RepeatCount;
            this.HorizontalResolution = other.HorizontalResolution;
            this.VerticalResolution = other.VerticalResolution;
            this.CurrentImageFormat = other.CurrentImageFormat;
        }

        /// <summary>
        /// Gets a list of supported image formats.
        /// </summary>
        public IReadOnlyCollection<IImageFormat> Formats { get; } = Bootstrapper.Instance.ImageFormats;

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
        public IList<ImageFrame<TPackedVector>> Frames { get; } = new List<ImageFrame<TPackedVector>>();

        /// <summary>
        /// Gets the list of properties for storing meta information about this image.
        /// </summary>
        /// <value>A list of image properties.</value>
        public IList<ImageProperty> Properties { get; } = new List<ImageProperty>();

        /// <summary>
        /// Gets the currently loaded image format.
        /// </summary>
        public IImageFormat CurrentImageFormat { get; internal set; }

        /// <inheritdoc/>
        public override IPixelAccessor<TPackedVector> Lock()
        {
            return Bootstrapper.Instance.GetPixelAccessor(this);
        }

        /// <summary>
        /// Saves the image to the given stream using the currently loaded image format.
        /// </summary>
        /// <param name="stream">The stream to save the image to.</param>
        /// <exception cref="ArgumentNullException">Thrown if the stream is null.</exception>
        public void Save(Stream stream)
        {
            Guard.NotNull(stream, nameof(stream));
            this.CurrentImageFormat.Encoder.Encode(this, stream);
        }

        /// <summary>
        /// Saves the image to the given stream using the given image format.
        /// </summary>
        /// <param name="stream">The stream to save the image to.</param>
        /// <param name="format">The format to save the image as.</param>
        /// <exception cref="ArgumentNullException">Thrown if the stream is null.</exception>
        public void Save(Stream stream, IImageFormat format)
        {
            Guard.NotNull(stream, nameof(stream));
            format.Encoder.Encode(this, stream);
        }

        /// <summary>
        /// Saves the image to the given stream using the given image encoder.
        /// </summary>
        /// <param name="stream">The stream to save the image to.</param>
        /// <param name="encoder">The encoder to save the image with.</param>
        /// <exception cref="ArgumentNullException">Thrown if the stream is null.</exception>
        public void Save(Stream stream, IImageEncoder encoder)
        {
            Guard.NotNull(stream, nameof(stream));
            encoder.Encode(this, stream);
        }

        /// <summary>
        /// Returns a Base64 encoded string from the given image. 
        /// </summary>
        /// <example>data:image/gif;base64,R0lGODlhAQABAIABAEdJRgAAACwAAAAAAQABAAACAkQBAA==</example>
        /// <returns>The <see cref="string"/></returns>
        public override string ToString()
        {
            using (MemoryStream stream = new MemoryStream())
            {
                this.Save(stream);
                stream.Flush();
                return $"data:{this.CurrentImageFormat.Encoder.MimeType};base64,{Convert.ToBase64String(stream.ToArray())}";
            }
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
            if (!this.Formats.Any()) { return; }

            if (!stream.CanRead)
            {
                throw new NotSupportedException("Cannot read from the stream.");
            }

            if (!stream.CanSeek)
            {
                throw new NotSupportedException("The stream does not support seeking.");
            }

            int maxHeaderSize = this.Formats.Max(x => x.Decoder.HeaderSize);
            if (maxHeaderSize > 0)
            {
                byte[] header = new byte[maxHeaderSize];

                stream.Position = 0;
                stream.Read(header, 0, maxHeaderSize);
                stream.Position = 0;

                IImageFormat format = this.Formats.FirstOrDefault(x => x.Decoder.IsSupportedFileFormat(header));
                if (format != null)
                {
                    format.Decoder.Decode(this, stream);
                    this.CurrentImageFormat = format;
                    return;
                }
            }

            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine("Image cannot be loaded. Available formats:");

            foreach (IImageFormat format in this.Formats)
            {
                stringBuilder.AppendLine("-" + format);
            }

            throw new NotSupportedException(stringBuilder.ToString());
        }
    }
}
