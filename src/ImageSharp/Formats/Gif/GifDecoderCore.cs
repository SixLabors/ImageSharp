// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.MetaData;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Memory;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Formats.Gif
{
    /// <summary>
    /// Performs the gif decoding operation.
    /// </summary>
    internal sealed class GifDecoderCore
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
        private Stream stream;

        /// <summary>
        /// The global color table.
        /// </summary>
        private IManagedByteBuffer globalColorTable;

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
        private GifGraphicControlExtension graphicsControlExtension;

        /// <summary>
        /// The image desciptor.
        /// </summary>
        private GifImageDescriptor imageDescriptor;

        /// <summary>
        /// The abstract metadata.
        /// </summary>
        private ImageMetaData metaData;

        /// <summary>
        /// The gif specific metadata.
        /// </summary>
        private GifMetaData gifMetaData;

        /// <summary>
        /// Initializes a new instance of the <see cref="GifDecoderCore"/> class.
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

        private MemoryAllocator MemoryAllocator => this.configuration.MemoryAllocator;

        /// <summary>
        /// Decodes the stream to the image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="stream">The stream containing image data. </param>
        /// <returns>The decoded image</returns>
        public Image<TPixel> Decode<TPixel>(Stream stream)
            where TPixel : struct, IPixel<TPixel>
        {
            Image<TPixel> image = null;
            ImageFrame<TPixel> previousFrame = null;
            try
            {
                this.ReadLogicalScreenDescriptorAndGlobalColorTable(stream);

                // Loop though the respective gif parts and read the data.
                int nextFlag = stream.ReadByte();
                while (nextFlag != GifConstants.Terminator)
                {
                    if (nextFlag == GifConstants.ImageLabel)
                    {
                        if (previousFrame != null && this.DecodingMode == FrameDecodingMode.First)
                        {
                            break;
                        }

                        this.ReadFrame(ref image, ref previousFrame);
                    }
                    else if (nextFlag == GifConstants.ExtensionIntroducer)
                    {
                        switch (stream.ReadByte())
                        {
                            case GifConstants.GraphicControlLabel:
                                this.ReadGraphicalControlExtension();
                                break;
                            case GifConstants.CommentLabel:
                                this.ReadComments();
                                break;
                            case GifConstants.ApplicationExtensionLabel:
                                this.ReadApplicationExtension();
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

            return image;
        }

        /// <summary>
        /// Reads the raw image information from the specified stream.
        /// </summary>
        /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
        public IImageInfo Identify(Stream stream)
        {
            try
            {
                this.ReadLogicalScreenDescriptorAndGlobalColorTable(stream);

                // Loop though the respective gif parts and read the data.
                int nextFlag = stream.ReadByte();
                while (nextFlag != GifConstants.Terminator)
                {
                    if (nextFlag == GifConstants.ImageLabel)
                    {
                        this.ReadImageDescriptor();
                    }
                    else if (nextFlag == GifConstants.ExtensionIntroducer)
                    {
                        switch (stream.ReadByte())
                        {
                            case GifConstants.GraphicControlLabel:

                                // Skip graphic control extension block
                                this.Skip(0);
                                break;
                            case GifConstants.CommentLabel:
                                this.ReadComments();
                                break;
                            case GifConstants.ApplicationExtensionLabel:
                                this.ReadApplicationExtension();
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

            return new ImageInfo(
                new PixelTypeInfo(this.logicalScreenDescriptor.BitsPerPixel),
                this.logicalScreenDescriptor.Width,
                this.logicalScreenDescriptor.Height,
                this.metaData);
        }

        /// <summary>
        /// Reads the graphic control extension.
        /// </summary>
        private void ReadGraphicalControlExtension()
        {
            this.stream.Read(this.buffer, 0, 6);

            this.graphicsControlExtension = GifGraphicControlExtension.Parse(this.buffer);
        }

        /// <summary>
        /// Reads the image descriptor.
        /// </summary>
        private void ReadImageDescriptor()
        {
            this.stream.Read(this.buffer, 0, 9);

            this.imageDescriptor = GifImageDescriptor.Parse(this.buffer);
        }

        /// <summary>
        /// Reads the logical screen descriptor.
        /// </summary>
        private void ReadLogicalScreenDescriptor()
        {
            this.stream.Read(this.buffer, 0, 7);

            this.logicalScreenDescriptor = GifLogicalScreenDescriptor.Parse(this.buffer);
        }

        /// <summary>
        /// Reads the application extension block parsing any animation information
        /// if present.
        /// </summary>
        private void ReadApplicationExtension()
        {
            int appLength = this.stream.ReadByte();

            // If the length is 11 then it's a valid extension and most likely
            // a NETSCAPE or ANIMEXTS extension. We want the loop count from this.
            if (appLength == GifConstants.ApplicationBlockSize)
            {
                this.stream.Skip(appLength);
                int subBlockSize = this.stream.ReadByte();

                // TODO: There's also a NETSCAPE buffer extension.
                // http://www.vurdalakov.net/misc/gif/netscape-buffering-application-extension
                if (subBlockSize == GifConstants.NetscapeLoopingSubBlockSize)
                {
                    this.stream.Read(this.buffer, 0, GifConstants.NetscapeLoopingSubBlockSize);
                    this.gifMetaData.RepeatCount = GifNetscapeLoopingApplicationExtension.Parse(this.buffer.AsSpan(1)).RepeatCount;
                    this.stream.Skip(1); // Skip the terminator.
                    return;
                }

                // Could be XMP or something else not supported yet.
                // Back up and skip.
                this.stream.Position -= appLength + 1;
                this.Skip(appLength);
                return;
            }

            this.Skip(appLength); // Not supported by any known decoder.
        }

        /// <summary>
        /// Skips the designated number of bytes in the stream.
        /// </summary>
        /// <param name="length">The number of bytes to skip.</param>
        private void Skip(int length)
        {
            this.stream.Skip(length);

            int flag;

            while ((flag = this.stream.ReadByte()) != 0)
            {
                this.stream.Skip(flag);
            }
        }

        /// <summary>
        /// Reads the gif comments.
        /// </summary>
        private void ReadComments()
        {
            int length;

            while ((length = this.stream.ReadByte()) != 0)
            {
                if (length > GifConstants.MaxCommentLength)
                {
                    throw new ImageFormatException($"Gif comment length '{length}' exceeds max '{GifConstants.MaxCommentLength}'");
                }

                if (this.IgnoreMetadata)
                {
                    this.stream.Seek(length, SeekOrigin.Current);
                    continue;
                }

                using (IManagedByteBuffer commentsBuffer = this.MemoryAllocator.AllocateManagedByteBuffer(length))
                {
                    this.stream.Read(commentsBuffer.Array, 0, length);
                    string comments = this.TextEncoding.GetString(commentsBuffer.Array, 0, length);
                    this.metaData.Properties.Add(new ImageProperty(GifConstants.Comments, comments));
                }
            }
        }

        /// <summary>
        /// Reads an individual gif frame.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="image">The image to decode the information to.</param>
        /// <param name="previousFrame">The previous frame.</param>
        private void ReadFrame<TPixel>(ref Image<TPixel> image, ref ImageFrame<TPixel> previousFrame)
            where TPixel : struct, IPixel<TPixel>
        {
            this.ReadImageDescriptor();

            IManagedByteBuffer localColorTable = null;
            IManagedByteBuffer indices = null;
            try
            {
                // Determine the color table for this frame. If there is a local one, use it otherwise use the global color table.
                if (this.imageDescriptor.LocalColorTableFlag)
                {
                    int length = this.imageDescriptor.LocalColorTableSize * 3;
                    localColorTable = this.configuration.MemoryAllocator.AllocateManagedByteBuffer(length, AllocationOptions.Clean);
                    this.stream.Read(localColorTable.Array, 0, length);
                }

                indices = this.configuration.MemoryAllocator.AllocateManagedByteBuffer(this.imageDescriptor.Width * this.imageDescriptor.Height, AllocationOptions.Clean);

                this.ReadFrameIndices(this.imageDescriptor, indices.GetSpan());
                ReadOnlySpan<Rgb24> colorTable = MemoryMarshal.Cast<byte, Rgb24>((localColorTable ?? this.globalColorTable).GetSpan());
                this.ReadFrameColors(ref image, ref previousFrame, indices.GetSpan(), colorTable, this.imageDescriptor);

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
        private void ReadFrameIndices(in GifImageDescriptor imageDescriptor, Span<byte> indices)
        {
            int dataSize = this.stream.ReadByte();
            using (var lzwDecoder = new LzwDecoder(this.configuration.MemoryAllocator, this.stream))
            {
                lzwDecoder.DecodePixels(imageDescriptor.Width, imageDescriptor.Height, dataSize, indices);
            }
        }

        /// <summary>
        /// Reads the frames colors, mapping indices to colors.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="image">The image to decode the information to.</param>
        /// <param name="previousFrame">The previous frame.</param>
        /// <param name="indices">The indexed pixels.</param>
        /// <param name="colorTable">The color table containing the available colors.</param>
        /// <param name="descriptor">The <see cref="GifImageDescriptor"/></param>
        private void ReadFrameColors<TPixel>(ref Image<TPixel> image, ref ImageFrame<TPixel> previousFrame, Span<byte> indices, ReadOnlySpan<Rgb24> colorTable, in GifImageDescriptor descriptor)
            where TPixel : struct, IPixel<TPixel>
        {
            ref byte indicesRef = ref MemoryMarshal.GetReference(indices);
            int imageWidth = this.logicalScreenDescriptor.Width;
            int imageHeight = this.logicalScreenDescriptor.Height;

            ImageFrame<TPixel> prevFrame = null;
            ImageFrame<TPixel> currentFrame = null;
            ImageFrame<TPixel> imageFrame;

            if (previousFrame is null)
            {
                // This initializes the image to become fully transparent because the alpha channel is zero.
                image = new Image<TPixel>(this.configuration, imageWidth, imageHeight, this.metaData);

                this.SetFrameMetaData(image.Frames.RootFrame.MetaData);

                imageFrame = image.Frames.RootFrame;
            }
            else
            {
                if (this.graphicsControlExtension.DisposalMethod == GifDisposalMethod.RestoreToPrevious)
                {
                    prevFrame = previousFrame;
                }

                currentFrame = image.Frames.AddFrame(previousFrame); // This clones the frame and adds it the collection

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

                ref TPixel rowRef = ref MemoryMarshal.GetReference(imageFrame.GetPixelRowSpan(writeY));
                var rgba = new Rgba32(0, 0, 0, 255);

                // #403 The left + width value can be larger than the image width
                for (int x = descriptor.Left; x < descriptor.Left + descriptor.Width && x < imageWidth; x++)
                {
                    int index = Unsafe.Add(ref indicesRef, i);

                    if (!this.graphicsControlExtension.TransparencyFlag
                        || this.graphicsControlExtension.TransparencyIndex != index)
                    {
                        ref TPixel pixel = ref Unsafe.Add(ref rowRef, x);
                        rgba.Rgb = colorTable[index];
                        pixel.PackFromRgba32(rgba);
                    }

                    i++;
                }
            }

            if (prevFrame != null)
            {
                previousFrame = prevFrame;
                return;
            }

            previousFrame = currentFrame ?? image.Frames.RootFrame;

            if (this.graphicsControlExtension.DisposalMethod == GifDisposalMethod.RestoreToBackground)
            {
                this.restoreArea = new Rectangle(descriptor.Left, descriptor.Top, descriptor.Width, descriptor.Height);
            }
        }

        /// <summary>
        /// Restores the current frame area to the background.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="frame">The frame.</param>
        private void RestoreToBackground<TPixel>(ImageFrame<TPixel> frame)
            where TPixel : struct, IPixel<TPixel>
        {
            if (this.restoreArea is null)
            {
                return;
            }

            BufferArea<TPixel> pixelArea = frame.PixelBuffer.GetArea(this.restoreArea.Value);
            pixelArea.Clear();

            this.restoreArea = null;
        }

        /// <summary>
        /// Sets the frames metadata.
        /// </summary>
        /// <param name="meta">The meta data.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetFrameMetaData(ImageFrameMetaData meta)
        {
            GifFrameMetaData gifMeta = meta.GetFormatMetaData(GifFormat.Instance);
            if (this.graphicsControlExtension.DelayTime > 0)
            {
                gifMeta.FrameDelay = this.graphicsControlExtension.DelayTime;
            }

            // Frames can either use the global table or their own local table.
            if (this.logicalScreenDescriptor.GlobalColorTableFlag
                && this.logicalScreenDescriptor.GlobalColorTableSize > 0)
            {
                gifMeta.ColorTableLength = this.logicalScreenDescriptor.GlobalColorTableSize;
            }
            else if (this.imageDescriptor.LocalColorTableFlag
                && this.imageDescriptor.LocalColorTableSize > 0)
            {
                gifMeta.ColorTableLength = this.imageDescriptor.LocalColorTableSize;
            }

            gifMeta.DisposalMethod = this.graphicsControlExtension.DisposalMethod;
        }

        /// <summary>
        /// Reads the logical screen descriptor and global color table blocks
        /// </summary>
        /// <param name="stream">The stream containing image data. </param>
        private void ReadLogicalScreenDescriptorAndGlobalColorTable(Stream stream)
        {
            this.stream = stream;

            // Skip the identifier
            this.stream.Skip(6);
            this.ReadLogicalScreenDescriptor();

            var meta = new ImageMetaData();

            // The Pixel Aspect Ratio is defined to be the quotient of the pixel's
            // width over its height.  The value range in this field allows
            // specification of the widest pixel of 4:1 to the tallest pixel of
            // 1:4 in increments of 1/64th.
            //
            // Values :        0 -   No aspect ratio information is given.
            //            1..255 -   Value used in the computation.
            //
            // Aspect Ratio = (Pixel Aspect Ratio + 15) / 64
            if (this.logicalScreenDescriptor.PixelAspectRatio > 0)
            {
                meta.ResolutionUnits = PixelResolutionUnit.AspectRatio;
                float ratio = (this.logicalScreenDescriptor.PixelAspectRatio + 15) / 64F;

                if (ratio > 1)
                {
                    meta.HorizontalResolution = ratio;
                    meta.VerticalResolution = 1;
                }
                else
                {
                    meta.VerticalResolution = 1 / ratio;
                    meta.HorizontalResolution = 1;
                }
            }

            this.metaData = meta;
            this.gifMetaData = meta.GetFormatMetaData(GifFormat.Instance);
            this.gifMetaData.ColorTableMode = this.logicalScreenDescriptor.GlobalColorTableFlag
            ? GifColorTableMode.Global
            : GifColorTableMode.Local;

            if (this.logicalScreenDescriptor.GlobalColorTableFlag)
            {
                int globalColorTableLength = this.logicalScreenDescriptor.GlobalColorTableSize * 3;
                this.gifMetaData.GlobalColorTableLength = globalColorTableLength;

                this.globalColorTable = this.MemoryAllocator.AllocateManagedByteBuffer(globalColorTableLength, AllocationOptions.Clean);

                // Read the global color table data from the stream
                stream.Read(this.globalColorTable.Array, 0, globalColorTableLength);
            }
        }
    }
}