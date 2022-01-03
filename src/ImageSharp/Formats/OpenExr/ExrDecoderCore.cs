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

            if (this.Compression is not ExrCompression.None)
            {
                ExrThrowHelper.ThrowNotSupported($"Compression {this.Compression} is not yet supported");
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
                case ExrPixelType.UnsignedInt:
                    this.DecodeUnsignedIntPixelData(stream, pixels);
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
            using IMemoryOwner<float> rowBuffer = this.memoryAllocator.Allocate<float>(this.Width * 4);
            Span<float> redPixelData = rowBuffer.GetSpan().Slice(0, this.Width);
            Span<float> greenPixelData = rowBuffer.GetSpan().Slice(this.Width, this.Width);
            Span<float> bluePixelData = rowBuffer.GetSpan().Slice(this.Width * 2, this.Width);
            Span<float> alphaPixelData = rowBuffer.GetSpan().Slice(this.Width * 3, this.Width);

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

                bool hasAlpha = false;
                for (int channelIdx = 0; channelIdx < this.Channels.Count; channelIdx++)
                {
                    ExrChannelInfo channel = this.Channels[channelIdx];
                    switch (channel.ChannelName)
                    {
                        case ExrConstants.ChannelNames.Red:
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

                        case ExrConstants.ChannelNames.Blue:
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

                        case ExrConstants.ChannelNames.Green:
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

                        case ExrConstants.ChannelNames.Alpha:
                            hasAlpha = true;
                            switch (channel.PixelType)
                            {
                                case ExrPixelType.Half:
                                    this.ReadPixelRowChannelHalfSingle(stream, alphaPixelData);
                                    break;
                                case ExrPixelType.Float:
                                    this.ReadPixelRowChannelSingle(stream, alphaPixelData);
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

                if (hasAlpha)
                {
                    for (int x = 0; x < this.Width; x++)
                    {
                        var pixelValue = new HalfVector4(redPixelData[x], greenPixelData[x], bluePixelData[x], alphaPixelData[x]);
                        color.FromVector4(pixelValue.ToVector4());
                        pixelRow[x] = color;
                    }
                }
                else
                {
                    for (int x = 0; x < this.Width; x++)
                    {
                        var pixelValue = new HalfVector4(redPixelData[x], greenPixelData[x], bluePixelData[x], 1.0f);
                        color.FromVector4(pixelValue.ToVector4());
                        pixelRow[x] = color;
                    }
                }
            }
        }

        private void DecodeUnsignedIntPixelData<TPixel>(BufferedReadStream stream, Buffer2D<TPixel> pixels)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using IMemoryOwner<uint> rowBuffer = this.memoryAllocator.Allocate<uint>(this.Width * 4);
            Span<uint> redPixelData = rowBuffer.GetSpan().Slice(0, this.Width);
            Span<uint> greenPixelData = rowBuffer.GetSpan().Slice(this.Width, this.Width);
            Span<uint> bluePixelData = rowBuffer.GetSpan().Slice(this.Width * 2, this.Width);
            Span<uint> alphaPixelData = rowBuffer.GetSpan().Slice(this.Width * 3, this.Width);

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

                bool hasAlpha = false;
                for (int channelIdx = 0; channelIdx < this.Channels.Count; channelIdx++)
                {
                    ExrChannelInfo channel = this.Channels[channelIdx];
                    switch (channel.ChannelName)
                    {
                        case ExrConstants.ChannelNames.Red:
                            switch (channel.PixelType)
                            {
                                case ExrPixelType.UnsignedInt:
                                    this.ReadPixelRowChannelUnsignedInt(stream, redPixelData);
                                    break;
                            }

                            break;

                        case ExrConstants.ChannelNames.Blue:
                            switch (channel.PixelType)
                            {
                                case ExrPixelType.UnsignedInt:
                                    this.ReadPixelRowChannelUnsignedInt(stream, bluePixelData);
                                    break;
                            }

                            break;

                        case ExrConstants.ChannelNames.Green:
                            switch (channel.PixelType)
                            {
                                case ExrPixelType.UnsignedInt:
                                    this.ReadPixelRowChannelUnsignedInt(stream, greenPixelData);
                                    break;
                            }

                            break;

                        case ExrConstants.ChannelNames.Alpha:
                            hasAlpha = true;
                            switch (channel.PixelType)
                            {
                                case ExrPixelType.UnsignedInt:
                                    this.ReadPixelRowChannelUnsignedInt(stream, alphaPixelData);
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

                if (hasAlpha)
                {
                    for (int x = 0; x < this.Width; x++)
                    {
                        var pixelValue = new Rgba128(redPixelData[x], greenPixelData[x], bluePixelData[x], alphaPixelData[x]);
                        color.FromVector4(pixelValue.ToVector4());
                        pixelRow[x] = color;
                    }
                }
                else
                {
                    for (int x = 0; x < this.Width; x++)
                    {
                        var pixelValue = new Rgb96(redPixelData[x], greenPixelData[x], bluePixelData[x]);
                        color.FromVector4(pixelValue.ToVector4());
                        pixelRow[x] = color;
                    }
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
                ExrThrowHelper.ThrowInvalidImageContentException("At least one channel of pixel data is expected!");
            }

            // Find pixel the type of any channel which is R, G, B or A.
            ExrPixelType? pixelType = null;
            for (int i = 0; i < this.Channels.Count; i++)
            {
                if (this.Channels[i].ChannelName.Equals(ExrConstants.ChannelNames.Blue) ||
                    this.Channels[i].ChannelName.Equals(ExrConstants.ChannelNames.Green) ||
                    this.Channels[i].ChannelName.Equals(ExrConstants.ChannelNames.Red) ||
                    this.Channels[i].ChannelName.Equals(ExrConstants.ChannelNames.Alpha))
                {
                    pixelType = this.Channels[i].PixelType;
                    break;
                }
            }

            if (!pixelType.HasValue)
            {
                ExrThrowHelper.ThrowInvalidImageContentException("Pixel channel data is unknown! Only R, G, B and A are supported.");
            }

            for (int i = 0; i < this.Channels.Count; i++)
            {
                // Ignore channels which we cannot interpret.
                if (!(this.Channels[i].ChannelName.Equals(ExrConstants.ChannelNames.Blue) ||
                      this.Channels[i].ChannelName.Equals(ExrConstants.ChannelNames.Green) ||
                      this.Channels[i].ChannelName.Equals(ExrConstants.ChannelNames.Red)))
                {
                    continue;
                }

                if (pixelType != this.Channels[i].PixelType)
                {
                    ExrThrowHelper.ThrowInvalidImageContentException("All channels are expected to have the same pixel data!");
                }
            }

            return pixelType.Value;
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
            // TODO: We ignore those for now.
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
                    case ExrConstants.AttributeNames.Channels:
                        IList<ExrChannelInfo> channels = this.ReadChannelList(stream, attribute.Length);
                        header.Channels = channels;
                        break;
                    case ExrConstants.AttributeNames.Compression:
                        header.Compression = (ExrCompression)stream.ReadByte();
                        break;
                    case ExrConstants.AttributeNames.DataWindow:
                        ExrBox2i dataWindow = this.ReadBoxInteger(stream);
                        header.DataWindow = dataWindow;
                        break;
                    case ExrConstants.AttributeNames.DisplayWindow:
                        ExrBox2i displayWindow = this.ReadBoxInteger(stream);
                        header.DisplayWindow = displayWindow;
                        break;
                    case ExrConstants.AttributeNames.LineOrder:
                        var lineOrder = (ExrLineOrder)stream.ReadByte();
                        header.LineOrder = lineOrder;
                        break;
                    case ExrConstants.AttributeNames.PixelAspectRatio:
                        float aspectRatio = stream.ReadSingle(this.buffer);
                        header.AspectRatio = aspectRatio;
                        break;
                    case ExrConstants.AttributeNames.ScreenWindowCenter:
                        float screenWindowCenterX = stream.ReadSingle(this.buffer);
                        float screenWindowCenterY = stream.ReadSingle(this.buffer);
                        header.ScreenWindowCenter = new PointF(screenWindowCenterX, screenWindowCenterY);
                        break;
                    case ExrConstants.AttributeNames.ScreenWindowWidth:
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

        private ExrBox2i ReadBoxInteger(BufferedReadStream stream)
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
