// <copyright file="Image.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Text;

    using Formats;

    /// <summary>
    /// Encapsulates an image, which consists of the pixel data for a graphics image and its attributes.
    /// </summary>
    /// <remarks>
    /// The image data is always stored in BGRA format, where the blue, green, red, and
    /// alpha values are simple bytes.
    /// </remarks>
    [DebuggerDisplay("Image: {Width}x{Height}")]
    public class Image : ImageBase, IImage
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
        /// The default collection of <see cref="IImageFormat"/>.
        /// </summary>
        private static readonly Lazy<List<IImageFormat>> DefaultFormats =
            new Lazy<List<IImageFormat>>(() => new List<IImageFormat>
            {
                 new BmpFormat(),
                 new JpegFormat(),
                 new PngFormat(),
                 new GifFormat()
            });

        /// <summary>
        /// Initializes a new instance of the <see cref="Image"/> class.
        /// </summary>
        public Image()
        {
            this.HorizontalResolution = DefaultHorizontalResolution;
            this.VerticalResolution = DefaultVerticalResolution;
            this.CurrentImageFormat = DefaultFormats.Value.First(f => f.GetType() == typeof(PngFormat));
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Image"/> class
        /// with the height and the width of the image.
        /// </summary>
        /// <param name="width">The width of the image in pixels.</param>
        /// <param name="height">The height of the image in pixels.</param>
        public Image(int width, int height)
            : base(width, height)
        {
            this.HorizontalResolution = DefaultHorizontalResolution;
            this.VerticalResolution = DefaultVerticalResolution;
            this.CurrentImageFormat = DefaultFormats.Value.First(f => f.GetType() == typeof(PngFormat));
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Image"/> class
        /// by making a copy from another image.
        /// </summary>
        /// <param name="other">The other image, where the clone should be made from.</param>
        /// <exception cref="ArgumentNullException"><paramref name="other"/> is null.</exception>
        public Image(Image other)
            : base(other)
        {
            foreach (ImageFrame frame in other.Frames)
            {
                if (frame != null)
                {
                    this.Frames.Add(new ImageFrame(frame));
                }
            }

            this.RepeatCount = other.RepeatCount;
            this.HorizontalResolution = other.HorizontalResolution;
            this.VerticalResolution = other.VerticalResolution;
            this.CurrentImageFormat = other.CurrentImageFormat;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Image"/> class.
        /// </summary>
        /// <param name="other">
        /// The other <see cref="ImageFrame"/> to create this instance from.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if the given <see cref="ImageFrame"/> is null.
        /// </exception>
        public Image(ImageFrame other)
            : base(other)
        {
            this.HorizontalResolution = DefaultHorizontalResolution;
            this.VerticalResolution = DefaultVerticalResolution;

            // Most likely a gif
            // TODO: Should this be aproperty on ImageFrame?
            this.CurrentImageFormat = DefaultFormats.Value.First(f => f.GetType() == typeof(GifFormat));
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Image"/> class.
        /// </summary>
        /// <param name="stream">
        /// The stream containing image information.
        /// </param>
        /// <exception cref="ArgumentNullException">Thrown if the <paramref name="stream"/> is null.</exception>
        public Image(Stream stream)
        {
            Guard.NotNull(stream, nameof(stream));
            this.Load(stream, Formats);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Image"/> class.
        /// </summary>
        /// <param name="stream">
        /// The stream containing image information.
        /// </param>
        /// <param name="formats">
        /// The collection of <see cref="IImageFormat"/>.
        /// </param>
        /// <exception cref="ArgumentNullException">Thrown if the stream is null.</exception>
        public Image(Stream stream, params IImageFormat[] formats)
        {
            Guard.NotNull(stream, nameof(stream));
            this.Load(stream, formats);
        }

        /// <summary>
        /// Gets a list of supported image formats.
        /// </summary>
        public static IList<IImageFormat> Formats => DefaultFormats.Value;

        /// <inheritdoc/>
        public double HorizontalResolution { get; set; }

        /// <inheritdoc/>
        public double VerticalResolution { get; set; }

        /// <inheritdoc/>
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

        /// <inheritdoc/>
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

        /// <inheritdoc/>
        public bool IsAnimated => this.Frames.Count > 0;

        /// <inheritdoc/>
        public ushort RepeatCount { get; set; }

        /// <inheritdoc/>
        public IList<ImageFrame> Frames { get; } = new List<ImageFrame>();

        /// <inheritdoc/>
        public IList<ImageProperty> Properties { get; } = new List<ImageProperty>();

        /// <inheritdoc/>
        public IImageFormat CurrentImageFormat { get; internal set; }

        /// <inheritdoc/>
        public void Save(Stream stream)
        {
            Guard.NotNull(stream, nameof(stream));
            this.CurrentImageFormat.Encoder.Encode(this, stream);
        }

        /// <inheritdoc/>
        public void Save(Stream stream, IImageFormat format)
        {
            Guard.NotNull(stream, nameof(stream));
            format.Encoder.Encode(this, stream);
        }

        /// <inheritdoc/>
        public void Save(Stream stream, IImageEncoder encoder)
        {
            Guard.NotNull(stream, nameof(stream));
            encoder.Encode(this, stream);
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (this.IsDisposed)
            {
                return;
            }

            if (disposing)
            {
                // Dispose of any managed resources here.
                if (this.Frames.Any())
                {
                    foreach (ImageFrame frame in this.Frames)
                    {
                        frame.Dispose();
                    }
                    this.Frames.Clear();
                }
            }

            base.Dispose(disposing);
        }

        /// <summary>
        /// Loads the image from the given stream.
        /// </summary>
        /// <param name="stream">
        /// The stream containing image information.
        /// </param>
        /// <param name="formats">
        /// The collection of <see cref="IImageFormat"/>.
        /// </param>
        /// <exception cref="NotSupportedException">
        /// Thrown if the stream is not readable nor seekable.
        /// </exception>
        private void Load(Stream stream, IList<IImageFormat> formats)
        {
            if (!stream.CanRead)
            {
                throw new NotSupportedException("Cannot read from the stream.");
            }

            if (!stream.CanSeek)
            {
                throw new NotSupportedException("The stream does not support seeking.");
            }

            if (formats.Count > 0)
            {
                int maxHeaderSize = formats.Max(x => x.Decoder.HeaderSize);
                if (maxHeaderSize > 0)
                {
                    byte[] header = new byte[maxHeaderSize];
                    
                    stream.Position = 0;
                    stream.Read(header, 0, maxHeaderSize);
                    stream.Position = 0;

                    IImageFormat format = formats.FirstOrDefault(x => x.Decoder.IsSupportedFileFormat(header));
                    if (format != null)
                    {
                        format.Decoder.Decode(this, stream);
                        this.CurrentImageFormat = format;
                        return;
                    }
                }
            }

            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine("Image cannot be loaded. Available formats:");

            foreach (IImageFormat format in formats)
            {
                stringBuilder.AppendLine("-" + format);
            }

            throw new NotSupportedException(stringBuilder.ToString());
        }
    }
}
