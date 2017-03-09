// <copyright file="Image.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System.Diagnostics;
    using System.IO;

    using Formats;

    /// <summary>
    /// Represents an image. Each pixel is a made up four 8-bit components red, green, blue, and alpha
    /// packed into a single unsigned integer value.
    /// </summary>
    [DebuggerDisplay("Image: {Width}x{Height}")]
    public sealed class Image : Image<Color>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Image"/> class
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
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Image"/> class.
        /// </summary>
        /// <param name="stream">
        /// The stream containing image information.
        /// </param>
        /// <exception cref="System.ArgumentNullException">Thrown if the <paramref name="stream"/> is null.</exception>
        public Image(Stream stream)
            : base(stream, null, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Image"/> class.
        /// </summary>
        /// <param name="stream">
        /// The stream containing image information.
        /// </param>
        /// <param name="options">
        /// The options for the decoder.
        /// </param>
        /// <exception cref="System.ArgumentNullException">Thrown if the <paramref name="stream"/> is null.</exception>
        public Image(Stream stream, IDecoderOptions options)
            : base(stream, options, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Image"/> class.
        /// </summary>
        /// <param name="stream">
        /// The stream containing image information.
        /// </param>
        /// <param name="configuration">
        /// The configuration providing initialization code which allows extending the library.
        /// </param>
        /// <exception cref="System.ArgumentNullException">Thrown if the <paramref name="stream"/> is null.</exception>
        public Image(Stream stream, Configuration configuration)
            : base(stream, null, configuration)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Image"/> class.
        /// </summary>
        /// <param name="stream">
        /// The stream containing image information.
        /// </param>
        /// <param name="options">
        /// The options for the decoder.
        /// </param>
        /// <param name="configuration">
        /// The configuration providing initialization code which allows extending the library.
        /// </param>
        /// <exception cref="System.ArgumentNullException">Thrown if the <paramref name="stream"/> is null.</exception>
        public Image(Stream stream, IDecoderOptions options, Configuration configuration)
            : base(stream, options, configuration)
        {
        }

#if !NETSTANDARD1_1
        /// <summary>
        /// Initializes a new instance of the <see cref="Image"/> class.
        /// </summary>
        /// <param name="filePath">
        /// A file path to read image information.
        /// </param>
        /// <exception cref="System.ArgumentNullException">Thrown if the <paramref name="filePath"/> is null.</exception>
        public Image(string filePath)
            : base(filePath, null, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Image"/> class.
        /// </summary>
        /// <param name="filePath">
        /// A file path to read image information.
        /// </param>
        /// <param name="options">
        /// The options for the decoder.
        /// </param>
        /// <exception cref="System.ArgumentNullException">Thrown if the <paramref name="filePath"/> is null.</exception>
        public Image(string filePath, IDecoderOptions options)
            : base(filePath, options, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Image"/> class.
        /// </summary>
        /// <param name="filePath">
        /// A file path to read image information.
        /// </param>
        /// <param name="configuration">
        /// The configuration providing initialization code which allows extending the library.
        /// </param>
        /// <exception cref="System.ArgumentNullException">Thrown if the <paramref name="filePath"/> is null.</exception>
        public Image(string filePath, Configuration configuration)
            : base(filePath, null, configuration)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Image"/> class.
        /// </summary>
        /// <param name="filePath">
        /// A file path to read image information.
        /// </param>
        /// <param name="options">
        /// The options for the decoder.
        /// </param>
        /// <param name="configuration">
        /// The configuration providing initialization code which allows extending the library.
        /// </param>
        /// <exception cref="System.ArgumentNullException">Thrown if the <paramref name="filePath"/> is null.</exception>
        public Image(string filePath, IDecoderOptions options, Configuration configuration)
            : base(filePath, options, configuration)
        {
        }
#endif

        /// <summary>
        /// Initializes a new instance of the <see cref="Image"/> class.
        /// </summary>
        /// <param name="bytes">
        /// The byte array containing image information.
        /// </param>
        /// <exception cref="System.ArgumentNullException">Thrown if the <paramref name="bytes"/> is null.</exception>
        public Image(byte[] bytes)
           : base(bytes, null, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Image"/> class.
        /// </summary>
        /// <param name="bytes">
        /// The byte array containing image information.
        /// </param>
        /// <param name="options">
        /// The options for the decoder.
        /// </param>
        /// <exception cref="System.ArgumentNullException">Thrown if the <paramref name="bytes"/> is null.</exception>
        public Image(byte[] bytes, IDecoderOptions options)
           : base(bytes, options, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Image"/> class.
        /// </summary>
        /// <param name="bytes">
        /// The byte array containing image information.
        /// </param>
        /// <param name="configuration">
        /// The configuration providing initialization code which allows extending the library.
        /// </param>
        /// <exception cref="System.ArgumentNullException">Thrown if the <paramref name="bytes"/> is null.</exception>
        public Image(byte[] bytes, Configuration configuration)
           : base(bytes, null, configuration)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Image"/> class.
        /// </summary>
        /// <param name="bytes">
        /// The byte array containing image information.
        /// </param>
        /// <param name="options">
        /// The options for the decoder.
        /// </param>
        /// <param name="configuration">
        /// The configuration providing initialization code which allows extending the library.
        /// </param>
        /// <exception cref="System.ArgumentNullException">Thrown if the <paramref name="bytes"/> is null.</exception>
        public Image(byte[] bytes, IDecoderOptions options, Configuration configuration)
           : base(bytes, options, configuration)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Image"/> class
        /// by making a copy from another image.
        /// </summary>
        /// <param name="other">The other image, where the clone should be made from.</param>
        /// <exception cref="System.ArgumentNullException"><paramref name="other"/> is null.</exception>
        public Image(Image other)
            : base(other)
        {
        }

        /// <inheritdoc />
        public override PixelAccessor<Color> Lock()
        {
            return new PixelAccessor(this);
        }

        /// <inheritdoc />
        internal override ImageFrame<Color> ToFrame()
        {
            return new ImageFrame(this);
        }
    }
}
