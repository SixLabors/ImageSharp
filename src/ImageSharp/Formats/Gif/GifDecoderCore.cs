// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.MetaData;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Formats.Gif
{
    /// <summary>
    /// Performs the gif decoding operation.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal sealed class GifDecoderCore<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// The temp buffer used to reduce allocations.
        /// </summary>
        private readonly byte[] buffer = new byte[16];

        /// <summary>
        /// The global configuration.
        /// </summary>
        private readonly Configuration configuration;

        /// <summary>
        /// The currently loaded stream.
        /// </summary>
        private Stream currentStream;

        /// <summary>
        /// The global color table.
        /// </summary>
        private Buffer<byte> globalColorTable;

        /// <summary>
        /// The global color table length
        /// </summary>
        private int globalColorTableLength;

        /// <summary>
        /// The previous frame.
        /// </summary>
        private ImageFrame<TPixel> previousFrame;

        /// <summary>
        /// The area to restore.
        /// </summary>
        private Rectangle? restoreArea;

        /// <summary>
        /// The logical screen descriptor.
        /// </summary>
        private GifLogicalScreenDescriptor logicalScreenDescriptor;

        /// <summary>
        /// The graphics control extension.
        /// </summary>
        private GifGraphicsControlExtension graphicsControlExtension;

        /// <summary>
        /// The metadata
        /// </summary>
        private ImageMetaData metaData;

        /// <summary>
        /// The image to decode the information to.
        /// </summary>
        private Image<TPixel> image;

        /// <summary>
        /// Initializes a new instance of the <see cref="GifDecoderCore{TPixel}"/> class.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        /// <param name="options">The decoder options.</param>
        public GifDecoderCore(Configuration configuration, IGifDecoderOptions options)
        {
            this.TextEncoding = options.TextEncoding ?? GifConstants.DefaultEncoding;
            this.IgnoreMetadata = options.IgnoreMetadata;
            this.DecodingMode = options.DecodingMode;
            this.configuration = configuration ?? Configuration.Default;
        }

        /// <summary>
        /// Gets or sets a value indicating whether the metadata should be ignored when the image is being decoded.
        /// </summary>
        public bool IgnoreMetadata { get; internal set; }

        /// <summary>
        /// Gets the text encoding
        /// </summary>
        public Encoding TextEncoding { get; }

        /// <summary>
        /// Gets the decoding mode for multi-frame images
        /// </summary>
        public FrameDecodingMode DecodingMode { get; }

        /// <summary>
        /// Decodes the stream to the image.
        /// </summary>
        /// <param name="stream">The stream containing image data. </param>
        /// <returns>The decoded image</returns>
        public Image<TPixel> Decode(Stream stream)
        {
            try
            {
                this.metaData = new ImageMetaData();

                this.currentStream = stream;

                // Skip the identifier
                this.currentStream.Skip(6);
                this.ReadLogicalScreenDescriptor();

                if (this.logicalScreenDescriptor.GlobalColorTableFlag)
                {
                    this.globalColorTableLength = this.logicalScreenDescriptor.GlobalColorTableSize * 3;
                    this.globalColorTable = Buffer<byte>.CreateClean(this.globalColorTableLength);

                    // Read the global color table from the stream
                    stream.Read(this.globalColorTable.Array, 0, this.globalColorTableLength);
                }

                // Loop though the respective gif parts and read the data.
                int nextFlag = stream.ReadByte();
                while (nextFlag != GifConstants.Terminator)
                {
                    if (nextFlag == GifConstants.ImageLabel)
                    {
                        if (this.previousFrame != null && this.DecodingMode == FrameDecodingMode.First)
                        {
                            break;
                        }

                        this.ReadFrame();
                    }
                    else if (nextFlag == GifConstants.ExtensionIntroducer)
                    {
                        int label = stream.ReadByte();
                        switch (label)
                        {
                            case GifConstants.GraphicControlLabel:
                                this.ReadGraphicalControlExtension();
                                break;
                            case GifConstants.CommentLabel:
                                this.ReadComments();
                                break;
                            case GifConstants.ApplicationExtensionLabel:

                                // The application extension length should be 11 but we've got test images that incorrectly
                                // set this to 252.
                                int appLength = stream.ReadByte();
                                this.Skip(appLength); // No need to read.
                                break;
                            case GifConstants.PlainTextLabel:
                                int plainLength = stream.ReadByte();
                                this.Skip(plainLength); // Not supported by any known decoder.
                                break;
                        }
                    }
                    else if (nextFlag == GifConstants.EndIntroducer)
                    {
                        break;
                    }

                    nextFlag = stream.ReadByte();
                    if (nextFlag == -1)
                    {
                        break;
                    }
                }
            }
            finally
            {
                this.globalColorTable?.Dispose();
            }

            return this.image;
        }

        /// <summary>
        /// Reads the graphic control extension.
        /// </summary>
        private void ReadGraphicalControlExtension()
        {
            this.currentStream.Read(this.buffer, 0, 6);

            byte packed = this.buffer[1];

            this.graphicsControlExtension = new GifGraphicsControlExtension
            {
                DelayTime = BitConverter.ToInt16(this.buffer, 2),
                TransparencyIndex = this.buffer[4],
                TransparencyFlag = (packed & 0x01) == 1,
                DisposalMethod = (DisposalMethod)((packed & 0x1C) >> 2)
            };
        }

        /// <summary>
        /// Reads the image descriptor
        /// </summary>
        /// <returns><see cref="GifImageDescriptor"/></returns>
        private GifImageDescriptor ReadImageDescriptor()
        {
            this.currentStream.Read(this.buffer, 0, 9);

            byte packed = this.buffer[8];

            var imageDescriptor = new GifImageDescriptor
            {
                Left = BitConverter.ToInt16(this.buffer, 0),
                Top = BitConverter.ToInt16(this.buffer, 2),
                Width = BitConverter.ToInt16(this.buffer, 4),
                Height = BitConverter.ToInt16(this.buffer, 6),
                LocalColorTableFlag = ((packed & 0x80) >> 7) == 1,
                LocalColorTableSize = 2 << (packed & 0x07),
                InterlaceFlag = ((packed & 0x40) >> 6) == 1
            };

            return imageDescriptor;
        }

        /// <summary>
        /// Reads the logical screen descriptor.
        /// </summary>
        private void ReadLogicalScreenDescriptor()
        {
            this.currentStream.Read(this.buffer, 0, 7);

            byte packed = this.buffer[4];

            this.logicalScreenDescriptor = new GifLogicalScreenDescriptor
            {
                Width = BitConverter.ToInt16(this.buffer, 0),
                Height = BitConverter.ToInt16(this.buffer, 2),
                BackgroundColorIndex = this.buffer[5],
                PixelAspectRatio = this.buffer[6],
                GlobalColorTableFlag = ((packed & 0x80) >> 7) == 1,
                GlobalColorTableSize = 2 << (packed & 0x07)
            };

            if (this.logicalScreenDescriptor.GlobalColorTableSize > 255 * 4)
            {
                throw new ImageFormatException($"Invalid gif colormap size '{this.logicalScreenDescriptor.GlobalColorTableSize}'");
            }
        }

        /// <summary>
        /// Skips the designated number of bytes in the stream.
        /// </summary>
        /// <param name="length">The number of bytes to skip.</param>
        private void Skip(int length)
        {
            this.currentStream.Skip(length);

            int flag;

            while ((flag = this.currentStream.ReadByte()) != 0)
            {
                this.currentStream.Skip(flag);
            }
        }

        /// <summary>
        /// Reads the gif comments.
        /// </summary>
        private void ReadComments()
        {
            int length;

            while ((length = this.currentStream.ReadByte()) != 0)
            {
                if (length > GifConstants.MaxCommentLength)
                {
                    throw new ImageFormatException($"Gif comment length '{length}' exceeds max '{GifConstants.MaxCommentLength}'");
                }

                if (this.IgnoreMetadata)
                {
                    this.currentStream.Seek(length, SeekOrigin.Current);
                    continue;
                }

                byte[] commentsBuffer = ArrayPool<byte>.Shared.Rent(length);

                try
                {
                    this.currentStream.Read(commentsBuffer, 0, length);
                    string comments = this.TextEncoding.GetString(commentsBuffer, 0, length);
                    this.metaData.Properties.Add(new ImageProperty(GifConstants.Comments, comments));
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(commentsBuffer);
                }
            }
        }

        /// <summary>
        /// Reads an individual gif frame.
        /// </summary>
        private void ReadFrame()
        {
            GifImageDescriptor imageDescriptor = this.ReadImageDescriptor();

            Buffer<byte> localColorTable = null;
            Buffer<byte> indices = null;
            try
            {
                // Determine the color table for this frame. If there is a local one, use it otherwise use the global color table.
                if (imageDescriptor.LocalColorTableFlag)
                {
                    int length = imageDescriptor.LocalColorTableSize * 3;
                    localColorTable = Buffer<byte>.CreateClean(length);
                    this.currentStream.Read(localColorTable.Array, 0, length);
                }

                indices = Buffer<byte>.CreateClean(imageDescriptor.Width * imageDescriptor.Height);

                this.ReadFrameIndices(imageDescriptor, indices);
                this.ReadFrameColors(indices, localColorTable ?? this.globalColorTable, imageDescriptor);

                // Skip any remaining blocks
                this.Skip(0);
            }
            finally
            {
                localColorTable?.Dispose();
                indices?.Dispose();
            }
        }

        /// <summary>
        /// Reads the frame indices marking the color to use for each pixel.
        /// </summary>
        /// <param name="imageDescriptor">The <see cref="GifImageDescriptor"/>.</param>
        /// <param name="indices">The pixel array to write to.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ReadFrameIndices(GifImageDescriptor imageDescriptor, Span<byte> indices)
        {
            int dataSize = this.currentStream.ReadByte();
            using (var lzwDecoder = new LzwDecoder(this.currentStream))
            {
                lzwDecoder.DecodePixels(imageDescriptor.Width, imageDescriptor.Height, dataSize, indices);
            }
        }

        /// <summary>
        /// Reads the frames colors, mapping indices to colors.
        /// </summary>
        /// <param name="indices">The indexed pixels.</param>
        /// <param name="colorTable">The color table containing the available colors.</param>
        /// <param name="descriptor">The <see cref="GifImageDescriptor"/></param>
        private void ReadFrameColors(Span<byte> indices, Span<byte> colorTable, GifImageDescriptor descriptor)
        {
            int imageWidth = this.logicalScreenDescriptor.Width;
            int imageHeight = this.logicalScreenDescriptor.Height;

            ImageFrame<TPixel> prevFrame = null;

            ImageFrame<TPixel> currentFrame = null;

            ImageFrame<TPixel> imageFrame;

            if (this.previousFrame == null)
            {
                // This initializes the image to become fully transparent because the alpha channel is zero.
                this.image = new Image<TPixel>(this.configuration, imageWidth, imageHeight, this.metaData);

                this.SetFrameMetaData(this.image.Frames.RootFrame.MetaData);

                imageFrame = this.image.Frames.RootFrame;
            }
            else
            {
                if (this.graphicsControlExtension != null &&
                    this.graphicsControlExtension.DisposalMethod == DisposalMethod.RestoreToPrevious)
                {
                    prevFrame = this.previousFrame;
                }

                currentFrame = this.image.Frames.AddFrame(this.previousFrame); // This clones the frame and adds it the collection

                this.SetFrameMetaData(currentFrame.MetaData);

                imageFrame = currentFrame;

                this.RestoreToBackground(imageFrame);
            }

            int i = 0;
            int interlacePass = 0; // The interlace pass
            int interlaceIncrement = 8; // The interlacing line increment
            int interlaceY = 0; // The current interlaced line

            for (int y = descriptor.Top; y < descriptor.Top + descriptor.Height; y++)
            {
                // Check if this image is interlaced.
                int writeY; // the target y offset to write to
                if (descriptor.InterlaceFlag)
                {
                    // If so then we read lines at predetermined offsets.
                    // When an entire image height worth of offset lines has been read we consider this a pass.
                    // With each pass the number of offset lines changes and the starting line changes.
                    if (interlaceY >= descriptor.Height)
                    {
                        interlacePass++;
                        switch (interlacePass)
                        {
                            case 1:
                                interlaceY = 4;
                                break;
                            case 2:
                                interlaceY = 2;
                                interlaceIncrement = 4;
                                break;
                            case 3:
                                interlaceY = 1;
                                interlaceIncrement = 2;
                                break;
                        }
                    }

                    writeY = interlaceY + descriptor.Top;

                    interlaceY += interlaceIncrement;
                }
                else
                {
                    writeY = y;
                }

                Span<TPixel> rowSpan = imageFrame.GetPixelRowSpan(writeY);

                var rgba = new Rgba32(0, 0, 0, 255);

                // #403 The left + width value can be larger than the image width
                for (int x = descriptor.Left; x < descriptor.Left + descriptor.Width && x < rowSpan.Length; x++)
                {
                    int index = indices[i];

                    if (this.graphicsControlExtension == null ||
                        this.graphicsControlExtension.TransparencyFlag == false ||
                        this.graphicsControlExtension.TransparencyIndex != index)
                    {
                        int indexOffset = index * 3;

                        ref TPixel pixel = ref rowSpan[x];
                        rgba.Rgb = colorTable.GetRgb24(indexOffset);

                        pixel.PackFromRgba32(rgba);
                    }

                    i++;
                }
            }

            if (prevFrame != null)
            {
                this.previousFrame = prevFrame;
                return;
            }

            this.previousFrame = currentFrame ?? this.image.Frames.RootFrame;

            if (this.graphicsControlExtension != null &&
                this.graphicsControlExtension.DisposalMethod == DisposalMethod.RestoreToBackground)
            {
                this.restoreArea = new Rectangle(descriptor.Left, descriptor.Top, descriptor.Width, descriptor.Height);
            }
        }

        /// <summary>
        /// Restores the current frame area to the background.
        /// </summary>
        /// <param name="frame">The frame.</param>
        private void RestoreToBackground(ImageFrame<TPixel> frame)
        {
            if (this.restoreArea == null)
            {
                return;
            }

            // Optimization for when the size of the frame is the same as the image size.
            if (this.restoreArea.Value.Width == this.image.Width &&
                this.restoreArea.Value.Height == this.image.Height)
            {
                using (PixelAccessor<TPixel> pixelAccessor = frame.Lock())
                {
                    pixelAccessor.Reset();
                }
            }
            else
            {
                using (var emptyRow = new PixelArea<TPixel>(this.restoreArea.Value.Width, ComponentOrder.Xyzw))
                {
                    using (PixelAccessor<TPixel> pixelAccessor = frame.Lock())
                    {
                        for (int y = this.restoreArea.Value.Top; y < this.restoreArea.Value.Top + this.restoreArea.Value.Height; y++)
                        {
                            pixelAccessor.CopyFrom(emptyRow, y, this.restoreArea.Value.Left);
                        }
                    }
                }
            }

            this.restoreArea = null;
        }

        /// <summary>
        /// Sets the frames metadata.
        /// </summary>
        /// <param name="meta">The meta data.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetFrameMetaData(ImageFrameMetaData meta)
        {
            if (this.graphicsControlExtension != null)
            {
                if (this.graphicsControlExtension.DelayTime > 0)
                {
                    meta.FrameDelay = this.graphicsControlExtension.DelayTime;
                }

                meta.DisposalMethod = this.graphicsControlExtension.DisposalMethod;
            }
        }
    }
}