// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.OpenExr
{
    /// <summary>
    /// Image encoder for writing an image to a stream in the OpenExr format.
    /// </summary>
    internal sealed class ExrEncoderCore : IImageEncoderInternals
    {
        /// <summary>
        /// Reusable buffer.
        /// </summary>
        private readonly byte[] buffer = new byte[8];

        /// <summary>
        /// Used for allocating memory during processing operations.
        /// </summary>
        private readonly MemoryAllocator memoryAllocator;

        /// <summary>
        /// The global configuration.
        /// </summary>
        private Configuration configuration;

        /// <summary>
        /// The pixel type of the image.
        /// </summary>
        private ExrPixelType? pixelType;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExrEncoderCore"/> class.
        /// </summary>
        /// <param name="options">The encoder options.</param>
        /// <param name="memoryAllocator">The memory manager.</param>
        public ExrEncoderCore(IExrEncoderOptions options, MemoryAllocator memoryAllocator)
        {
            this.memoryAllocator = memoryAllocator;
            this.pixelType = options.PixelType;
        }

        /// <summary>
        /// Encodes the image to the specified stream from the <see cref="ImageFrame{TPixel}"/>.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="image">The <see cref="ImageFrame{TPixel}"/> to encode from.</param>
        /// <param name="stream">The <see cref="Stream"/> to encode the image data to.</param>
        /// <param name="cancellationToken">The token to request cancellation.</param>
        public void Encode<TPixel>(Image<TPixel> image, Stream stream, CancellationToken cancellationToken)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Guard.NotNull(image, nameof(image));
            Guard.NotNull(stream, nameof(stream));

            this.configuration = image.GetConfiguration();
            ImageMetadata metadata = image.Metadata;
            ExrMetadata exrMetadata = metadata.GetExrMetadata();
            this.pixelType ??= exrMetadata.PixelType;

            int width = image.Width;
            int height = image.Height;
            ExrPixelType pixelType = ExrPixelType.Float;
            var header = new ExrHeader()
            {
                Compression = ExrCompression.None,
                AspectRatio = 1.0f,
                DataWindow = new ExrBox2i(0, 0, width - 1, height - 1),
                DisplayWindow = new ExrBox2i(0, 0, width - 1, height - 1),
                LineOrder = ExrLineOrder.IncreasingY,
                ScreenWindowCenter = new PointF(0.0f, 0.0f),
                ScreenWindowWidth = 1,
                Channels = new List<ExrChannelInfo>()
                {
                    new("B", pixelType, 0, 1, 1),
                    new("G", pixelType, 0, 1, 1),
                    new("R", pixelType, 0, 1, 1),
                }
            };

            // Write magick bytes.
            BinaryPrimitives.WriteInt32LittleEndian(this.buffer, ExrConstants.MagickBytes);
            stream.Write(this.buffer.AsSpan(0, 4));

            // Version number.
            this.buffer[0] = 2;

            // Second, third and fourth bytes store info about the image, all set to zero.
            this.buffer[1] = 0;
            this.buffer[2] = 0;
            this.buffer[3] = 0;
            stream.Write(this.buffer.AsSpan(0, 4));

            // Write EXR header.
            this.WriteHeader(stream, header);

            // Write pixel data.
            Buffer2D<TPixel> pixels = image.Frames.RootFrame.PixelBuffer;
            using IMemoryOwner<float> rgbBuffer = this.memoryAllocator.Allocate<float>(width * 3);
            Span<float> redBuffer = rgbBuffer.GetSpan().Slice(0, width);
            Span<float> blueBuffer = rgbBuffer.GetSpan().Slice(width, width);
            Span<float> greenBuffer = rgbBuffer.GetSpan().Slice(width * 2, width);

            // Write offsets to each pixel row.
            uint rowSizeBytes = (uint)(width * 3 * 4);
            this.WriteRowOffsets(stream, height, rowSizeBytes);
            for (int y = 0; y < height; y++)
            {
                Span<TPixel> pixelRowSpan = pixels.DangerousGetRowSpan(y);

                for (int x = 0; x < width; x++)
                {
                    var vector4 = pixelRowSpan[x].ToVector4();
                    redBuffer[x] = vector4.X;
                    greenBuffer[x] = vector4.Y;
                    blueBuffer[x] = vector4.Z;
                }

                // Write row index.
                BinaryPrimitives.WriteUInt32LittleEndian(this.buffer, (uint)y);
                stream.Write(this.buffer.AsSpan(0, 4));

                // Write pixel row data size.
                BinaryPrimitives.WriteUInt32LittleEndian(this.buffer, rowSizeBytes);
                stream.Write(this.buffer.AsSpan(0, 4));

                for (int x = 0; x < width; x++)
                {
                    this.WriteSingle(stream, blueBuffer[x]);
                }

                for (int x = 0; x < width; x++)
                {
                    this.WriteSingle(stream, greenBuffer[x]);
                }

                for (int x = 0; x < width; x++)
                {
                    this.WriteSingle(stream, redBuffer[x]);
                }
            }
        }

        private void WriteHeader(Stream stream, ExrHeader header)
        {
            this.WriteChannels(stream, header.Channels);
            this.WriteCompression(stream, header.Compression.Value);
            this.WriteDataWindow(stream, header.DataWindow.Value);
            this.WriteDisplayWindow(stream, header.DisplayWindow.Value);
            this.WritePixelAspectRatio(stream, header.AspectRatio.Value);
            this.WriteLineOrder(stream, header.LineOrder.Value);
            this.WriteScreenWindowCenter(stream, header.ScreenWindowCenter.Value);
            this.WriteScreenWindowWidth(stream, header.ScreenWindowWidth.Value);
            stream.WriteByte(0);
        }

        private void WriteRowOffsets(Stream stream, int height, uint rowSizeBytes)
        {
            ulong startOfPixelData = (ulong)stream.Position + (8 * (ulong)height);
            ulong offset = startOfPixelData;
            for (int i = 0; i < height; i++)
            {
                BinaryPrimitives.WriteUInt64LittleEndian(this.buffer, offset);
                stream.Write(this.buffer);
                offset += 4 + 4 + rowSizeBytes;
            }
        }

        private void WriteChannels(Stream stream, IList<ExrChannelInfo> channels)
        {
            int attributeSize = 0;
            foreach (ExrChannelInfo channelInfo in channels)
            {
                attributeSize += channelInfo.ChannelName.Length + 1;
                attributeSize += 16;
            }

            // Last zero byte.
            attributeSize++;
            this.WriteAttributeInformation(stream, "channels", "chlist", attributeSize);

            foreach (ExrChannelInfo channelInfo in channels)
            {
                this.WriteChannelInfo(stream, channelInfo);
            }

            // Last byte should be zero.
            stream.WriteByte(0);
        }

        private void WriteCompression(Stream stream, ExrCompression compression)
        {
            this.WriteAttributeInformation(stream, "compression", "compression", 1);
            stream.WriteByte((byte)compression);
        }

        private void WritePixelAspectRatio(Stream stream, float aspectRatio)
        {
            this.WriteAttributeInformation(stream, "pixelAspectRatio", "float", 4);
            this.WriteSingle(stream, aspectRatio);
        }

        private void WriteLineOrder(Stream stream, ExrLineOrder lineOrder)
        {
            this.WriteAttributeInformation(stream, "lineOrder", "lineOrder", 1);
            stream.WriteByte((byte)lineOrder);
        }

        private void WriteScreenWindowCenter(Stream stream, PointF screenWindowCenter)
        {
            this.WriteAttributeInformation(stream, "screenWindowCenter", "v2f", 8);
            this.WriteSingle(stream, screenWindowCenter.X);
            this.WriteSingle(stream, screenWindowCenter.Y);
        }

        private void WriteScreenWindowWidth(Stream stream, float screenWindowWidth)
        {
            this.WriteAttributeInformation(stream, "screenWindowWidth", "float", 4);
            this.WriteSingle(stream, screenWindowWidth);
        }

        private void WriteDataWindow(Stream stream, ExrBox2i dataWindow)
        {
            this.WriteAttributeInformation(stream, "dataWindow", "box2i", 16);
            this.WriteBox2i(stream, dataWindow);
        }

        private void WriteDisplayWindow(Stream stream, ExrBox2i displayWindow)
        {
            this.WriteAttributeInformation(stream, "displayWindow", "box2i", 16);
            this.WriteBox2i(stream, displayWindow);
        }

        private void WriteAttributeInformation(Stream stream, string name, string type, int size)
        {
            // Write attribute name.
            this.WriteString(stream, name);

            // Write attribute type.
            this.WriteString(stream, type);

            // Write attribute size.
            BinaryPrimitives.WriteUInt32LittleEndian(this.buffer, (uint)size);
            stream.Write(this.buffer.AsSpan(0, 4));
        }

        private void WriteChannelInfo(Stream stream, ExrChannelInfo channelInfo)
        {
            this.WriteString(stream, channelInfo.ChannelName);

            BinaryPrimitives.WriteInt32LittleEndian(this.buffer, (int)channelInfo.PixelType);
            stream.Write(this.buffer.AsSpan(0, 4));

            stream.WriteByte(channelInfo.PLinear);

            // Next 3 bytes are reserved and will set to zero.
            stream.WriteByte(0);
            stream.WriteByte(0);
            stream.WriteByte(0);

            BinaryPrimitives.WriteInt32LittleEndian(this.buffer, channelInfo.XSampling);
            stream.Write(this.buffer.AsSpan(0, 4));

            BinaryPrimitives.WriteInt32LittleEndian(this.buffer, channelInfo.YSampling);
            stream.Write(this.buffer.AsSpan(0, 4));
        }

        private void WriteString(Stream stream, string str)
        {
            foreach (char c in str)
            {
                stream.WriteByte((byte)c);
            }

            // Write termination byte.
            stream.WriteByte(0);
        }

        private void WriteBox2i(Stream stream, ExrBox2i box)
        {
            BinaryPrimitives.WriteInt32LittleEndian(this.buffer, box.XMin);
            stream.Write(this.buffer.AsSpan(0, 4));

            BinaryPrimitives.WriteInt32LittleEndian(this.buffer, box.YMin);
            stream.Write(this.buffer.AsSpan(0, 4));

            BinaryPrimitives.WriteInt32LittleEndian(this.buffer, box.XMax);
            stream.Write(this.buffer.AsSpan(0, 4));

            BinaryPrimitives.WriteInt32LittleEndian(this.buffer, box.YMax);
            stream.Write(this.buffer.AsSpan(0, 4));
        }

        private unsafe void WriteSingle(Stream stream, float value)
        {
            BinaryPrimitives.WriteInt32LittleEndian(this.buffer, *(int*)&value);
            stream.Write(this.buffer.AsSpan(0, 4));
        }
    }
}
