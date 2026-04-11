// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.
#nullable disable

using System.Buffers;
using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using SixLabors.ImageSharp.Formats.Exr.Compression;
using SixLabors.ImageSharp.Formats.Exr.Constants;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Exr;

/// <summary>
/// Performs the OpenExr decoding operation.
/// </summary>
internal sealed class ExrDecoderCore : ImageDecoderCore
{
    private const float Scale32Bit = 1f / 0xFFFFFFFF;

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
    private readonly Configuration configuration;

    /// <summary>
    /// The metadata.
    /// </summary>
    private ImageMetadata metadata;

    /// <summary>
    /// The exr specific metadata.
    /// </summary>
    private ExrMetadata exrMetadata;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExrDecoderCore"/> class.
    /// </summary>
    /// <param name="options">The options.</param>
    public ExrDecoderCore(ExrDecoderOptions options)
        : base(options.GeneralOptions)
    {
        this.configuration = options.GeneralOptions.Configuration;
        this.memoryAllocator = this.configuration.MemoryAllocator;
    }

    /// <summary>
    /// Gets or sets the image width.
    /// </summary>
    private int Width { get; set; }

    /// <summary>
    /// Gets or sets the image height.
    /// </summary>
    private int Height { get; set; }

    /// <summary>
    /// Gets or sets the image channel info's.
    /// </summary>
    private IList<ExrChannelInfo> Channels { get; set; }

    /// <summary>
    /// Gets or sets the compression method.
    /// </summary>
    private ExrCompression Compression { get; set; }

    /// <summary>
    /// Gets or sets the image data type, either RGB, RGBA or gray.
    /// </summary>
    private ExrImageDataType ImageDataType { get; set; }

    /// <summary>
    /// Gets or sets the pixel type.
    /// </summary>
    private ExrPixelType PixelType { get; set; }

    /// <summary>
    /// Gets or sets the header attributes.
    /// </summary>
    private ExrHeaderAttributes HeaderAttributes { get; set; }

    /// <inheritdoc />
    protected override Image<TPixel> Decode<TPixel>(BufferedReadStream stream, CancellationToken cancellationToken)
    {
        this.ReadExrHeader(stream);
        if (!this.IsSupportedCompression())
        {
            ExrThrowHelper.ThrowNotSupported($"Compression {this.Compression} is not yet supported");
        }

        Image<TPixel> image = new(this.configuration, this.Width, this.Height, this.metadata);
        Buffer2D<TPixel> pixels = image.GetRootFramePixelBuffer();

        switch (this.PixelType)
        {
            case ExrPixelType.Half:
            case ExrPixelType.Float:
                this.DecodeFloatingPointPixelData(stream, pixels, cancellationToken);
                break;
            case ExrPixelType.UnsignedInt:
                this.DecodeUnsignedIntPixelData(stream, pixels, cancellationToken);
                break;
            default:
                ExrThrowHelper.ThrowNotSupported("Pixel type is not supported");
                break;
        }

        return image;
    }

    private void DecodeFloatingPointPixelData<TPixel>(BufferedReadStream stream, Buffer2D<TPixel> pixels, CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        bool hasAlpha = this.HasAlpha();
        uint bytesPerRow = ExrUtils.CalculateBytesPerRow(this.Channels, (uint)this.Width);
        uint rowsPerBlock = ExrUtils.RowsPerBlock(this.Compression);
        uint bytesPerBlock = bytesPerRow * rowsPerBlock;
        int width = this.Width;
        int height = this.Height;
        int channelCount = this.Channels.Count;

        using IMemoryOwner<float> rowBuffer = this.memoryAllocator.Allocate<float>(width * 4);
        using IMemoryOwner<byte> decompressedPixelDataBuffer = this.memoryAllocator.Allocate<byte>((int)bytesPerBlock);
        Span<byte> decompressedPixelData = decompressedPixelDataBuffer.GetSpan();
        Span<float> redPixelData = rowBuffer.GetSpan()[..width];
        Span<float> greenPixelData = rowBuffer.GetSpan().Slice(width, width);
        Span<float> bluePixelData = rowBuffer.GetSpan().Slice(width * 2, width);
        Span<float> alphaPixelData = rowBuffer.GetSpan().Slice(width * 3, width);

        using ExrBaseDecompressor decompressor = ExrDecompressorFactory.Create(this.Compression, this.memoryAllocator, width, bytesPerBlock, bytesPerRow, rowsPerBlock, channelCount);

        int decodedRows = 0;
        while (decodedRows < height)
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
                    offset += ReadFloatChannelData(stream, channel, decompressedPixelData[offset..], redPixelData, greenPixelData, bluePixelData, alphaPixelData, width);
                }

                for (int x = 0; x < width; x++)
                {
                    HalfVector4 pixelValue = new(redPixelData[x], greenPixelData[x], bluePixelData[x], hasAlpha ? alphaPixelData[x] : 1.0f);
                    pixelRow[x] = TPixel.FromVector4(pixelValue.ToVector4());
                }

                decodedRows++;
            }

            stream.Position = nextRowOffsetPosition;

            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    private void DecodeUnsignedIntPixelData<TPixel>(BufferedReadStream stream, Buffer2D<TPixel> pixels, CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        bool hasAlpha = this.HasAlpha();
        uint bytesPerRow = ExrUtils.CalculateBytesPerRow(this.Channels, (uint)this.Width);
        uint rowsPerBlock = ExrUtils.RowsPerBlock(this.Compression);
        uint bytesPerBlock = bytesPerRow * rowsPerBlock;
        int width = this.Width;
        int height = this.Height;
        int channelCount = this.Channels.Count;

        using IMemoryOwner<uint> rowBuffer = this.memoryAllocator.Allocate<uint>(width * 4);
        using IMemoryOwner<byte> decompressedPixelDataBuffer = this.memoryAllocator.Allocate<byte>((int)bytesPerBlock);
        Span<byte> decompressedPixelData = decompressedPixelDataBuffer.GetSpan();
        Span<uint> redPixelData = rowBuffer.GetSpan()[..width];
        Span<uint> greenPixelData = rowBuffer.GetSpan().Slice(width, width);
        Span<uint> bluePixelData = rowBuffer.GetSpan().Slice(width * 2, width);
        Span<uint> alphaPixelData = rowBuffer.GetSpan().Slice(width * 3, width);

        using ExrBaseDecompressor decompressor = ExrDecompressorFactory.Create(this.Compression, this.memoryAllocator, width, bytesPerBlock, bytesPerRow, rowsPerBlock, channelCount);

        int decodedRows = 0;
        while (decodedRows < height)
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
                    offset += this.ReadUnsignedIntChannelData(stream, channel, decompressedPixelData[offset..], redPixelData, greenPixelData, bluePixelData, alphaPixelData, width);
                }

                for (int x = 0; x < width; x++)
                {
                    Rgb96 pixelValue = new(redPixelData[x], greenPixelData[x], bluePixelData[x]);
                    pixelRow[x] = TPixel.FromVector4(pixelValue.ToVector4());
                }

                decodedRows++;
            }

            stream.Position = nextRowOffsetPosition;

            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    private static int ReadFloatChannelData(
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
                return ReadChannelData(channel, decompressedPixelData, redPixelData, width);

            case ExrConstants.ChannelNames.Blue:
                return ReadChannelData(channel, decompressedPixelData, bluePixelData, width);

            case ExrConstants.ChannelNames.Green:
                return ReadChannelData(channel, decompressedPixelData, greenPixelData, width);

            case ExrConstants.ChannelNames.Alpha:
                return ReadChannelData(channel, decompressedPixelData, alphaPixelData, width);

            case ExrConstants.ChannelNames.Luminance:
                int bytesRead = ReadChannelData(channel, decompressedPixelData, redPixelData, width);
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
                return ReadChannelData(channel, decompressedPixelData, redPixelData, width);

            case ExrConstants.ChannelNames.Blue:
                return ReadChannelData(channel, decompressedPixelData, bluePixelData, width);

            case ExrConstants.ChannelNames.Green:
                return ReadChannelData(channel, decompressedPixelData, greenPixelData, width);

            case ExrConstants.ChannelNames.Alpha:
                return ReadChannelData(channel, decompressedPixelData, alphaPixelData, width);

            case ExrConstants.ChannelNames.Luminance:
                int bytesRead = ReadChannelData(channel, decompressedPixelData, redPixelData, width);
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

    private static int ReadChannelData(ExrChannelInfo channel, Span<byte> decompressedPixelData, Span<float> pixelData, int width) => channel.PixelType switch
    {
        ExrPixelType.Half => ReadPixelRowChannelHalfSingle(decompressedPixelData, pixelData, width),
        ExrPixelType.Float => ReadPixelRowChannelSingle(decompressedPixelData, pixelData, width),
        _ => 0,
    };

    private static int ReadChannelData(ExrChannelInfo channel, Span<byte> decompressedPixelData, Span<uint> pixelData, int width) => channel.PixelType switch
    {
        ExrPixelType.UnsignedInt => ReadPixelRowChannelUnsignedInt(decompressedPixelData, pixelData, width),
        _ => 0,
    };

    private static int ReadPixelRowChannelHalfSingle(Span<byte> decompressedPixelData, Span<float> channelData, int width)
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

    private static int ReadPixelRowChannelSingle(Span<byte> decompressedPixelData, Span<float> channelData, int width)
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

    private static int ReadPixelRowChannelUnsignedInt(Span<byte> decompressedPixelData, Span<uint> channelData, int width)
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
    protected override ImageInfo Identify(BufferedReadStream stream, CancellationToken cancellationToken)
    {
        ExrHeaderAttributes header = this.ReadExrHeader(stream);

        return new ImageInfo(new Size(header.DataWindow.XMax, header.DataWindow.YMax), this.metadata);
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

    private ExrImageDataType ReadImageDataType()
    {
        bool hasRedChannel = false;
        bool hasGreenChannel = false;
        bool hasBlueChannel = false;
        bool hasAlphaChannel = false;
        bool hasLuminance = false;
        foreach (ExrChannelInfo channelInfo in this.Channels)
        {
            if (channelInfo.ChannelName.Equals("A", StringComparison.Ordinal))
            {
                hasAlphaChannel = true;
            }

            if (channelInfo.ChannelName.Equals("R", StringComparison.Ordinal))
            {
                hasRedChannel = true;
            }

            if (channelInfo.ChannelName.Equals("G", StringComparison.Ordinal))
            {
                hasGreenChannel = true;
            }

            if (channelInfo.ChannelName.Equals("B", StringComparison.Ordinal))
            {
                hasBlueChannel = true;
            }

            if (channelInfo.ChannelName.Equals("Y", StringComparison.Ordinal))
            {
                hasLuminance = true;
            }
        }

        if (hasRedChannel && hasGreenChannel && hasBlueChannel && hasAlphaChannel)
        {
            return ExrImageDataType.Rgba;
        }

        if (hasRedChannel && hasGreenChannel && hasBlueChannel)
        {
            return ExrImageDataType.Rgb;
        }

        if (hasLuminance && this.Channels.Count == 1)
        {
            return ExrImageDataType.Gray;
        }

        return ExrImageDataType.Unknown;
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
        if ((flagsByte0 & (1 << 1)) != 0)
        {
            ExrThrowHelper.ThrowNotSupported("Decoding tiled exr images is not supported yet!");
        }

        // Discard the next two bytes.
        int bytesRead = stream.Read(this.buffer, 0, 2);
        if (bytesRead != 2)
        {
            ExrThrowHelper.ThrowInvalidImageContentException("Could not read enough data for exr file!");
        }

        this.HeaderAttributes = this.ParseHeaderAttributes(stream);

        this.Width = this.HeaderAttributes.DataWindow.XMax - this.HeaderAttributes.DataWindow.XMin + 1;
        this.Height = this.HeaderAttributes.DataWindow.YMax - this.HeaderAttributes.DataWindow.YMin + 1;
        this.Channels = this.HeaderAttributes.Channels;
        this.Compression = this.HeaderAttributes.Compression;
        this.PixelType = this.ValidateChannels();
        this.ImageDataType = this.ReadImageDataType();

        this.metadata = new ImageMetadata();

        this.exrMetadata = this.metadata.GetExrMetadata();
        this.exrMetadata.PixelType = this.PixelType;
        this.exrMetadata.ImageDataType = this.ImageDataType;
        this.exrMetadata.Compression = this.Compression;

        return this.HeaderAttributes;
    }

    private ExrHeaderAttributes ParseHeaderAttributes(BufferedReadStream stream)
    {
        ExrAttribute attribute = this.ReadAttribute(stream);

        IList<ExrChannelInfo> channels = null;
        ExrBox2i? dataWindow = null;
        ExrCompression? compression = null;
        ExrBox2i? displayWindow = null;
        ExrLineOrder? lineOrder = null;
        float? aspectRatio = null;
        float? screenWindowCenterX = null;
        float? screenWindowCenterY = null;
        float? screenWindowWidth = null;
        uint? tileXSize = null;
        uint? tileYSize = null;
        int? chunkCount = null;
        while (!attribute.Equals(ExrAttribute.EmptyAttribute))
        {
            switch (attribute.Name)
            {
                case ExrConstants.AttributeNames.Channels:
                    channels = this.ReadChannelList(stream, attribute.Length);
                    break;
                case ExrConstants.AttributeNames.Compression:
                    compression = (ExrCompression)stream.ReadByte();
                    break;
                case ExrConstants.AttributeNames.DataWindow:
                    dataWindow = this.ReadBoxInteger(stream);
                    break;
                case ExrConstants.AttributeNames.DisplayWindow:
                    displayWindow = this.ReadBoxInteger(stream);
                    break;
                case ExrConstants.AttributeNames.LineOrder:
                    lineOrder = (ExrLineOrder)stream.ReadByte();
                    break;
                case ExrConstants.AttributeNames.PixelAspectRatio:
                    aspectRatio = this.ReadSingle(stream);
                    break;
                case ExrConstants.AttributeNames.ScreenWindowCenter:
                    screenWindowCenterX = this.ReadSingle(stream);
                    screenWindowCenterY = this.ReadSingle(stream);
                    break;
                case ExrConstants.AttributeNames.ScreenWindowWidth:
                    screenWindowWidth = this.ReadSingle(stream);
                    break;
                case ExrConstants.AttributeNames.Tiles:
                    tileXSize = this.ReadUnsignedInteger(stream);
                    tileYSize = this.ReadUnsignedInteger(stream);
                    break;
                case ExrConstants.AttributeNames.ChunkCount:
                    chunkCount = this.ReadSignedInteger(stream);
                    break;
                default:
                    // Skip unknown attribute bytes.
                    stream.Skip(attribute.Length);
                    break;
            }

            attribute = this.ReadAttribute(stream);
        }

        if (!displayWindow.HasValue)
        {
            ExrThrowHelper.ThrowInvalidImageContentException("Invalid exr image header, the displayWindow attribute is missing!");
        }

        if (!dataWindow.HasValue)
        {
            ExrThrowHelper.ThrowInvalidImageContentException("Invalid exr image header, the dataWindow attribute is missing!");
        }

        if (channels is null)
        {
            ExrThrowHelper.ThrowInvalidImageContentException("Invalid exr image header, the channels attribute is missing!");
        }

        if (!compression.HasValue)
        {
            ExrThrowHelper.ThrowInvalidImageContentException("Invalid exr image header, the compression attribute is missing!");
        }

        if (!lineOrder.HasValue)
        {
            ExrThrowHelper.ThrowInvalidImageContentException("Invalid exr image header, the lineOrder attribute is missing!");
        }

        if (!aspectRatio.HasValue)
        {
            ExrThrowHelper.ThrowInvalidImageContentException("Invalid exr image header, the aspectRatio attribute is missing!");
        }

        if (!screenWindowWidth.HasValue)
        {
            ExrThrowHelper.ThrowInvalidImageContentException("Invalid exr image header, the screenWindowWidth attribute is missing!");
        }

        if (!screenWindowCenterX.HasValue || !screenWindowCenterY.HasValue)
        {
            ExrThrowHelper.ThrowInvalidImageContentException("Invalid exr image header, the screenWindowCenter attribute is missing!");
        }

        ExrHeaderAttributes header = new(
            channels,
            compression.Value,
            dataWindow.Value,
            displayWindow.Value,
            lineOrder.Value,
            aspectRatio.Value,
            screenWindowWidth.Value,
            new PointF(screenWindowCenterX.Value, screenWindowCenterY.Value),
            tileXSize,
            tileYSize,
            chunkCount);
        return header;
    }

    private ExrAttribute ReadAttribute(BufferedReadStream stream)
    {
        string attributeName = ReadString(stream);
        if (attributeName.Equals(string.Empty, StringComparison.Ordinal))
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
        List<ExrChannelInfo> channels = [];
        while (attributeSize > 1)
        {
            ExrChannelInfo channelInfo = this.ReadChannelInfo(stream, out int bytesRead);
            channels.Add(channelInfo);
            attributeSize -= bytesRead;
        }

        // Last byte should be a null byte.
        if (stream.ReadByte() == -1)
        {
            ExrThrowHelper.ThrowInvalidImageContentException("Could not read enough data to read exr channel list!");
        }

        return channels;
    }

    private ExrChannelInfo ReadChannelInfo(BufferedReadStream stream, out int bytesRead)
    {
        string channelName = ReadString(stream);
        bytesRead = channelName.Length + 1;

        ExrPixelType pixelType = (ExrPixelType)this.ReadSignedInteger(stream);
        bytesRead += 4;

        byte pLinear = (byte)stream.ReadByte();

        // Next 3 bytes are reserved bytes and not use.
        if (stream.Read(this.buffer, 0, 3) != 3)
        {
            ExrThrowHelper.ThrowInvalidImageContentException("Could not read enough data to read exr channel info!");
        }

        bytesRead += 4;

        int xSampling = this.ReadSignedInteger(stream);
        bytesRead += 4;

        int ySampling = this.ReadSignedInteger(stream);
        bytesRead += 4;

        return new ExrChannelInfo(channelName, pixelType, pLinear, xSampling, ySampling);
    }

    private static string ReadString(BufferedReadStream stream)
    {
        StringBuilder str = new();
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
            if (this.Channels[i].ChannelName.Equals(ExrConstants.ChannelNames.Blue, StringComparison.Ordinal) ||
                this.Channels[i].ChannelName.Equals(ExrConstants.ChannelNames.Green, StringComparison.Ordinal) ||
                this.Channels[i].ChannelName.Equals(ExrConstants.ChannelNames.Red, StringComparison.Ordinal) ||
                this.Channels[i].ChannelName.Equals(ExrConstants.ChannelNames.Alpha, StringComparison.Ordinal) ||
                this.Channels[i].ChannelName.Equals(ExrConstants.ChannelNames.Luminance, StringComparison.Ordinal))
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

    private bool IsSupportedCompression() => this.Compression switch
    {
        ExrCompression.None or ExrCompression.Zip or ExrCompression.Zips or ExrCompression.RunLengthEncoded or ExrCompression.B44 => true,
        _ => false,
    };

    private bool HasAlpha()
    {
        foreach (ExrChannelInfo channelInfo in this.Channels)
        {
            if (channelInfo.ChannelName.Equals("A", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Reads a unsigned long value from the stream.
    /// </summary>
    /// <param name="stream">The stream to read the data from.</param>
    /// <returns>The unsigned long value.</returns>
    private ulong ReadUnsignedLong(BufferedReadStream stream)
    {
        int bytesRead = stream.Read(this.buffer, 0, 8);
        if (bytesRead != 8)
        {
            ExrThrowHelper.ThrowInvalidImageContentException("Not enough data to read a unsigned long from the stream!");
        }

        return BinaryPrimitives.ReadUInt64LittleEndian(this.buffer);
    }

    /// <summary>
    /// Reads a unsigned integer value from the stream.
    /// </summary>
    /// <param name="stream">The stream to read the data from.</param>
    /// <returns>The integer value.</returns>
    private uint ReadUnsignedInteger(BufferedReadStream stream)
    {
        int bytesRead = stream.Read(this.buffer, 0, 4);
        if (bytesRead != 4)
        {
            ExrThrowHelper.ThrowInvalidImageContentException("Not enough data to read a unsigned int from the stream!");
        }

        return BinaryPrimitives.ReadUInt32LittleEndian(this.buffer);
    }

    /// <summary>
    /// Reads a signed integer value from the stream.
    /// </summary>
    /// <param name="stream">The stream to read the data from.</param>
    /// <returns>The integer value.</returns>
    private int ReadSignedInteger(BufferedReadStream stream)
    {
        int bytesRead = stream.Read(this.buffer, 0, 4);
        if (bytesRead != 4)
        {
            ExrThrowHelper.ThrowInvalidImageContentException("Not enough data to read a signed int from the stream!");
        }

        return BinaryPrimitives.ReadInt32LittleEndian(this.buffer);
    }

    /// <summary>
    /// Reads a float value from the stream.
    /// </summary>
    /// <param name="stream">The stream to read the data from.</param>
    /// <returns>The float value.</returns>
    private float ReadSingle(BufferedReadStream stream)
    {
        int bytesRead = stream.Read(this.buffer, 0, 4);
        if (bytesRead != 4)
        {
            ExrThrowHelper.ThrowInvalidImageContentException("Not enough data to read a float value from the stream!");
        }

        int intValue = BinaryPrimitives.ReadInt32BigEndian(this.buffer);

        return Unsafe.As<int, float>(ref intValue);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TPixel ColorScaleTo32Bit<TPixel>(uint r, uint g, uint b, uint a)
        where TPixel : unmanaged, IPixel<TPixel>
        => TPixel.FromScaledVector4(new Vector4(r, g, b, a) * Scale32Bit);
}
