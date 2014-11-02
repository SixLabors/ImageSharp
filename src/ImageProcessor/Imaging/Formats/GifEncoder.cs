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
//   Adapted from <see href="http://github.com/DataDink/Bumpkit/blob/master/BumpKit/BumpKit/GifEncoder.cs"/>
//   </remarks>
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Imaging.Formats
{
    using System;
    using System.Drawing;
    using System.Drawing.Imaging;
    using System.IO;
    using System.Linq;

    /// <summary>
    /// Encodes multiple images as an animated gif to a stream.
    /// <remarks>
    /// Uses default .NET GIF encoding and adds animation headers.
    /// Adapted from <see href="http://github.com/DataDink/Bumpkit/blob/master/BumpKit/BumpKit/GifEncoder.cs"/>
    /// </remarks>
    /// </summary>
    public class GifEncoder
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
        /// The converter for creating the output image from a byte array.
        /// </summary>
        private static readonly ImageConverter Converter = new ImageConverter();

        /// <summary>
        /// The stream.
        /// </summary>
        // ReSharper disable once FieldCanBeMadeReadOnly.Local
        private MemoryStream imageStream;

        /// <summary>
        /// The height.
        /// </summary>
        private int? height;

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

        #region Constructors
        /// <summary>
        /// Initializes a new instance of the <see cref="GifEncoder"/> class.
        /// </summary>
        /// <param name="width">
        /// Sets the width for this gif or null to use the first frame's width.
        /// </param>
        /// <param name="height">
        /// Sets the height for this gif or null to use the first frame's height.
        /// </param>
        /// <param name="repeatCount">
        /// The number of times to repeat the animation.
        /// </param>
        public GifEncoder(int? width = null, int? height = null, int? repeatCount = null)
        {
            this.imageStream = new MemoryStream();
            this.width = width;
            this.height = height;
            this.repeatCount = repeatCount;
        }
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

                this.WriteGraphicControlBlock(gifStream, Convert.ToInt32(frame.Delay.TotalMilliseconds / 10F));
                this.WriteImageBlock(gifStream, !this.isFirstImage, frame.X, frame.Y, frame.Image.Width, frame.Image.Height);
            }

            this.isFirstImage = false;
        }

        /// <summary>
        /// Saves the completed gif to an <see cref="Image"/>
        /// </summary>
        /// <returns>The completed animated gif.</returns>
        public Image Save()
        {
            // Complete File
            this.WriteByte(FileTrailer);

            // Push the data
            this.imageStream.Flush();
            this.imageStream.Position = 0;
            byte[] bytes = this.imageStream.ToArray();
            this.imageStream.Dispose();
            return (Image)Converter.ConvertFrom(bytes);
        }
        #endregion

        #region Methods
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
            // File Header signature and version.
            this.WriteString(FileType);
            this.WriteString(FileVersion);

            // Write the logical screen descriptor.
            this.WriteShort(this.width.GetValueOrDefault(w)); // Initial Logical Width
            this.WriteShort(this.height.GetValueOrDefault(h)); // Initial Logical Height

            // Read the global color table info.
            sourceGif.Position = SourceGlobalColorInfoPosition;
            this.WriteByte(sourceGif.ReadByte());

            this.WriteByte(0); // Background Color Index
            this.WriteByte(0); // Pixel aspect ratio
            this.WriteColorTable(sourceGif);

            // Application Extension Header
            int count = this.repeatCount.GetValueOrDefault(0);
            if (count != 1)
            {
                // 0 means loop indefinitely. count is set as play n + 1 times.
                count = Math.Max(0, count - 1);
                this.WriteShort(ApplicationExtensionBlockIdentifier);
                this.WriteByte(ApplicationBlockSize);

                this.WriteString(ApplicationIdentification);
                this.WriteByte(3); // Application block length
                this.WriteByte(1);
                this.WriteShort(count); // Repeat count for images.

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
            this.imageStream.WriteByte(Convert.ToByte(value));
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
            this.imageStream.Write(colorTable, 0, colorTable.Length);
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
            this.WriteShort(frameDelay); // Setting frame delay
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
            // Local Image Descriptor
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

                this.imageStream.WriteByte(Convert.ToByte(dataLength));
                this.imageStream.Write(imgData, 0, dataLength);
                dataLength = sourceGif.ReadByte();
            }

            this.imageStream.WriteByte(0); // Terminator
        }

        /// <summary>
        /// The write short.
        /// </summary>
        /// <param name="value">
        /// The value.
        /// </param>
        private void WriteShort(int value)
        {
            // Leave only one significant byte.
            this.imageStream.WriteByte(Convert.ToByte(value & 0xff));
            this.imageStream.WriteByte(Convert.ToByte((value >> 8) & 0xff));
        }

        /// <summary>
        /// The write string.
        /// </summary>
        /// <param name="value">
        /// The value.
        /// </param>
        private void WriteString(string value)
        {
            this.imageStream.Write(value.ToArray().Select(c => (byte)c).ToArray(), 0, value.Length);
        }
        #endregion
    }
}