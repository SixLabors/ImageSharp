// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;
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

        private readonly MemoryAllocator memoryAllocator;

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

            var infoHeader = new BmpInfoHeader(
                headerSize: BmpInfoHeader.Size,
                height: image.Height,
                width: image.Width,
                bitsPerPixel: bpp,
                planes: 1,
                imageSize: image.Height * bytesPerLine,
                clrUsed: 0,
                clrImportant: 0,
                xPelsPerMeter: hResolution,
                yPelsPerMeter: vResolution);

            var fileHeader = new BmpFileHeader(
                type: 19778, // BM
                offset: 54,
                reserved: 0,
                fileSize: 54 + infoHeader.ImageSize);

#if NETCOREAPP2_1
            Span<byte> buffer = stackalloc byte[40];
#else
            byte[] buffer = new byte[40];
#endif
            fileHeader.WriteTo(buffer);

            stream.Write(buffer, 0, BmpFileHeader.Size);

            infoHeader.WriteTo(buffer);

            stream.Write(buffer, 0, 40);

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
                    PixelOperations<TPixel>.Instance.ToBgra32Bytes(pixelSpan, row.GetSpan(), pixelSpan.Length);
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
                    PixelOperations<TPixel>.Instance.ToBgr24Bytes(pixelSpan, row.GetSpan(), pixelSpan.Length);
                    stream.Write(row.Array, 0, row.Length());
                }
            }
        }
    }
}
