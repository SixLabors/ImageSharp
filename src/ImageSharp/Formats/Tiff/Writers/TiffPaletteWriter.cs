// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Runtime.InteropServices;

using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Formats.Experimental.Tiff.Compression;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Quantization;

namespace SixLabors.ImageSharp.Formats.Experimental.Tiff.Writers
{
    internal sealed class TiffPaletteWriter<TPixel> : TiffBaseColorWriter<TPixel>
        where TPixel : unmanaged, IPixel<TPixel>
    {
        private readonly int maxColors;
        private readonly int colorPaletteSize;
        private readonly int colorPaletteBytes;
        private readonly IndexedImageFrame<TPixel> quantizedImage;

        public TiffPaletteWriter(
            ImageFrame<TPixel> image,
            IQuantizer quantizer,
            MemoryAllocator memoryAllocator,
            Configuration configuration,
            TiffEncoderEntriesCollector entriesCollector,
            int bitsPerPixel)
             : base(image, memoryAllocator, configuration, entriesCollector)
        {
            DebugGuard.NotNull(quantizer, nameof(quantizer));
            DebugGuard.NotNull(configuration, nameof(configuration));
            DebugGuard.NotNull(entriesCollector, nameof(entriesCollector));
            DebugGuard.MustBeBetweenOrEqualTo(bitsPerPixel, 4, 8, nameof(bitsPerPixel));

            this.BitsPerPixel = bitsPerPixel;
            this.maxColors = this.BitsPerPixel == 4 ? 16 : 256;
            this.colorPaletteSize = this.maxColors * 3;
            this.colorPaletteBytes = this.colorPaletteSize * 2;
            using IQuantizer<TPixel> frameQuantizer = quantizer.CreatePixelSpecificQuantizer<TPixel>(this.Configuration, new QuantizerOptions()
            {
                MaxColors = this.maxColors
            });
            this.quantizedImage = frameQuantizer.BuildPaletteAndQuantizeFrame(image, image.Bounds());

            this.AddColorMapTag();
        }

        /// <inheritdoc />
        public override int BitsPerPixel { get; }

        /// <inheritdoc />
        protected override void EncodeStrip(int y, int height, TiffBaseCompressor compressor)
        {
            Span<byte> pixels = GetStripPixels(((IPixelSource)this.quantizedImage).PixelBuffer, y, height);
            if (this.BitsPerPixel == 4)
            {
                using IMemoryOwner<byte> rows4bitBuffer = this.MemoryAllocator.Allocate<byte>(pixels.Length / 2);
                Span<byte> rows4bit = rows4bitBuffer.GetSpan();
                int idx = 0;
                for (int i = 0; i < rows4bit.Length; i++)
                {
                    rows4bit[i] = (byte)((pixels[idx] << 4) | (pixels[idx + 1] & 0xF));
                    idx += 2;
                }

                compressor.CompressStrip(rows4bit, height);
            }
            else
            {
                compressor.CompressStrip(pixels, height);
            }
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing) => this.quantizedImage?.Dispose();

        private void AddColorMapTag()
        {
            using IMemoryOwner<byte> colorPaletteBuffer = this.MemoryAllocator.AllocateManagedByteBuffer(this.colorPaletteBytes);
            Span<byte> colorPalette = colorPaletteBuffer.GetSpan();

            ReadOnlySpan<TPixel> quantizedColors = this.quantizedImage.Palette.Span;
            int quantizedColorBytes = quantizedColors.Length * 3 * 2;

            // In the ColorMap, black is represented by 0, 0, 0 and white is represented by 65535, 65535, 65535.
            Span<Rgb48> quantizedColorRgb48 = MemoryMarshal.Cast<byte, Rgb48>(colorPalette.Slice(0, quantizedColorBytes));
            PixelOperations<TPixel>.Instance.ToRgb48(this.Configuration, quantizedColors, quantizedColorRgb48);

            // It can happen that the quantized colors are less than the expected maximum per channel.
            var diffToMaxColors = this.maxColors - quantizedColors.Length;

            // In a TIFF ColorMap, all the Red values come first, followed by the Green values,
            // then the Blue values. Convert the quantized palette to this format.
            var palette = new ushort[this.colorPaletteSize];
            int paletteIdx = 0;
            for (int i = 0; i < quantizedColors.Length; i++)
            {
                palette[paletteIdx++] = quantizedColorRgb48[i].R;
            }

            paletteIdx += diffToMaxColors;

            for (int i = 0; i < quantizedColors.Length; i++)
            {
                palette[paletteIdx++] = quantizedColorRgb48[i].G;
            }

            paletteIdx += diffToMaxColors;

            for (int i = 0; i < quantizedColors.Length; i++)
            {
                palette[paletteIdx++] = quantizedColorRgb48[i].B;
            }

            var colorMap = new ExifShortArray(ExifTagValue.ColorMap)
            {
                Value = palette
            };

            this.EntriesCollector.Add(colorMap);
        }
    }
}
