namespace ImageProcessor
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Text;

    using ImageProcessor.Formats;

    /// <summary>
    /// Image class with stores the pixels and provides common functionality
    /// such as loading images from files and streams or operation like resizing or cutting.
    /// </summary>
    /// <remarks>The image data is alway stored in RGBA format, where the red, the blue, the
    /// alpha values are simple bytes.</remarks>
    [DebuggerDisplay("Image: {PixelWidth}x{PixelHeight}")]
    public class Image : ImageBase
    {
        #region Constants

        /// <summary>
        /// The default density value (dots per inch) in x direction. The default value is 75 dots per inch.
        /// </summary>
        public const double DefaultDensityX = 75;
        /// <summary>
        /// The default density value (dots per inch) in y direction. The default value is 75 dots per inch.
        /// </summary>
        public const double DefaultDensityY = 75;

        private static readonly Lazy<List<IImageDecoder>> defaultDecoders = new Lazy<List<IImageDecoder>>(() => new List<IImageDecoder>
        {
            //new BmpDecoder(),
            //new JpegDecoder(),
            //new PngDecoder(),
            new GifDecoder(),
        });

        private static readonly Lazy<List<IImageEncoder>> defaultEncoders = new Lazy<List<IImageEncoder>>(() => new List<IImageEncoder>
        {
            //new BmpEncoder(),
            //new JpegEncoder(),
            //new PngEncoder(),
        });

        /// <summary>
        /// Gets a list of default decoders.
        /// </summary>
        public static IList<IImageDecoder> Decoders
        {
            get { return defaultDecoders.Value; }
        }

        /// <summary>
        /// Gets a list of default encoders.
        /// </summary>
        public static IList<IImageEncoder> Encoders
        {
            get { return defaultEncoders.Value; }
        }

        #endregion

        #region Fields

        private readonly object _lockObject = new object();

        #endregion

        /// <summary>
        /// If not 0, this field specifies the number of hundredths (1/100) of a second to 
        /// wait before continuing with the processing of the Data Stream. 
        /// The clock starts ticking immediately after the graphic is rendered. 
        /// This field may be used in conjunction with the User Input Flag field. 
        /// </summary>
        public int? DelayTime { get; set; }

        #region Properties

        /// <summary>
        /// Gets or sets the resolution of the image in x direction. It is defined as 
        /// number of dots per inch and should be an positive value.
        /// </summary>
        /// <value>The density of the image in x direction.</value>
        public double DensityX { get; set; }

        /// <summary>
        /// Gets or sets the resolution of the image in y direction. It is defined as 
        /// number of dots per inch and should be an positive value.
        /// </summary>
        /// <value>The density of the image in y direction.</value>
        public double DensityY { get; set; }

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
                double densityX = DensityX;

                if (densityX <= 0)
                {
                    densityX = DefaultDensityX;
                }

                return Width / densityX;
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
                double densityY = DensityY;

                if (densityY <= 0)
                {
                    densityY = DefaultDensityY;
                }

                return Height / densityY;
            }
        }

        /// <summary>
        /// Gets a value indicating whether this image is animated.
        /// </summary>
        /// <value>
        /// <c>true</c> if this image is animated; otherwise, <c>false</c>.
        /// </value>
        public bool IsAnimated
        {
            get { return _frames.Count > 0; }
        }

        private IList<ImageFrame> _frames = new List<ImageFrame>();
        /// <summary>
        /// Get the other frames for the animation.
        /// </summary>
        /// <value>The list of frame images.</value>
        public IList<ImageFrame> Frames
        {
            get { return _frames; }
        }

        private IList<ImageProperty> _properties = new List<ImageProperty>();
        /// <summary>
        /// Gets the list of properties for storing meta information about this image.
        /// </summary>
        /// <value>A list of image properties.</value>
        public IList<ImageProperty> Properties
        {
            get { return _properties; }
        }

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="Image"/> class
        /// with the height and the width of the image.
        /// </summary>
        /// <param name="width">The width of the image in pixels.</param>
        /// <param name="height">The height of the image in pixels.</param>
        public Image(int width, int height)
            : base(width, height)
        {
            DensityX = DefaultDensityX;
            DensityY = DefaultDensityY;
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
            if (other == null) throw new ArgumentNullException("Other image cannot be null.");

            foreach (ImageFrame frame in other.Frames)
            {
                if (frame != null)
                {
                    Frames.Add(new ImageFrame(frame));
                }
            }

            DensityX = DefaultDensityX;
            DensityY = DefaultDensityY;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Image"/> class.
        /// </summary>
        public Image()
        {
            DensityX = DefaultDensityX;
            DensityY = DefaultDensityY;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Image"/> class.
        /// </summary>
        public Image(Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException("stream");
            }

            Load(stream, Decoders);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Image"/> class.
        /// </summary>
        public Image(Stream stream, params IImageDecoder[] decoders)
        {
            if (stream == null)
            {
                throw new ArgumentNullException("stream");
            }

            Load(stream, decoders);
        }

        #endregion Constructors

        #region Methods

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

                        var decoder = decoders.FirstOrDefault(x => x.IsSupportedFileFormat(header));
                        if (decoder != null)
                        {
                            decoder.Decode(this, stream);
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

        #endregion Methods
    }
}
