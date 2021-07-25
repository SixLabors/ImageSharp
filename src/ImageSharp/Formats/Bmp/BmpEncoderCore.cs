// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Quantization;

namespace SixLabors.ImageSharp.Formats.Bmp
{
    /// <summary>
    /// Image encoder for writing an image to a stream as a Windows bitmap.
    /// </summary>
    internal sealed class BmpEncoderCore : IImageEncoderInternals
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
        /// The color palette for an 1 bit image will have 2 entry's with 4 bytes for each entry.
        /// </summary>
        private const int ColorPaletteSize1Bit = 8;

        /// <summary>
        /// Used for allocating memory during processing operations.
        /// </summary>
        private readonly MemoryAllocator memoryAllocator;

        /// <summary>
        /// The global configuration.
        /// </summary>
        private Configuration configuration;

        /// <summary>
        /// The color depth, in number of bits per pixel.
        /// </summary>
        private BmpBitsPerPixel? bitsPerPixel;

        /// <summary>
        /// A bitmap v4 header will only be written, if the user explicitly wants support for transparency.
        /// In this case the compression type BITFIELDS will be used.
        /// Otherwise a bitmap v3 header will be written, which is supported by almost all decoders.
        /// </summary>
        private readonly bool writeV4Header;

        /// <summary>
        /// The quantizer for reducing the color count for 8-Bit, 4-Bit and 1-Bit images.
        /// </summary>
        private readonly IQuantizer quantizer;

        /// <summary>
        /// Initializes a new instance of the <see cref="BmpEncoderCore"/> class.
        /// </summary>
        /// <param name="options">The encoder options.</param>
        /// <param name="memoryAllocator">The memory manager.</param>
        public BmpEncoderCore(IBmpEncoderOptions options, MemoryAllocator memoryAllocator)
        {
            this.memoryAllocator = memoryAllocator;
            this.bitsPerPixel = options.BitsPerPixel;
            this.writeV4Header = options.SupportTransparency;
            this.quantizer = options.Quantizer ?? KnownQuantizers.Octree;
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
            BmpMetadata bmpMetadata = metadata.GetBmpMetadata();
            this.bitsPerPixel ??= bmpMetadata.BitsPerPixel;

            short bpp = (short)this.bitsPerPixel;
            int bytesPerLine = 4 * (((image.Width * bpp) + 31) / 32);
            this.padding = bytesPerLine - (int)(image.Width * (bpp / 8F));

            // Set Resolution.
            int hResolution = 0;
            int vResolution = 0;

            if (metadata.ResolutionUnits != PixelResolutionUnit.AspectRatio)
            {
                if (metadata.HorizontalResolution > 0 && metadata.VerticalResolution > 0)
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
            }

            int infoHeaderSize = this.writeV4Header ? BmpInfoHeader.SizeV4 : BmpInfoHeader.SizeV3;
            var infoHeader = new BmpInfoHeader(
                headerSize: infoHeaderSize,
                height: image.Height,
                width: image.Width,
                bitsPerPixel: bpp,
                planes: 1,
                imageSize: image.Height * bytesPerLine,
                clrUsed: 0,
                clrImportant: 0,
                xPelsPerMeter: hResolution,
                yPelsPerMeter: vResolution);

            if (this.writeV4Header && this.bitsPerPixel == BmpBitsPerPixel.Pixel32)
            {
                infoHeader.AlphaMask = Rgba32AlphaMask;
                infoHeader.RedMask = Rgba32RedMask;
                infoHeader.GreenMask = Rgba32GreenMask;
                infoHeader.BlueMask = Rgba32BlueMask;
                infoHeader.Compression = BmpCompression.BitFields;
            }

            int colorPaletteSize = 0;
            if (this.bitsPerPixel == BmpBitsPerPixel.Pixel8)
            {
                colorPaletteSize = ColorPaletteSize8Bit;
            }
            else if (this.bitsPerPixel == BmpBitsPerPixel.Pixel4)
            {
                colorPaletteSize = ColorPaletteSize4Bit;
            }
            else if (this.bitsPerPixel == BmpBitsPerPixel.Pixel1)
            {
                colorPaletteSize = ColorPaletteSize1Bit;
            }

            var fileHeader = new BmpFileHeader(
                type: BmpConstants.TypeMarkers.Bitmap,
                fileSize: BmpFileHeader.Size + infoHeaderSize + colorPaletteSize + infoHeader.ImageSize,
                reserved: 0,
                offset: BmpFileHeader.Size + infoHeaderSize + colorPaletteSize);

            Span<byte> buffer = stackalloc byte[infoHeaderSize];
            fileHeader.WriteTo(buffer);

            stream.Write(buffer, 0, BmpFileHeader.Size);

            if (this.writeV4Header)
            {
                infoHeader.WriteV4Header(buffer);
            }
            else
            {
                infoHeader.WriteV3Header(buffer);
            }

            stream.Write(buffer, 0, infoHeaderSize);

            this.WriteImage(stream, image.Frames.RootFrame);

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
                case BmpBitsPerPixel.Pixel32:
                    this.Write32Bit(stream, pixels);
                    break;

                case BmpBitsPerPixel.Pixel24:
                    this.Write24Bit(stream, pixels);
                    break;

                case BmpBitsPerPixel.Pixel16:
                    this.Write16Bit(stream, pixels);
                    break;

                case BmpBitsPerPixel.Pixel8:
                    this.Write8Bit(stream, image);
                    break;

                case BmpBitsPerPixel.Pixel4:
                    this.Write4BitColor(stream, image);
                    break;

                case BmpBitsPerPixel.Pixel1:
                    this.Write1BitColor(stream, image);
                    break;
            }
        }

        private IMemoryOwner<byte> AllocateRow(int width, int bytesPerPixel)
            => this.memoryAllocator.AllocatePaddedPixelRowBuffer(width, bytesPerPixel, this.padding);

        /// <summary>
        /// Writes the 32bit color palette to the stream.
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
                Span<TPixel> pixelSpan = pixels.GetRowSpan(y);
                PixelOperations<TPixel>.Instance.ToBgra32Bytes(
                    this.configuration,
                    pixelSpan,
                    rowSpan,
                    pixelSpan.Length);
                stream.Write(rowSpan);
            }
        }

        /// <summary>
        /// Writes the 24bit color palette to the stream.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="stream">The <see cref="Stream"/> to write to.</param>
        /// <param name="pixels">The <see cref="Buffer2D{TPixel}"/> containing pixel data.</param>
        private void Write24Bit<TPixel>(Stream stream, Buffer2D<TPixel> pixels)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            int width = pixels.Width;
            int rowBytesWithoutPadding = width * 3;
            using IMemoryOwner<byte> row = this.AllocateRow(width, 3);
            Span<byte> rowSpan = row.GetSpan();

            for (int y = pixels.Height - 1; y >= 0; y--)
            {
                Span<TPixel> pixelSpan = pixels.GetRowSpan(y);
                PixelOperations<TPixel>.Instance.ToBgr24Bytes(
                    this.configuration,
                    pixelSpan,
                    row.Slice(0, rowBytesWithoutPadding),
                    width);
                stream.Write(rowSpan);
            }
        }

        /// <summary>
        /// Writes the 16bit color palette to the stream.
        /// </summary>
        /// <typeparam name="TPixel">The type of the pixel.</typeparam>
        /// <param name="stream">The <see cref="Stream"/> to write to.</param>
        /// <param name="pixels">The <see cref="Buffer2D{TPixel}"/> containing pixel data.</param>
        private void Write16Bit<TPixel>(Stream stream, Buffer2D<TPixel> pixels)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            int width = pixels.Width;
            int rowBytesWithoutPadding = width * 2;
            using IMemoryOwner<byte> row = this.AllocateRow(width, 2);
            Span<byte> rowSpan = row.GetSpan();

            for (int y = pixels.Height - 1; y >= 0; y--)
            {
                Span<TPixel> pixelSpan = pixels.GetRowSpan(y);

                PixelOperations<TPixel>.Instance.ToBgra5551Bytes(
                    this.configuration,
                    pixelSpan,
                    row.Slice(0, rowBytesWithoutPadding),
                    pixelSpan.Length);

                stream.Write(rowSpan);
            }
        }

        /// <summary>
        /// Writes an 8 bit image with a color palette. The color palette has 256 entry's with 4 bytes for each entry.
        /// </summary>
        /// <typeparam name="TPixel">The type of the pixel.</typeparam>
        /// <param name="stream">The <see cref="Stream"/> to write to.</param>
        /// <param name="image"> The <see cref="ImageFrame{TPixel}"/> containing pixel data.</param>
        private void Write8Bit<TPixel>(Stream stream, ImageFrame<TPixel> image)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            bool isL8 = typeof(TPixel) == typeof(L8);
            using IMemoryOwner<byte> colorPaletteBuffer = this.memoryAllocator.Allocate<byte>(ColorPaletteSize8Bit, AllocationOptions.Clean);
            Span<byte> colorPalette = colorPaletteBuffer.GetSpan();

            if (isL8)
            {
                this.Write8BitGray(stream, image, colorPalette);
            }
            else
            {
                this.Write8BitColor(stream, image, colorPalette);
            }
        }

        /// <summary>
        /// Writes an 8 bit color image with a color palette. The color palette has 256 entry's with 4 bytes for each entry.
        /// </summary>
        /// <typeparam name="TPixel">The type of the pixel.</typeparam>
        /// <param name="stream">The <see cref="Stream"/> to write to.</param>
        /// <param name="image"> The <see cref="ImageFrame{TPixel}"/> containing pixel data.</param>
        /// <param name="colorPalette">A byte span of size 1024 for the color palette.</param>
        private void Write8BitColor<TPixel>(Stream stream, ImageFrame<TPixel> image, Span<byte> colorPalette)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using IQuantizer<TPixel> frameQuantizer = this.quantizer.CreatePixelSpecificQuantizer<TPixel>(this.configuration);
            using IndexedImageFrame<TPixel> quantized = frameQuantizer.BuildPaletteAndQuantizeFrame(image, image.Bounds());

            ReadOnlySpan<TPixel> quantizedColorPalette = quantized.Palette.Span;
            this.WriteColorPalette(stream, quantizedColorPalette, colorPalette);

            for (int y = image.Height - 1; y >= 0; y--)
            {
                ReadOnlySpan<byte> pixelSpan = quantized.GetPixelRowSpan(y);
                stream.Write(pixelSpan);

                for (int i = 0; i < this.padding; i++)
                {
                    stream.WriteByte(0);
                }
            }
        }

        /// <summary>
        /// Writes an 8 bit gray image with a color palette. The color palette has 256 entry's with 4 bytes for each entry.
        /// </summary>
        /// <typeparam name="TPixel">The type of the pixel.</typeparam>
        /// <param name="stream">The <see cref="Stream"/> to write to.</param>
        /// <param name="image"> The <see cref="ImageFrame{TPixel}"/> containing pixel data.</param>
        /// <param name="colorPalette">A byte span of size 1024 for the color palette.</param>
        private void Write8BitGray<TPixel>(Stream stream, ImageFrame<TPixel> image, Span<byte> colorPalette)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            // Create a color palette with 256 different gray values.
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

            stream.Write(colorPalette);

            for (int y = image.Height - 1; y >= 0; y--)
            {
                ReadOnlySpan<TPixel> inputPixelRow = image.GetPixelRowSpan(y);
                ReadOnlySpan<byte> outputPixelRow = MemoryMarshal.AsBytes(inputPixelRow);
                stream.Write(outputPixelRow);

                for (int i = 0; i < this.padding; i++)
                {
                    stream.WriteByte(0);
                }
            }
        }

        /// <summary>
        /// Writes an 4 bit color image with a color palette. The color palette has 16 entry's with 4 bytes for each entry.
        /// </summary>
        /// <typeparam name="TPixel">The type of the pixel.</typeparam>
        /// <param name="stream">The <see cref="Stream"/> to write to.</param>
        /// <param name="image"> The <see cref="ImageFrame{TPixel}"/> containing pixel data.</param>
        private void Write4BitColor<TPixel>(Stream stream, ImageFrame<TPixel> image)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using IQuantizer<TPixel> frameQuantizer = this.quantizer.CreatePixelSpecificQuantizer<TPixel>(this.configuration, new QuantizerOptions()
            {
                MaxColors = 16
            });
            using IndexedImageFrame<TPixel> quantized = frameQuantizer.BuildPaletteAndQuantizeFrame(image, image.Bounds());
            using IMemoryOwner<byte> colorPaletteBuffer = this.memoryAllocator.Allocate<byte>(ColorPaletteSize4Bit, AllocationOptions.Clean);

            Span<byte> colorPalette = colorPaletteBuffer.GetSpan();
            ReadOnlySpan<TPixel> quantizedColorPalette = quantized.Palette.Span;
            this.WriteColorPalette(stream, quantizedColorPalette, colorPalette);

            ReadOnlySpan<byte> pixelRowSpan = quantized.GetPixelRowSpan(0);
            int rowPadding = pixelRowSpan.Length % 2 != 0 ? this.padding - 1 : this.padding;
            for (int y = image.Height - 1; y >= 0; y--)
            {
                pixelRowSpan = quantized.GetPixelRowSpan(y);

                int endIdx = pixelRowSpan.Length % 2 == 0 ? pixelRowSpan.Length : pixelRowSpan.Length - 1;
                for (int i = 0; i < endIdx; i += 2)
                {
                    stream.WriteByte((byte)((pixelRowSpan[i] << 4) | pixelRowSpan[i + 1]));
                }

                if (pixelRowSpan.Length % 2 != 0)
                {
                    stream.WriteByte((byte)((pixelRowSpan[pixelRowSpan.Length - 1] << 4) | 0));
                }

                for (int i = 0; i < rowPadding; i++)
                {
                    stream.WriteByte(0);
                }
            }
        }

        /// <summary>
        /// Writes a 1 bit image with a color palette. The color palette has 2 entry's with 4 bytes for each entry.
        /// </summary>
        /// <typeparam name="TPixel">The type of the pixel.</typeparam>
        /// <param name="stream">The <see cref="Stream"/> to write to.</param>
        /// <param name="image"> The <see cref="ImageFrame{TPixel}"/> containing pixel data.</param>
        private void Write1BitColor<TPixel>(Stream stream, ImageFrame<TPixel> image)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using IQuantizer<TPixel> frameQuantizer = this.quantizer.CreatePixelSpecificQuantizer<TPixel>(this.configuration, new QuantizerOptions()
            {
                MaxColors = 2
            });
            using IndexedImageFrame<TPixel> quantized = frameQuantizer.BuildPaletteAndQuantizeFrame(image, image.Bounds());
            using IMemoryOwner<byte> colorPaletteBuffer = this.memoryAllocator.Allocate<byte>(ColorPaletteSize1Bit, AllocationOptions.Clean);

            Span<byte> colorPalette = colorPaletteBuffer.GetSpan();
            ReadOnlySpan<TPixel> quantizedColorPalette = quantized.Palette.Span;
            this.WriteColorPalette(stream, quantizedColorPalette, colorPalette);

            ReadOnlySpan<byte> quantizedPixelRow = quantized.GetPixelRowSpan(0);
            int rowPadding = quantizedPixelRow.Length % 8 != 0 ? this.padding - 1 : this.padding;
            for (int y = image.Height - 1; y >= 0; y--)
            {
                quantizedPixelRow = quantized.GetPixelRowSpan(y);

                int endIdx = quantizedPixelRow.Length % 8 == 0 ? quantizedPixelRow.Length : quantizedPixelRow.Length - 8;
                for (int i = 0; i < endIdx; i += 8)
                {
                    Write1BitPalette(stream, i, i + 8, quantizedPixelRow);
                }

                if (quantizedPixelRow.Length % 8 != 0)
                {
                    int startIdx = quantizedPixelRow.Length - 7;
                    endIdx = quantizedPixelRow.Length;
                    Write1BitPalette(stream, startIdx, endIdx, quantizedPixelRow);
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
        /// <param name="stream">The <see cref="Stream"/> to write to.</param>
        /// <param name="quantizedColorPalette">The color palette from the quantized image.</param>
        /// <param name="colorPalette">A temporary byte span to write the color palette to.</param>
        private void WriteColorPalette<TPixel>(Stream stream, ReadOnlySpan<TPixel> quantizedColorPalette, Span<byte> colorPalette)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            int quantizedColorBytes = quantizedColorPalette.Length * 4;
            PixelOperations<TPixel>.Instance.ToBgra32(this.configuration, quantizedColorPalette, MemoryMarshal.Cast<byte, Bgra32>(colorPalette.Slice(0, quantizedColorBytes)));
            Span<uint> colorPaletteAsUInt = MemoryMarshal.Cast<byte, uint>(colorPalette);
            for (int i = 0; i < colorPaletteAsUInt.Length; i++)
            {
                colorPaletteAsUInt[i] = colorPaletteAsUInt[i] & 0x00FFFFFF; // Padding byte, always 0.
            }

            stream.Write(colorPalette);
        }

        /// <summary>
        /// Writes a 1-bit palette.
        /// </summary>
        /// <param name="stream">The stream to write the palette to.</param>
        /// <param name="startIdx">The start index.</param>
        /// <param name="endIdx">The end index.</param>
        /// <param name="quantizedPixelRow">A quantized pixel row.</param>
        private static void Write1BitPalette(Stream stream, int startIdx, int endIdx, ReadOnlySpan<byte> quantizedPixelRow)
        {
            int shift = 7;
            byte indices = 0;
            for (int j = startIdx; j < endIdx; j++)
            {
                indices = (byte)(indices | ((byte)(quantizedPixelRow[j] & 1) << shift));
                shift--;
            }

            stream.WriteByte(indices);
        }
    }
}
