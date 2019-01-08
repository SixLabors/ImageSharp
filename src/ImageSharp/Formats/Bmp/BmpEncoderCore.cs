// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;

using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.MetaData;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Memory;

namespace SixLabors.ImageSharp.Formats.Bmp
{
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
        /// The mask for the alpha channel of the color for a 32 bit rgba bitmaps.
        /// </summary>
        private const int Rgba32AlphaMask = 0xFF << 24;

        /// <summary>
        /// The mask for the red part of the color for a 32 bit rgba bitmaps.
        /// </summary>
        private const int Rgba32RedMask = 0xFF << 16;

        /// <summary>
        /// The mask for the green part of the color for a 32 bit rgba bitmaps.
        /// </summary>
        private const int Rgba32GreenMask = 0xFF << 8;

        /// <summary>
        /// The mask for the blue part of the color for a 32 bit rgba bitmaps.
        /// </summary>
        private const int Rgba32BlueMask = 0xFF;

        private readonly MemoryAllocator memoryAllocator;

        private Configuration configuration;

        private BmpBitsPerPixel? bitsPerPixel;

        /// <summary>
        /// Initializes a new instance of the <see cref="BmpEncoderCore"/> class.
        /// </summary>
        /// <param name="options">The encoder options</param>
        /// <param name="memoryAllocator">The memory manager</param>
        public BmpEncoderCore(IBmpEncoderOptions options, MemoryAllocator memoryAllocator)
        {
            this.memoryAllocator = memoryAllocator;
            this.bitsPerPixel = options.BitsPerPixel;
        }

        /// <summary>
        /// Encodes the image to the specified stream from the <see cref="ImageFrame{TPixel}"/>.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="image">The <see cref="ImageFrame{TPixel}"/> to encode from.</param>
        /// <param name="stream">The <see cref="Stream"/> to encode the image data to.</param>
        public void Encode<TPixel>(Image<TPixel> image, Stream stream)
            where TPixel : struct, IPixel<TPixel>
        {
            Guard.NotNull(image, nameof(image));
            Guard.NotNull(stream, nameof(stream));

            this.configuration = image.GetConfiguration();
            ImageMetaData metaData = image.MetaData;
            BmpMetaData bmpMetaData = metaData.GetFormatMetaData(BmpFormat.Instance);
            this.bitsPerPixel = this.bitsPerPixel ?? bmpMetaData.BitsPerPixel;

            short bpp = (short)this.bitsPerPixel;
            int bytesPerLine = 4 * (((image.Width * bpp) + 31) / 32);
            this.padding = bytesPerLine - (int)(image.Width * (bpp / 8F));

            // Set Resolution.
            int hResolution = 0;
            int vResolution = 0;

            if (metaData.ResolutionUnits != PixelResolutionUnit.AspectRatio)
            {
                if (metaData.HorizontalResolution > 0 && metaData.VerticalResolution > 0)
                {
                    switch (metaData.ResolutionUnits)
                    {
                        case PixelResolutionUnit.PixelsPerInch:

                            hResolution = (int)Math.Round(UnitConverter.InchToMeter(metaData.HorizontalResolution));
                            vResolution = (int)Math.Round(UnitConverter.InchToMeter(metaData.VerticalResolution));
                            break;

                        case PixelResolutionUnit.PixelsPerCentimeter:

                            hResolution = (int)Math.Round(UnitConverter.CmToMeter(metaData.HorizontalResolution));
                            vResolution = (int)Math.Round(UnitConverter.CmToMeter(metaData.VerticalResolution));
                            break;

                        case PixelResolutionUnit.PixelsPerMeter:
                            hResolution = (int)Math.Round(metaData.HorizontalResolution);
                            vResolution = (int)Math.Round(metaData.VerticalResolution);

                            break;
                    }
                }
            }

            int infoHeaderSize = BmpInfoHeader.SizeV4;
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

            infoHeader.RedMask = Rgba32RedMask;
            infoHeader.GreenMask = Rgba32GreenMask;
            infoHeader.BlueMask = Rgba32BlueMask;
            infoHeader.Compression = BmpCompression.BitFields;
            if (this.bitsPerPixel == BmpBitsPerPixel.Pixel32)
            {
                infoHeader.AlphaMask = Rgba32AlphaMask;
            }

            var fileHeader = new BmpFileHeader(
                type: BmpConstants.TypeMarkers.Bitmap,
                fileSize: BmpFileHeader.Size + infoHeaderSize + infoHeader.ImageSize,
                reserved: 0,
                offset: BmpFileHeader.Size + infoHeaderSize);

#if NETCOREAPP2_1
            Span<byte> buffer = stackalloc byte[infoHeaderSize];
#else
            byte[] buffer = new byte[infoHeaderSize];
#endif
            fileHeader.WriteTo(buffer);

            stream.Write(buffer, 0, BmpFileHeader.Size);

            infoHeader.WriteV4Header(buffer);

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
            where TPixel : struct, IPixel<TPixel>
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
            }
        }

        private IManagedByteBuffer AllocateRow(int width, int bytesPerPixel) => this.memoryAllocator.AllocatePaddedPixelRowBuffer(width, bytesPerPixel, this.padding);

        /// <summary>
        /// Writes the 32bit color palette to the stream.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="stream">The <see cref="Stream"/> to write to.</param>
        /// <param name="pixels">The <see cref="Buffer2D{TPixel}"/> containing pixel data.</param>
        private void Write32Bit<TPixel>(Stream stream, Buffer2D<TPixel> pixels)
            where TPixel : struct, IPixel<TPixel>
        {
            using (IManagedByteBuffer row = this.AllocateRow(pixels.Width, 4))
            {
                for (int y = pixels.Height - 1; y >= 0; y--)
                {
                    Span<TPixel> pixelSpan = pixels.GetRowSpan(y);
                    PixelOperations<TPixel>.Instance.ToBgra32Bytes(
                        this.configuration,
                        pixelSpan,
                        row.GetSpan(),
                        pixelSpan.Length);
                    stream.Write(row.Array, 0, row.Length());
                }
            }
        }

        /// <summary>
        /// Writes the 24bit color palette to the stream.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="stream">The <see cref="Stream"/> to write to.</param>
        /// <param name="pixels">The <see cref="Buffer2D{TPixel}"/> containing pixel data.</param>
        private void Write24Bit<TPixel>(Stream stream, Buffer2D<TPixel> pixels)
            where TPixel : struct, IPixel<TPixel>
        {
            using (IManagedByteBuffer row = this.AllocateRow(pixels.Width, 3))
            {
                for (int y = pixels.Height - 1; y >= 0; y--)
                {
                    Span<TPixel> pixelSpan = pixels.GetRowSpan(y);
                    PixelOperations<TPixel>.Instance.ToBgr24Bytes(
                        this.configuration,
                        pixelSpan,
                        row.GetSpan(),
                        pixelSpan.Length);
                    stream.Write(row.Array, 0, row.Length());
                }
            }
        }
    }
}
