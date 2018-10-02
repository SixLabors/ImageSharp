// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;

namespace SixLabors.ImageSharp.Formats.Png
{
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
        /// Validates the png header.
        /// </summary>
        /// <exception cref="NotSupportedException">
        /// Thrown if the image does pass validation.
        /// </exception>
        public void Validate()
        {
            if (!PngConstants.ColorTypes.TryGetValue(this.ColorType, out byte[] supportedBitDepths))
            {
                throw new NotSupportedException($"Invalid or unsupported color type. Was '{this.ColorType}'.");
            }

            if (supportedBitDepths.AsSpan().IndexOf(this.BitDepth) == -1)
            {
                throw new NotSupportedException($"Invalid or unsupported bit depth. Was '{this.BitDepth}'.");
            }

            if (this.FilterMethod != 0)
            {
                throw new NotSupportedException($"Invalid filter method. Expected 0. Was '{this.FilterMethod}'.");
            }

            // The png specification only defines 'None' and 'Adam7' as interlaced methods.
            if (this.InterlaceMethod != PngInterlaceMode.None && this.InterlaceMethod != PngInterlaceMode.Adam7)
            {
                throw new NotSupportedException($"Invalid interlace method. Expected 'None' or 'Adam7'. Was '{this.InterlaceMethod}'.");
            }
        }

        /// <summary>
        /// Writes the header to the given buffer.
        /// </summary>
        /// <param name="buffer">The buffer to write to.</param>
        public void WriteTo(Span<byte> buffer)
        {
            BinaryPrimitives.WriteInt32BigEndian(buffer.Slice(0, 4), this.Width);
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
        {
            return new PngHeader(
              width: BinaryPrimitives.ReadInt32BigEndian(data.Slice(0, 4)),
              height: BinaryPrimitives.ReadInt32BigEndian(data.Slice(4, 4)),
              bitDepth: data[8],
              colorType: (PngColorType)data[9],
              compressionMethod: data[10],
              filterMethod: data[11],
              interlaceMethod: (PngInterlaceMode)data[12]);
        }
    }
}
