// --------------------------------------------------------------------------------------------------------------------
// <copyright file="GifEncoder - Copy.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Imaging
{
    #region

    using System;
    using System.Drawing;
    using System.Drawing.Imaging;
    using System.IO;
    using System.Linq;

    #endregion

    /// <summary>
    ///     Encodes multiple images as an animated gif to a stream.
    ///     <remarks>
    ///         Always wire this up in a using block.
    ///         Disposing the encoder will complete the file.
    ///         Uses default .NET GIF encoding and adds animation headers.
    ///     </remarks>
    /// </summary>
    /// <summary>
    ///     Encodes multiple images as an animated gif to a stream. <br />
    ///     ALWAYS ALWAYS ALWAYS wire this up   in a using block <br />
    ///     Disposing the encoder will complete the file. <br />
    ///     Uses default .net GIF encoding and adds animation headers.
    /// </summary>
    public class GifEncoder2 : IDisposable
    {
        #region Constants

        /// <summary>
        /// The application block size.
        /// </summary>
        private const byte ApplicationBlockSize = 0x0b;

        /// <summary>
        /// The application extension block identifier.
        /// </summary>
        private const int ApplicationExtensionBlockIdentifier = 0xff21;

        /// <summary>
        /// The application identification.
        /// </summary>
        private const string ApplicationIdentification = "NETSCAPE2.0";

        /// <summary>
        /// The file trailer.
        /// </summary>
        private const byte FileTrailer = 0x3b;

        /// <summary>
        /// The file type.
        /// </summary>
        private const string FileType = "GIF";

        /// <summary>
        /// The file version.
        /// </summary>
        private const string FileVersion = "89a";

        /// <summary>
        /// The graphic control extension block identifier.
        /// </summary>
        private const int GraphicControlExtensionBlockIdentifier = 0xf921;

        /// <summary>
        /// The graphic control extension block size.
        /// </summary>
        private const byte GraphicControlExtensionBlockSize = 0x04;

        /// <summary>
        /// The source color block length.
        /// </summary>
        private const long SourceColorBlockLength = 768;

        /// <summary>
        /// The source color block position.
        /// </summary>
        private const long SourceColorBlockPosition = 13;

        /// <summary>
        /// The source global color info position.
        /// </summary>
        private const long SourceGlobalColorInfoPosition = 10;

        /// <summary>
        /// The source graphic control extension length.
        /// </summary>
        private const long SourceGraphicControlExtensionLength = 8;

        /// <summary>
        /// The source graphic control extension position.
        /// </summary>
        private const long SourceGraphicControlExtensionPosition = 781;

        /// <summary>
        /// The source image block header length.
        /// </summary>
        private const long SourceImageBlockHeaderLength = 11;

        /// <summary>
        /// The source image block position.
        /// </summary>
        private const long SourceImageBlockPosition = 789;

        #endregion

        #region Fields

        /// <summary>
        /// The _stream.
        /// </summary>
        private readonly Stream _stream;

        /// <summary>
        /// The _height.
        /// </summary>
        private int? _height;

        /// <summary>
        /// The _is first image.
        /// </summary>
        private bool _isFirstImage = true;

        /// <summary>
        /// The _repeat count.
        /// </summary>
        private int? _repeatCount;

        /// <summary>
        /// The _width.
        /// </summary>
        private int? _width;

        #endregion

        #region Constructors and Destructors

        /// <summary>
        /// Initializes a new instance of the <see cref="GifEncoder2"/> class. 
        /// Encodes multiple images as an animated gif to a stream. <br/>
        ///     ALWAYS ALWAYS ALWAYS wire this in a using block <br/>
        ///     Disposing the encoder will complete the file. <br/>
        ///     Uses default .net GIF encoding and adds animation headers.
        /// </summary>
        /// <param name="stream">
        /// The stream that will be written to.
        /// </param>
        /// <param name="width">
        /// Sets the width for this gif or null to use the first frame's width.
        /// </param>
        /// <param name="height">
        /// Sets the height for this gif or null to use the first frame's height.
        /// </param>
        /// <param name="repeatCount">
        /// The repeat Count.
        /// </param>
        public GifEncoder2(Stream stream, int? width = null, int? height = null, int? repeatCount = null)
        {
            this._stream = stream;
            this._width = width;
            this._height = height;
            this._repeatCount = repeatCount;
        }

        #endregion

        #region Public Properties

        /// <summary>
        /// Gets or sets the frame delay.
        /// </summary>
        public TimeSpan FrameDelay { get; set; }

        #endregion

        #region Public Methods and Operators

        /// <summary>
        /// Adds a frame to this animation.
        /// </summary>
        /// <param name="img">
        /// The image to add
        /// </param>
        /// <param name="x">
        /// The positioning x offset this image should be displayed at.
        /// </param>
        /// <param name="y">
        /// The positioning y offset this image should be displayed at.
        /// </param>
        /// <param name="frameDelay">
        /// The frame Delay.
        /// </param>
        public void AddFrame(Image img, int x = 0, int y = 0, TimeSpan? frameDelay = null)
        {
            using (var gifStream = new MemoryStream())
            {
                img.Save(gifStream, ImageFormat.Gif);
                if (this._isFirstImage)
                {
                    // Steal the global color table info
                    this.InitHeader(gifStream, img.Width, img.Height);
                }

                this.WriteGraphicControlBlock(gifStream, frameDelay.GetValueOrDefault(this.FrameDelay));
                this.WriteImageBlock(gifStream, !this._isFirstImage, x, y, img.Width, img.Height);
            }

            this._isFirstImage = false;
        }

        /// <summary>
        /// The dispose.
        /// </summary>
        public void Dispose()
        {
            // Complete Application Block
            this.WriteByte(0);

            // Complete File
            this.WriteByte(FileTrailer);

            // Pushing data
            this._stream.Flush();
        }

        #endregion

        #region Methods

        /// <summary>
        /// The init header.
        /// </summary>
        /// <param name="sourceGif">
        /// The source gif.
        /// </param>
        /// <param name="w">
        /// The w.
        /// </param>
        /// <param name="h">
        /// The h.
        /// </param>
        private void InitHeader(Stream sourceGif, int w, int h)
        {
            // File Header
            this.WriteString(FileType);
            this.WriteString(FileVersion);
            this.WriteShort(this._width.GetValueOrDefault(w)); // Initial Logical Width
            this.WriteShort(this._height.GetValueOrDefault(h)); // Initial Logical Height
            sourceGif.Position = SourceGlobalColorInfoPosition;
            this.WriteByte(sourceGif.ReadByte()); // Global Color Table Info
            this.WriteByte(0); // Background Color Index
            this.WriteByte(0); // Pixel aspect ratio
            this.WriteColorTable(sourceGif);

            // App Extension Header
            this.WriteShort(ApplicationExtensionBlockIdentifier);
            this.WriteByte(ApplicationBlockSize);
            this.WriteString(ApplicationIdentification);
            this.WriteByte(3); // Application block length
            this.WriteByte(1);
            this.WriteShort(this._repeatCount.GetValueOrDefault(0)); // Repeat count for images.
            this.WriteByte(0); // terminator
        }

        /// <summary>
        /// The write byte.
        /// </summary>
        /// <param name="value">
        /// The value.
        /// </param>
        private void WriteByte(int value)
        {
            this._stream.WriteByte(Convert.ToByte(value));
        }

        /// <summary>
        /// The write color table.
        /// </summary>
        /// <param name="sourceGif">
        /// The source gif.
        /// </param>
        private void WriteColorTable(Stream sourceGif)
        {
            sourceGif.Position = SourceColorBlockPosition; // Locating the image color table
            var colorTable = new byte[SourceColorBlockLength];
            sourceGif.Read(colorTable, 0, colorTable.Length);
            this._stream.Write(colorTable, 0, colorTable.Length);
        }

        /// <summary>
        /// The write graphic control block.
        /// </summary>
        /// <param name="sourceGif">
        /// The source gif.
        /// </param>
        /// <param name="frameDelay">
        /// The frame delay.
        /// </param>
        private void WriteGraphicControlBlock(Stream sourceGif, TimeSpan frameDelay)
        {
            sourceGif.Position = SourceGraphicControlExtensionPosition; // Locating the source GCE
            var blockhead = new byte[SourceGraphicControlExtensionLength];
            sourceGif.Read(blockhead, 0, blockhead.Length); // Reading source GCE

            this.WriteShort(GraphicControlExtensionBlockIdentifier); // Identifier
            this.WriteByte(GraphicControlExtensionBlockSize); // Block Size
            this.WriteByte(blockhead[3] & 0xf7 | 0x08); // Setting disposal flag
            this.WriteShort(Convert.ToInt32(frameDelay.TotalMilliseconds / 10)); // Setting frame delay
            this.WriteByte(blockhead[6]); // Transparent color index
            this.WriteByte(0); // Terminator
        }

        /// <summary>
        /// The write image block.
        /// </summary>
        /// <param name="sourceGif">
        /// The source gif.
        /// </param>
        /// <param name="includeColorTable">
        /// The include color table.
        /// </param>
        /// <param name="x">
        /// The x.
        /// </param>
        /// <param name="y">
        /// The y.
        /// </param>
        /// <param name="h">
        /// The h.
        /// </param>
        /// <param name="w">
        /// The w.
        /// </param>
        private void WriteImageBlock(Stream sourceGif, bool includeColorTable, int x, int y, int h, int w)
        {
            sourceGif.Position = SourceImageBlockPosition; // Locating the image block
            var header = new byte[SourceImageBlockHeaderLength];
            sourceGif.Read(header, 0, header.Length);
            this.WriteByte(header[0]); // Separator
            this.WriteShort(x); // Position X
            this.WriteShort(y); // Position Y
            this.WriteShort(h); // Height
            this.WriteShort(w); // Width

            if (includeColorTable)
            {
                // If first frame, use global color table - else use local
                sourceGif.Position = SourceGlobalColorInfoPosition;
                this.WriteByte(sourceGif.ReadByte() & 0x3f | 0x80); // Enabling local color table
                this.WriteColorTable(sourceGif);
            }
            else
            {
                this.WriteByte(header[9] & 0x07 | 0x07); // Disabling local color table
            }

            this.WriteByte(header[10]); // LZW Min Code Size

            // Read/Write image data
            sourceGif.Position = SourceImageBlockPosition + SourceImageBlockHeaderLength;

            int dataLength = sourceGif.ReadByte();
            while (dataLength > 0)
            {
                var imgData = new byte[dataLength];
                sourceGif.Read(imgData, 0, dataLength);

                this._stream.WriteByte(Convert.ToByte(dataLength));
                this._stream.Write(imgData, 0, dataLength);
                dataLength = sourceGif.ReadByte();
            }

            this._stream.WriteByte(0); // Terminator
        }

        /// <summary>
        /// The write short.
        /// </summary>
        /// <param name="value">
        /// The value.
        /// </param>
        private void WriteShort(int value)
        {
            this._stream.WriteByte(Convert.ToByte(value & 0xff));
            this._stream.WriteByte(Convert.ToByte((value >> 8) & 0xff));
        }

        /// <summary>
        /// The write string.
        /// </summary>
        /// <param name="value">
        /// The value.
        /// </param>
        private void WriteString(string value)
        {
            this._stream.Write(value.ToArray().Select(c => (byte)c).ToArray(), 0, value.Length);
        }

        #endregion
    }
}