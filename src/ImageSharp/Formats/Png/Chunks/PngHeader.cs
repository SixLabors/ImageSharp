// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.
#nullable disable

using System.Buffers.Binary;

namespace SixLabors.ImageSharp.Formats.Png.Chunks;

/// <summary>
/// Represents the png header chunk.
/// </summary>
internal readonly struct PngHeader
{
    public const int Size = 13;

    public PngHeader(
        int width,
        int height,
        byte bitDepth,
        PngColorType colorType,
        byte compressionMethod,
        byte filterMethod,
        PngInterlaceMode interlaceMethod)
    {
        this.Width = width;
        this.Height = height;
        this.BitDepth = bitDepth;
        this.ColorType = colorType;
        this.CompressionMethod = compressionMethod;
        this.FilterMethod = filterMethod;
        this.InterlaceMethod = interlaceMethod;
    }

    /// <summary>
    /// Gets the dimension in x-direction of the image in pixels.
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// Gets the dimension in y-direction of the image in pixels.
    /// </summary>
    public int Height { get; }

    /// <summary>
    /// Gets the bit depth.
    /// Bit depth is a single-byte integer giving the number of bits per sample
    /// or per palette index (not per pixel). Valid values are 1, 2, 4, 8, and 16,
    /// although not all values are allowed for all color types.
    /// </summary>
    public byte BitDepth { get; }

    /// <summary>
    /// Gets the color type.
    /// Color type is a integer that describes the interpretation of the
    /// image data. Color type codes represent sums of the following values:
    /// 1 (palette used), 2 (color used), and 4 (alpha channel used).
    /// </summary>
    public PngColorType ColorType { get; }

    /// <summary>
    /// Gets the compression method.
    /// Indicates the method used to compress the image data. At present,
    /// only compression method 0 (deflate/inflate compression with a sliding
    /// window of at most 32768 bytes) is defined.
    /// </summary>
    public byte CompressionMethod { get; }

    /// <summary>
    /// Gets the preprocessing method.
    /// Indicates the preprocessing method applied to the image
    /// data before compression. At present, only filter method 0
    /// (adaptive filtering with five basic filter types) is defined.
    /// </summary>
    public byte FilterMethod { get; }

    /// <summary>
    /// Gets the transmission order.
    /// Indicates the transmission order of the image data.
    /// Two values are currently defined: 0 (no interlace) or 1 (Adam7 interlace).
    /// </summary>
    public PngInterlaceMode InterlaceMethod { get; }

    /// <summary>
    /// Gets the number of bytes of one pixel. A pixel of less than 8 bits gets 1 byte.
    /// </summary>
    /// <param name="colorType">The color type.</param>
    /// <param name="bitDepth">The number of bits of one sample.</param>
    /// <returns>The number of bytes.</returns>
    public static int CalculateBytesPerPixel(PngColorType colorType, int bitDepth)
        => colorType switch
        {
            PngColorType.Grayscale => bitDepth == 16 ? 2 : 1,
            PngColorType.GrayscaleWithAlpha => bitDepth == 16 ? 4 : 2,
            PngColorType.Palette => 1,
            PngColorType.Rgb => bitDepth == 16 ? 6 : 3,

            // PngColorType.RgbWithAlpha
            _ => bitDepth == 16 ? 8 : 4,
        };

    /// <summary>
    /// Gets the number of bytes of a scanline, without the filter type byte.
    /// </summary>
    /// <remarks>
    /// Samples of less than 8 bits pack into bytes, and the last byte of a scanline can have unused bits. See PNG Third Edition, section 7.2 "Scanlines":
    /// https://www.w3.org/TR/png-3/#7Scanline. Only gray and palette pixels have less than 8 bits, and these pixels have 1 byte per pixel. The calculation uses
    /// 64-bit values, so a large width cannot wrap. <see cref="Validate"/> makes sure that the scanline of the image width fits in an <see cref="int"/>.
    /// </remarks>
    /// <param name="width">The number of pixels of the scanline.</param>
    /// <param name="bitDepth">The number of bits of one sample.</param>
    /// <param name="bytesPerPixel">The number of bytes of one pixel.</param>
    /// <returns>The number of bytes.</returns>
    public static long CalculateScanlineLength(int width, int bitDepth, int bytesPerPixel)
        => bitDepth < 8 ? (((long)width * bitDepth) + 7) / 8 : (long)width * bytesPerPixel;

    /// <summary>
    /// Checks the values of the header.
    /// </summary>
    /// <remarks>
    /// The checks use the rules of PNG Third Edition, section 11.2.1 "IHDR Image header": https://www.w3.org/TR/png-3/#11IHDR
    /// </remarks>
    /// <exception cref="InvalidImageContentException">A value of the header is not valid.</exception>
    public void Validate()
    {
        // The width and the height are 1 to 2^31-1. Parse reads them as signed values, so a value more than 2^31-1 is less than 0.
        if (this.Width <= 0 || this.Height <= 0)
        {
            PngThrowHelper.ThrowInvalidImageContentException($"Invalid image dimensions. Was '{this.Width}x{this.Height}'.");
        }

        if (!PngConstants.ColorTypes.TryGetValue(this.ColorType, out byte[] supportedBitDepths))
        {
            PngThrowHelper.ThrowInvalidImageContentException($"Invalid or unsupported color type. Was '{this.ColorType}'.");
        }

        if (supportedBitDepths.AsSpan().IndexOf(this.BitDepth) == -1)
        {
            PngThrowHelper.ThrowInvalidImageContentException($"Invalid or unsupported bit depth. Was '{this.BitDepth}'.");
        }

        if (this.CompressionMethod != 0)
        {
            PngThrowHelper.ThrowInvalidImageContentException($"Invalid compression method. Expected 0. Was '{this.CompressionMethod}'.");
        }

        if (this.FilterMethod != 0)
        {
            PngThrowHelper.ThrowInvalidImageContentException($"Invalid filter method. Expected 0. Was '{this.FilterMethod}'.");
        }

        // The png specification only defines 'None' and 'Adam7' as interlaced methods.
        if (this.InterlaceMethod is not PngInterlaceMode.None and not PngInterlaceMode.Adam7)
        {
            PngThrowHelper.ThrowInvalidImageContentException($"Invalid interlace method. Expected 'None' or 'Adam7'. Was '{this.InterlaceMethod}'.");
        }

        // The decoder holds a scanline and its filter type byte in one buffer, so their length must fit in an int.
        if (CalculateScanlineLength(this.Width, this.BitDepth, CalculateBytesPerPixel(this.ColorType, this.BitDepth)) >= int.MaxValue)
        {
            PngThrowHelper.ThrowInvalidImageContentException("PNG scanline length exceeds the supported maximum.");
        }
    }

    /// <summary>
    /// Writes the header to the given buffer.
    /// </summary>
    /// <param name="buffer">The buffer to write to.</param>
    public void WriteTo(Span<byte> buffer)
    {
        BinaryPrimitives.WriteInt32BigEndian(buffer[..4], this.Width);
        BinaryPrimitives.WriteInt32BigEndian(buffer.Slice(4, 4), this.Height);

        buffer[8] = this.BitDepth;
        buffer[9] = (byte)this.ColorType;
        buffer[10] = this.CompressionMethod;
        buffer[11] = this.FilterMethod;
        buffer[12] = (byte)this.InterlaceMethod;
    }

    /// <summary>
    /// Parses the PngHeader from the given data buffer.
    /// </summary>
    /// <param name="data">The data to parse.</param>
    /// <returns>The parsed PngHeader.</returns>
    public static PngHeader Parse(ReadOnlySpan<byte> data)
        => new(
            width: BinaryPrimitives.ReadInt32BigEndian(data[..4]),
            height: BinaryPrimitives.ReadInt32BigEndian(data.Slice(4, 4)),
            bitDepth: data[8],
            colorType: (PngColorType)data[9],
            compressionMethod: data[10],
            filterMethod: data[11],
            interlaceMethod: (PngInterlaceMode)data[12]);
}
