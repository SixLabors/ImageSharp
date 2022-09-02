// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;
using System.Buffers;
using System.Buffers.Binary;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tga
{
    /// <summary>
    /// Image encoder for writing an image to a stream as a truevision targa image.
    /// </summary>
    internal sealed class TgaEncoderCore : IImageEncoderInternals
    {
        /// <summary>
        /// Used for allocating memory during processing operations.
        /// </summary>
        private readonly MemoryAllocator memoryAllocator;

        /// <summary>
        /// The global configuration.
        /// </summary>
        private Configuration configuration;

        /// <summary>
        /// Reusable buffer for writing data.
        /// </summary>
        private readonly byte[] buffer = new byte[2];

        /// <summary>
        /// The color depth, in number of bits per pixel.
        /// </summary>
        private TgaBitsPerPixel? bitsPerPixel;

        /// <summary>
        /// Indicates if run length compression should be used.
        /// </summary>
        private readonly TgaCompression compression;

        /// <summary>
        /// Initializes a new instance of the <see cref="TgaEncoderCore"/> class.
        /// </summary>
        /// <param name="options">The encoder options.</param>
        /// <param name="memoryAllocator">The memory manager.</param>
        public TgaEncoderCore(ITgaEncoderOptions options, MemoryAllocator memoryAllocator)
        {
            this.memoryAllocator = memoryAllocator;
            this.bitsPerPixel = options.BitsPerPixel;
            this.compression = options.Compression;
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

            this.configuration = image.GetConfiguration();
            ImageMetadata metadata = image.Metadata;
            TgaMetadata tgaMetadata = metadata.GetTgaMetadata();
            this.bitsPerPixel ??= tgaMetadata.BitsPerPixel;

            TgaImageType imageType = this.compression is TgaCompression.RunLength ? TgaImageType.RleTrueColor : TgaImageType.TrueColor;
            if (this.bitsPerPixel == TgaBitsPerPixel.Pixel8)
            {
                imageType = this.compression is TgaCompression.RunLength ? TgaImageType.RleBlackAndWhite : TgaImageType.BlackAndWhite;
            }

            byte imageDescriptor = 0;
            if (this.compression is TgaCompression.RunLength)
            {
                // If compression is used, set bit 5 of the image descriptor to indicate a left top origin.
                imageDescriptor |= 0x20;
            }

            if (this.bitsPerPixel is TgaBitsPerPixel.Pixel32)
            {
                // Indicate, that 8 bit are used for the alpha channel.
                imageDescriptor |= 0x8;
            }

            if (this.bitsPerPixel is TgaBitsPerPixel.Pixel16)
            {
                // Indicate, that 1 bit is used for the alpha channel.
                imageDescriptor |= 0x1;
            }

            var fileHeader = new TgaFileHeader(
                idLength: 0,
                colorMapType: 0,
                imageType: imageType,
                cMapStart: 0,
                cMapLength: 0,
                cMapDepth: 0,
                xOffset: 0,
                yOffset: this.compression is TgaCompression.RunLength ? (short)image.Height : (short)0, // When run length encoding is used, the origin should be top left instead of the default bottom left.
                width: (short)image.Width,
                height: (short)image.Height,
                pixelDepth: (byte)this.bitsPerPixel.Value,
                imageDescriptor: imageDescriptor);

            Span<byte> buffer = stackalloc byte[TgaFileHeader.Size];
            fileHeader.WriteTo(buffer);

            stream.Write(buffer, 0, TgaFileHeader.Size);
            if (this.compression is TgaCompression.RunLength)
            {
                this.WriteRunLengthEncodedImage(stream, image.Frames.RootFrame);
            }
            else
            {
                this.WriteImage(stream, image.Frames.RootFrame);
            }

            stream.Flush();
        }

        /// <summary>
        /// Writes the pixel data to the binary stream.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="stream">The <see cref="Stream"/> to write to.</param>
        /// <param name="image">
        /// The <see cref="ImageFrame{TPixel}"/> containing pixel data.
        /// </param>
        private void WriteImage<TPixel>(Stream stream, ImageFrame<TPixel> image)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Buffer2D<TPixel> pixels = image.PixelBuffer;
            switch (this.bitsPerPixel)
            {
                case TgaBitsPerPixel.Pixel8:
                    this.Write8Bit(stream, pixels);
                    break;

                case TgaBitsPerPixel.Pixel16:
                    this.Write16Bit(stream, pixels);
                    break;

                case TgaBitsPerPixel.Pixel24:
                    this.Write24Bit(stream, pixels);
                    break;

                case TgaBitsPerPixel.Pixel32:
                    this.Write32Bit(stream, pixels);
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// Writes a run length encoded tga image to the stream.
        /// </summary>
        /// <typeparam name="TPixel">The pixel type.</typeparam>
        /// <param name="stream">The stream to write the image to.</param>
        /// <param name="image">The image to encode.</param>
        private void WriteRunLengthEncodedImage<TPixel>(Stream stream, ImageFrame<TPixel> image)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Rgba32 color = default;
            Buffer2D<TPixel> pixels = image.PixelBuffer;
            for (int y = 0; y < image.Height; y++)
            {
                Span<TPixel> pixelRow = pixels.DangerousGetRowSpan(y);
                for (int x = 0; x < image.Width;)
                {
                    TPixel currentPixel = pixelRow[x];
                    currentPixel.ToRgba32(ref color);
                    byte equalPixelCount = this.FindEqualPixels(pixelRow, x);

                    if (equalPixelCount > 0)
                    {
                        // Write the number of equal pixels, with the high bit set, indicating ist a compressed pixel run.
                        stream.WriteByte((byte)(equalPixelCount | 128));
                        this.WritePixel(stream, currentPixel, color);
                        x += equalPixelCount + 1;
                    }
                    else
                    {
                        // Write Raw Packet (i.e., Non-Run-Length Encoded):
                        byte unEqualPixelCount = this.FindUnEqualPixels(pixelRow, x);
                        stream.WriteByte(unEqualPixelCount);
                        this.WritePixel(stream, currentPixel, color);
                        x++;
                        for (int i = 0; i < unEqualPixelCount; i++)
                        {
                            currentPixel = pixelRow[x];
                            currentPixel.ToRgba32(ref color);
                            this.WritePixel(stream, currentPixel, color);
                            x++;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Writes a the pixel to the stream.
        /// </summary>
        /// <typeparam name="TPixel">The type of the pixel.</typeparam>
        /// <param name="stream">The stream to write to.</param>
        /// <param name="currentPixel">The current pixel.</param>
        /// <param name="color">The color of the pixel to write.</param>
        private void WritePixel<TPixel>(Stream stream, TPixel currentPixel, Rgba32 color)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            switch (this.bitsPerPixel)
            {
                case TgaBitsPerPixel.Pixel8:
                    int luminance = GetLuminance(currentPixel);
                    stream.WriteByte((byte)luminance);
                    break;

                case TgaBitsPerPixel.Pixel16:
                    var bgra5551 = new Bgra5551(color.ToVector4());
                    BinaryPrimitives.TryWriteInt16LittleEndian(this.buffer, (short)bgra5551.PackedValue);
                    stream.WriteByte(this.buffer[0]);
                    stream.WriteByte(this.buffer[1]);

                    break;

                case TgaBitsPerPixel.Pixel24:
                    stream.WriteByte(color.B);
                    stream.WriteByte(color.G);
                    stream.WriteByte(color.R);
                    break;

                case TgaBitsPerPixel.Pixel32:
                    stream.WriteByte(color.B);
                    stream.WriteByte(color.G);
                    stream.WriteByte(color.R);
                    stream.WriteByte(color.A);
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// Finds consecutive pixels which have the same value up to 128 pixels maximum.
        /// </summary>
        /// <typeparam name="TPixel">The pixel type.</typeparam>
        /// <param name="pixelRow">A pixel row of the image to encode.</param>
        /// <param name="xStart">X coordinate to start searching for the same pixels.</param>
        /// <returns>The number of equal pixels.</returns>
        private byte FindEqualPixels<TPixel>(Span<TPixel> pixelRow, int xStart)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            byte equalPixelCount = 0;
            TPixel startPixel = pixelRow[xStart];
            for (int x = xStart + 1; x < pixelRow.Length; x++)
            {
                TPixel nextPixel = pixelRow[x];
                if (startPixel.Equals(nextPixel))
                {
                    equalPixelCount++;
                }
                else
                {
                    return equalPixelCount;
                }

                if (equalPixelCount >= 127)
                {
                    return equalPixelCount;
                }
            }

            return equalPixelCount;
        }

        /// <summary>
        /// Finds consecutive pixels which are unequal up to 128 pixels maximum.
        /// </summary>
        /// <typeparam name="TPixel">The pixel type.</typeparam>
        /// <param name="pixelRow">A pixel row of the image to encode.</param>
        /// <param name="xStart">X coordinate to start searching for the unequal pixels.</param>
        /// <returns>The number of equal pixels.</returns>
        private byte FindUnEqualPixels<TPixel>(Span<TPixel> pixelRow, int xStart)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            byte unEqualPixelCount = 0;
            TPixel currentPixel = pixelRow[xStart];
            for (int x = xStart + 1; x < pixelRow.Length; x++)
            {
                TPixel nextPixel = pixelRow[x];
                if (currentPixel.Equals(nextPixel))
                {
                    return unEqualPixelCount;
                }

                unEqualPixelCount++;

                if (unEqualPixelCount >= 127)
                {
                    return unEqualPixelCount;
                }

                currentPixel = nextPixel;
            }

            return unEqualPixelCount;
        }

        private IMemoryOwner<byte> AllocateRow(int width, int bytesPerPixel)
            => this.memoryAllocator.AllocatePaddedPixelRowBuffer(width, bytesPerPixel, 0);

        /// <summary>
        /// Writes the 8bit pixels uncompressed to the stream.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="stream">The <see cref="Stream"/> to write to.</param>
        /// <param name="pixels">The <see cref="Buffer2D{TPixel}"/> containing pixel data.</param>
        private void Write8Bit<TPixel>(Stream stream, Buffer2D<TPixel> pixels)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using IMemoryOwner<byte> row = this.AllocateRow(pixels.Width, 1);
            Span<byte> rowSpan = row.GetSpan();

            for (int y = pixels.Height - 1; y >= 0; y--)
            {
                Span<TPixel> pixelSpan = pixels.DangerousGetRowSpan(y);
                PixelOperations<TPixel>.Instance.ToL8Bytes(
                    this.configuration,
                    pixelSpan,
                    rowSpan,
                    pixelSpan.Length);
                stream.Write(rowSpan);
            }
        }

        /// <summary>
        /// Writes the 16bit pixels uncompressed to the stream.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="stream">The <see cref="Stream"/> to write to.</param>
        /// <param name="pixels">The <see cref="Buffer2D{TPixel}"/> containing pixel data.</param>
        private void Write16Bit<TPixel>(Stream stream, Buffer2D<TPixel> pixels)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using IMemoryOwner<byte> row = this.AllocateRow(pixels.Width, 2);
            Span<byte> rowSpan = row.GetSpan();

            for (int y = pixels.Height - 1; y >= 0; y--)
            {
                Span<TPixel> pixelSpan = pixels.DangerousGetRowSpan(y);
                PixelOperations<TPixel>.Instance.ToBgra5551Bytes(
                    this.configuration,
                    pixelSpan,
                    rowSpan,
                    pixelSpan.Length);
                stream.Write(rowSpan);
            }
        }

        /// <summary>
        /// Writes the 24bit pixels uncompressed to the stream.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="stream">The <see cref="Stream"/> to write to.</param>
        /// <param name="pixels">The <see cref="Buffer2D{TPixel}"/> containing pixel data.</param>
        private void Write24Bit<TPixel>(Stream stream, Buffer2D<TPixel> pixels)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using IMemoryOwner<byte> row = this.AllocateRow(pixels.Width, 3);
            Span<byte> rowSpan = row.GetSpan();

            for (int y = pixels.Height - 1; y >= 0; y--)
            {
                Span<TPixel> pixelSpan = pixels.DangerousGetRowSpan(y);
                PixelOperations<TPixel>.Instance.ToBgr24Bytes(
                    this.configuration,
                    pixelSpan,
                    rowSpan,
                    pixelSpan.Length);
                stream.Write(rowSpan);
            }
        }

        /// <summary>
        /// Writes the 32bit pixels uncompressed to the stream.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="stream">The <see cref="Stream"/> to write to.</param>
        /// <param name="pixels">The <see cref="Buffer2D{TPixel}"/> containing pixel data.</param>
        private void Write32Bit<TPixel>(Stream stream, Buffer2D<TPixel> pixels)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using IMemoryOwner<byte> row = this.AllocateRow(pixels.Width, 4);
            Span<byte> rowSpan = row.GetSpan();

            for (int y = pixels.Height - 1; y >= 0; y--)
            {
                Span<TPixel> pixelSpan = pixels.DangerousGetRowSpan(y);
                PixelOperations<TPixel>.Instance.ToBgra32Bytes(
                    this.configuration,
                    pixelSpan,
                    rowSpan,
                    pixelSpan.Length);
                stream.Write(rowSpan);
            }
        }

        /// <summary>
        /// Convert the pixel values to grayscale using ITU-R Recommendation BT.709.
        /// </summary>
        /// <param name="sourcePixel">The pixel to get the luminance from.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static int GetLuminance<TPixel>(TPixel sourcePixel)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            var vector = sourcePixel.ToVector4();
            return ColorNumerics.GetBT709Luminance(ref vector, 256);
        }
    }
}
