// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Buffers.Binary;
using System.Numerics;
using System.Numerics.Tensors;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Compression.Ccitt;
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
    /// Used for allocating memory during processing operations.
    /// </summary>
    private readonly MemoryAllocator memoryAllocator;

    /// <summary>
    /// The color type from the encoder options, or <see langword="null"/> to use the image metadata.
    /// </summary>
    private readonly BmpColorType? configuredColorType;

    /// <summary>
    /// The bit depth from the encoder options, or <see langword="null"/> to use the image metadata.
    /// </summary>
    private readonly BmpBitsPerPixel? configuredBitsPerPixel;

    /// <summary>
    /// The compression from the encoder options, or <see langword="null"/> to use the image metadata.
    /// </summary>
    private readonly BmpCompression? configuredCompression;

    /// <summary>
    /// The amount to pad each row by.
    /// </summary>
    private int padding;

    /// <summary>
    /// The color type of the image being encoded.
    /// </summary>
    private BmpColorType colorType;

    /// <summary>
    /// Whether the encoder writes the indexed rows as RLE8 or RLE4 data. Only 8-bit and 4-bit palette bitmaps can use
    /// RLE.
    /// </summary>
    private bool runLength;

    /// <summary>
    /// Whether the encoder writes the indexed rows as Huffman 1D data in an OS/2 2.x bitmap. Only 1-bit palette bitmaps
    /// can use Huffman 1D.
    /// </summary>
    private bool huffman1D;

    /// <summary>
    /// The color depth of the image being encoded, in number of bits per pixel. It always fits <see cref="colorType"/>.
    /// </summary>
    private BmpBitsPerPixel bitsPerPixel;

    /// <summary>
    /// The info header version. The alpha color types write a V4 header, which holds an alpha mask. An ICC profile
    /// needs a V5 header. Huffman 1D needs an OS/2 2.x header. Other images write a V3 header, which almost all decoders
    /// read.
    /// </summary>
    private BmpInfoHeaderType infoHeaderType;

    /// <summary>
    /// The quantizer from the encoder options, or <see langword="null"/> to select one from the image metadata.
    /// </summary>
    private readonly IQuantizer? configuredQuantizer;

    /// <summary>
    /// The quantizer for reducing the color count in one-, two-, four- and eight-bit indexed images.
    /// <see cref="Encode{TPixel}(ImageFrame{TPixel}, ImageMetadata, Stream, CancellationToken)"/> selects it for each
    /// image before it writes the pixels.
    /// </summary>
    private IQuantizer quantizer = KnownQuantizers.Hexadecatree;

    /// <summary>
    /// The options of <see cref="quantizer"/> with the transparent color mode of the encoder.
    /// <see cref="Encode{TPixel}(ImageFrame{TPixel}, ImageMetadata, Stream, CancellationToken)"/> sets them with the
    /// quantizer.
    /// </summary>
    private QuantizerOptions quantizerOptions = KnownQuantizers.Hexadecatree.Options;

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
        this.configuredColorType = encoder.ColorType;
        this.configuredBitsPerPixel = encoder.BitsPerPixel;
        this.configuredCompression = encoder.Compression;

        this.configuredQuantizer = encoder.Quantizer;
        this.pixelSamplingStrategy = encoder.PixelSamplingStrategy;
        this.transparentColorMode = encoder.TransparentColorMode;
        this.processedAlphaMask = encoder.ProcessedAlphaMask;
        this.skipFileHeader = encoder.SkipFileHeader;
        this.skipMetadata = encoder.SkipMetadata;
        this.isDoubleHeight = encoder.UseDoubleHeight;
    }

    /// <summary>
    /// Gets the zero bytes for the padding of an 8-bit row. That padding is 0 to 3 bytes.
    /// </summary>
    private static ReadOnlySpan<byte> PaddingBytes => [0, 0, 0];

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
        IccProfile? profile = IccProfile.SelectValidProfile(frame.Metadata.IccProfile, metadata.IccProfile);
        this.Encode(frame, metadata, profile, stream, cancellationToken);
    }

    /// <summary>
    /// Encodes a frame with an ICC profile that the caller has already validated.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="frame">The source frame.</param>
    /// <param name="metadata">The image metadata.</param>
    /// <param name="sourceIccProfile">The validated ICC profile, or null.</param>
    /// <param name="stream">The destination stream.</param>
    /// <param name="cancellationToken">The token to request cancellation.</param>
    public void Encode<TPixel>(
        ImageFrame<TPixel> frame,
        ImageMetadata metadata,
        IccProfile? sourceIccProfile,
        Stream stream,
        CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // ICC offsets start at the DIB header, whether it follows a BMP file header or sits inside an ICO/CUR resource.
        long infoHeaderPosition = stream.Position + (this.skipFileHeader ? 0 : BmpFileHeader.Size);

        Configuration configuration = frame.Configuration;
        BmpMetadata bmpMetadata = metadata.GetBmpMetadata();

        // As in the PNG encoder, the encoder options take precedence over the metadata. A bit depth that does not fit the
        // color type falls back to the default depth of that color type.
        this.colorType = this.configuredColorType ?? bmpMetadata.ColorType;
        this.bitsPerPixel = BmpConstants.GetBitsPerPixel(this.colorType, this.configuredBitsPerPixel ?? bmpMetadata.BitsPerPixel);
        this.infoHeaderType = this.colorType is BmpColorType.Bgra or BmpColorType.Bgra5551
            ? BmpInfoHeaderType.WinVersion4
            : BmpInfoHeaderType.WinVersion3;

        // BI_RLE8 compresses 8-bit bitmaps and BI_RLE4 compresses 4-bit bitmaps. Windows defines RLE for no other depth:
        // https://learn.microsoft.com/en-us/windows/win32/gdi/bitmap-compression
        // The encoder does not write OS/2 RLE24, so it writes the bitmaps of other depths uncompressed.
        BmpCompression compression = this.configuredCompression ?? bmpMetadata.Compression;
        this.runLength = compression is BmpCompression.RunLength && this.bitsPerPixel is BmpBitsPerPixel.Bit8 or BmpBitsPerPixel.Bit4;

        ushort bpp = (ushort)this.bitsPerPixel;

        // A row has its pixel bytes, then zero padding to a multiple of 4 bytes. A row of 1, 2 or 4-bit indices can end
        // inside a byte. That last byte holds pixels, so it is not padding.
        int bytesPerLine = (int)(4 * ((((uint)frame.Width * bpp) + 31) / 32));
        this.padding = bytesPerLine - (int)((((uint)frame.Width * bpp) + 7) / 8);

        // An indexed bitmap has a color table of 2^bpp entries of 4 bytes. A direct-color bitmap has no color table.
        int colorPaletteSize = bpp <= 8 ? 4 << bpp : 0;

        // Without a configured quantizer, an indexed image keeps the color table of its decoded bitmap when the table fits
        // the output depth. The PNG, GIF and icon encoders do the same. Dithering stays off and color matching is exact,
        // so each pixel that has a palette color keeps that color. Other images use the default quantizer.
        ReadOnlyMemory<Color>? colorTable = bmpMetadata.ColorTable;
        if (this.configuredQuantizer is not null)
        {
            this.quantizer = this.configuredQuantizer;
        }
        else if (colorPaletteSize > 0 && colorTable?.Length > 0 && colorTable.Value.Length <= 1 << bpp)
        {
            this.quantizer = EncodingUtilities.CreateDecodedPaletteQuantizer(colorTable.Value, this.transparentColorMode);
        }
        else
        {
            this.quantizer = KnownQuantizers.Hexadecatree;
        }

        // The indexed paths quantize with these options, so the transparent color mode of the encoder applies to every
        // quantizer, as the GIF encoder does.
        TransparentColorMode mode = this.transparentColorMode;
        this.quantizerOptions = this.quantizer.Options.DeepClone(o => o.TransparentColorMode = mode);

        // Direct samples and palette entries store RGB. Indexed compatibility is determined by the palette, not its indices.
        PixelColorType pixelColorType = colorPaletteSize > 0 ? PixelColorType.Indexed : PixelColorType.RGB;
        IccProfile? outputIccProfile = IccProfile.GetCompatibleProfileForEncoding(
            sourceIccProfile,
            pixelColorType,
            this.memoryAllocator,
            out ColorProfileConverter? converter);

        // ICC describes the stored colors and must remain even when optional metadata is skipped.
        ReadOnlySpan<byte> iccProfileData = outputIccProfile is null ? default : outputIccProfile.GetData().Span;
        int iccProfileSize = iccProfileData.Length;
        if (!iccProfileData.IsEmpty)
        {
            this.infoHeaderType = BmpInfoHeaderType.WinVersion5;
        }

        // Huffman 1D compresses 1-bit bitmaps, and only the OS/2 2.x header defines it. That header has no field for an
        // ICC profile, so the encoder writes a bitmap with a profile uncompressed, with the V5 header. See
        // BITMAPINFOHEADER2: http://www.edm2.com/index.php/BITMAPINFOHEADER2
        this.huffman1D = compression is BmpCompression.Huffman1D && this.bitsPerPixel is BmpBitsPerPixel.Bit1 && iccProfileData.IsEmpty;
        if (this.huffman1D)
        {
            this.infoHeaderType = BmpInfoHeaderType.Os2Version2;
        }

        int infoHeaderSize = this.infoHeaderType switch
        {
            BmpInfoHeaderType.WinVersion3 => BmpInfoHeader.SizeV3,
            BmpInfoHeaderType.WinVersion4 => BmpInfoHeader.SizeV4,
            BmpInfoHeaderType.WinVersion5 => BmpInfoHeader.SizeV5,
            BmpInfoHeaderType.Os2Version2 => BmpInfoHeader.Os2v2Size,
            _ => BmpInfoHeader.SizeV3
        };

        // ICO/CUR DIB headers include the XOR bitmap and following AND mask in one doubled height.
        int height = frame.Height;
        if (this.isDoubleHeight)
        {
            height <<= 1;
        }

        BmpInfoHeader infoHeader = this.CreateBmpInfoHeader(frame.Width, height, infoHeaderSize, bpp, bytesPerLine, metadata, iccProfileData);

        // A V3 header has no mask fields. With BI_BITFIELDS, the red, green and blue masks follow the header in the
        // place of the color table. V4 and V5 headers hold the masks.
        int bitFieldsSize =
            this.infoHeaderType is BmpInfoHeaderType.WinVersion3 && infoHeader.Compression is BmpInfoCompression.BitFields
            ? 3 * sizeof(int)
            : 0;

        // The size fields of the headers are 32-bit signed integers in this encoder and in its decoder. Compressed data has
        // its size only after the encode, so Encode checks it then.
        bool compressed = this.runLength || this.huffman1D;
        long dataSize = compressed ? 0 : (long)height * bytesPerLine;
        if (BmpFileHeader.Size + infoHeaderSize + bitFieldsSize + colorPaletteSize + iccProfileSize + dataSize > int.MaxValue)
        {
            throw new ImageFormatException("Image is too large to encode in BMP format.");
        }

        // The largest header is the 124-byte V5 header. The V3 mask block adds 12 bytes to the 40-byte V3 header. The OS/2
        // 2.x header has 64 bytes.
        Span<byte> buffer = stackalloc byte[infoHeaderSize + bitFieldsSize];

        // ICO/CUR resources contain a DIB directly; standalone BMP files additionally require BITMAPFILEHEADER.
        if (!this.skipFileHeader)
        {
            WriteBitmapFileHeader(stream, infoHeaderSize, colorPaletteSize + bitFieldsSize, iccProfileSize, infoHeader, buffer);
        }

        this.WriteBitmapInfoHeader(stream, infoHeader, buffer, infoHeaderSize, bitFieldsSize);
        this.WriteImage(configuration, stream, frame, converter, cancellationToken);

        if (compressed)
        {
            // The size of compressed data is known only after the encode. The output stream is seekable, so write the size
            // into the headers now. The pixel data starts after the info header and the color table.
            long end = stream.Position;
            long compressedSize = end - (infoHeaderPosition + infoHeaderSize + colorPaletteSize);
            if (BmpFileHeader.Size + infoHeaderSize + colorPaletteSize + iccProfileSize + compressedSize > int.MaxValue)
            {
                throw new ImageFormatException("Image is too large to encode in BMP format.");
            }

            int imageSize = (int)compressedSize;

            // biSizeImage (cbImage in the OS/2 2.x header) is at byte 20 of the info header.
            BinaryPrimitives.WriteInt32LittleEndian(buffer, imageSize);
            stream.Position = infoHeaderPosition + 20;
            stream.Write(buffer[..4]);

            if (!this.skipFileHeader)
            {
                // bfSize is at byte 2 of the file header. The ICC profile follows the pixel data.
                int fileSize = BmpFileHeader.Size + infoHeaderSize + colorPaletteSize + imageSize + iccProfileSize;
                BinaryPrimitives.WriteInt32LittleEndian(buffer, fileSize);
                stream.Position = infoHeaderPosition - BmpFileHeader.Size + 2;
                stream.Write(buffer[..4]);
            }

            stream.Position = end;
        }

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
    /// <param name="iccProfileData">The encoded ICC profile, or empty when no compatible profile is selected.</param>
    /// <returns>The bitmap information header.</returns>
    private BmpInfoHeader CreateBmpInfoHeader(
        int width,
        int height,
        int infoHeaderSize,
        ushort bpp,
        int bytesPerLine,
        ImageMetadata metadata,
        ReadOnlySpan<byte> iccProfileData)
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

        // Compressed data has its size only after the encode. Encode writes the size into the headers then.
        if (this.runLength)
        {
            infoHeader.Compression = bpp == 8 ? BmpInfoCompression.Rle8 : BmpInfoCompression.Rle4;
            infoHeader.ImageSize = 0;
        }
        else if (this.huffman1D)
        {
            infoHeader.Compression = BmpInfoCompression.Huffman1D;
            infoHeader.ImageSize = 0;
        }

        // The color types with masks use BI_BITFIELDS. Palette and RGB use BI_RGB, which has fixed layouts.
        switch (this.colorType)
        {
            case BmpColorType.Bgra:
                infoHeader.AlphaMask = Rgba32AlphaMask;
                infoHeader.RedMask = Rgba32RedMask;
                infoHeader.GreenMask = Rgba32GreenMask;
                infoHeader.BlueMask = Rgba32BlueMask;
                infoHeader.Compression = BmpInfoCompression.BitFields;
                break;
            case BmpColorType.Bgr565:
                infoHeader.RedMask = BmpConstants.Bgr565Masks.Red;
                infoHeader.GreenMask = BmpConstants.Bgr565Masks.Green;
                infoHeader.BlueMask = BmpConstants.Bgr565Masks.Blue;
                infoHeader.Compression = BmpInfoCompression.BitFields;
                break;
            case BmpColorType.Bgra5551:
                infoHeader.AlphaMask = BmpConstants.Bgr555Masks.Alpha;
                infoHeader.RedMask = BmpConstants.Bgr555Masks.Red;
                infoHeader.GreenMask = BmpConstants.Bgr555Masks.Green;
                infoHeader.BlueMask = BmpConstants.Bgr555Masks.Blue;
                infoHeader.Compression = BmpInfoCompression.BitFields;
                break;
        }

        if (this.infoHeaderType is BmpInfoHeaderType.WinVersion5 && !iccProfileData.IsEmpty)
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
    /// <param name="iccProfileData">The read-only profile bytes, or empty to omit the profile.</param>
    /// <param name="buffer">The buffer.</param>
    /// <param name="infoHeaderPosition">The absolute stream position where the DIB header begins.</param>
    private static void WriteColorProfile(Stream stream, ReadOnlySpan<byte> iccProfileData, Span<byte> buffer, long infoHeaderPosition)
    {
        if (!iccProfileData.IsEmpty)
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
    /// <param name="colorTableSize">The size in bytes of the color table: the palette, or the V3 bit-field masks.</param>
    /// <param name="iccProfileSize">The size in bytes of the color profile.</param>
    /// <param name="infoHeader">The information header to write.</param>
    /// <param name="buffer">The buffer to write to.</param>
    private static void WriteBitmapFileHeader(
        Stream stream,
        int infoHeaderSize,
        int colorTableSize,
        int iccProfileSize,
        BmpInfoHeader infoHeader,
        Span<byte> buffer)
    {
        BmpFileHeader fileHeader = new(
            type: BmpConstants.TypeMarkers.Bitmap,
            fileSize: BmpFileHeader.Size + infoHeaderSize + colorTableSize + iccProfileSize + infoHeader.ImageSize,
            reserved: 0,
            offset: BmpFileHeader.Size + infoHeaderSize + colorTableSize);

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
    /// <param name="bitFieldsSize">The size in bytes of the mask block after a V3 header, or 0 when it has no masks.</param>
    private void WriteBitmapInfoHeader(Stream stream, BmpInfoHeader infoHeader, Span<byte> buffer, int infoHeaderSize, int bitFieldsSize)
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
            case BmpInfoHeaderType.Os2Version2:
                infoHeader.WriteOs2Version2Header(buffer);
                break;
        }

        // With BI_BITFIELDS, the color table of a V3 bitmap holds three masks, in the order red, green, blue.
        if (bitFieldsSize > 0)
        {
            Span<byte> masks = buffer.Slice(infoHeaderSize, bitFieldsSize);
            BinaryPrimitives.WriteInt32LittleEndian(masks[..4], infoHeader.RedMask);
            BinaryPrimitives.WriteInt32LittleEndian(masks.Slice(4, 4), infoHeader.GreenMask);
            BinaryPrimitives.WriteInt32LittleEndian(masks.Slice(8, 4), infoHeader.BlueMask);
        }

        stream.Write(buffer[..(infoHeaderSize + bitFieldsSize)]);
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
        // The indexed paths quantize, and the quantizer applies the transparent color mode. The direct-color paths
        // replace transparent pixels in each converted row, so the source frame stays unchanged without a copy.
        bool replaceTransparent = EncodingUtilities.ShouldReplaceTransparentPixels<TPixel>(this.transparentColorMode);
        Buffer2D<TPixel> pixels = frame.PixelBuffer;

        switch (this.bitsPerPixel)
        {
            case BmpBitsPerPixel.Bit32:
                // BI_RGB does not use the fourth byte of a 32-bit pixel, so a BGR pixel stores 0 there. See BITMAPINFOHEADER,
                // biBitCount: https://learn.microsoft.com/en-us/previous-versions/dd183376(v=vs.85)
                // An icon bitmap is BI_RGB too, but it keeps alpha in the fourth byte, as icon readers expect.
                this.WriteDirectColorPixels<TPixel, Bgra32>(
                    configuration,
                    stream,
                    pixels,
                    converter,
                    replaceTransparent,
                    this.colorType is BmpColorType.Bgr && !this.isDoubleHeight,
                    cancellationToken);
                break;

            case BmpBitsPerPixel.Bit24:
                this.WriteDirectColorPixels<TPixel, Bgr24>(configuration, stream, pixels, converter, replaceTransparent, false, cancellationToken);
                break;

            case BmpBitsPerPixel.Bit16 when this.colorType is BmpColorType.Bgr565:
                this.WriteDirectColorPixels<TPixel, Bgr565>(configuration, stream, pixels, converter, replaceTransparent, false, cancellationToken);
                break;

            case BmpBitsPerPixel.Bit16:
                // A BI_RGB 16-bit pixel is 5-5-5, and BI_RGB does not use its high bit, so a BGR pixel stores 0 there. The
                // 5-5-5-1 color type keeps alpha in the high bit with its alpha mask. See BITMAPINFOHEADER, biBitCount:
                // https://learn.microsoft.com/en-us/previous-versions/dd183376(v=vs.85)
                this.WriteDirectColorPixels<TPixel, Bgra5551>(
                    configuration,
                    stream,
                    pixels,
                    converter,
                    replaceTransparent,
                    this.colorType is BmpColorType.Bgr,
                    cancellationToken);
                break;

            case BmpBitsPerPixel.Bit8:
                this.Write8BitIndexedPixels(configuration, stream, frame, converter, cancellationToken);
                break;

            case BmpBitsPerPixel.Bit4:
            case BmpBitsPerPixel.Bit2:
            case BmpBitsPerPixel.Bit1:
                this.WriteIndexedColorPixels(configuration, stream, frame, (int)this.bitsPerPixel, converter, cancellationToken);
                break;
        }

        // The mask stores alpha only. Replacement keeps alpha, so the source frame gives the same mask.
        if (this.processedAlphaMask)
        {
            this.ProcessedAlphaMask(stream, frame, cancellationToken);
        }
    }

    /// <summary>
    /// Allocates one BMP row: its pixel bytes, then its alignment padding.
    /// </summary>
    /// <param name="pixelBytes">The byte count of the pixels in the row. It includes a last byte that holds fewer pixels.</param>
    /// <returns>The owned row buffer.</returns>
    private IMemoryOwner<byte> AllocateRow(int pixelBytes)
    {
        // Pooled memory can hold old data. The row writers write only the pixel bytes, so a clean buffer keeps each
        // padding byte zero for all rows.
        return this.memoryAllocator.Allocate<byte>(pixelBytes + this.padding, AllocationOptions.Clean);
    }

    /// <summary>
    /// Writes bottom-up direct-color rows in the requested BMP sample layout.
    /// </summary>
    /// <typeparam name="TPixelFrom">The source pixel format.</typeparam>
    /// <typeparam name="TPixelTo">The packed BMP sample format.</typeparam>
    /// <param name="configuration">The pixel conversion configuration.</param>
    /// <param name="stream">The destination stream.</param>
    /// <param name="pixels">The source pixels, which remain unchanged.</param>
    /// <param name="converter">The converter from sRGB to the output profile, or null for unchanged samples.</param>
    /// <param name="replaceTransparent">Whether pixels with alpha 0 are written as transparent black.</param>
    /// <param name="clearUnusedAlpha">
    /// Whether the alpha part of each pixel is written as 0. The alpha part is the fourth byte of a 32-bit pixel, or the
    /// high bit of a 16-bit pixel.
    /// </param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    private void WriteDirectColorPixels<TPixelFrom, TPixelTo>(
        Configuration configuration,
        Stream stream,
        Buffer2D<TPixelFrom> pixels,
        ColorProfileConverter? converter,
        bool replaceTransparent,
        bool clearUnusedAlpha,
        CancellationToken cancellationToken)
        where TPixelFrom : unmanaged, IPixel<TPixelFrom>
        where TPixelTo : unmanaged, IPixel<TPixelTo>
    {
        int width = pixels.Width;
        int bytesPerPixel = Unsafe.SizeOf<TPixelTo>();
        using IMemoryOwner<byte> row = this.AllocateRow(width * bytesPerPixel);
        Span<byte> rowSpan = row.GetSpan();
        Span<TPixelTo> packedRow = MemoryMarshal.Cast<byte, TPixelTo>(rowSpan[..(width * bytesPerPixel)]);

        // A 32-bit pixel is one little-endian uint with the fourth byte in bits 24..31, so an AND with 0x00FFFFFF clears
        // that byte. A 16-bit pixel is one little-endian ushort with alpha in bit 15, so an AND with 0x7FFF clears it.
        Span<byte> packedBytes = rowSpan[..(width * bytesPerPixel)];
        PixelOperations<TPixelTo> pixelOperations = PixelOperations<TPixelTo>.Instance;
        PixelOperations<TPixelFrom> sourceOperations = PixelOperations<TPixelFrom>.Instance;

        // The destination's bulk From operation delegates to the existing BGR, BGRA, Bgra5551 or Bgr565 source packer.
        // Keep alignment padding outside the pixel span, and allocate no ICC scratch for unchanged output.
        if (converter is null && !replaceTransparent)
        {
            for (int y = pixels.Height - 1; y >= 0; y--)
            {
                cancellationToken.ThrowIfCancellationRequested();

                pixelOperations.From(configuration, pixels.DangerousGetRowSpan(y), packedRow);
                if (clearUnusedAlpha)
                {
                    ClearUnusedAlpha(packedBytes, bytesPerPixel);
                }

                stream.Write(rowSpan);
            }

            return;
        }

        // Change a straight floating copy of each row, so the source pixels stay unchanged. Transparent pixels are
        // replaced before the profile conversion, then the destination pixel format performs the final quantization.
        using IMemoryOwner<Vector4> vectorBuffer = this.memoryAllocator.Allocate<Vector4>(width);
        Span<Vector4> vectors = vectorBuffer.GetSpan()[..width];

        for (int y = pixels.Height - 1; y >= 0; y--)
        {
            cancellationToken.ThrowIfCancellationRequested();

            sourceOperations.ToUnassociatedScaledVector4(configuration, pixels.DangerousGetRowSpan(y), vectors);

            if (replaceTransparent)
            {
                EncodingUtilities.ReplaceTransparentPixels(vectors);
            }

            if (converter is null)
            {
                pixelOperations.FromUnassociatedScaledVector4Destructive(configuration, vectors, packedRow);
            }
            else
            {
                converter.Convert(configuration, vectors, packedRow, PixelAlphaRepresentation.Unassociated);
            }

            if (clearUnusedAlpha)
            {
                ClearUnusedAlpha(packedBytes, bytesPerPixel);
            }

            stream.Write(rowSpan);
        }
    }

    /// <summary>
    /// Writes 0 in the alpha part of each packed pixel of a row.
    /// </summary>
    /// <param name="pixels">The packed pixels of the row.</param>
    /// <param name="bytesPerPixel">The pixel size: 4 for B, G, R and the fourth byte, or 2 for 5-5-5 and the high bit.</param>
    private static void ClearUnusedAlpha(Span<byte> pixels, int bytesPerPixel)
    {
        // The tensor operations use the widest vectors of the hardware.
        if (bytesPerPixel == 4)
        {
            Span<uint> words = MemoryMarshal.Cast<byte, uint>(pixels);
            TensorPrimitives.BitwiseAnd(words, 0x00FFFFFFu, words);
        }
        else
        {
            Span<ushort> words = MemoryMarshal.Cast<byte, ushort>(pixels);
            TensorPrimitives.BitwiseAnd(words, (ushort)0x7FFF, words);
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

        if (is8BitLuminance)
        {
            // The generated palette stores gray value i at entry i, so each source luminance byte is also its palette index.
            this.Write8BitIndexedGrayscalePixels(stream, encodingFrame, converter, cancellationToken);
        }
        else
        {
            // Other source formats need the quantizer to select palette colors and produce an index for each pixel.
            this.WriteIndexedColorPixels(configuration, stream, encodingFrame, 8, converter, cancellationToken);
        }
    }

    /// <summary>
    /// Quantizes source colors and writes the resulting RGB palette and the packed palette indices.
    /// </summary>
    /// <typeparam name="TPixel">The type of the pixel.</typeparam>
    /// <param name="configuration">The global configuration.</param>
    /// <param name="stream">The <see cref="Stream"/> to write to.</param>
    /// <param name="encodingFrame"> The <see cref="ImageFrame{TPixel}"/> containing pixel data.</param>
    /// <param name="bitsPerIndex">The bit count of one index: 1, 2, 4 or 8.</param>
    /// <param name="converter">The converter from sRGB to the output profile, or null for unchanged colors.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    private void WriteIndexedColorPixels<TPixel>(
        Configuration configuration,
        Stream stream,
        ImageFrame<TPixel> encodingFrame,
        int bitsPerIndex,
        ColorProfileConverter? converter,
        CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // An 8-bit index can select each color that a quantizer can make, so the 8-bit path keeps the quantizer options.
        // A smaller index limits the palette to 2^bitsPerIndex colors. Thus each index fits in bitsPerIndex bits, as the
        // index packer requires.
        QuantizerOptions options = this.quantizerOptions;
        if (bitsPerIndex < 8)
        {
            options = options.DeepClone();
            options.MaxColors = 1 << bitsPerIndex;
        }

        using IQuantizer<TPixel> frameQuantizer = this.quantizer.CreatePixelSpecificQuantizer<TPixel>(configuration, options);

        frameQuantizer.BuildPalette(this.pixelSamplingStrategy, encodingFrame);
        using IndexedImageFrame<TPixel> quantized = frameQuantizer.QuantizeFrame(encodingFrame, encodingFrame.Bounds);

        // The color table has 2^bitsPerIndex entries of 4 bytes. The entries after the quantized palette stay zero.
        using IMemoryOwner<byte> colorPaletteBuffer = this.memoryAllocator.Allocate<byte>(4 << bitsPerIndex, AllocationOptions.Clean);
        this.WriteColorPalette(configuration, stream, quantized.Palette.Span, colorPaletteBuffer.GetSpan(), converter);

        if (this.runLength)
        {
            // Rows are bottom-up, so row 0 is the last row in the stream.
            using IMemoryOwner<byte> rleRow = this.memoryAllocator.Allocate<byte>(BmpRleEncoder.GetMaxRowLength(encodingFrame.Width));
            for (int y = encodingFrame.Height - 1; y >= 0; y--)
            {
                cancellationToken.ThrowIfCancellationRequested();
                BmpRleEncoder.WriteRow(stream, quantized.DangerousGetRowSpan(y), rleRow.GetSpan(), bitsPerIndex, y == 0);
            }

            return;
        }

        if (this.huffman1D)
        {
            // The data uses the one-dimensional coding scheme of ITU-T T.4. An EOL code comes before the first row and
            // after each row. Six EOL codes (RTC) end the data, so the EOL code after the last row is the first of them.
            // A white run is palette index 0, and the first code bit is the most significant bit of each byte, as the
            // decoder reads it. See ITU-T Recommendation T.4 (07/2003), section 4.1, pages 3-4:
            // https://www.itu.int/rec/T-REC-T.4
            using CcittBitWriter writer = new(stream, this.memoryAllocator);
            writer.WriteEol();

            // Rows are bottom-up, so row 0 is the last row in the stream.
            for (int y = encodingFrame.Height - 1; y >= 0; y--)
            {
                cancellationToken.ThrowIfCancellationRequested();
                T4RowEncoder.EncodeRow(writer, quantized.DangerousGetRowSpan(y), 0);
                writer.WriteEol();
            }

            for (int i = 1; i < 6; i++)
            {
                writer.WriteEol();
            }

            writer.Flush();
            return;
        }

        if (bitsPerIndex == 8)
        {
            for (int y = encodingFrame.Height - 1; y >= 0; y--)
            {
                cancellationToken.ThrowIfCancellationRequested();
                this.Write8BitIndexRow(stream, quantized.DangerousGetRowSpan(y));
            }

            return;
        }

        int width = encodingFrame.Width;
        using IMemoryOwner<byte> row = this.AllocateRow(((width * bitsPerIndex) + 7) / 8);
        Span<byte> rowSpan = row.GetSpan();

        for (int y = encodingFrame.Height - 1; y >= 0; y--)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // The packer writes the index bytes of the row. The padding after them stays zero from the allocation.
            EncodingUtilities.PackIndices(quantized.DangerousGetRowSpan(y), rowSpan, bitsPerIndex);
            stream.Write(rowSpan);
        }
    }

    /// <summary>
    /// Writes one row of 8-bit palette indices and its alignment padding.
    /// </summary>
    /// <param name="stream">The <see cref="Stream"/> to write to.</param>
    /// <param name="indices">The indices of the row, one byte each.</param>
    private void Write8BitIndexRow(Stream stream, ReadOnlySpan<byte> indices)
    {
        // 8-bit indices need no packing, so the encoder writes the source row directly and does not copy it. Only a row
        // with padding needs a second write.
        stream.Write(indices);

        if (this.padding > 0)
        {
            stream.Write(PaddingBytes[..this.padding]);
        }
    }

    /// <summary>
    /// Writes an RGB grayscale palette and uses the source luminance bytes directly as eight-bit indices.
    /// </summary>
    /// <typeparam name="TPixel">The type of the pixel.</typeparam>
    /// <param name="stream">The <see cref="Stream"/> to write to.</param>
    /// <param name="encodingFrame"> The <see cref="ImageFrame{TPixel}"/> containing pixel data.</param>
    /// <param name="converter">The converter from sRGB to the output profile, or null for unchanged colors.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    private void Write8BitIndexedGrayscalePixels<TPixel>(
        Stream stream,
        ImageFrame<TPixel> encodingFrame,
        ColorProfileConverter? converter,
        CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Configuration configuration = encodingFrame.Configuration;

        // The loop below writes all 1024 bytes of the color table.
        using IMemoryOwner<byte> colorPaletteBuffer = this.memoryAllocator.Allocate<byte>(ColorPaletteSize8Bit);
        Span<byte> colorPalette = colorPaletteBuffer.GetSpan();

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

        // The caller selected single-component eight-bit luminance pixels. Their bytes index the gray ramp built above.
        // ICC changes the palette colors after index selection. It does not change which entry each pixel uses.
        Buffer2D<TPixel> imageBuffer = encodingFrame.PixelBuffer;
        if (this.runLength)
        {
            // Rows are bottom-up, so row 0 is the last row in the stream.
            using IMemoryOwner<byte> rleRow = this.memoryAllocator.Allocate<byte>(BmpRleEncoder.GetMaxRowLength(encodingFrame.Width));
            for (int y = encodingFrame.Height - 1; y >= 0; y--)
            {
                cancellationToken.ThrowIfCancellationRequested();
                BmpRleEncoder.WriteRow(stream, MemoryMarshal.AsBytes(imageBuffer.DangerousGetRowSpan(y)), rleRow.GetSpan(), 8, y == 0);
            }

            return;
        }

        for (int y = encodingFrame.Height - 1; y >= 0; y--)
        {
            cancellationToken.ThrowIfCancellationRequested();
            this.Write8BitIndexRow(stream, MemoryMarshal.AsBytes(imageBuffer.DangerousGetRowSpan(y)));
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

            converter.Convert(configuration, quantizedColorPalette, vectors.GetSpan());
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
    /// Writes the bottom-up 1-bit transparency mask required by an ICO/CUR bitmap resource.
    /// </summary>
    /// <typeparam name="TPixel">The source pixel type.</typeparam>
    /// <param name="stream">The destination stream.</param>
    /// <param name="encodingFrame">The source frame.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    private void ProcessedAlphaMask<TPixel>(Stream stream, ImageFrame<TPixel> encodingFrame, CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // The mask has one bit for each pixel, the first pixel in the most significant bit. A bit of 1 marks a pixel
        // with alpha 0. Each row is padded to a multiple of 4 bytes.
        Configuration configuration = encodingFrame.Configuration;
        int width = encodingFrame.Width;
        int maskRowLength = (((width + 7) / 8) + 3) & ~3;

        using IMemoryOwner<A8> alphaBuffer = this.memoryAllocator.Allocate<A8>(width);
        Span<A8> alpha = alphaBuffer.GetSpan();

        // IsZero writes one bool for each pixel: 1 for alpha 0, else 0. These bytes are the 1-bit mask indices. IsZero
        // can process the last elements of a row two times, so it writes to a second buffer, not in place.
        using IMemoryOwner<byte> flagBuffer = this.memoryAllocator.Allocate<byte>(width);
        Span<byte> flags = flagBuffer.GetSpan();
        Span<bool> isTransparent = MemoryMarshal.Cast<byte, bool>(flags);

        // The packer writes only the mask bytes, so the padding stays zero from the allocation.
        using IMemoryOwner<byte> maskBuffer = this.memoryAllocator.Allocate<byte>(maskRowLength, AllocationOptions.Clean);
        Span<byte> mask = maskBuffer.GetSpan();

        for (int y = encodingFrame.Height - 1; y >= 0; y--)
        {
            cancellationToken.ThrowIfCancellationRequested();

            PixelOperations<TPixel>.Instance.To(configuration, encodingFrame.PixelBuffer.DangerousGetRowSpan(y), alpha);
            TensorPrimitives.IsZero(MemoryMarshal.AsBytes(alpha), isTransparent);
            EncodingUtilities.PackIndices(flags, mask, 1);
            stream.Write(mask);
        }
    }
}
