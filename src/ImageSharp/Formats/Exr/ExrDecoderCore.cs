// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.
#nullable disable

using System.Buffers;
using System.Buffers.Binary;
using System.Numerics;
using System.Numerics.Tensors;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using SixLabors.ImageSharp.ColorProfiles;
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
    /// <summary>
    /// The mask of the file version in the version field.
    /// </summary>
    private const uint VersionMask = 0xFF;

    /// <summary>
    /// The version field flags of the tiled (0x200), deep (0x800) and multi-part (0x1000) files. The decoder reads only single-part scanline files.
    /// </summary>
    private const uint UnsupportedFlags = 0x200 | 0x800 | 0x1000;

    /// <summary>
    /// The number of compression methods that OpenEXR defines. A larger value is not valid.
    /// </summary>
    private const int CompressionMethodCount = 14;

    /// <summary>
    /// Reusable buffer.
    /// </summary>
    private InlineArray8<byte> buffer;

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
    /// The decode plane of each channel: 0 for R and Y, 1 for G, 2 for B and 3 for A. Other channels have -1. The decoder skips their samples.
    /// </summary>
    private int[] channelPlanes;

    /// <summary>
    /// A value that shows whether the image has an A channel.
    /// </summary>
    private bool hasAlpha;

    /// <summary>
    /// A value that shows whether the image has a Y channel.
    /// </summary>
    private bool hasLuminance;

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

    /// <summary>
    /// Gets or sets the decoded bytes of a row. A row holds the samples of every channel.
    /// </summary>
    private uint BytesPerRow { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of rows in a scanline chunk.
    /// </summary>
    private uint RowsPerBlock { get; set; }

    /// <summary>
    /// Gets or sets the decoded bytes of a full scanline chunk. The header check proves that this value fits in an int.
    /// </summary>
    private uint BytesPerBlock { get; set; }

    /// <summary>
    /// Gets or sets the number of scanline chunks.
    /// </summary>
    private uint ChunkCount { get; set; }

    /// <summary>
    /// Gets or sets the earliest valid stream position for a scanline chunk.
    /// </summary>
    private long MinimumChunkOffset { get; set; }

    /// <inheritdoc />
    protected override Image<TPixel> Decode<TPixel>(BufferedReadStream stream, CancellationToken cancellationToken)
    {
        this.ReadExrHeader(stream);
        ColorProfileConverter colorConverter = ExrColorConverter.Create(this.exrMetadata, false);

        // The factory owns the list of supported compression methods. The decoder creates the decompressor and the decode buffers before it allocates the
        // image. Thus an unsupported compression method does not allocate pixels.
        using ExrBaseDecompressor decompressor = ExrDecompressorFactory.Create(
            this.Compression,
            this.memoryAllocator,
            this.Width,
            this.BytesPerBlock,
            this.BytesPerRow,
            this.Channels,
            this.Options.SegmentIntegrityHandling);

        // An EXR file can omit color channels. The decoder clears the planes one time. Thus the planes of missing channels stay black in all rows.
        using IMemoryOwner<float> planeBuffer = this.memoryAllocator.Allocate<float>(this.Width * 4, AllocationOptions.Clean);
        using IMemoryOwner<byte> blockBuffer = this.memoryAllocator.Allocate<byte>((int)this.BytesPerBlock);

        Image<TPixel> image = null;
        try
        {
            image = new Image<TPixel>(this.configuration, this.Width, this.Height, this.metadata);
            this.DecodePixelData(
                stream,
                image.GetRootFramePixelBuffer(),
                decompressor,
                planeBuffer.GetSpan(),
                blockBuffer.GetSpan(),
                colorConverter,
                cancellationToken);

            return image;
        }
        catch
        {
            image?.Dispose();
            throw;
        }
    }

    /// <inheritdoc />
    protected override ImageInfo Identify(BufferedReadStream stream, CancellationToken cancellationToken)
    {
        this.ReadExrHeader(stream);

        return new ImageInfo(new Size(this.Width, this.Height), this.metadata);
    }

    /// <summary>
    /// Decodes the scanline chunks of the image.
    /// </summary>
    /// <typeparam name="TPixel">The type of the pixels.</typeparam>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="pixels">The pixel buffer.</param>
    /// <param name="decompressor">The decompressor for the stored compression method.</param>
    /// <param name="planes">The cleared R, G, B and A planes of one row, each <see cref="Width"/> samples long.</param>
    /// <param name="block">The reusable buffer for the decoded bytes of one chunk.</param>
    /// <param name="colorConverter">The conversion from stored linear RGB to sRGB working pixels.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    private void DecodePixelData<TPixel>(
        BufferedReadStream stream,
        Buffer2D<TPixel> pixels,
        ExrBaseDecompressor decompressor,
        Span<float> planes,
        Span<byte> block,
        ColorProfileConverter colorConverter,
        CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        int width = this.Width;
        int bytesPerRow = (int)this.BytesPerRow;
        uint rowsPerBlock = this.RowsPerBlock;
        IList<ExrChannelInfo> channels = this.Channels;
        int[] channelPlanes = this.channelPlanes;
        bool hasAlpha = this.hasAlpha;
        bool hasLuminance = this.hasLuminance;
        bool isUnsignedInt = this.PixelType == ExrPixelType.UnsignedInt;

        // The planes hold the 32-bit sample of each channel. A UINT plane holds the bits of the UINT samples.
        Span<float> redPixelData = planes[..width];
        Span<float> greenPixelData = planes.Slice(width, width);
        Span<float> bluePixelData = planes.Slice(width * 2, width);
        Span<float> alphaPixelData = planes.Slice(width * 3, width);
        InlineArray16<Vector4> colorStorage = default;
        Span<Vector4> colors = colorStorage;

        // A UINT image without an A channel is opaque. Its alpha plane holds the bits of uint.MaxValue.
        if (isUnsignedInt && !hasAlpha)
        {
            MemoryMarshal.Cast<float, uint>(alphaPixelData).Fill(uint.MaxValue);
        }

        // The offset table holds one entry for each chunk, in increasing y order. See "OpenEXR File Layout",
        // section "Component Four: Offset Tables", "Scan Lines".
        for (uint chunk = 0; chunk < this.ChunkCount; chunk++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            uint rowStart = chunk * rowsPerBlock;
            int rowsInBlock = (int)Math.Min(rowsPerBlock, (uint)this.Height - rowStart);
            uint uncompressedBytes = (uint)(bytesPerRow * rowsInBlock);
            ulong chunkOffset = this.ReadUnsignedLong(stream);
            long nextRowOffsetPosition = stream.Position;

            // In the IgnoreImageData mode, the decoder skips a chunk that has a damaged header or a damaged block. The rows of this chunk keep the fill value.
            if (this.TryReadChunkHeader(stream, chunkOffset, rowStart, uncompressedBytes, out uint compressedBytes) &&
                decompressor.Decompress(stream, compressedBytes, uncompressedBytes, block))
            {
                for (int row = 0; row < rowsInBlock; row++)
                {
                    // In the IgnoreImageData mode, a block that ends early decodes only the first pixels of some rows. The decoder does not convert the other
                    // pixels. Thus they keep the fill value.
                    int rowWidth = decompressor.GetCompletePixelCount(row);
                    if (rowWidth == 0)
                    {
                        break;
                    }

                    // A row holds the samples of every channel in the order of the channel list. The decoder skips the samples of a channel without a plane.
                    ReadOnlySpan<byte> rowData = block.Slice(row * bytesPerRow, bytesPerRow);
                    int offset = 0;
                    for (int channel = 0; channel < channels.Count; channel++)
                    {
                        bool isHalf = channels[channel].PixelType == ExrPixelType.Half;
                        int plane = channelPlanes[channel];
                        if (plane >= 0)
                        {
                            Span<float> planeData = planes.Slice(plane * width, width);
                            if (isHalf)
                            {
                                ReadPixelRowChannelHalfSingle(rowData[offset..], planeData, width);
                            }
                            else
                            {
                                ReadPixelRowChannelSingle(rowData[offset..], planeData, width);
                            }
                        }

                        offset += width * (isHalf ? sizeof(ushort) : sizeof(float));
                    }

                    // The Y channel uses the red plane. Gray pixels have the same value in R, G and B.
                    if (hasLuminance)
                    {
                        redPixelData.CopyTo(greenPixelData);
                        redPixelData.CopyTo(bluePixelData);
                    }

                    Span<TPixel> pixelRow = pixels.DangerousGetRowSpan((int)rowStart + row);
                    if (isUnsignedInt)
                    {
                        for (int x = 0; x < rowWidth; x += colors.Length)
                        {
                            int count = Math.Min(colors.Length, rowWidth - x);
                            Span<Vector4> colorBlock = colors[..count];

                            // Interleave the UINT component bits, but do not convert their values. The shared interleaver only shuffles and stores these
                            // temporary float views.
                            SimdUtils.InterleaveFloatPlanes(
                                redPixelData.Slice(x, count),
                                greenPixelData.Slice(x, count),
                                bluePixelData.Slice(x, count),
                                alphaPixelData.Slice(x, count),
                                colorBlock);

                            // Normalize the native UINT samples with the existing tensor conversions. Both spans start at the same address. Each vector load
                            // comes before its store.
                            Span<float> components = MemoryMarshal.Cast<Vector4, float>(colorBlock);
                            TensorPrimitives.ConvertTruncating<uint, float>(MemoryMarshal.Cast<Vector4, uint>(colorBlock), components);
                            TensorPrimitives.Multiply(components, 1F / uint.MaxValue, components);
                            colorConverter.Convert(this.configuration, colorBlock, pixelRow.Slice(x, count), PixelAlphaRepresentation.Associated);
                        }
                    }
                    else
                    {
                        for (int x = 0; x < rowWidth; x += colors.Length)
                        {
                            int count = Math.Min(colors.Length, rowWidth - x);
                            Span<Vector4> colorBlock = colors[..count];

                            // Keep the native precision through the color conversion. The converter packs the typed destination only after it restores the
                            // linear samples to the working transfer curve.
                            SimdUtils.InterleaveFloatPlanes(
                                redPixelData.Slice(x, count),
                                greenPixelData.Slice(x, count),
                                bluePixelData.Slice(x, count),
                                hasAlpha ? alphaPixelData.Slice(x, count) : [],
                                colorBlock);

                            colorConverter.Convert(this.configuration, colorBlock, pixelRow.Slice(x, count), PixelAlphaRepresentation.Associated);
                        }
                    }
                }
            }

            stream.Position = nextRowOffsetPosition;
        }
    }

    /// <summary>
    /// Reads the header of one scanline chunk. The method compares the header with the stream, the data window and the block sizes. This method owns the block
    /// size checks. The method reports a damaged chunk header through
    /// <see cref="SegmentIntegrityHandlingExtensions.ThrowOrIgnoreImageDataError(SegmentIntegrityHandling, string)"/>.
    /// </summary>
    /// <param name="stream">The stream containing the image data.</param>
    /// <param name="chunkOffset">The chunk offset from the offset table.</param>
    /// <param name="rowStart">The first row of the chunk, relative to the data window.</param>
    /// <param name="uncompressedBytes">The decoded byte count of the rows of the chunk.</param>
    /// <param name="compressedBytes">The declared byte count of the chunk pixel data.</param>
    /// <returns>
    /// <see langword="true"/> if the stream is at the chunk pixel data, and all the pixel data is in the stream.
    /// <see langword="false"/> if the chunk header is damaged and the decoder ignores image data errors.
    /// </returns>
    private bool TryReadChunkHeader(BufferedReadStream stream, ulong chunkOffset, uint rowStart, uint uncompressedBytes, out uint compressedBytes)
    {
        compressedBytes = 0;

        // A chunk starts after the offset table. It needs 8 bytes for its y coordinate and its byte count.
        // The header check proved that the offset table fits in the stream, so the subtraction cannot wrap.
        if (chunkOffset < (ulong)this.MinimumChunkOffset || chunkOffset > (ulong)(stream.Length - 8))
        {
            this.Options.SegmentIntegrityHandling.ThrowOrIgnoreImageDataError("EXR chunk offset is outside the bounds of the stream.");
            return false;
        }

        stream.Position = (long)chunkOffset;
        int y = this.ReadSignedInteger(stream);
        compressedBytes = this.ReadUnsignedInteger(stream);

        // The y coordinate of a chunk is the first row of the chunk at its index in the offset table.
        if (y != this.HeaderAttributes.DataWindow.YMin + (long)rowStart)
        {
            this.Options.SegmentIntegrityHandling.ThrowOrIgnoreImageDataError("EXR chunk y coordinate does not match its offset table entry.");
            return false;
        }

        // The packed data is not larger than the decoded data of a full chunk. The packed data ends in the stream. NONE stores each block without change.
        if (compressedBytes > this.BytesPerBlock ||
            !stream.IsReadRangeValid(stream.Position, compressedBytes) ||
            (this.Compression == ExrCompression.None && compressedBytes != uncompressedBytes))
        {
            this.Options.SegmentIntegrityHandling.ThrowOrIgnoreImageDataError("EXR chunk byte count is not valid for the chunk or the stream.");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Reads a pixel row with the pixel data being 16 bit half values.
    /// </summary>
    /// <param name="decompressedPixelData">The decompressed pixel data.</param>
    /// <param name="channelData">The channel data as float.</param>
    /// <param name="width">The width of a row in pixels.</param>
    private static void ReadPixelRowChannelHalfSingle(ReadOnlySpan<byte> decompressedPixelData, Span<float> channelData, int width)
    {
        int offset = 0;
        for (int x = 0; x < width; x++)
        {
            ushort shortValue = BinaryPrimitives.ReadUInt16LittleEndian(decompressedPixelData.Slice(offset, 2));
            channelData[x] = HalfTypeHelper.Unpack(shortValue);
            offset += 2;
        }
    }

    /// <summary>
    /// Reads a pixel row with 32 bit FLOAT or UINT pixel data. The method copies the sample bits without change.
    /// </summary>
    /// <param name="decompressedPixelData">The decompressed pixel data.</param>
    /// <param name="channelData">The pixel data as float.</param>
    /// <param name="width">The width in pixels of a row.</param>
    private static void ReadPixelRowChannelSingle(ReadOnlySpan<byte> decompressedPixelData, Span<float> channelData, int width)
    {
        int offset = 0;
        for (int x = 0; x < width; x++)
        {
            int intValue = BinaryPrimitives.ReadInt32LittleEndian(decompressedPixelData.Slice(offset, 4));
            channelData[x] = Unsafe.As<int, float>(ref intValue);
            offset += 4;
        }
    }

    /// <summary>
    /// Makes sure that the channel list is valid. Then the method calculates the channel layout: the decode plane of each channel, the pixel type and the image
    /// data type.
    /// </summary>
    private void ValidateChannels()
    {
        IList<ExrChannelInfo> channels = this.Channels;

        // A file needs at least one channel.
        if (channels.Count == 0)
        {
            ExrThrowHelper.ThrowInvalidImageContentException("EXR channel list has no channels.");
        }

        int[] channelPlanes = new int[channels.Count];
        ExrPixelType? pixelType = null;
        bool hasRed = false;
        bool hasGreen = false;
        bool hasBlue = false;
        bool hasAlpha = false;
        bool hasLuminance = false;
        for (int i = 0; i < channels.Count; i++)
        {
            ExrChannelInfo channel = channels[i];

            // OpenEXR defines the pixel types 0 (UINT), 1 (HALF) and 2 (FLOAT).
            if ((uint)channel.PixelType > (uint)ExrPixelType.Float)
            {
                ExrThrowHelper.ThrowInvalidImageContentException("EXR channel has a pixel type that is not valid.");
            }

            // A sampling factor less than 1 is not valid.
            if (channel.XSampling < 1 || channel.YSampling < 1)
            {
                ExrThrowHelper.ThrowInvalidImageContentException("EXR channel has an x or y sampling factor below 1.");
            }

            // The decoder reads only channels with one sample for each pixel.
            if (channel.XSampling != 1 || channel.YSampling != 1)
            {
                ExrThrowHelper.ThrowNotSupported("EXR channels with x or y sampling other than 1 are not supported.");
            }

            switch (channel.ChannelName)
            {
                case ExrConstants.ChannelNames.Red:
                    channelPlanes[i] = 0;
                    hasRed = true;
                    break;
                case ExrConstants.ChannelNames.Green:
                    channelPlanes[i] = 1;
                    hasGreen = true;
                    break;
                case ExrConstants.ChannelNames.Blue:
                    channelPlanes[i] = 2;
                    hasBlue = true;
                    break;
                case ExrConstants.ChannelNames.Alpha:
                    channelPlanes[i] = 3;
                    hasAlpha = true;
                    break;
                case ExrConstants.ChannelNames.Luminance:
                    channelPlanes[i] = 0;
                    hasLuminance = true;
                    break;
                default:
                    // The row holds the samples of the other channels too. The decoder skips them.
                    channelPlanes[i] = -1;
                    continue;
            }

            if (!pixelType.HasValue)
            {
                pixelType = channel.PixelType;
            }
            else if (pixelType != channel.PixelType)
            {
                ExrThrowHelper.ThrowNotSupported("Pixel channel data is expected to be the same for all channels.");
            }
        }

        if (!pixelType.HasValue)
        {
            ExrThrowHelper.ThrowNotSupported("Pixel channel data is unknown! Only R, G, B, A and Y are supported.");
        }

        this.channelPlanes = channelPlanes;
        this.hasAlpha = hasAlpha;
        this.hasLuminance = hasLuminance;
        this.PixelType = pixelType.Value;

        if (hasRed && hasGreen && hasBlue)
        {
            this.ImageDataType = hasAlpha ? ExrImageDataType.Rgba : ExrImageDataType.Rgb;
        }
        else if (hasLuminance && channels.Count == 1)
        {
            this.ImageDataType = ExrImageDataType.Gray;
        }
        else
        {
            this.ImageDataType = ExrImageDataType.Unknown;
        }
    }

    /// <summary>
    /// Reads the EXR image header. Then the method calculates the image size and the channel layout.
    /// <see href="https://openexr.com/en/latest/OpenEXRFileLayout.html#header-attributes-all-files/"/>
    /// </summary>
    /// <param name="stream">The stream.</param>
    private void ReadExrHeader(BufferedReadStream stream)
    {
        // Skip over the magick bytes, we already know its an EXR image.
        stream.Skip(4);

        // The low byte of the version field holds the file version. The other bytes hold the feature flags.
        uint versionAndFlags = this.ReadUnsignedInteger(stream);
        if ((versionAndFlags & VersionMask) != 2)
        {
            ExrThrowHelper.ThrowNotSupportedVersion();
        }

        if ((versionAndFlags & UnsupportedFlags) != 0)
        {
            ExrThrowHelper.ThrowNotSupported("Decoding tiled, deep or multi-part EXR images is not supported.");
        }

        this.HeaderAttributes = this.ParseHeaderAttributes(stream);

        ExrBox2i dataWindow = this.HeaderAttributes.DataWindow;
        if (dataWindow.XMax < dataWindow.XMin || dataWindow.YMax < dataWindow.YMin)
        {
            ExrThrowHelper.ThrowInvalidImageContentException("EXR DataWindow max values must be greater than or equal to min values.");
        }

        long width = (long)dataWindow.XMax - dataWindow.XMin + 1;
        long height = (long)dataWindow.YMax - dataWindow.YMin + 1;

        // The decoder keeps each row as four color planes. This limit on the width prevents an overflow of the width * 4 buffer sizes.
        if (width > int.MaxValue / 4 || height > int.MaxValue)
        {
            ExrThrowHelper.ThrowInvalidImageContentException("EXR DataWindow dimensions exceed the maximum allowed size.");
        }

        this.Width = (int)width;
        this.Height = (int)height;
        this.Channels = this.HeaderAttributes.Channels;
        this.Compression = this.HeaderAttributes.Compression;
        this.ValidateChannels();

        // The decoder holds the decoded bytes of one chunk in a buffer. Thus the size of a full chunk must fit in an int. The width limit and the channel count
        // keep the products less than ulong.MaxValue.
        ulong bytesPerRow = ExrUtils.CalculateBytesPerRow(this.Channels, (uint)this.Width);
        uint rowsPerBlock = ExrUtils.RowsPerBlock(this.Compression);
        ulong bytesPerBlock = bytesPerRow * rowsPerBlock;
        if (bytesPerBlock > int.MaxValue)
        {
            ExrThrowHelper.ThrowInvalidImageContentException("EXR block size exceeds the maximum allowed size.");
        }

        this.BytesPerRow = (uint)bytesPerRow;
        this.RowsPerBlock = rowsPerBlock;
        this.BytesPerBlock = (uint)bytesPerBlock;
        this.ChunkCount = (uint)(((long)this.Height + rowsPerBlock - 1) / rowsPerBlock);
        long offsetTableByteCount = this.ChunkCount * sizeof(ulong);

        // The scanline offset table is between the header and the pixel chunks. If the table is in the stream, all later chunk offsets
        // point after the table.
        if (stream.Position > stream.Length || offsetTableByteCount > stream.Length - stream.Position)
        {
            ExrThrowHelper.ThrowInvalidImageContentException("EXR chunk offset table is outside the bounds of the stream.");
        }

        this.MinimumChunkOffset = stream.Position + offsetTableByteCount;

        this.metadata = new ImageMetadata();

        this.exrMetadata = this.metadata.GetExrMetadata();
        this.exrMetadata.PixelType = this.PixelType;
        this.exrMetadata.ImageDataType = this.ImageDataType;
        this.exrMetadata.Compression = this.Compression;
        this.exrMetadata.Primaries = this.HeaderAttributes.Primaries;
        this.exrMetadata.WhitePoint = this.HeaderAttributes.WhitePoint;
    }

    /// <summary>
    /// Parses the image header attributes.
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <returns>The image header attributes.</returns>
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
        RgbPrimariesChromaticityCoordinates primaries = KnownRgbWorkingSpaces.SRgb.ChromaticityCoordinates;
        CieXyChromaticityCoordinates whitePoint = KnownIlluminants.D65Chromaticity;
        while (!attribute.Equals(ExrAttribute.EmptyAttribute))
        {
            switch (attribute.Name)
            {
                case ExrConstants.AttributeNames.Channels:
                    channels = this.ReadChannelList(stream, attribute.Length);
                    break;
                case ExrConstants.AttributeNames.Compression:
                {
                    ValidateAttribute(attribute, ExrConstants.AttibuteTypes.Compression, 1);
                    byte value = this.ReadBytes(stream, 1)[0];

                    // A value outside the defined compression methods is not valid.
                    if (value >= CompressionMethodCount)
                    {
                        ExrThrowHelper.ThrowInvalidImageContentException("EXR compression attribute has a value that is not valid.");
                    }

                    compression = (ExrCompression)value;
                    break;
                }

                case ExrConstants.AttributeNames.Chromaticities:
                    // The attribute stores eight little-endian floats, not an ICC profile or a transfer curve.
                    ValidateAttribute(attribute, ExrConstants.AttibuteTypes.Chromaticities, 8 * sizeof(float));
                    primaries = new RgbPrimariesChromaticityCoordinates(
                        new(this.ReadSingle(stream), this.ReadSingle(stream)),
                        new(this.ReadSingle(stream), this.ReadSingle(stream)),
                        new(this.ReadSingle(stream), this.ReadSingle(stream)));

                    whitePoint = new CieXyChromaticityCoordinates(this.ReadSingle(stream), this.ReadSingle(stream));
                    break;
                case ExrConstants.AttributeNames.DataWindow:
                    ValidateAttribute(attribute, ExrConstants.AttibuteTypes.BoxInt, 4 * sizeof(int));
                    dataWindow = this.ReadBoxInteger(stream);
                    break;
                case ExrConstants.AttributeNames.DisplayWindow:
                    ValidateAttribute(attribute, ExrConstants.AttibuteTypes.BoxInt, 4 * sizeof(int));
                    displayWindow = this.ReadBoxInteger(stream);
                    break;
                case ExrConstants.AttributeNames.LineOrder:
                    ValidateAttribute(attribute, ExrConstants.AttibuteTypes.LineOrder, 1);
                    lineOrder = (ExrLineOrder)this.ReadBytes(stream, 1)[0];
                    break;
                case ExrConstants.AttributeNames.PixelAspectRatio:
                    ValidateAttribute(attribute, ExrConstants.AttibuteTypes.Float, sizeof(float));
                    aspectRatio = this.ReadSingle(stream);
                    break;
                case ExrConstants.AttributeNames.ScreenWindowCenter:
                    ValidateAttribute(attribute, ExrConstants.AttibuteTypes.TwoFloat, 2 * sizeof(float));
                    screenWindowCenterX = this.ReadSingle(stream);
                    screenWindowCenterY = this.ReadSingle(stream);
                    break;
                case ExrConstants.AttributeNames.ScreenWindowWidth:
                    ValidateAttribute(attribute, ExrConstants.AttibuteTypes.Float, sizeof(float));
                    screenWindowWidth = this.ReadSingle(stream);
                    break;
                default:
                    // Skip unknown attribute bytes.
                    stream.Skip(attribute.Length);
                    break;
            }

            attribute = this.ReadAttribute(stream);
        }

        // Each scanline image has these attributes. See "OpenEXR File Layout", section "Header Attributes (All Files)".
        string missingAttribute =
            channels is null ? ExrConstants.AttributeNames.Channels :
            !compression.HasValue ? ExrConstants.AttributeNames.Compression :
            !dataWindow.HasValue ? ExrConstants.AttributeNames.DataWindow :
            !displayWindow.HasValue ? ExrConstants.AttributeNames.DisplayWindow :
            !lineOrder.HasValue ? ExrConstants.AttributeNames.LineOrder :
            !aspectRatio.HasValue ? ExrConstants.AttributeNames.PixelAspectRatio :
            !screenWindowCenterX.HasValue ? ExrConstants.AttributeNames.ScreenWindowCenter :
            !screenWindowWidth.HasValue ? ExrConstants.AttributeNames.ScreenWindowWidth :
            null;

        if (missingAttribute is not null)
        {
            ExrThrowHelper.ThrowInvalidImageContentException($"Invalid exr image header, the {missingAttribute} attribute is missing!");
        }

        ExrHeaderAttributes header = new(
            channels,
            primaries,
            whitePoint,
            compression.Value,
            dataWindow.Value,
            displayWindow.Value,
            lineOrder.Value,
            aspectRatio.Value,
            screenWindowWidth.Value,
            new PointF(screenWindowCenterX.Value, screenWindowCenterY.Value));
        return header;
    }

    /// <summary>
    /// Makes sure that a known attribute has the correct type and size before the decoder reads its value. If the size is wrong, all later attribute reads
    /// start at the wrong position.
    /// </summary>
    /// <param name="attribute">The attribute.</param>
    /// <param name="type">The type of the attribute.</param>
    /// <param name="size">The size in bytes of a value of the type.</param>
    private static void ValidateAttribute(ExrAttribute attribute, string type, int size)
    {
        if (attribute.Length != size || !attribute.Type.Equals(type, StringComparison.Ordinal))
        {
            ExrThrowHelper.ThrowInvalidImageContentException($"EXR {attribute.Name} attribute must have the type {type} and a size of {size} bytes.");
        }
    }

    /// <summary>
    /// Reads a attrbute from the stream, which consist of a name, a type and a size in bytes.
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <returns>A attribute.</returns>
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

    /// <summary>
    /// Reads a box attribute, which is a xMin, xMax and yMin, yMax value.
    /// </summary>
    /// <param name="stream">The stream to reaad from.</param>
    /// <returns>A box struct.</returns>
    private ExrBox2i ReadBoxInteger(BufferedReadStream stream)
    {
        int xMin = this.ReadSignedInteger(stream);
        int yMin = this.ReadSignedInteger(stream);
        int xMax = this.ReadSignedInteger(stream);
        int yMax = this.ReadSignedInteger(stream);

        return new ExrBox2i(xMin, yMin, xMax, yMax);
    }

    /// <summary>
    /// Reads the channel list from the stream.
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="attributeSize">The size in bytes of the channel list attribute.</param>
    /// <returns>The channel list.</returns>
    private List<ExrChannelInfo> ReadChannelList(BufferedReadStream stream, int attributeSize)
    {
        List<ExrChannelInfo> channels = [];
        while (attributeSize > 1)
        {
            ExrChannelInfo channelInfo = this.ReadChannelInfo(stream, out int bytesRead);
            channels.Add(channelInfo);
            attributeSize -= bytesRead;
        }

        // An empty channel name ends the list.
        if (this.ReadBytes(stream, 1)[0] != 0)
        {
            ExrThrowHelper.ThrowInvalidImageContentException("EXR channel list does not end with a null byte.");
        }

        return channels;
    }

    /// <summary>
    /// Reads the channel information from the stream.
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="bytesRead">The bytes read.</param>
    /// <returns>Channel info.</returns>
    private ExrChannelInfo ReadChannelInfo(BufferedReadStream stream, out int bytesRead)
    {
        string channelName = ReadString(stream);
        bytesRead = channelName.Length + 1;

        ExrPixelType pixelType = (ExrPixelType)this.ReadSignedInteger(stream);
        bytesRead += 4;

        // The pLinear byte comes before 3 reserved bytes. The decoder does not use the reserved bytes.
        byte pLinear = this.ReadBytes(stream, 4)[0];
        bytesRead += 4;

        int xSampling = this.ReadSignedInteger(stream);
        bytesRead += 4;

        int ySampling = this.ReadSignedInteger(stream);
        bytesRead += 4;

        return new ExrChannelInfo(channelName, pixelType, pLinear, xSampling, ySampling);
    }

    /// <summary>
    /// Reads a the string from the stream.
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <returns>A string.</returns>
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

    /// <summary>
    /// Reads a unsigned long value from the stream.
    /// </summary>
    /// <param name="stream">The stream to read the data from.</param>
    /// <returns>The unsigned long value.</returns>
    private ulong ReadUnsignedLong(BufferedReadStream stream) => BinaryPrimitives.ReadUInt64LittleEndian(this.ReadBytes(stream, sizeof(ulong)));

    /// <summary>
    /// Reads a unsigned integer value from the stream.
    /// </summary>
    /// <param name="stream">The stream to read the data from.</param>
    /// <returns>The integer value.</returns>
    private uint ReadUnsignedInteger(BufferedReadStream stream) => BinaryPrimitives.ReadUInt32LittleEndian(this.ReadBytes(stream, sizeof(uint)));

    /// <summary>
    /// Reads a signed integer value from the stream.
    /// </summary>
    /// <param name="stream">The stream to read the data from.</param>
    /// <returns>The integer value.</returns>
    private int ReadSignedInteger(BufferedReadStream stream) => BinaryPrimitives.ReadInt32LittleEndian(this.ReadBytes(stream, sizeof(int)));

    /// <summary>
    /// Reads a float value from the stream.
    /// </summary>
    /// <param name="stream">The stream to read the data from.</param>
    /// <returns>The float value.</returns>
    private float ReadSingle(BufferedReadStream stream) => BinaryPrimitives.ReadSingleLittleEndian(this.ReadBytes(stream, sizeof(float)));

    /// <summary>
    /// Reads the next bytes of the stream into the reusable buffer. This method owns the check that each read is complete.
    /// </summary>
    /// <param name="stream">The stream to read the data from.</param>
    /// <param name="count">The number of bytes to read. The value is at most 8.</param>
    /// <returns>The bytes. The next read replaces them.</returns>
    private Span<byte> ReadBytes(BufferedReadStream stream, int count)
    {
        Span<byte> bytes = ((Span<byte>)this.buffer)[..count];
        if (stream.Read(bytes) != count)
        {
            ExrThrowHelper.ThrowInvalidImageContentException("Not enough data to read a value from the EXR stream.");
        }

        return bytes;
    }
}
