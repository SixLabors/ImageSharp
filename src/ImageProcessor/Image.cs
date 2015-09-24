// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Image.cs" company="James South">
//   Copyright © James South and contributors.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Image class which stores the pixels and provides common functionality
//   such as loading images from files and streams or operation like resizing or cropping.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Text;

    using Formats;

    /// <summary>
    /// Image class which stores the pixels and provides common functionality
    /// such as loading images from files and streams or operation like resizing or cropping.
    /// </summary>
    /// <remarks>
    /// The image data is always stored in BGRA format, where the blue, green, red, and 
    /// alpha values are simple bytes.
    /// </remarks>
    [DebuggerDisplay("Image: {Width}x{Height}")]
    public class Image : ImageBase
    {
        /// <summary>
        /// The default horizontal resolution value (dots per inch) in x direction. 
        /// The default value is 96 dots per inch.
        /// </summary>
        public const double DefaultHorizontalResolution = 96;

        /// <summary>
        /// The default vertical resolution value (dots per inch) in y direction. 
        /// The default value is 96 dots per inch.
        /// </summary>
        public const double DefaultVerticalResolution = 96;

        /// <summary>
        /// The default collection of <see cref="IImageDecoder"/>.
        /// </summary>
        private static readonly Lazy<List<IImageDecoder>> DefaultDecoders =
            new Lazy<List<IImageDecoder>>(() => new List<IImageDecoder>
            {
                 new BmpDecoder(),
                 new JpegDecoder(),
                 new PngDecoder(),
                // new GifDecoder(),
            });

        /// <summary>
        /// The default collection of <see cref="IImageEncoder"/>.
        /// </summary>
        private static readonly Lazy<List<IImageEncoder>> DefaultEncoders =
            new Lazy<List<IImageEncoder>>(() => new List<IImageEncoder>
            {
                new BmpEncoder(),
                new JpegEncoder(),
                new PngEncoder()
            });

        /// <summary>
        /// Initializes a new instance of the <see cref="Image"/> class.
        /// </summary>
        public Image()
        {
            this.HorizontalResolution = DefaultHorizontalResolution;
            this.VerticalResolution = DefaultVerticalResolution;
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
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Image"/> class
        /// by making a copy from another image.
        /// </summary>
        /// <param name="other">The other image, where the clone should be made from.</param>
        /// <exception cref="ArgumentNullException"><paramref name="other"/> is null
        /// (Nothing in Visual Basic).</exception>
        public Image(Image other)
            : base(other)
        {
            Guard.NotNull(other, nameof(other), "Other image cannot be null.");

            foreach (ImageFrame frame in other.Frames)
            {
                if (frame != null)
                {
                    this.Frames.Add(new ImageFrame(frame));
                }
            }

            this.HorizontalResolution = DefaultHorizontalResolution;
            this.VerticalResolution = DefaultVerticalResolution;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Image"/> class.
        /// </summary>
        /// <param name="stream">
        /// The stream containing image information.
        /// </param>
        public Image(Stream stream)
        {
            Guard.NotNull(stream, nameof(stream));
            this.Load(stream, Decoders);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Image"/> class.
        /// </summary>
        /// <param name="stream">
        /// The stream containing image information.
        /// </param>
        /// <param name="decoders">
        /// The collection of <see cref="IImageDecoder"/>.
        /// </param>
        public Image(Stream stream, params IImageDecoder[] decoders)
        {
            Guard.NotNull(stream, "stream");
            this.Load(stream, decoders);
        }

        /// <summary>
        /// Gets a list of default decoders.
        /// </summary>
        public static IList<IImageDecoder> Decoders => DefaultDecoders.Value;

        /// <summary>
        /// Gets a list of default encoders.
        /// </summary>
        public static IList<IImageEncoder> Encoders => DefaultEncoders.Value;

        /// <summary>
        /// Gets or sets the frame delay.
        /// If not 0, this field specifies the number of hundredths (1/100) of a second to 
        /// wait before continuing with the processing of the Data Stream. 
        /// The clock starts ticking immediately after the graphic is rendered. 
        /// </summary>
        public int FrameDelay { get; set; }

        /// <summary>
        /// Gets or sets the resolution of the image in x- direction. It is defined as 
        /// number of dots per inch and should be an positive value.
        /// </summary>
        /// <value>The density of the image in x- direction.</value>
        public double HorizontalResolution { get; set; }

        /// <summary>
        /// Gets or sets the resolution of the image in y- direction. It is defined as 
        /// number of dots per inch and should be an positive value.
        /// </summary>
        /// <value>The density of the image in y- direction.</value>
        public double VerticalResolution { get; set; }

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
        /// <c>true</c> if this image is animated; otherwise, <c>false</c>.
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
        public IList<ImageFrame> Frames { get; } = new List<ImageFrame>();

        /// <summary>
        /// Gets the list of properties for storing meta information about this image.
        /// </summary>
        /// <value>A list of image properties.</value>
        public IList<ImageProperty> Properties { get; } = new List<ImageProperty>();

        internal IImageDecoder Decoder { get; set; }

        /// <summary>
        /// Loads the image from the given stream.
        /// </summary>
        /// <param name="stream">
        /// The stream containing image information.
        /// </param>
        /// <param name="decoders">
        /// The collection of <see cref="IImageDecoder"/>.
        /// </param>
        /// <exception cref="NotSupportedException">
        /// Thrown if the stream is not readable nor seekable.
        /// </exception>
        private void Load(Stream stream, IList<IImageDecoder> decoders)
        {
            try
            {
                if (!stream.CanRead)
                {
                    throw new NotSupportedException("Cannot read from the stream.");
                }

                if (!stream.CanSeek)
                {
                    throw new NotSupportedException("The stream does not support seeking.");
                }

                if (decoders.Count > 0)
                {
                    int maxHeaderSize = decoders.Max(x => x.HeaderSize);
                    if (maxHeaderSize > 0)
                    {
                        byte[] header = new byte[maxHeaderSize];

                        stream.Read(header, 0, maxHeaderSize);
                        stream.Position = 0;

                        IImageDecoder decoder = decoders.FirstOrDefault(x => x.IsSupportedFileFormat(header));
                        if (decoder != null)
                        {
                            this.Decoder = decoder;
                            this.Decoder.Decode(this, stream);
                            return;
                        }
                    }
                }

                StringBuilder stringBuilder = new StringBuilder();
                stringBuilder.AppendLine("Image cannot be loaded. Available decoders:");

                foreach (IImageDecoder decoder in decoders)
                {
                    stringBuilder.AppendLine("-" + decoder);
                }

                throw new NotSupportedException(stringBuilder.ToString());
            }
            finally
            {
                stream.Dispose();
            }
        }
    }
}
