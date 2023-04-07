// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using SixLabors.ImageSharp.Formats.OpenExr.Compression;
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

        private ExrCompressionType Compression { get; set; }

        private ExrImageDataType ImageDataType { get; set; }

        private ExrImageType ImageType { get; set; }

        private ExrHeaderAttributes HeaderAttributes { get; set; }

        /// <inheritdoc />
        public Image<TPixel> Decode<TPixel>(BufferedReadStream stream, CancellationToken cancellationToken)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            this.ReadExrHeader(stream);
            if (!this.IsSupportedCompression())
            {
                ExrThrowHelper.ThrowNotSupported($"Compression {this.Compression} is not yet supported");
            }

            ExrPixelType pixelType = this.ValidateChannels();
            this.ReadImageDataType();

            var image = new Image<TPixel>(this.Configuration, this.Width, this.Height, this.metadata);
            Buffer2D<TPixel> pixels = image.GetRootFramePixelBuffer();

            switch (pixelType)
            {
                case ExrPixelType.Half:
                case ExrPixelType.Float:
                    if (this.ImageType is ExrImageType.ScanLine)
                    {
                        this.DecodeFloatingPointPixelData(stream, pixels);
                    }
                    else
                    {
                        this.DecodeTiledFloatingPointPixelData(stream, pixels);
                    }

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
            bool hasAlpha = this.HasAlpha();
            uint bytesPerRow = this.CalculateBytesPerRow((uint)this.Width);
            uint rowsPerBlock = this.RowsPerBlock();
            uint bytesPerBlock = bytesPerRow * rowsPerBlock;
            int width = this.Width;
            int height = this.Height;

            using IMemoryOwner<float> rowBuffer = this.memoryAllocator.Allocate<float>(width * 4);
            using IMemoryOwner<byte> decompressedPixelDataBuffer = this.memoryAllocator.Allocate<byte>((int)bytesPerBlock);
            Span<byte> decompressedPixelData = decompressedPixelDataBuffer.GetSpan();
            Span<float> redPixelData = rowBuffer.GetSpan().Slice(0, width);
            Span<float> greenPixelData = rowBuffer.GetSpan().Slice(width, width);
            Span<float> bluePixelData = rowBuffer.GetSpan().Slice(width * 2, width);
            Span<float> alphaPixelData = rowBuffer.GetSpan().Slice(width * 3, width);

            using ExrBaseDecompressor decompressor = ExrDecompressorFactory.Create(this.Compression, this.memoryAllocator, bytesPerBlock);

            TPixel color = default;
            for (uint y = 0; y < height; y += rowsPerBlock)
            {
                ulong rowOffset = this.ReadUnsignedLong(stream);
                long nextRowOffsetPosition = stream.Position;

                stream.Position = (long)rowOffset;
                uint rowStartIndex = this.ReadUnsignedInteger(stream);

                uint compressedBytesCount = this.ReadUnsignedInteger(stream);
                decompressor.Decompress(stream, compressedBytesCount, decompressedPixelData);

                int offset = 0;
                for (uint rowIndex = rowStartIndex; rowIndex < rowStartIndex + rowsPerBlock && rowIndex < height; rowIndex++)
                {
                    Span<TPixel> pixelRow = pixels.DangerousGetRowSpan((int)rowIndex);
                    for (int channelIdx = 0; channelIdx < this.Channels.Count; channelIdx++)
                    {
                        ExrChannelInfo channel = this.Channels[channelIdx];
                        offset += this.ReadFloatChannelData(stream, channel, decompressedPixelData.Slice(offset), redPixelData, greenPixelData, bluePixelData, alphaPixelData, width);
                    }

                    for (int x = 0; x < width; x++)
                    {
                        var pixelValue = new HalfVector4(redPixelData[x], greenPixelData[x], bluePixelData[x], hasAlpha ? alphaPixelData[x] : 1.0f);
                        color.FromVector4(pixelValue.ToVector4());
                        pixelRow[x] = color;
                    }

                }

                stream.Position = nextRowOffsetPosition;
            }
        }

        private void DecodeUnsignedIntPixelData<TPixel>(BufferedReadStream stream, Buffer2D<TPixel> pixels)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            bool hasAlpha = this.HasAlpha();
            uint bytesPerRow = this.CalculateBytesPerRow((uint)this.Width);
            uint rowsPerBlock = this.RowsPerBlock();
            uint bytesPerBlock = bytesPerRow * rowsPerBlock;
            int width = this.Width;
            int height = this.Height;

            using IMemoryOwner<uint> rowBuffer = this.memoryAllocator.Allocate<uint>(width * 4);
            using IMemoryOwner<byte> decompressedPixelDataBuffer = this.memoryAllocator.Allocate<byte>((int)bytesPerBlock);
            Span<byte> decompressedPixelData = decompressedPixelDataBuffer.GetSpan();
            Span<uint> redPixelData = rowBuffer.GetSpan().Slice(0, width);
            Span<uint> greenPixelData = rowBuffer.GetSpan().Slice(width, width);
            Span<uint> bluePixelData = rowBuffer.GetSpan().Slice(width * 2, width);
            Span<uint> alphaPixelData = rowBuffer.GetSpan().Slice(width * 3, width);

            using ExrBaseDecompressor decompressor = ExrDecompressorFactory.Create(this.Compression, this.memoryAllocator, bytesPerBlock);

            TPixel color = default;
            for (uint y = 0; y < height; y += rowsPerBlock)
            {
                ulong rowOffset = this.ReadUnsignedLong(stream);
                long nextRowOffsetPosition = stream.Position;

                stream.Position = (long)rowOffset;
                uint rowStartIndex = this.ReadUnsignedInteger(stream);

                uint compressedBytesCount = this.ReadUnsignedInteger(stream);
                decompressor.Decompress(stream, compressedBytesCount, decompressedPixelData);

                int offset = 0;
                for (uint rowIndex = rowStartIndex; rowIndex < rowStartIndex + rowsPerBlock && rowIndex < height; rowIndex++)
                {
                    Span<TPixel> pixelRow = pixels.DangerousGetRowSpan((int)rowIndex);
                    for (int channelIdx = 0; channelIdx < this.Channels.Count; channelIdx++)
                    {
                        ExrChannelInfo channel = this.Channels[channelIdx];
                        offset += this.ReadUnsignedIntChannelData(stream, channel, decompressedPixelData, redPixelData, greenPixelData, bluePixelData, alphaPixelData, width);
                    }

                    stream.Position = nextRowOffsetPosition;

                    for (int x = 0; x < width; x++)
                    {
                        var pixelValue = new Rgba128(redPixelData[x], greenPixelData[x], bluePixelData[x], hasAlpha ? alphaPixelData[x] : uint.MaxValue);
                        color.FromVector4(pixelValue.ToVector4());
                        pixelRow[x] = color;
                    }
                }
            }
        }

        private void DecodeTiledFloatingPointPixelData<TPixel>(BufferedReadStream stream, Buffer2D<TPixel> pixels)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            if (!this.HeaderAttributes.ChunkCount.HasValue)
            {
                ExrThrowHelper.ThrowInvalidImageContentException("Missing chunk count in tiled image");
            }

            int chunks = this.HeaderAttributes.ChunkCount.Value;

            bool hasAlpha = this.HasAlpha();
            uint tileWidth = (uint)this.HeaderAttributes.TileXSize;
            uint tileHeight = (uint)this.HeaderAttributes.TileYSize;
            int width = this.Width;
            int height = this.Height;
            uint bytesPerRow = this.CalculateBytesPerRow((uint)this.Width);
            uint bytesPerBlockRow = this.CalculateBytesPerRow(tileWidth);
            uint rowsPerBlock = this.RowsPerBlock();
            uint bytesPerBlock = bytesPerBlockRow * rowsPerBlock;
            uint columnsPerBlock = (uint)(this.Height / tileHeight);
            uint tilesPerScanline = (uint)((this.Width + tileWidth - 1) / tileWidth);

            using IMemoryOwner<float> rowBuffer = this.memoryAllocator.Allocate<float>((int)(tileWidth * 4));
            using IMemoryOwner<byte> decompressedPixelDataBuffer = this.memoryAllocator.Allocate<byte>((width * height) / chunks);
            Span<byte> decompressedPixelData = decompressedPixelDataBuffer.GetSpan();
            Span<float> redPixelData = rowBuffer.GetSpan().Slice(0, (int)tileWidth);
            Span<float> greenPixelData = rowBuffer.GetSpan().Slice((int)tileWidth, (int)tileWidth);
            Span<float> bluePixelData = rowBuffer.GetSpan().Slice((int)(tileWidth * 2), (int)tileWidth);
            Span<float> alphaPixelData = rowBuffer.GetSpan().Slice((int)(tileWidth * 3), (int)tileWidth);

            using ExrBaseDecompressor decompressor = ExrDecompressorFactory.Create(this.Compression, this.memoryAllocator, bytesPerBlock);

            uint y = 0;
            uint x = 0;
            TPixel color = default;
            for (int chunk = 0; chunk < chunks; chunk++)
            {
                ulong dataOffset = this.ReadUnsignedLong(stream);
                long nextOffsetPosition = stream.Position;
                stream.Position = (long)dataOffset;
                uint tileX = this.ReadUnsignedInteger(stream);
                uint tileY = this.ReadUnsignedInteger(stream);
                uint levelX = this.ReadUnsignedInteger(stream);
                uint levelY = this.ReadUnsignedInteger(stream);

                uint compressedBytesCount = this.ReadUnsignedInteger(stream);
                decompressor.Decompress(stream, compressedBytesCount, decompressedPixelData);

                int offset = 0;
                for (y = 0; y < height; y += rowsPerBlock)
                {
                    for (x = 0; x < tilesPerScanline; x++)
                    {
                        uint rowStartIndex = tileHeight * tileY;
                        uint columnStartIndex = tileWidth * tileX;
                        for (uint rowIndex = rowStartIndex; rowIndex < rowStartIndex + rowsPerBlock && rowIndex < height; rowIndex++)
                        {
                            Span<TPixel> pixelRow = pixels.DangerousGetRowSpan((int)rowIndex);
                            for (int channelIdx = 0; channelIdx < this.Channels.Count; channelIdx++)
                            {
                                ExrChannelInfo channel = this.Channels[channelIdx];
                                offset += this.ReadFloatChannelData(
                                    stream,
                                    channel,
                                    decompressedPixelData.Slice(offset),
                                    redPixelData,
                                    greenPixelData,
                                    bluePixelData,
                                    alphaPixelData,
                                    (int)tileWidth);
                            }

                            uint columnEndIdx = (uint)Math.Min(columnStartIndex + tileWidth, width);
                            int channelOffset = 0;
                            for (int pixelRowIdx = (int)columnStartIndex; pixelRowIdx < columnEndIdx; pixelRowIdx++)
                            {
                                var pixelValue = new HalfVector4(redPixelData[channelOffset],
                                    greenPixelData[channelOffset], bluePixelData[channelOffset],
                                    hasAlpha ? alphaPixelData[channelOffset] : 1.0f);
                                color.FromVector4(pixelValue.ToVector4());
                                pixelRow[pixelRowIdx] = color;
                                channelOffset++;
                            }
                        }
                    }
                }

                stream.Position = nextOffsetPosition;
            }
        }

        private int ReadFloatChannelData(
            BufferedReadStream stream,
            ExrChannelInfo channel,
            Span<byte> decompressedPixelData,
            Span<float> redPixelData,
            Span<float> greenPixelData,
            Span<float> bluePixelData,
            Span<float> alphaPixelData,
            int width)
        {
            switch (channel.ChannelName)
            {
                case ExrConstants.ChannelNames.Red:
                    return this.ReadChannelData(channel, decompressedPixelData, redPixelData, width);

                case ExrConstants.ChannelNames.Blue:
                    return this.ReadChannelData(channel, decompressedPixelData, bluePixelData, width);

                case ExrConstants.ChannelNames.Green:
                    return this.ReadChannelData(channel, decompressedPixelData, greenPixelData, width);

                case ExrConstants.ChannelNames.Alpha:
                    return this.ReadChannelData(channel, decompressedPixelData, alphaPixelData, width);

                case ExrConstants.ChannelNames.Luminance:
                    int bytesRead = this.ReadChannelData(channel, decompressedPixelData, redPixelData, width);
                    redPixelData.CopyTo(bluePixelData);
                    redPixelData.CopyTo(greenPixelData);

                    return bytesRead;

                default:
                    // Skip unknown channel.
                    int channelDataSizeInBytes = channel.PixelType is ExrPixelType.Float or ExrPixelType.UnsignedInt ? 4 : 2;
                    stream.Position += width * channelDataSizeInBytes;
                    return channelDataSizeInBytes;
            }
        }

        private int ReadUnsignedIntChannelData(
            BufferedReadStream stream,
            ExrChannelInfo channel,
            Span<byte> decompressedPixelData,
            Span<uint> redPixelData,
            Span<uint> greenPixelData,
            Span<uint> bluePixelData,
            Span<uint> alphaPixelData,
            int width)
        {
            switch (channel.ChannelName)
            {
                case ExrConstants.ChannelNames.Red:
                    return this.ReadChannelData(channel, decompressedPixelData, redPixelData, width);

                case ExrConstants.ChannelNames.Blue:
                    return this.ReadChannelData(channel, decompressedPixelData, bluePixelData, width);

                case ExrConstants.ChannelNames.Green:
                    return this.ReadChannelData(channel, decompressedPixelData, greenPixelData, width);

                case ExrConstants.ChannelNames.Alpha:
                    return this.ReadChannelData(channel, decompressedPixelData, alphaPixelData, width);

                case ExrConstants.ChannelNames.Luminance:
                    int bytesRead = this.ReadChannelData(channel, decompressedPixelData, redPixelData, width);
                    redPixelData.CopyTo(bluePixelData);
                    redPixelData.CopyTo(greenPixelData);
                    return bytesRead;

                default:
                    // Skip unknown channel.
                    int channelDataSizeInBytes = channel.PixelType is ExrPixelType.Float or ExrPixelType.UnsignedInt ? 4 : 2;
                    stream.Position += this.Width * channelDataSizeInBytes;
                    return channelDataSizeInBytes;
            }
        }

        private int ReadChannelData(ExrChannelInfo channel, Span<byte> decompressedPixelData, Span<float> pixelData, int width)
        {
            switch (channel.PixelType)
            {
                case ExrPixelType.Half:
                    return this.ReadPixelRowChannelHalfSingle(decompressedPixelData, pixelData, width);
                case ExrPixelType.Float:
                    return this.ReadPixelRowChannelSingle(decompressedPixelData, pixelData, width);
            }

            return 0;
        }

        private int ReadChannelData(ExrChannelInfo channel, Span<byte> decompressedPixelData, Span<uint> pixelData, int width)
        {
            switch (channel.PixelType)
            {
                case ExrPixelType.UnsignedInt:
                    return this.ReadPixelRowChannelUnsignedInt(decompressedPixelData, pixelData, width);
            }

            return 0;
        }

        private int ReadPixelRowChannelHalfSingle(Span<byte> decompressedPixelData, Span<float> channelData, int width)
        {
            int offset = 0;
            for (int x = 0; x < width; x++)
            {
                ushort shortValue = BinaryPrimitives.ReadUInt16LittleEndian(decompressedPixelData.Slice(offset, 2));
                channelData[x] = HalfTypeHelper.Unpack(shortValue);
                offset += 2;
            }

            return offset;
        }

        private int ReadPixelRowChannelSingle(Span<byte> decompressedPixelData, Span<float> channelData, int width)
        {
            int offset = 0;
            for (int x = 0; x < width; x++)
            {
                int intValue = BinaryPrimitives.ReadInt32LittleEndian(decompressedPixelData.Slice(offset, 4));
                channelData[x] = Unsafe.As<int, float>(ref intValue);
                offset += 4;
            }

            return offset;
        }

        private int ReadPixelRowChannelUnsignedInt(Span<byte> decompressedPixelData, Span<uint> channelData, int width)
        {
            int offset = 0;
            for (int x = 0; x < width; x++)
            {
                channelData[x] = BinaryPrimitives.ReadUInt32LittleEndian(decompressedPixelData.Slice(offset, 4));
                offset += 4;
            }

            return offset;
        }

        /// <inheritdoc />
        public IImageInfo Identify(BufferedReadStream stream, CancellationToken cancellationToken)
        {
            ExrHeaderAttributes header = this.ReadExrHeader(stream);

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
            ExrPixelType pixelType = this.FindPixelType();

            return pixelType;
        }

        private ExrHeaderAttributes ReadExrHeader(BufferedReadStream stream)
        {
            // Skip over the magick bytes, we already know its an EXR image.
            stream.Skip(4);

            // Read version number.
            byte version = (byte)stream.ReadByte();
            if (version != 2)
            {
                ExrThrowHelper.ThrowNotSupportedVersion();
            }

            // Next three bytes contain info's about the image.
            byte flagsByte0 = (byte)stream.ReadByte();
            byte flagsByte1 = (byte)stream.ReadByte();
            byte flagsByte2 = (byte)stream.ReadByte();
            if ((flagsByte0 & (1 << 1)) != 0)
            {
                this.ImageType = ExrImageType.Tiled;
            }

            this.HeaderAttributes = this.ParseHeaderAttributes(stream);

            if (!this.HeaderAttributes.IsValid())
            {
                ExrThrowHelper.ThrowInvalidImageHeader();
            }

            this.Width = this.HeaderAttributes.DataWindow.Value.XMax - this.HeaderAttributes.DataWindow.Value.XMin + 1;
            this.Height = this.HeaderAttributes.DataWindow.Value.YMax - this.HeaderAttributes.DataWindow.Value.YMin + 1;
            this.Channels = this.HeaderAttributes.Channels;
            this.Compression = this.HeaderAttributes.Compression.GetValueOrDefault();

            this.metadata = new ImageMetadata();

            return this.HeaderAttributes;
        }

        private ExrHeaderAttributes ParseHeaderAttributes(BufferedReadStream stream)
        {
            ExrAttribute attribute = this.ReadAttribute(stream);
            var header = new ExrHeaderAttributes();

            while (!attribute.Equals(ExrAttribute.EmptyAttribute))
            {
                switch (attribute.Name)
                {
                    case ExrConstants.AttributeNames.Channels:
                        IList<ExrChannelInfo> channels = this.ReadChannelList(stream, attribute.Length);
                        header.Channels = channels;
                        break;
                    case ExrConstants.AttributeNames.Compression:
                        header.Compression = (ExrCompressionType)stream.ReadByte();
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
                    case ExrConstants.AttributeNames.Tiles:
                        header.TileXSize = this.ReadUnsignedInteger(stream);
                        header.TileYSize = this.ReadUnsignedInteger(stream);
                        int mode = stream.ReadByte();
                        if (mode != 0)
                        {
                            ExrThrowHelper.ThrowNotSupported("Unsupported tile mode. Only mode 0 is supported yet.");
                        }

                        break;
                    case ExrConstants.AttributeNames.ChunkCount:
                        header.ChunkCount = this.ReadSignedInteger(stream);
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

            int attributeSize = this.ReadSignedInteger(stream);

            return new ExrAttribute(attributeName, attributeType, attributeSize);
        }

        private ExrBox2i ReadBoxInteger(BufferedReadStream stream)
        {
            int xMin = this.ReadSignedInteger(stream);
            int yMin = this.ReadSignedInteger(stream);
            int xMax = this.ReadSignedInteger(stream);
            int yMax = this.ReadSignedInteger(stream);

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

            var pixelType = (ExrPixelType)this.ReadSignedInteger(stream);
            bytesRead += 4;

            byte pLinear = (byte)stream.ReadByte();
            byte reserved0 = (byte)stream.ReadByte();
            byte reserved1 = (byte)stream.ReadByte();
            byte reserved2 = (byte)stream.ReadByte();
            bytesRead += 4;

            int xSampling = this.ReadSignedInteger(stream);
            bytesRead += 4;

            int ySampling = this.ReadSignedInteger(stream);
            bytesRead += 4;

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

        private ExrPixelType FindPixelType()
        {
            ExrPixelType? pixelType = null;
            for (int i = 0; i < this.Channels.Count; i++)
            {
                if (this.Channels[i].ChannelName.Equals(ExrConstants.ChannelNames.Blue) ||
                    this.Channels[i].ChannelName.Equals(ExrConstants.ChannelNames.Green) ||
                    this.Channels[i].ChannelName.Equals(ExrConstants.ChannelNames.Red) ||
                    this.Channels[i].ChannelName.Equals(ExrConstants.ChannelNames.Alpha) ||
                    this.Channels[i].ChannelName.Equals(ExrConstants.ChannelNames.Luminance))
                {
                    if (!pixelType.HasValue)
                    {
                        pixelType = this.Channels[i].PixelType;
                    }
                    else
                    {
                        if (pixelType != this.Channels[i].PixelType)
                        {
                            ExrThrowHelper.ThrowNotSupported("Pixel channel data is expected to be the same for all channels.");
                        }
                    }
                }
            }

            if (!pixelType.HasValue)
            {
                ExrThrowHelper.ThrowNotSupported("Pixel channel data is unknown! Only R, G, B, A and Y are supported.");
            }

            return pixelType.Value;
        }

        private bool IsSupportedCompression()
        {
            switch (this.Compression)
            {
                case ExrCompressionType.None:
                case ExrCompressionType.Zip:
                case ExrCompressionType.Zips:
                case ExrCompressionType.RunLengthEncoded:
                case ExrCompressionType.B44:
                    return true;
            }

            return false;
        }

        private void ReadImageDataType()
        {
            bool hasRedChannel = false;
            bool hasGreenChannel = false;
            bool hasBlueChannel = false;
            bool hasAlphaChannel = false;
            bool hasLuminance = false;
            foreach (ExrChannelInfo channelInfo in this.Channels)
            {
                if (channelInfo.ChannelName.Equals("A"))
                {
                    hasAlphaChannel = true;
                }

                if (channelInfo.ChannelName.Equals("R"))
                {
                    hasRedChannel = true;
                }

                if (channelInfo.ChannelName.Equals("G"))
                {
                    hasGreenChannel = true;
                }

                if (channelInfo.ChannelName.Equals("B"))
                {
                    hasBlueChannel = true;
                }

                if (channelInfo.ChannelName.Equals("Y"))
                {
                    hasLuminance = true;
                }
            }

            if (hasRedChannel && hasGreenChannel && hasBlueChannel && hasAlphaChannel)
            {
                this.ImageDataType = ExrImageDataType.Rgba;
                return;
            }

            if (hasRedChannel && hasGreenChannel && hasBlueChannel)
            {
                this.ImageDataType = ExrImageDataType.Rgb;
                return;
            }

            if (hasLuminance && this.Channels.Count == 1)
            {
                this.ImageDataType = ExrImageDataType.Gray;
                return;
            }

            ExrThrowHelper.ThrowNotSupported("The image contains channels, which are not supported!");
        }

        private bool HasAlpha()
        {
            foreach (ExrChannelInfo channelInfo in this.Channels)
            {
                if (channelInfo.ChannelName.Equals("A"))
                {
                    return true;
                }
            }

            return false;
        }

        private uint CalculateBytesPerRow(uint width)
        {
            uint bytesPerRow = 0;
            foreach (ExrChannelInfo channelInfo in this.Channels)
            {
                if (channelInfo.ChannelName.Equals("A") || channelInfo.ChannelName.Equals("R") || channelInfo.ChannelName.Equals("G") || channelInfo.ChannelName.Equals("B") || channelInfo.ChannelName.Equals("Y"))
                {
                    if (channelInfo.PixelType == ExrPixelType.Half)
                    {
                        bytesPerRow += 2 * width;
                    }
                    else
                    {
                        bytesPerRow += 4 * width;
                    }
                }
            }

            return bytesPerRow;
        }

        private uint RowsPerBlock()
        {
            switch (this.Compression)
            {
                case ExrCompressionType.Zip:
                case ExrCompressionType.Pxr24:
                    return 16;
                case ExrCompressionType.B44:
                case ExrCompressionType.B44A:
                case ExrCompressionType.Piz:
                    return 32;

                default:
                    return 1;
            }
        }

        private ulong ReadUnsignedLong(BufferedReadStream stream)
        {
            int bytesRead = stream.Read(this.buffer, 0, 8);
            if (bytesRead != 8)
            {
                ExrThrowHelper.ThrowInvalidImageContentException("Could not read enough data from the stream!");
            }

            return BinaryPrimitives.ReadUInt64LittleEndian(this.buffer);
        }

        private uint ReadUnsignedInteger(BufferedReadStream stream)
        {
            int bytesRead = stream.Read(this.buffer, 0, 4);
            if (bytesRead != 4)
            {
                ExrThrowHelper.ThrowInvalidImageContentException("Could not read enough data from the stream!");
            }

            return BinaryPrimitives.ReadUInt32LittleEndian(this.buffer);
        }

        private int ReadSignedInteger(BufferedReadStream stream)
        {
            int bytesRead = stream.Read(this.buffer, 0, 4);
            if (bytesRead != 4)
            {
                ExrThrowHelper.ThrowInvalidImageContentException("Could not read enough data from the stream!");
            }

            return BinaryPrimitives.ReadInt32LittleEndian(this.buffer);
        }
    }
}
