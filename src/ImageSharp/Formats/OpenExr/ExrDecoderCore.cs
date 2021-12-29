// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using SixLabors.ImageSharp.Formats.Pbm;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.OpenExr
{
    /// <summary>
    /// Performs the OpenExr decoding operation.
    /// </summary>
    internal sealed class ExrDecoderCore : IImageDecoderInternals
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
        /// The bitmap decoder options.
        /// </summary>
        private readonly IExrDecoderOptions options;

        /// <summary>
        /// The metadata.
        /// </summary>
        private ImageMetadata metadata;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExrDecoderCore"/> class.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        /// <param name="options">The options.</param>
        public ExrDecoderCore(Configuration configuration, IExrDecoderOptions options)
        {
            this.Configuration = configuration;
            this.memoryAllocator = configuration.MemoryAllocator;
            this.options = options;
        }

        /// <inheritdoc />
        public Configuration Configuration { get; }

        /// <summary>
        /// Gets the dimensions of the image.
        /// </summary>
        public Size Dimensions => new(this.Width, this.Height);

        private int Width { get; set; }

        private int Height { get; set; }

        private IList<ExrChannelInfo> Channels { get; set; }

        private ExrCompression Compression { get; set; }

        /// <inheritdoc />
        public Image<TPixel> Decode<TPixel>(BufferedReadStream stream, CancellationToken cancellationToken)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            this.ReadExrHeader(stream);

            if (this.Compression != ExrCompression.None)
            {
                ExrThrowHelper.ThrowNotSupported("Only uncompressed EXR images are supported");
            }

            ExrPixelType pixelType = this.ValidateChannels();

            var image = new Image<TPixel>(this.Configuration, this.Width, this.Height, this.metadata);
            Buffer2D<TPixel> pixels = image.GetRootFramePixelBuffer();

            switch (pixelType)
            {
                case ExrPixelType.Half:
                case ExrPixelType.Float:
                    this.DecodeFloatingPointPixelData(stream, pixels);
                    break;
                default:
                    ExrThrowHelper.ThrowNotSupported("Pixel type is not supported");
                    break;
            }

            return image;
        }

        private void DecodeFloatingPointPixelData<TPixel>(BufferedReadStream stream, Buffer2D<TPixel> pixels)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using IMemoryOwner<float> rowBuffer = this.memoryAllocator.Allocate<float>(this.Width * 3);
            Span<float> redPixelData = rowBuffer.GetSpan().Slice(0, this.Width);
            Span<float> bluePixelData = rowBuffer.GetSpan().Slice(this.Width, this.Width);
            Span<float> greenPixelData = rowBuffer.GetSpan().Slice(this.Width * 2, this.Width);

            TPixel color = default;
            for (int y = 0; y < this.Height; y++)
            {
                stream.Read(this.buffer, 0, 8);
                ulong rowOffset = BinaryPrimitives.ReadUInt64LittleEndian(this.buffer);
                long nextRowOffsetPosition = stream.Position;

                stream.Position = (long)rowOffset;
                stream.Read(this.buffer, 0, 4);
                uint rowIndex = BinaryPrimitives.ReadUInt32LittleEndian(this.buffer);

                Span<TPixel> pixelRow = pixels.DangerousGetRowSpan((int)rowIndex);

                stream.Read(this.buffer, 0, 4);
                uint pixelDataSize = BinaryPrimitives.ReadUInt32LittleEndian(this.buffer);

                for (int channelIdx = 0; channelIdx < this.Channels.Count; channelIdx++)
                {
                    ExrChannelInfo channel = this.Channels[channelIdx];
                    switch (channel.ChannelName)
                    {
                        case "R":
                            switch (channel.PixelType)
                            {
                                case ExrPixelType.Half:
                                    this.ReadPixelRowChannelHalfSingle(stream, redPixelData);
                                    break;
                                case ExrPixelType.Float:
                                    this.ReadPixelRowChannelSingle(stream, redPixelData);
                                    break;
                            }

                            break;

                        case "B":
                            switch (channel.PixelType)
                            {
                                case ExrPixelType.Half:
                                    this.ReadPixelRowChannelHalfSingle(stream, bluePixelData);
                                    break;
                                case ExrPixelType.Float:
                                    this.ReadPixelRowChannelSingle(stream, bluePixelData);
                                    break;
                            }

                            break;

                        case "G":
                            switch (channel.PixelType)
                            {
                                case ExrPixelType.Half:
                                    this.ReadPixelRowChannelHalfSingle(stream, greenPixelData);
                                    break;
                                case ExrPixelType.Float:
                                    this.ReadPixelRowChannelSingle(stream, greenPixelData);
                                    break;
                            }

                            break;

                        default:
                            // Skip unknown channel.
                            int channelDataSizeInBytes = channel.PixelType is ExrPixelType.Float or ExrPixelType.UnsignedInt ? 4 : 2;
                            stream.Position += this.Width * channelDataSizeInBytes;
                            break;
                    }
                }

                stream.Position = nextRowOffsetPosition;

                for (int x = 0; x < this.Width; x++)
                {
                    var pixelValue = new HalfVector4(redPixelData[x], greenPixelData[x], bluePixelData[x], 1.0f);
                    color.FromVector4(pixelValue.ToVector4());
                    pixelRow[x] = color;
                }
            }
        }

        private void DecodeUnsignedIntPixelData<TPixel>(BufferedReadStream stream, Buffer2D<TPixel> pixels)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using IMemoryOwner<uint> rowBuffer = this.memoryAllocator.Allocate<uint>(this.Width * 3);
            Span<uint> redPixelData = rowBuffer.GetSpan().Slice(0, this.Width);
            Span<uint> bluePixelData = rowBuffer.GetSpan().Slice(this.Width, this.Width);
            Span<uint> greenPixelData = rowBuffer.GetSpan().Slice(this.Width * 2, this.Width);

            TPixel color = default;
            for (int y = 0; y < this.Height; y++)
            {
                stream.Read(this.buffer, 0, 8);
                ulong rowOffset = BinaryPrimitives.ReadUInt64LittleEndian(this.buffer);
                long nextRowOffsetPosition = stream.Position;

                stream.Position = (long)rowOffset;
                stream.Read(this.buffer, 0, 4);
                uint rowIndex = BinaryPrimitives.ReadUInt32LittleEndian(this.buffer);

                Span<TPixel> pixelRow = pixels.DangerousGetRowSpan((int)rowIndex);

                stream.Read(this.buffer, 0, 4);
                uint pixelDataSize = BinaryPrimitives.ReadUInt32LittleEndian(this.buffer);

                for (int channelIdx = 0; channelIdx < this.Channels.Count; channelIdx++)
                {
                    ExrChannelInfo channel = this.Channels[channelIdx];
                    switch (channel.ChannelName)
                    {
                        case "R":
                            switch (channel.PixelType)
                            {
                                case ExrPixelType.UnsignedInt:
                                    this.ReadPixelRowChannelUnsignedInt(stream, redPixelData);
                                    break;
                            }

                            break;

                        case "B":
                            switch (channel.PixelType)
                            {
                                case ExrPixelType.UnsignedInt:
                                    this.ReadPixelRowChannelUnsignedInt(stream, bluePixelData);
                                    break;
                            }

                            break;

                        case "G":
                            switch (channel.PixelType)
                            {
                                case ExrPixelType.UnsignedInt:
                                    this.ReadPixelRowChannelUnsignedInt(stream, greenPixelData);
                                    break;
                            }

                            break;

                        default:
                            // Skip unknown channel.
                            int channelDataSizeInBytes = channel.PixelType is ExrPixelType.Float or ExrPixelType.UnsignedInt ? 4 : 2;
                            stream.Position += this.Width * channelDataSizeInBytes;
                            break;
                    }
                }

                stream.Position = nextRowOffsetPosition;

                for (int x = 0; x < this.Width; x++)
                {
                    var pixelValue = new Rgb96(redPixelData[x], greenPixelData[x], bluePixelData[x]);
                    color.FromVector4(pixelValue.ToVector4());
                    pixelRow[x] = color;
                }
            }
        }

        private void ReadPixelRowChannelHalfSingle(BufferedReadStream stream, Span<float> channelData)
        {
            for (int x = 0; x < this.Width; x++)
            {
                channelData[x] = stream.ReadHalfSingle(this.buffer);
            }
        }

        private void ReadPixelRowChannelSingle(BufferedReadStream stream, Span<float> channelData)
        {
            for (int x = 0; x < this.Width; x++)
            {
                channelData[x] = stream.ReadSingle(this.buffer);
            }
        }

        private void ReadPixelRowChannelUnsignedInt(BufferedReadStream stream, Span<uint> channelData)
        {
            for (int x = 0; x < this.Width; x++)
            {
                stream.Read(this.buffer, 0, 4);
                channelData[x] = BinaryPrimitives.ReadUInt32LittleEndian(this.buffer);
            }
        }

        /// <inheritdoc />
        public IImageInfo Identify(BufferedReadStream stream, CancellationToken cancellationToken)
        {
            ExrHeader header = this.ReadExrHeader(stream);

            int bitsPerPixel = this.CalculateBitsPerPixel();

            return new ImageInfo(new PixelTypeInfo(bitsPerPixel), this.Width, this.Height, new ImageMetadata());
        }

        private int CalculateBitsPerPixel()
        {
            int bitsPerPixel = 0;
            for (int i = 0; i < this.Channels.Count; i++)
            {
                ExrChannelInfo channel = this.Channels[0];
                if (channel.PixelType is ExrPixelType.Float or ExrPixelType.UnsignedInt)
                {
                    bitsPerPixel += 32;
                }
                else if (channel.PixelType == ExrPixelType.Half)
                {
                    bitsPerPixel += 16;
                }
            }

            return bitsPerPixel;
        }

        private ExrPixelType ValidateChannels()
        {
            if (this.Channels.Count == 0)
            {
                ExrThrowHelper.ThrowInvalidImageContentException("At least one channel of pixel data expected!");
            }

            ExrPixelType pixelType = this.Channels[0].PixelType;
            for (int i = 1; i < this.Channels.Count; i++)
            {
                if (pixelType != this.Channels[i].PixelType)
                {
                    ExrThrowHelper.ThrowInvalidImageContentException("All channels are expected to have the same pixel data!");
                }
            }

            return pixelType;
        }

        private ExrHeader ReadExrHeader(BufferedReadStream stream)
        {
            // Skip over the magick bytes.
            stream.Read(this.buffer, 0, 4);

            // Read version number.
            byte version = (byte)stream.ReadByte();
            if (version != 2)
            {
                ExrThrowHelper.ThrowNotSupportedVersion();
            }

            // Next three bytes contain info's about the image.
            // We ignore those for now.
            stream.Read(this.buffer, 0, 3);

            ExrHeader header = this.ParseHeader(stream);

            if (!header.IsValid())
            {
                ExrThrowHelper.ThrowInvalidImageHeader();
            }

            this.Width = header.DataWindow.Value.XMax - header.DataWindow.Value.XMin + 1;
            this.Height = header.DataWindow.Value.YMax - header.DataWindow.Value.YMin + 1;
            this.Channels = header.Channels;
            this.Compression = header.Compression.GetValueOrDefault();

            this.metadata = new ImageMetadata();

            return header;
        }

        private ExrHeader ParseHeader(BufferedReadStream stream)
        {
            ExrAttribute attribute = this.ReadAttribute(stream);
            var header = new ExrHeader();

            while (!attribute.Equals(ExrAttribute.EmptyAttribute))
            {
                switch (attribute.Name)
                {
                    case "channels":
                        IList<ExrChannelInfo> channels = this.ReadChannelList(stream, attribute.Length);
                        header.Channels = channels;
                        break;
                    case "compression":
                        header.Compression = (ExrCompression)stream.ReadByte();
                        break;
                    case "dataWindow":
                        ExrBox2i dataWindow = this.ReadBox2i(stream);
                        header.DataWindow = dataWindow;
                        break;
                    case "displayWindow":
                        ExrBox2i displayWindow = this.ReadBox2i(stream);
                        header.DisplayWindow = displayWindow;
                        break;
                    case "lineOrder":
                        var lineOrder = (ExrLineOrder)stream.ReadByte();
                        header.LineOrder = lineOrder;
                        break;
                    case "pixelAspectRatio":
                        float aspectRatio = stream.ReadSingle(this.buffer);
                        header.AspectRatio = aspectRatio;
                        break;
                    case "screenWindowCenter":
                        float screenWindowCenterX = stream.ReadSingle(this.buffer);
                        float screenWindowCenterY = stream.ReadSingle(this.buffer);
                        header.ScreenWindowCenter = new PointF(screenWindowCenterX, screenWindowCenterY);
                        break;
                    case "screenWindowWidth":
                        float screenWindowWidth = stream.ReadSingle(this.buffer);
                        header.ScreenWindowWidth = screenWindowWidth;
                        break;
                    default:
                        // Skip unknown attribute bytes.
                        stream.Skip(attribute.Length);
                        break;
                }

                attribute = this.ReadAttribute(stream);
            }

            return header;
        }

        private ExrAttribute ReadAttribute(BufferedReadStream stream)
        {
            string attributeName = ReadString(stream);
            if (attributeName.Equals(string.Empty))
            {
                return ExrAttribute.EmptyAttribute;
            }

            string attributeType = ReadString(stream);

            stream.Read(this.buffer, 0, 4);
            int attributeSize = BinaryPrimitives.ReadInt32LittleEndian(this.buffer);

            return new ExrAttribute(attributeName, attributeType, attributeSize);
        }

        private ExrBox2i ReadBox2i(BufferedReadStream stream)
        {
            stream.Read(this.buffer, 0, 4);
            int xMin = BinaryPrimitives.ReadInt32LittleEndian(this.buffer);

            stream.Read(this.buffer, 0, 4);
            int yMin = BinaryPrimitives.ReadInt32LittleEndian(this.buffer);

            stream.Read(this.buffer, 0, 4);
            int xMax = BinaryPrimitives.ReadInt32LittleEndian(this.buffer);

            stream.Read(this.buffer, 0, 4);
            int yMax = BinaryPrimitives.ReadInt32LittleEndian(this.buffer);

            return new ExrBox2i(xMin, yMin, xMax, yMax);
        }

        private List<ExrChannelInfo> ReadChannelList(BufferedReadStream stream, int attributeSize)
        {
            var channels = new List<ExrChannelInfo>();
            while (attributeSize > 1)
            {
                ExrChannelInfo channelInfo = this.ReadChannelInfo(stream, out int bytesRead);
                channels.Add(channelInfo);
                attributeSize -= bytesRead;
            }

            // Last byte should be a null byte.
            int byteRead = stream.ReadByte();

            return channels;
        }

        private ExrChannelInfo ReadChannelInfo(BufferedReadStream stream, out int bytesRead)
        {
            string channelName = ReadString(stream);
            bytesRead = channelName.Length + 1;

            stream.Read(this.buffer, 0, 4);
            bytesRead += 4;
            var pixelType = (ExrPixelType)BinaryPrimitives.ReadInt32LittleEndian(this.buffer);

            byte pLinear = (byte)stream.ReadByte();
            byte reserved0 = (byte)stream.ReadByte();
            byte reserved1 = (byte)stream.ReadByte();
            byte reserved2 = (byte)stream.ReadByte();
            bytesRead += 4;

            stream.Read(this.buffer, 0, 4);
            bytesRead += 4;
            int xSampling = BinaryPrimitives.ReadInt32LittleEndian(this.buffer);

            stream.Read(this.buffer, 0, 4);
            bytesRead += 4;
            int ySampling = BinaryPrimitives.ReadInt32LittleEndian(this.buffer);

            return new ExrChannelInfo(channelName, pixelType, pLinear, xSampling, ySampling);
        }

        private static string ReadString(BufferedReadStream stream)
        {
            var str = new StringBuilder();
            int character = stream.ReadByte();
            if (character == 0)
            {
                // End of file header reached.
                return string.Empty;
            }

            while (character != 0)
            {
                if (character == -1)
                {
                    ExrThrowHelper.ThrowInvalidImageHeader();
                }

                str.Append((char)character);
                character = stream.ReadByte();
            }

            return str.ToString();
        }
    }
}
