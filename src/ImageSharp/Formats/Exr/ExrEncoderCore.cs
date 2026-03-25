// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Formats.Exr.Compression;
using SixLabors.ImageSharp.Formats.Exr.Constants;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Exr;

/// <summary>
/// Image encoder for writing an image to a stream in the OpenExr format.
/// </summary>
internal sealed class ExrEncoderCore
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
    private readonly Configuration configuration;

    /// <summary>
    /// The encoder with options.
    /// </summary>
    private readonly ExrEncoder encoder;

    /// <summary>
    /// The pixel type of the image.
    /// </summary>
    private ExrPixelType? pixelType;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExrEncoderCore"/> class.
    /// </summary>
    /// <param name="encoder">The encoder with options.</param>
    /// <param name="configuration">The configuration.</param>
    /// <param name="memoryAllocator">The memory manager.</param>
    public ExrEncoderCore(ExrEncoder encoder, Configuration configuration, MemoryAllocator memoryAllocator)
    {
        this.configuration = configuration;
        this.encoder = encoder;
        this.memoryAllocator = memoryAllocator;
        this.Compression = encoder.Compression ?? ExrCompression.None;
    }

    /// <summary>
    /// Gets or sets the compression implementation to use when encoding the image.
    /// </summary>
    internal ExrCompression Compression { get; set; }

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

        Buffer2D<TPixel> pixels = image.Frames.RootFrame.PixelBuffer;

        ImageMetadata metadata = image.Metadata;
        ExrMetadata exrMetadata = metadata.GetExrMetadata();
        this.pixelType ??= exrMetadata.PixelType;
        int width = image.Width;
        int height = image.Height;
        float aspectRatio = 1.0f;
        ExrBox2i dataWindow = new(0, 0, width - 1, height - 1);
        ExrBox2i displayWindow = new(0, 0, width - 1, height - 1);
        ExrLineOrder lineOrder = ExrLineOrder.IncreasingY;
        PointF screenWindowCenter = new(0.0f, 0.0f);
        int screenWindowWidth = 1;
        List<ExrChannelInfo> channels =
        [
            new(ExrConstants.ChannelNames.Blue, this.pixelType.Value, 0, 1, 1),
            new(ExrConstants.ChannelNames.Green, this.pixelType.Value, 0, 1, 1),
            new(ExrConstants.ChannelNames.Red, this.pixelType.Value, 0, 1, 1),
        ];
        ExrHeaderAttributes header = new(
            channels,
            this.Compression,
            dataWindow,
            displayWindow,
            lineOrder,
            aspectRatio,
            screenWindowWidth,
            screenWindowCenter);

        // Write magick bytes.
        BinaryPrimitives.WriteInt32LittleEndian(this.buffer, ExrConstants.MagickBytes);
        stream.Write(this.buffer.AsSpan(0, 4));

        // Version number.
        this.buffer[0] = 2;

        // Second, third and fourth bytes store info about the image, set all to default: zero.
        this.buffer[1] = 0;
        this.buffer[2] = 0;
        this.buffer[3] = 0;
        stream.Write(this.buffer.AsSpan(0, 4));

        // Write EXR header.
        this.WriteHeader(stream, header);

        // Next is offsets table to each pixel row, which will be written after the pixel data was written.
        int bytesPerChannel = this.pixelType == ExrPixelType.Half ? 2 : 4;
        int numberOfChannels = 3;
        uint rowSizeBytes = (uint)(width * numberOfChannels * bytesPerChannel);
        ulong startOfRowOffsetData = (ulong)stream.Position;
        stream.Position += 8 * height;

        // Write pixel data.
        switch (this.pixelType)
        {
            case ExrPixelType.Half:
            case ExrPixelType.Float:
            {
                ulong[] rowOffsets = this.EncodeFloatingPointPixelData(stream, pixels, width, height, channels, this.Compression);
                stream.Position = (long)startOfRowOffsetData;
                this.WriteRowOffsets(stream, height, rowOffsets);
                break;
            }

            case ExrPixelType.UnsignedInt:
            {
                ulong[] rowOffsets = this.EncodeUnsignedIntPixelData(stream, pixels, width, height, channels, this.Compression);
                stream.Position = (long)startOfRowOffsetData;
                this.WriteRowOffsets(stream, height, rowOffsets);
                break;
            }
        }
    }

    private ulong[] EncodeFloatingPointPixelData<TPixel>(
        Stream stream,
        Buffer2D<TPixel> pixels,
        int width,
        int height,
        List<ExrChannelInfo> channels,
        ExrCompression compression)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        uint bytesPerRow = ExrUtils.CalculateBytesPerRow(channels, (uint)width);
        uint rowsPerBlock = ExrUtils.RowsPerBlock(compression);
        uint bytesPerBlock = bytesPerRow * rowsPerBlock;
        int channelCount = channels.Count;

        using IMemoryOwner<float> rgbBuffer = this.memoryAllocator.Allocate<float>(width * 3, AllocationOptions.Clean);
        using IMemoryOwner<byte> rowBlockBuffer = this.memoryAllocator.Allocate<byte>((int)bytesPerBlock, AllocationOptions.Clean);
        Span<float> redBuffer = rgbBuffer.GetSpan()[..width];
        Span<float> greenBuffer = rgbBuffer.GetSpan().Slice(width, width);
        Span<float> blueBuffer = rgbBuffer.GetSpan().Slice(width * 2, width);

        using ExrBaseCompressor compressor = ExrCompressorFactory.Create(compression, this.memoryAllocator, stream, bytesPerBlock, bytesPerRow);

        ulong[] rowOffsets = new ulong[height];
        for (uint y = 0; y < height; y += rowsPerBlock)
        {
            rowOffsets[y] = (ulong)stream.Position;

            // Write row index.
            BinaryPrimitives.WriteUInt32LittleEndian(this.buffer, y);
            stream.Write(this.buffer.AsSpan(0, 4));

            // At this point, it is not yet known how much bytes the compressed data will take up, keep stream position.
            long pixelDataSizePos = stream.Position;
            stream.Position = pixelDataSizePos + 4;

            uint rowsInBlockCount = 0;
            for (uint rowIndex = y; rowIndex < y + rowsPerBlock && rowIndex < height; rowIndex++)
            {
                Span<TPixel> pixelRowSpan = pixels.DangerousGetRowSpan((int)rowIndex);
                for (int x = 0; x < width; x++)
                {
                    Vector4 vector4 = pixelRowSpan[x].ToVector4();
                    redBuffer[x] = vector4.X;
                    greenBuffer[x] = vector4.Y;
                    blueBuffer[x] = vector4.Z;
                }

                // Write pixel data to row block buffer.
                Span<byte> rowBlockSpan = rowBlockBuffer.GetSpan().Slice((int)(rowsInBlockCount * bytesPerRow), (int)bytesPerRow);
                switch (this.pixelType)
                {
                    case ExrPixelType.Float:
                        WriteSingleRow(rowBlockSpan, width, blueBuffer, greenBuffer, redBuffer);
                        break;
                    case ExrPixelType.Half:
                        WriteHalfSingleRow(rowBlockSpan, width, blueBuffer, greenBuffer, redBuffer);
                        break;
                }

                rowsInBlockCount++;
            }

            // Write compressed pixel row data to the stream.
            uint compressedBytes = compressor.CompressRowBlock(rowBlockBuffer.GetSpan(), (int)rowsInBlockCount);
            long positionAfterPixelData = stream.Position;

            // Write pixel row data size.
            BinaryPrimitives.WriteUInt32LittleEndian(this.buffer, compressedBytes);
            stream.Position = pixelDataSizePos;
            stream.Write(this.buffer.AsSpan(0, 4));
            stream.Position = positionAfterPixelData;
        }

        return rowOffsets;
    }

    private ulong[] EncodeUnsignedIntPixelData<TPixel>(
        Stream stream,
        Buffer2D<TPixel> pixels,
        int width,
        int height,
        List<ExrChannelInfo> channels,
        ExrCompression compression)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        uint bytesPerRow = ExrUtils.CalculateBytesPerRow(channels, (uint)width);
        uint rowsPerBlock = ExrUtils.RowsPerBlock(compression);
        uint bytesPerBlock = bytesPerRow * rowsPerBlock;
        int channelCount = channels.Count;

        using IMemoryOwner<uint> rgbBuffer = this.memoryAllocator.Allocate<uint>(width * 3, AllocationOptions.Clean);
        using IMemoryOwner<byte> rowBlockBuffer = this.memoryAllocator.Allocate<byte>((int)bytesPerBlock, AllocationOptions.Clean);
        Span<uint> redBuffer = rgbBuffer.GetSpan().Slice(0, width);
        Span<uint> greenBuffer = rgbBuffer.GetSpan().Slice(width, width);
        Span<uint> blueBuffer = rgbBuffer.GetSpan().Slice(width * 2, width);

        using ExrBaseCompressor compressor = ExrCompressorFactory.Create(compression, this.memoryAllocator, stream, bytesPerBlock, bytesPerRow);

        Rgb96 rgb = default;
        ulong[] rowOffsets = new ulong[height];
        for (uint y = 0; y < height; y += rowsPerBlock)
        {
            rowOffsets[y] = (ulong)stream.Position;

            // Write row index.
            BinaryPrimitives.WriteUInt32LittleEndian(this.buffer, y);
            stream.Write(this.buffer.AsSpan(0, 4));

            // At this point, it is not yet known how much bytes the compressed data will take up, keep stream position.
            long pixelDataSizePos = stream.Position;
            stream.Position = pixelDataSizePos + 4;

            uint rowsInBlockCount = 0;
            for (uint rowIndex = y; rowIndex < y + rowsPerBlock && rowIndex < height; rowIndex++)
            {
                Span<TPixel> pixelRowSpan = pixels.DangerousGetRowSpan((int)rowIndex);
                for (int x = 0; x < width; x++)
                {
                    Vector4 vector4 = pixelRowSpan[x].ToVector4();
                    Rgb96.FromVector4(vector4);

                    redBuffer[x] = rgb.R;
                    greenBuffer[x] = rgb.G;
                    blueBuffer[x] = rgb.B;
                }

                // Write row data to row block buffer.
                Span<byte> rowBlockSpan = rowBlockBuffer.GetSpan().Slice((int)(rowsInBlockCount * bytesPerRow), (int)bytesPerRow);
                WriteUnsignedIntRow(rowBlockSpan, width, blueBuffer, greenBuffer, redBuffer);
                rowsInBlockCount++;
            }

            // Write pixel row data compressed to the stream.
            uint compressedBytes = compressor.CompressRowBlock(rowBlockBuffer.GetSpan(), (int)rowsInBlockCount);
            long positionAfterPixelData = stream.Position;

            // Write pixel row data size.
            BinaryPrimitives.WriteUInt32LittleEndian(this.buffer, compressedBytes);
            stream.Position = pixelDataSizePos;
            stream.Write(this.buffer.AsSpan(0, 4));
            stream.Position = positionAfterPixelData;
        }

        return rowOffsets;
    }

    private void WriteHeader(Stream stream, ExrHeaderAttributes header)
    {
        this.WriteChannels(stream, header.Channels);
        this.WriteCompression(stream, header.Compression);
        this.WriteDataWindow(stream, header.DataWindow);
        this.WriteDisplayWindow(stream, header.DisplayWindow);
        this.WritePixelAspectRatio(stream, header.AspectRatio);
        this.WriteLineOrder(stream, header.LineOrder);
        this.WriteScreenWindowCenter(stream, header.ScreenWindowCenter);
        this.WriteScreenWindowWidth(stream, header.ScreenWindowWidth);
        stream.WriteByte(0);
    }

    private static void WriteSingleRow(Span<byte> buffer, int width, Span<float> blueBuffer, Span<float> greenBuffer, Span<float> redBuffer)
    {
        int offset = 0;
        for (int x = 0; x < width; x++)
        {
            WriteSingleToBuffer(buffer.Slice(offset, 4), blueBuffer[x]);
            offset += 4;
        }

        for (int x = 0; x < width; x++)
        {
            WriteSingleToBuffer(buffer.Slice(offset, 4), greenBuffer[x]);
            offset += 4;
        }

        for (int x = 0; x < width; x++)
        {
            WriteSingleToBuffer(buffer.Slice(offset, 4), redBuffer[x]);
            offset += 4;
        }
    }

    private static void WriteHalfSingleRow(Span<byte> buffer, int width, Span<float> blueBuffer, Span<float> greenBuffer, Span<float> redBuffer)
    {
        int offset = 0;
        for (int x = 0; x < width; x++)
        {
            WriteHalfSingleToBuffer(buffer.Slice(offset, 2), blueBuffer[x]);
            offset += 2;
        }

        for (int x = 0; x < width; x++)
        {
            WriteHalfSingleToBuffer(buffer.Slice(offset, 2), greenBuffer[x]);
            offset += 2;
        }

        for (int x = 0; x < width; x++)
        {
            WriteHalfSingleToBuffer(buffer.Slice(offset, 2), redBuffer[x]);
            offset += 2;
        }
    }

    private static void WriteUnsignedIntRow(Span<byte> buffer, int width, Span<uint> blueBuffer, Span<uint> greenBuffer, Span<uint> redBuffer)
    {
        int offset = 0;
        for (int x = 0; x < width; x++)
        {
            WriteUnsignedIntToBuffer(buffer.Slice(offset, 4), blueBuffer[x]);
            offset += 4;
        }

        for (int x = 0; x < width; x++)
        {
            WriteUnsignedIntToBuffer(buffer.Slice(offset, 4), greenBuffer[x]);
            offset += 4;
        }

        for (int x = 0; x < width; x++)
        {
            WriteUnsignedIntToBuffer(buffer.Slice(offset, 4), redBuffer[x]);
            offset += 4;
        }
    }

    private void WriteRowOffsets(Stream stream, int height, ulong[] rowOffsets)
    {
        ulong startOfRowOffsetData = (ulong)stream.Position;
        ulong offset = startOfRowOffsetData;
        for (int i = 0; i < height; i++)
        {
            BinaryPrimitives.WriteUInt64LittleEndian(this.buffer, rowOffsets[i]);
            stream.Write(this.buffer);
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
        this.WriteAttributeInformation(stream, ExrConstants.AttributeNames.Channels, ExrConstants.AttibuteTypes.ChannelList, attributeSize);

        foreach (ExrChannelInfo channelInfo in channels)
        {
            this.WriteChannelInfo(stream, channelInfo);
        }

        // Last byte should be zero.
        stream.WriteByte(0);
    }

    private void WriteCompression(Stream stream, ExrCompression compression)
    {
        this.WriteAttributeInformation(stream, ExrConstants.AttributeNames.Compression, ExrConstants.AttibuteTypes.Compression, 1);
        stream.WriteByte((byte)compression);
    }

    private void WritePixelAspectRatio(Stream stream, float aspectRatio)
    {
        this.WriteAttributeInformation(stream, ExrConstants.AttributeNames.PixelAspectRatio, ExrConstants.AttibuteTypes.Float, 4);
        this.WriteSingle(stream, aspectRatio);
    }

    private void WriteLineOrder(Stream stream, ExrLineOrder lineOrder)
    {
        this.WriteAttributeInformation(stream, ExrConstants.AttributeNames.LineOrder, ExrConstants.AttibuteTypes.LineOrder, 1);
        stream.WriteByte((byte)lineOrder);
    }

    private void WriteScreenWindowCenter(Stream stream, PointF screenWindowCenter)
    {
        this.WriteAttributeInformation(stream, ExrConstants.AttributeNames.ScreenWindowCenter, ExrConstants.AttibuteTypes.TwoFloat, 8);
        this.WriteSingle(stream, screenWindowCenter.X);
        this.WriteSingle(stream, screenWindowCenter.Y);
    }

    private void WriteScreenWindowWidth(Stream stream, float screenWindowWidth)
    {
        this.WriteAttributeInformation(stream, ExrConstants.AttributeNames.ScreenWindowWidth, ExrConstants.AttibuteTypes.Float, 4);
        this.WriteSingle(stream, screenWindowWidth);
    }

    private void WriteDataWindow(Stream stream, ExrBox2i dataWindow)
    {
        this.WriteAttributeInformation(stream, ExrConstants.AttributeNames.DataWindow, ExrConstants.AttibuteTypes.BoxInt, 16);
        this.WriteBoxInteger(stream, dataWindow);
    }

    private void WriteDisplayWindow(Stream stream, ExrBox2i displayWindow)
    {
        this.WriteAttributeInformation(stream, ExrConstants.AttributeNames.DisplayWindow, ExrConstants.AttibuteTypes.BoxInt, 16);
        this.WriteBoxInteger(stream, displayWindow);
    }

    private void WriteAttributeInformation(Stream stream, string name, string type, int size)
    {
        // Write attribute name.
        WriteString(stream, name);

        // Write attribute type.
        WriteString(stream, type);

        // Write attribute size.
        BinaryPrimitives.WriteUInt32LittleEndian(this.buffer, (uint)size);
        stream.Write(this.buffer.AsSpan(0, 4));
    }

    private void WriteChannelInfo(Stream stream, ExrChannelInfo channelInfo)
    {
        WriteString(stream, channelInfo.ChannelName);

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

    private static void WriteString(Stream stream, string str)
    {
        foreach (char c in str)
        {
            stream.WriteByte((byte)c);
        }

        // Write termination byte.
        stream.WriteByte(0);
    }

    private void WriteBoxInteger(Stream stream, ExrBox2i box)
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

    [MethodImpl(InliningOptions.ShortMethod)]
    private unsafe void WriteSingle(Stream stream, float value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(this.buffer, *(int*)&value);
        stream.Write(this.buffer.AsSpan(0, 4));
    }

    [MethodImpl(InliningOptions.ShortMethod)]
    private static unsafe void WriteSingleToBuffer(Span<byte> buffer, float value) => BinaryPrimitives.WriteInt32LittleEndian(buffer, *(int*)&value);

    [MethodImpl(InliningOptions.ShortMethod)]
    private static void WriteHalfSingleToBuffer(Span<byte> buffer, float value)
    {
        ushort valueAsShort = HalfTypeHelper.Pack(value);
        BinaryPrimitives.WriteUInt16LittleEndian(buffer, valueAsShort);
    }

    [MethodImpl(InliningOptions.ShortMethod)]
    private static void WriteUnsignedIntToBuffer(Span<byte> buffer, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(buffer, value);
}
