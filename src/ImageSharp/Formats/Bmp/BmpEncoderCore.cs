// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Quantization;

namespace SixLabors.ImageSharp.Formats.Bmp;

/// <summary>
/// Image encoder for writing an image to a stream as a Windows bitmap.
/// </summary>
internal sealed class BmpEncoderCore
{
    /// <summary>
    /// The amount to pad each row by.
    /// </summary>
    private int padding;

    /// <summary>
    /// The mask for the alpha channel of the color for 32 bit rgba bitmaps.
    /// </summary>
    private const int Rgba32AlphaMask = 0xFF << 24;

    /// <summary>
    /// The mask for the red part of the color for 32 bit rgba bitmaps.
    /// </summary>
    private const int Rgba32RedMask = 0xFF << 16;

    /// <summary>
    /// The mask for the green part of the color for 32 bit rgba bitmaps.
    /// </summary>
    private const int Rgba32GreenMask = 0xFF << 8;

    /// <summary>
    /// The mask for the blue part of the color for 32 bit rgba bitmaps.
    /// </summary>
    private const int Rgba32BlueMask = 0xFF;

    /// <summary>
    /// The color palette for an 8 bit image will have 256 entry's with 4 bytes for each entry.
    /// </summary>
    private const int ColorPaletteSize8Bit = 1024;

    /// <summary>
    /// The color palette for an 4 bit image will have 16 entry's with 4 bytes for each entry.
    /// </summary>
    private const int ColorPaletteSize4Bit = 64;

    /// <summary>
    /// The color palette for an 2 bit image will have 4 entry's with 4 bytes for each entry.
    /// </summary>
    private const int ColorPaletteSize2Bit = 16;

    /// <summary>
    /// The color palette for an 1 bit image will have 2 entry's with 4 bytes for each entry.
    /// </summary>
    private const int ColorPaletteSize1Bit = 8;

    /// <summary>
    /// Used for allocating memory during processing operations.
    /// </summary>
    private readonly MemoryAllocator memoryAllocator;

    /// <summary>
    /// The color depth, in number of bits per pixel.
    /// </summary>
    private BmpBitsPerPixel? bitsPerPixel;

    /// <summary>
    /// A bitmap v4 header will only be written, if the user explicitly wants support for transparency.
    /// In this case the compression type BITFIELDS will be used.
    /// If the image contains a color profile, a bitmap v5 header is written, which is needed to write this info.
    /// Otherwise a bitmap v3 header will be written, which is supported by almost all decoders.
    /// </summary>
    private BmpInfoHeaderType infoHeaderType;

    /// <summary>
    /// The quantizer for reducing the color count in one-, two-, four- and eight-bit indexed images.
    /// </summary>
    private readonly IQuantizer quantizer;

    /// <summary>
    /// The pixel sampling strategy for quantization.
    /// </summary>
    private readonly IPixelSamplingStrategy pixelSamplingStrategy;

    /// <summary>
    /// The transparent color mode.
    /// </summary>
    private readonly TransparentColorMode transparentColorMode;

    /// <inheritdoc cref="BmpDecoderOptions.ProcessedAlphaMask"/>
    private readonly bool processedAlphaMask;

    /// <inheritdoc cref="BmpDecoderOptions.SkipFileHeader"/>
    private readonly bool skipFileHeader;

    /// <summary>
    /// Whether optional image metadata should be omitted.
    /// </summary>
    private readonly bool skipMetadata;

    /// <inheritdoc cref="BmpDecoderOptions.UseDoubleHeight"/>
    private readonly bool isDoubleHeight;

    /// <summary>
    /// Initializes a new instance of the <see cref="BmpEncoderCore"/> class.
    /// </summary>
    /// <param name="encoder">The encoder with options.</param>
    /// <param name="memoryAllocator">The memory manager.</param>
    public BmpEncoderCore(BmpEncoder encoder, MemoryAllocator memoryAllocator)
    {
        this.memoryAllocator = memoryAllocator;
        this.bitsPerPixel = encoder.BitsPerPixel;

        // TODO: Use a palette quantizer if supplied.
        this.quantizer = encoder.Quantizer ?? KnownQuantizers.Hexadecatree;
        this.pixelSamplingStrategy = encoder.PixelSamplingStrategy;
        this.transparentColorMode = encoder.TransparentColorMode;
        this.infoHeaderType = encoder.SupportTransparency ? BmpInfoHeaderType.WinVersion4 : BmpInfoHeaderType.WinVersion3;
        this.processedAlphaMask = encoder.ProcessedAlphaMask;
        this.skipFileHeader = encoder.SkipFileHeader;
        this.skipMetadata = encoder.SkipMetadata;
        this.isDoubleHeight = encoder.UseDoubleHeight;
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

        this.Encode(image.Frames.RootFrame, image.Metadata, stream, cancellationToken);
    }

    /// <summary>
    /// Encodes a source frame using the supplied image metadata.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="frame">The source frame.</param>
    /// <param name="metadata">The source image metadata.</param>
    /// <param name="stream">The destination stream.</param>
    /// <param name="cancellationToken">The token to request cancellation.</param>
    internal void Encode<TPixel>(ImageFrame<TPixel> frame, ImageMetadata metadata, Stream stream, CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // ICC offsets start at the DIB header, whether it follows a BMP file header or sits inside an ICO/CUR resource.
        long infoHeaderPosition = stream.Position + (this.skipFileHeader ? 0 : BmpFileHeader.Size);

        Configuration configuration = frame.Configuration;
        BmpMetadata bmpMetadata = metadata.GetBmpMetadata();
        this.bitsPerPixel ??= bmpMetadata.BitsPerPixel;

        ushort bpp = (ushort)this.bitsPerPixel;
        int bytesPerLine = (int)(4 * ((((uint)frame.Width * bpp) + 31) / 32));
        this.padding = bytesPerLine - (int)(frame.Width * (bpp / 8F));

        int colorPaletteSize = this.bitsPerPixel switch
        {
            BmpBitsPerPixel.Bit8 => ColorPaletteSize8Bit,
            BmpBitsPerPixel.Bit4 => ColorPaletteSize4Bit,
            BmpBitsPerPixel.Bit2 => ColorPaletteSize2Bit,
            BmpBitsPerPixel.Bit1 => ColorPaletteSize1Bit,
            _ => 0
        };

        // Direct samples and palette entries store RGB. Indexed compatibility is determined by the palette, not its indices.
        PixelColorType colorType = colorPaletteSize > 0 ? PixelColorType.Indexed : PixelColorType.RGB;
        IccProfile? sourceIccProfile = frame.Metadata.IccProfile ?? metadata.IccProfile;
        IccProfile? outputIccProfile = IccProfile.GetIccProfileForEncoding(
            sourceIccProfile,
            colorType,
            this.memoryAllocator,
            out ColorProfileConverter? converter);

        // ICC describes the stored colors and must remain even when optional metadata is skipped.
        byte[]? iccProfileData = outputIccProfile?.ToByteArray();
        int iccProfileSize = iccProfileData?.Length ?? 0;
        if (iccProfileData is not null)
        {
            this.infoHeaderType = BmpInfoHeaderType.WinVersion5;
        }

        int infoHeaderSize = this.infoHeaderType switch
        {
            BmpInfoHeaderType.WinVersion3 => BmpInfoHeader.SizeV3,
            BmpInfoHeaderType.WinVersion4 => BmpInfoHeader.SizeV4,
            BmpInfoHeaderType.WinVersion5 => BmpInfoHeader.SizeV5,
            _ => BmpInfoHeader.SizeV3
        };

        // ICO/CUR DIB headers include the XOR bitmap and following AND mask in one doubled height.
        int height = frame.Height;
        if (this.isDoubleHeight)
        {
            height <<= 1;
        }

        BmpInfoHeader infoHeader = this.CreateBmpInfoHeader(frame.Width, height, infoHeaderSize, bpp, bytesPerLine, metadata, iccProfileData);

        Span<byte> buffer = stackalloc byte[infoHeaderSize];

        // ICO/CUR resources contain a DIB directly; standalone BMP files additionally require BITMAPFILEHEADER.
        if (!this.skipFileHeader)
        {
            WriteBitmapFileHeader(stream, infoHeaderSize, colorPaletteSize, iccProfileSize, infoHeader, buffer);
        }

        this.WriteBitmapInfoHeader(stream, infoHeader, buffer, infoHeaderSize);
        this.WriteImage(configuration, stream, frame, converter, cancellationToken);
        WriteColorProfile(stream, iccProfileData, buffer, infoHeaderPosition);

        stream.Flush();
    }

    /// <summary>
    /// Creates the bitmap information header.
    /// </summary>
    /// <param name="width">The width of the image.</param>
    /// <param name="height">The height of the image.</param>
    /// <param name="infoHeaderSize">Size of the information header.</param>
    /// <param name="bpp">The bits per pixel.</param>
    /// <param name="bytesPerLine">The bytes per line.</param>
    /// <param name="metadata">The metadata.</param>
    /// <param name="iccProfileData">The icc profile data.</param>
    /// <returns>The bitmap information header.</returns>
    private BmpInfoHeader CreateBmpInfoHeader(
        int width,
        int height,
        int infoHeaderSize,
        ushort bpp,
        int bytesPerLine,
        ImageMetadata metadata,
        byte[]? iccProfileData)
    {
        int hResolution = 0;
        int vResolution = 0;

        if (!this.skipMetadata
            && metadata.ResolutionUnits != PixelResolutionUnit.AspectRatio
            && metadata.HorizontalResolution > 0
            && metadata.VerticalResolution > 0)
        {
            switch (metadata.ResolutionUnits)
            {
                case PixelResolutionUnit.PixelsPerInch:

                    hResolution = (int)Math.Round(UnitConverter.InchToMeter(metadata.HorizontalResolution));
                    vResolution = (int)Math.Round(UnitConverter.InchToMeter(metadata.VerticalResolution));
                    break;

                case PixelResolutionUnit.PixelsPerCentimeter:

                    hResolution = (int)Math.Round(UnitConverter.CmToMeter(metadata.HorizontalResolution));
                    vResolution = (int)Math.Round(UnitConverter.CmToMeter(metadata.VerticalResolution));
                    break;

                case PixelResolutionUnit.PixelsPerMeter:
                    hResolution = (int)Math.Round(metadata.HorizontalResolution);
                    vResolution = (int)Math.Round(metadata.VerticalResolution);

                    break;
            }
        }

        BmpInfoHeader infoHeader = new(
            headerSize: infoHeaderSize,
            width: width,
            height: height,
            planes: 1,
            bitsPerPixel: bpp,
            imageSize: height * bytesPerLine,
            xPelsPerMeter: hResolution,
            yPelsPerMeter: vResolution,
            clrUsed: 0,
            clrImportant: 0);

        if ((this.infoHeaderType is BmpInfoHeaderType.WinVersion4 or BmpInfoHeaderType.WinVersion5) && this.bitsPerPixel == BmpBitsPerPixel.Bit32)
        {
            infoHeader.AlphaMask = Rgba32AlphaMask;
            infoHeader.RedMask = Rgba32RedMask;
            infoHeader.GreenMask = Rgba32GreenMask;
            infoHeader.BlueMask = Rgba32BlueMask;
            infoHeader.Compression = BmpCompression.BitFields;
        }

        if (this.infoHeaderType is BmpInfoHeaderType.WinVersion5 && iccProfileData != null)
        {
            infoHeader.ProfileSize = iccProfileData.Length;
            infoHeader.CsType = BmpColorSpace.PROFILE_EMBEDDED;
            infoHeader.Intent = BmpRenderingIntent.LCS_GM_IMAGES;
        }

        return infoHeader;
    }

    /// <summary>
    /// Writes the color profile to the stream.
    /// </summary>
    /// <param name="stream">The stream to write to.</param>
    /// <param name="iccProfileData">The color profile data.</param>
    /// <param name="buffer">The buffer.</param>
    /// <param name="infoHeaderPosition">The absolute stream position where the DIB header begins.</param>
    private static void WriteColorProfile(Stream stream, byte[]? iccProfileData, Span<byte> buffer, long infoHeaderPosition)
    {
        if (iccProfileData != null)
        {
            // The offset, in bytes, from the beginning of the BITMAPV5HEADER structure to the start of the profile data.
            int profileOffset = (int)(stream.Position - infoHeaderPosition);
            stream.Write(iccProfileData);
            long position = stream.Position;

            // The V5 profile-offset field follows the V4 header and the four-byte rendering intent.
            BinaryPrimitives.WriteInt32LittleEndian(buffer, profileOffset);
            stream.Position = infoHeaderPosition + BmpInfoHeader.SizeV4 + sizeof(uint);
            stream.Write(buffer[..4]);
            stream.Position = position;
        }
    }

    /// <summary>
    /// Writes the bitmap file header.
    /// </summary>
    /// <param name="stream">The stream to write the header to.</param>
    /// <param name="infoHeaderSize">Size of the bitmap information header.</param>
    /// <param name="colorPaletteSize">Size of the color palette.</param>
    /// <param name="iccProfileSize">The size in bytes of the color profile.</param>
    /// <param name="infoHeader">The information header to write.</param>
    /// <param name="buffer">The buffer to write to.</param>
    private static void WriteBitmapFileHeader(
        Stream stream,
        int infoHeaderSize,
        int colorPaletteSize,
        int iccProfileSize,
        BmpInfoHeader infoHeader,
        Span<byte> buffer)
    {
        BmpFileHeader fileHeader = new(
            type: BmpConstants.TypeMarkers.Bitmap,
            fileSize: BmpFileHeader.Size + infoHeaderSize + colorPaletteSize + iccProfileSize + infoHeader.ImageSize,
            reserved: 0,
            offset: BmpFileHeader.Size + infoHeaderSize + colorPaletteSize);

        fileHeader.WriteTo(buffer);
        stream.Write(buffer, 0, BmpFileHeader.Size);
    }

    /// <summary>
    /// Writes the bitmap information header.
    /// </summary>
    /// <param name="stream">The stream to write info header into.</param>
    /// <param name="infoHeader">The information header.</param>
    /// <param name="buffer">The buffer.</param>
    /// <param name="infoHeaderSize">Size of the information header.</param>
    private void WriteBitmapInfoHeader(Stream stream, BmpInfoHeader infoHeader, Span<byte> buffer, int infoHeaderSize)
    {
        switch (this.infoHeaderType)
        {
            case BmpInfoHeaderType.WinVersion3:
                infoHeader.WriteV3Header(buffer);
                break;
            case BmpInfoHeaderType.WinVersion4:
                infoHeader.WriteV4Header(buffer);
                break;
            case BmpInfoHeaderType.WinVersion5:
                infoHeader.WriteV5Header(buffer);
                break;
        }

        stream.Write(buffer, 0, infoHeaderSize);
    }

    /// <summary>
    /// Writes the pixel data to the binary stream.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="configuration">The global configuration.</param>
    /// <param name="stream">The <see cref="Stream"/> to write to.</param>
    /// <param name="frame">
    /// The <see cref="ImageFrame{TPixel}"/> containing pixel data.
    /// </param>
    /// <param name="converter">The converter from sRGB to the output profile, or null for unchanged samples.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    private void WriteImage<TPixel>(
        Configuration configuration,
        Stream stream,
        ImageFrame<TPixel> frame,
        ColorProfileConverter? converter,
        CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ImageFrame<TPixel>? clonedFrame = null;
        try
        {
            // No need to clone when quantizing. The quantizer will do it for us.
            // TODO: We should really try to avoid the clone entirely.
            int bpp = this.bitsPerPixel != null ? (int)this.bitsPerPixel : 32;
            if (bpp > 8 && EncodingUtilities.ShouldReplaceTransparentPixels<TPixel>(this.transparentColorMode))
            {
                clonedFrame = frame.Clone();
                EncodingUtilities.ReplaceTransparentPixels(clonedFrame);
            }

            ImageFrame<TPixel> encodingFrame = clonedFrame ?? frame;
            Buffer2D<TPixel> pixels = encodingFrame.PixelBuffer;

            switch (this.bitsPerPixel)
            {
                case BmpBitsPerPixel.Bit32:
                    this.WriteDirectColorPixels<TPixel, Bgra32>(configuration, stream, pixels, converter, cancellationToken);
                    break;

                case BmpBitsPerPixel.Bit24:
                    this.WriteDirectColorPixels<TPixel, Bgr24>(configuration, stream, pixels, converter, cancellationToken);
                    break;

                case BmpBitsPerPixel.Bit16:
                    this.WriteDirectColorPixels<TPixel, Bgra5551>(configuration, stream, pixels, converter, cancellationToken);
                    break;

                case BmpBitsPerPixel.Bit8:
                    this.Write8BitIndexedPixels(configuration, stream, encodingFrame, converter, cancellationToken);
                    break;

                case BmpBitsPerPixel.Bit4:
                    this.Write4BitIndexedPixels(configuration, stream, encodingFrame, converter, cancellationToken);
                    break;

                case BmpBitsPerPixel.Bit2:
                    this.Write2BitIndexedPixels(configuration, stream, encodingFrame, converter, cancellationToken);
                    break;

                case BmpBitsPerPixel.Bit1:
                    this.Write1BitIndexedPixels(configuration, stream, encodingFrame, converter, cancellationToken);
                    break;
            }

            if (this.processedAlphaMask)
            {
                ProcessedAlphaMask(stream, encodingFrame);
            }
        }
        finally
        {
            clonedFrame?.Dispose();
        }
    }

    /// <summary>
    /// Allocates one packed BMP row, including its four-byte alignment padding.
    /// </summary>
    /// <param name="width">The number of pixels in the row.</param>
    /// <param name="bytesPerPixel">The packed sample size.</param>
    /// <returns>The owned row buffer.</returns>
    private IMemoryOwner<byte> AllocateRow(int width, int bytesPerPixel)
        => this.memoryAllocator.AllocatePaddedPixelRowBuffer(width, bytesPerPixel, this.padding);

    /// <summary>
    /// Writes bottom-up direct-color rows in the requested BMP sample layout.
    /// </summary>
    /// <typeparam name="TPixelFrom">The source pixel format.</typeparam>
    /// <typeparam name="TPixelTo">The packed BMP sample format.</typeparam>
    /// <param name="configuration">The pixel conversion configuration.</param>
    /// <param name="stream">The destination stream.</param>
    /// <param name="pixels">The source pixels, which remain unchanged.</param>
    /// <param name="converter">The converter from sRGB to the output profile, or null for unchanged samples.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    private void WriteDirectColorPixels<TPixelFrom, TPixelTo>(
        Configuration configuration,
        Stream stream,
        Buffer2D<TPixelFrom> pixels,
        ColorProfileConverter? converter,
        CancellationToken cancellationToken)
        where TPixelFrom : unmanaged, IPixel<TPixelFrom>
        where TPixelTo : unmanaged, IPixel<TPixelTo>
    {
        int width = pixels.Width;
        int bytesPerPixel = Unsafe.SizeOf<TPixelTo>();
        using IMemoryOwner<byte> row = this.AllocateRow(width, bytesPerPixel);
        Span<byte> rowSpan = row.GetSpan();
        Span<TPixelTo> packedRow = MemoryMarshal.Cast<byte, TPixelTo>(rowSpan[..(width * bytesPerPixel)]);
        PixelOperations<TPixelTo> pixelOperations = PixelOperations<TPixelTo>.Instance;

        // The destination's bulk From operation delegates to the existing BGR, BGRA or Bgra5551 source packer.
        // Keep alignment padding outside the pixel span, and allocate no ICC scratch for unchanged output.
        if (converter is null)
        {
            for (int y = pixels.Height - 1; y >= 0; y--)
            {
                cancellationToken.ThrowIfCancellationRequested();

                ReadOnlySpan<TPixelFrom> pixelSpan = pixels.DangerousGetRowSpan(y);
                pixelOperations.From(configuration, pixelSpan, packedRow);
                stream.Write(rowSpan);
            }

            return;
        }

        // Transform a separate floating row before the final sample packing. Reuse both buffers for every scanline.
        using IMemoryOwner<Vector4> vectorBuffer = this.memoryAllocator.Allocate<Vector4>(width);
        using IMemoryOwner<Rgb> rgbBuffer = this.memoryAllocator.Allocate<Rgb>(width);
        Span<Vector4> vectors = vectorBuffer.GetSpan();
        Span<Rgb> rgb = rgbBuffer.GetSpan();

        for (int y = pixels.Height - 1; y >= 0; y--)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ReadOnlySpan<TPixelFrom> pixelSpan = pixels.DangerousGetRowSpan(y);
            converter.Convert(configuration, pixelSpan, vectors, rgb);

            // ICC returns straight RGB and retains alpha. The destination pixel format performs the final quantization.
            pixelOperations.FromUnassociatedScaledVector4Destructive(configuration, vectors, packedRow);
            stream.Write(rowSpan);
        }
    }

    /// <summary>
    /// Writes eight-bit palette indices, using a fixed gray palette for eight-bit luminance input or quantizing other input.
    /// </summary>
    /// <typeparam name="TPixel">The type of the pixel.</typeparam>
    /// <param name="configuration">The global configuration.</param>
    /// <param name="stream">The <see cref="Stream"/> to write to.</param>
    /// <param name="encodingFrame"> The <see cref="ImageFrame{TPixel}"/> containing pixel data.</param>
    /// <param name="converter">The converter from sRGB to the output profile, or null for unchanged colors.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    private void Write8BitIndexedPixels<TPixel>(
        Configuration configuration,
        Stream stream,
        ImageFrame<TPixel> encodingFrame,
        ColorProfileConverter? converter,
        CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // This checks the source pixel format. Eight-bit BMP output can contain either grayscale or color palettes.
        PixelTypeInfo info = TPixel.GetPixelTypeInfo();
        bool is8BitLuminance =
            info.BitsPerPixel == 8
            && info.ColorType == PixelColorType.Luminance
            && info.AlphaRepresentation == PixelAlphaRepresentation.None
            && info.ComponentInfo!.Value.ComponentCount == 1;

        using IMemoryOwner<byte> colorPaletteBuffer = this.memoryAllocator.Allocate<byte>(ColorPaletteSize8Bit, AllocationOptions.Clean);
        Span<byte> colorPalette = colorPaletteBuffer.GetSpan();

        if (is8BitLuminance)
        {
            // The generated palette stores gray value i at entry i, so each source luminance byte is also its palette index.
            this.Write8BitIndexedGrayscalePixels(stream, encodingFrame, colorPalette, converter, cancellationToken);
        }
        else
        {
            // Other source formats need the quantizer to select palette colors and produce an index for each pixel.
            this.Write8BitIndexedColorPixels(stream, encodingFrame, colorPalette, converter, cancellationToken);
        }
    }

    /// <summary>
    /// Quantizes source colors and writes the resulting RGB palette and eight-bit indices.
    /// </summary>
    /// <typeparam name="TPixel">The type of the pixel.</typeparam>
    /// <param name="stream">The <see cref="Stream"/> to write to.</param>
    /// <param name="encodingFrame"> The <see cref="ImageFrame{TPixel}"/> containing pixel data.</param>
    /// <param name="colorPalette">A byte span of size 1024 for the color palette.</param>
    /// <param name="converter">The converter from sRGB to the output profile, or null for unchanged colors.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    private void Write8BitIndexedColorPixels<TPixel>(
        Stream stream,
        ImageFrame<TPixel> encodingFrame,
        Span<byte> colorPalette,
        ColorProfileConverter? converter,
        CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Configuration configuration = encodingFrame.Configuration;

        using IQuantizer<TPixel> frameQuantizer = this.quantizer.CreatePixelSpecificQuantizer<TPixel>(configuration);

        frameQuantizer.BuildPalette(this.pixelSamplingStrategy, encodingFrame);
        using IndexedImageFrame<TPixel> quantized = frameQuantizer.QuantizeFrame(encodingFrame, encodingFrame.Bounds);

        ReadOnlySpan<TPixel> quantizedColorPalette = quantized.Palette.Span;
        this.WriteColorPalette(configuration, stream, quantizedColorPalette, colorPalette, converter);

        for (int y = encodingFrame.Height - 1; y >= 0; y--)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ReadOnlySpan<byte> pixelSpan = quantized.DangerousGetRowSpan(y);
            stream.Write(pixelSpan);

            for (int i = 0; i < this.padding; i++)
            {
                stream.WriteByte(0);
            }
        }
    }

    /// <summary>
    /// Writes an RGB grayscale palette and uses the source luminance bytes directly as eight-bit indices.
    /// </summary>
    /// <typeparam name="TPixel">The type of the pixel.</typeparam>
    /// <param name="stream">The <see cref="Stream"/> to write to.</param>
    /// <param name="encodingFrame"> The <see cref="ImageFrame{TPixel}"/> containing pixel data.</param>
    /// <param name="colorPalette">A byte span of size 1024 for the color palette.</param>
    /// <param name="converter">The converter from sRGB to the output profile, or null for unchanged colors.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    private void Write8BitIndexedGrayscalePixels<TPixel>(
        Stream stream,
        ImageFrame<TPixel> encodingFrame,
        Span<byte> colorPalette,
        ColorProfileConverter? converter,
        CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Configuration configuration = encodingFrame.Configuration;

        // Entry i stores RGB (i, i, i). This order lets the source luminance bytes select their matching entries directly.
        for (int i = 0; i <= 255; i++)
        {
            int idx = i * 4;
            byte grayValue = (byte)i;
            colorPalette[idx] = grayValue;
            colorPalette[idx + 1] = grayValue;
            colorPalette[idx + 2] = grayValue;

            // Padding byte, always 0.
            colorPalette[idx + 3] = 0;
        }

        if (converter is null)
        {
            stream.Write(colorPalette);
        }
        else
        {
            // L8 samples are indices into this RGB ramp. Transform the table, leaving those indices unchanged.
            // BMP stores B, G, R and a reserved byte. The palette writer clears that fourth byte after RGB conversion.
            ReadOnlySpan<Bgra32> grayPalette = MemoryMarshal.Cast<byte, Bgra32>(colorPalette);
            this.WriteColorPalette(configuration, stream, grayPalette, colorPalette, converter);
        }

        Buffer2D<TPixel> imageBuffer = encodingFrame.PixelBuffer;
        for (int y = encodingFrame.Height - 1; y >= 0; y--)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ReadOnlySpan<TPixel> inputPixelRow = imageBuffer.DangerousGetRowSpan(y);

            // The caller selected single-component eight-bit luminance pixels. Their bytes index the gray ramp built above.
            // ICC changes the palette colors after index selection; it does not change which entry each pixel uses.
            ReadOnlySpan<byte> outputPixelRow = MemoryMarshal.AsBytes(inputPixelRow);
            stream.Write(outputPixelRow);

            for (int i = 0; i < this.padding; i++)
            {
                stream.WriteByte(0);
            }
        }
    }

    /// <summary>
    /// Writes an RGB palette with up to sixteen colors and packs two four-bit indices per byte.
    /// </summary>
    /// <typeparam name="TPixel">The type of the pixel.</typeparam>
    /// <param name="configuration">The global configuration.</param>
    /// <param name="stream">The <see cref="Stream"/> to write to.</param>
    /// <param name="encodingFrame"> The <see cref="ImageFrame{TPixel}"/> containing pixel data.</param>
    /// <param name="converter">The converter from sRGB to the output profile, or null for unchanged colors.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    private void Write4BitIndexedPixels<TPixel>(
        Configuration configuration,
        Stream stream,
        ImageFrame<TPixel> encodingFrame,
        ColorProfileConverter? converter,
        CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using IQuantizer<TPixel> frameQuantizer = this.quantizer.CreatePixelSpecificQuantizer<TPixel>(configuration, new QuantizerOptions
        {
            MaxColors = 16,
            Dither = this.quantizer.Options.Dither,
            DitherScale = this.quantizer.Options.DitherScale
        });

        frameQuantizer.BuildPalette(this.pixelSamplingStrategy, encodingFrame);

        using IndexedImageFrame<TPixel> quantized = frameQuantizer.QuantizeFrame(encodingFrame, encodingFrame.Bounds);
        using IMemoryOwner<byte> colorPaletteBuffer = this.memoryAllocator.Allocate<byte>(ColorPaletteSize4Bit, AllocationOptions.Clean);

        Span<byte> colorPalette = colorPaletteBuffer.GetSpan();
        ReadOnlySpan<TPixel> quantizedColorPalette = quantized.Palette.Span;
        this.WriteColorPalette(configuration, stream, quantizedColorPalette, colorPalette, converter);

        ReadOnlySpan<byte> pixelRowSpan = quantized.DangerousGetRowSpan(0);
        int rowPadding = pixelRowSpan.Length % 2 != 0 ? this.padding - 1 : this.padding;
        for (int y = encodingFrame.Height - 1; y >= 0; y--)
        {
            cancellationToken.ThrowIfCancellationRequested();

            pixelRowSpan = quantized.DangerousGetRowSpan(y);

            int endIdx = pixelRowSpan.Length % 2 == 0 ? pixelRowSpan.Length : pixelRowSpan.Length - 1;

            // BMP stores the first index in the high four bits and the second in the low four bits.
            for (int i = 0; i < endIdx; i += 2)
            {
                stream.WriteByte((byte)((pixelRowSpan[i] << 4) | pixelRowSpan[i + 1]));
            }

            if (pixelRowSpan.Length % 2 != 0)
            {
                // An odd-width row leaves the final byte's low four bits unused.
                stream.WriteByte((byte)((pixelRowSpan[^1] << 4) | 0));
            }

            for (int i = 0; i < rowPadding; i++)
            {
                stream.WriteByte(0);
            }
        }
    }

    /// <summary>
    /// Writes an RGB palette with up to four colors and packs four two-bit indices per byte.
    /// </summary>
    /// <typeparam name="TPixel">The type of the pixel.</typeparam>
    /// <param name="configuration">The global configuration.</param>
    /// <param name="stream">The <see cref="Stream"/> to write to.</param>
    /// <param name="encodingFrame"> The <see cref="ImageFrame{TPixel}"/> containing pixel data.</param>
    /// <param name="converter">The converter from sRGB to the output profile, or null for unchanged colors.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    private void Write2BitIndexedPixels<TPixel>(
        Configuration configuration,
        Stream stream,
        ImageFrame<TPixel> encodingFrame,
        ColorProfileConverter? converter,
        CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using IQuantizer<TPixel> frameQuantizer = this.quantizer.CreatePixelSpecificQuantizer<TPixel>(configuration, new QuantizerOptions
        {
            MaxColors = 4,
            Dither = this.quantizer.Options.Dither,
            DitherScale = this.quantizer.Options.DitherScale
        });

        frameQuantizer.BuildPalette(this.pixelSamplingStrategy, encodingFrame);

        using IndexedImageFrame<TPixel> quantized = frameQuantizer.QuantizeFrame(encodingFrame, encodingFrame.Bounds);
        using IMemoryOwner<byte> colorPaletteBuffer = this.memoryAllocator.Allocate<byte>(ColorPaletteSize2Bit, AllocationOptions.Clean);

        Span<byte> colorPalette = colorPaletteBuffer.GetSpan();
        ReadOnlySpan<TPixel> quantizedColorPalette = quantized.Palette.Span;
        this.WriteColorPalette(configuration, stream, quantizedColorPalette, colorPalette, converter);

        ReadOnlySpan<byte> pixelRowSpan = quantized.DangerousGetRowSpan(0);
        int rowPadding = pixelRowSpan.Length % 4 != 0 ? this.padding - 1 : this.padding;
        for (int y = encodingFrame.Height - 1; y >= 0; y--)
        {
            cancellationToken.ThrowIfCancellationRequested();

            pixelRowSpan = quantized.DangerousGetRowSpan(y);

            int endIdx = pixelRowSpan.Length % 4 == 0 ? pixelRowSpan.Length : pixelRowSpan.Length - 4;
            int i = 0;

            // Each byte stores four indices in bits 7..6, 5..4, 3..2 and 1..0, in pixel order.
            for (i = 0; i < endIdx; i += 4)
            {
                stream.WriteByte((byte)((pixelRowSpan[i] << 6) | (pixelRowSpan[i + 1] << 4) | (pixelRowSpan[i + 2] << 2) | pixelRowSpan[i + 3]));
            }

            if (pixelRowSpan.Length % 4 != 0)
            {
                // Pack the remaining indices from the high bits downward. Unused low bits remain zero.
                int shift = 6;
                byte pixelData = 0;
                for (; i < pixelRowSpan.Length; i++)
                {
                    pixelData = (byte)(pixelData | (pixelRowSpan[i] << shift));
                    shift -= 2;
                }

                stream.WriteByte(pixelData);
            }

            for (i = 0; i < rowPadding; i++)
            {
                stream.WriteByte(0);
            }
        }
    }

    /// <summary>
    /// Writes an RGB palette with up to two colors and packs eight one-bit indices per byte.
    /// </summary>
    /// <typeparam name="TPixel">The type of the pixel.</typeparam>
    /// <param name="configuration">The global configuration.</param>
    /// <param name="stream">The <see cref="Stream"/> to write to.</param>
    /// <param name="encodingFrame"> The <see cref="ImageFrame{TPixel}"/> containing pixel data.</param>
    /// <param name="converter">The converter from sRGB to the output profile, or null for unchanged colors.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    private void Write1BitIndexedPixels<TPixel>(
        Configuration configuration,
        Stream stream,
        ImageFrame<TPixel> encodingFrame,
        ColorProfileConverter? converter,
        CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using IQuantizer<TPixel> frameQuantizer = this.quantizer.CreatePixelSpecificQuantizer<TPixel>(configuration, new QuantizerOptions
        {
            MaxColors = 2,
            Dither = this.quantizer.Options.Dither,
            DitherScale = this.quantizer.Options.DitherScale
        });

        frameQuantizer.BuildPalette(this.pixelSamplingStrategy, encodingFrame);

        using IndexedImageFrame<TPixel> quantized = frameQuantizer.QuantizeFrame(encodingFrame, encodingFrame.Bounds);
        using IMemoryOwner<byte> colorPaletteBuffer = this.memoryAllocator.Allocate<byte>(ColorPaletteSize1Bit, AllocationOptions.Clean);

        Span<byte> colorPalette = colorPaletteBuffer.GetSpan();
        ReadOnlySpan<TPixel> quantizedColorPalette = quantized.Palette.Span;
        this.WriteColorPalette(configuration, stream, quantizedColorPalette, colorPalette, converter);

        ReadOnlySpan<byte> quantizedPixelRow = quantized.DangerousGetRowSpan(0);
        int rowPadding = quantizedPixelRow.Length % 8 != 0 ? this.padding - 1 : this.padding;
        for (int y = encodingFrame.Height - 1; y >= 0; y--)
        {
            cancellationToken.ThrowIfCancellationRequested();

            quantizedPixelRow = quantized.DangerousGetRowSpan(y);

            int endIdx = quantizedPixelRow.Length % 8 == 0 ? quantizedPixelRow.Length : quantizedPixelRow.Length - 8;
            for (int i = 0; i < endIdx; i += 8)
            {
                Write1BitIndices(stream, i, i + 8, quantizedPixelRow);
            }

            if (quantizedPixelRow.Length % 8 != 0)
            {
                int startIdx = quantizedPixelRow.Length - (quantizedPixelRow.Length % 8);
                endIdx = quantizedPixelRow.Length;
                Write1BitIndices(stream, startIdx, endIdx, quantizedPixelRow);
            }

            for (int i = 0; i < rowPadding; i++)
            {
                stream.WriteByte(0);
            }
        }
    }

    /// <summary>
    /// Writes the color palette to the stream. The color palette has 4 bytes for each entry.
    /// </summary>
    /// <typeparam name="TPixel">The type of the pixel.</typeparam>
    /// <param name="configuration">The global configuration.</param>
    /// <param name="stream">The <see cref="Stream"/> to write to.</param>
    /// <param name="quantizedColorPalette">The color palette from the quantized image.</param>
    /// <param name="colorPalette">A temporary byte span to write the color palette to.</param>
    /// <param name="converter">The converter from sRGB to the output profile, or null for unchanged colors.</param>
    private void WriteColorPalette<TPixel>(
        Configuration configuration,
        Stream stream,
        ReadOnlySpan<TPixel> quantizedColorPalette,
        Span<byte> colorPalette,
        ColorProfileConverter? converter)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        int quantizedColorBytes = quantizedColorPalette.Length * 4;

        // Bgra32 provides the B, G and R byte order required by BMP. Its alpha byte becomes BMP's reserved byte below.
        Span<Bgra32> packedPalette = MemoryMarshal.Cast<byte, Bgra32>(colorPalette[..quantizedColorBytes]);
        if (converter is null)
        {
            PixelOperations<TPixel>.Instance.ToBgra32(configuration, quantizedColorPalette, packedPalette);
        }
        else
        {
            // Quantization uses sRGB colors. Convert only this output copy before packing the BMP RGB table.
            using IMemoryOwner<Vector4> vectors = this.memoryAllocator.Allocate<Vector4>(quantizedColorPalette.Length);
            using IMemoryOwner<Rgb> scratch = this.memoryAllocator.Allocate<Rgb>(quantizedColorPalette.Length);

            converter.Convert(configuration, quantizedColorPalette, vectors.GetSpan(), scratch.GetSpan());
            PixelOperations<Bgra32>.Instance.FromUnassociatedScaledVector4Destructive(configuration, vectors.GetSpan(), packedPalette);
        }

        // BMP palette entries have no alpha. Clear each reserved byte, including unused entries in the output table.
        Span<uint> colorPaletteAsUInt = MemoryMarshal.Cast<byte, uint>(colorPalette);
        for (int i = 0; i < colorPaletteAsUInt.Length; i++)
        {
            colorPaletteAsUInt[i] &= 0x00FFFFFF;
        }

        stream.Write(colorPalette);
    }

    /// <summary>
    /// Packs up to eight palette indices into one byte, starting at the most significant bit.
    /// </summary>
    /// <param name="stream">The stream to write the palette to.</param>
    /// <param name="startIdx">The start index.</param>
    /// <param name="endIdx">The end index.</param>
    /// <param name="quantizedPixelRow">A quantized pixel row.</param>
    private static void Write1BitIndices(Stream stream, int startIdx, int endIdx, ReadOnlySpan<byte> quantizedPixelRow)
    {
        // The first pixel occupies bit 7. A partial group leaves the remaining low bits zero.
        int shift = 7;
        byte indices = 0;
        for (int j = startIdx; j < endIdx; j++)
        {
            indices = (byte)(indices | ((byte)(quantizedPixelRow[j] & 1) << shift));
            shift--;
        }

        stream.WriteByte(indices);
    }

    /// <summary>
    /// Writes the bottom-up 1-bit transparency mask required by an ICO/CUR bitmap resource.
    /// </summary>
    /// <typeparam name="TPixel">The source pixel type.</typeparam>
    /// <param name="stream">The destination stream.</param>
    /// <param name="encodingFrame">The source frame.</param>
    private static void ProcessedAlphaMask<TPixel>(Stream stream, ImageFrame<TPixel> encodingFrame)
          where TPixel : unmanaged, IPixel<TPixel>
    {
        // Each byte represents eight pixels and every scanline is padded to a 4-byte DIB boundary.
        int arrayWidth = (encodingFrame.Width + 7) / 8;
        int padding = arrayWidth % 4;
        if (padding is not 0)
        {
            padding = 4 - padding;
        }

        Span<byte> mask = stackalloc byte[arrayWidth];
        Span<byte> paddingBytes = stackalloc byte[3];
        paddingBytes.Clear();

        for (int y = encodingFrame.Height - 1; y >= 0; y--)
        {
            mask.Clear();
            Span<TPixel> row = encodingFrame.PixelBuffer.DangerousGetRowSpan(y);

            for (int i = 0; i < arrayWidth; i++)
            {
                int x = i * 8;

                // The final byte can represent fewer than eight pixels when the image width is not byte-aligned.
                int pixelCount = Math.Min(8, encodingFrame.Width - x);
                for (int j = 0; j < pixelCount; j++)
                {
                    WriteAlphaMask(row[x + j], ref mask[i], j);
                }
            }

            stream.Write(mask);

            // Alpha-mask rows are DWORD-aligned, and the final row padding must extend the stream.
            stream.Write(paddingBytes[..padding]);
        }
    }

    /// <summary>
    /// Sets one most-significant-bit-first transparency flag in an ICO/CUR AND-mask byte.
    /// </summary>
    /// <typeparam name="TPixel">The source pixel type.</typeparam>
    /// <param name="pixel">The source pixel.</param>
    /// <param name="mask">The destination mask byte.</param>
    /// <param name="index">The pixel index within the byte.</param>
    private static void WriteAlphaMask<TPixel>(in TPixel pixel, ref byte mask, in int index)
          where TPixel : unmanaged, IPixel<TPixel>
    {
        Rgba32 rgba = pixel.ToRgba32();
        if (rgba.A is 0)
        {
            mask |= unchecked((byte)(0b10000000 >> index));
        }
    }
}
