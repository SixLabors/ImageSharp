// --------------------------------------------------------------------------------------------------------------------
// <copyright file="GifEncoder.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Encodes multiple images as an animated gif to a stream.
//   <remarks>
//   Always wire this up in a using block.
//   Disposing the encoder will complete the file.
//   Uses default .NET GIF encoding and adds animation headers.
//   </remarks>
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Imaging
{
    #region Using
    using System;
    using System.Drawing;
    using System.Drawing.Imaging;
    using System.IO;
    using System.Linq;
    #endregion

    /// <summary>
    /// Encodes multiple images as an animated gif to a stream.
    /// <remarks>
    /// Always wire this up in a using block.
    /// Disposing the encoder will complete the file.
    /// Uses default .NET GIF encoding and adds animation headers.
    /// </remarks>
    /// </summary>
    public class GifEncoder : IDisposable
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
        /// The stream.
        /// </summary>
        private Stream inputStream;

        /// <summary>
        /// The height.
        /// </summary>
        private int? height;

        /// <summary>
        /// A value indicating whether this instance of the given entity has been disposed.
        /// </summary>
        /// <value><see langword="true"/> if this instance has been disposed; otherwise, <see langword="false"/>.</value>
        /// <remarks>
        /// If the entity is disposed, it must not be disposed a second
        /// time. The isDisposed field is set the first time the entity
        /// is disposed. If the isDisposed field is true, then the Dispose()
        /// method will not dispose again. This help not to prolong the entity's
        /// life in the Garbage Collector.
        /// </remarks>
        private bool isDisposed;

        /// <summary>
        /// The is first image.
        /// </summary>
        private bool isFirstImage = true;

        /// <summary>
        /// The repeat count.
        /// </summary>
        private int? repeatCount;

        /// <summary>
        /// The width.
        /// </summary>
        private int? width;
        #endregion

        #region Constructors and Destructors
        /// <summary>
        /// Initializes a new instance of the <see cref="GifEncoder"/> class.
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
        /// The number of times to repeat the animation.
        /// </param>
        public GifEncoder(Stream stream, int? width = null, int? height = null, int? repeatCount = null)
        {
            this.inputStream = stream;
            this.width = width;
            this.height = height;
            this.repeatCount = repeatCount;
        }

        /// <summary>
        /// Finalizes an instance of the <see cref="GifEncoder"/> class. 
        /// </summary>
        /// <remarks>
        /// Use C# destructor syntax for finalization code.
        /// This destructor will run only if the Dispose method 
        /// does not get called.
        /// It gives your base class the opportunity to finalize.
        /// Do not provide destructors in types derived from this class.
        /// </remarks>
        ~GifEncoder()
        {
            // Do not re-create Dispose clean-up code here.
            // Calling Dispose(false) is optimal in terms of
            // readability and maintainability.
            this.Dispose(false);
        }
        #endregion

        #region Properties
        /// <summary>
        ///     Gets or sets the frame delay.
        /// </summary>
        public TimeSpan FrameDelay { get; set; }
        #endregion

        #region Public Methods and Operators
        /// <summary>
        /// Adds a frame to the gif.
        /// </summary>
        /// <param name="frame">
        /// The <see cref="GifFrame"/> containing the image.
        /// </param>
        public void AddFrame(GifFrame frame)
        {
            using (MemoryStream gifStream = new MemoryStream())
            {
                frame.Image.Save(gifStream, ImageFormat.Gif);
                if (this.isFirstImage)
                {
                    // Steal the global color table info
                    this.WriteHeaderBlock(gifStream, frame.Image.Width, frame.Image.Height);
                }

                this.WriteGraphicControlBlock(gifStream, frame.Delay);
                this.WriteImageBlock(gifStream, !this.isFirstImage, frame.X, frame.Y, frame.Image.Width, frame.Image.Height);
            }

            this.isFirstImage = false;
        }

        public Image Save()
        {
            
        }

        /// <summary>
        /// Disposes the object and frees resources for the Garbage Collector.
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);

            // This object will be cleaned up by the Dispose method.
            // Therefore, you should call GC.SupressFinalize to
            // take this object off the finalization queue 
            // and prevent finalization code for this object
            // from executing a second time.
            GC.SuppressFinalize(this);
        }
        #endregion

        #region Methods
        /// <summary>
        /// Disposes the object and frees resources for the Garbage Collector.
        /// </summary>
        /// <param name="disposing">
        /// If true, the object gets disposed.
        /// </param>
        protected virtual void Dispose(bool disposing)
        {
            if (this.isDisposed)
            {
                return;
            }

            if (disposing)
            {
                // Dispose of any managed resources here.
                // Complete Application Block
                this.WriteByte(0);

                // Complete File
                this.WriteByte(FileTrailer);

                // Pushing data
                this.inputStream.Flush();

                // Dispose of the memory stream from Load and the image.
                if (this.inputStream != null)
                {
                    this.inputStream.Dispose();
                    this.inputStream = null;
                }
            }

            // Call the appropriate methods to clean up
            // unmanaged resources here.
            // Note disposing is done.
            this.isDisposed = true;
        }

        /// <summary>
        /// Writes the header block of the animated gif to the stream.
        /// </summary>
        /// <param name="sourceGif">
        /// The source gif.
        /// </param>
        /// <param name="w">
        /// The width of the image.
        /// </param>
        /// <param name="h">
        /// The height of the image.
        /// </param>
        private void WriteHeaderBlock(Stream sourceGif, int w, int h)
        {
            int count = this.repeatCount.GetValueOrDefault(0);

            // File Header
            this.WriteString(FileType);
            this.WriteString(FileVersion);
            this.WriteShort(this.width.GetValueOrDefault(w)); // Initial Logical Width
            this.WriteShort(this.height.GetValueOrDefault(h)); // Initial Logical Height
            sourceGif.Position = SourceGlobalColorInfoPosition;
            this.WriteByte(sourceGif.ReadByte()); // Global Color Table Info
            this.WriteByte(0); // Background Color Index
            this.WriteByte(0); // Pixel aspect ratio
            this.WriteColorTable(sourceGif);

            // The different browsers interpret the spec differently when adding a loop.
            // If the loop count is one IE and FF &lt; 3 (incorrectly) loop an extra number of times.
            // Removing the Netscape header should fix this.
            if (count != 1)
            {
                // Application Extension Header
                this.WriteShort(ApplicationExtensionBlockIdentifier);
                this.WriteByte(ApplicationBlockSize);
                this.WriteString(ApplicationIdentification);
                this.WriteByte(3); // Application block length
                this.WriteByte(1);
                this.WriteShort(this.repeatCount.GetValueOrDefault(0)); // Repeat count for images.
                this.WriteByte(0); // Terminator
            }
        }

        /// <summary>
        /// The write byte.
        /// </summary>
        /// <param name="value">
        /// The value.
        /// </param>
        private void WriteByte(int value)
        {
            this.inputStream.WriteByte(Convert.ToByte(value));
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
            byte[] colorTable = new byte[SourceColorBlockLength];
            sourceGif.Read(colorTable, 0, colorTable.Length);
            this.inputStream.Write(colorTable, 0, colorTable.Length);
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
        private void WriteGraphicControlBlock(Stream sourceGif, int frameDelay)
        {
            sourceGif.Position = SourceGraphicControlExtensionPosition; // Locating the source GCE
            byte[] blockhead = new byte[SourceGraphicControlExtensionLength];
            sourceGif.Read(blockhead, 0, blockhead.Length); // Reading source GCE

            this.WriteShort(GraphicControlExtensionBlockIdentifier); // Identifier
            this.WriteByte(GraphicControlExtensionBlockSize); // Block Size
            this.WriteByte(blockhead[3] & 0xf7 | 0x08); // Setting disposal flag
            this.WriteShort(Convert.ToInt32(frameDelay / 10)); // Setting frame delay
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
        /// The x position to write the image block.
        /// </param>
        /// <param name="y">
        /// The y position to write the image block.
        /// </param>
        /// <param name="h">
        /// The height of the image block.
        /// </param>
        /// <param name="w">
        /// The width of the image block.
        /// </param>
        private void WriteImageBlock(Stream sourceGif, bool includeColorTable, int x, int y, int h, int w)
        {
            sourceGif.Position = SourceImageBlockPosition; // Locating the image block
            byte[] header = new byte[SourceImageBlockHeaderLength];
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
                byte[] imgData = new byte[dataLength];
                sourceGif.Read(imgData, 0, dataLength);

                this.inputStream.WriteByte(Convert.ToByte(dataLength));
                this.inputStream.Write(imgData, 0, dataLength);
                dataLength = sourceGif.ReadByte();
            }

            this.inputStream.WriteByte(0); // Terminator
        }

        /// <summary>
        /// The write short.
        /// </summary>
        /// <param name="value">
        /// The value.
        /// </param>
        private void WriteShort(int value)
        {
            this.inputStream.WriteByte(Convert.ToByte(value & 0xff));
            this.inputStream.WriteByte(Convert.ToByte((value >> 8) & 0xff));
        }

        /// <summary>
        /// The write string.
        /// </summary>
        /// <param name="value">
        /// The value.
        /// </param>
        private void WriteString(string value)
        {
            this.inputStream.Write(value.ToArray().Select(c => (byte)c).ToArray(), 0, value.Length);
        }
        #endregion
    }
}