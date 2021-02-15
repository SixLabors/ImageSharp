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
    internal class TiffPaletteWriter<TPixel> : TiffBaseColorWriter<TPixel>
        where TPixel : unmanaged, IPixel<TPixel>
    {
        private const int ColorsPerChannel = 256;
        private const int ColorPaletteSize = ColorsPerChannel * 3;
        private const int ColorPaletteBytes = ColorPaletteSize * 2;

        private readonly IndexedImageFrame<TPixel> quantized;

        public TiffPaletteWriter(ImageFrame<TPixel> image, IQuantizer quantizer, MemoryAllocator memoryAllocator, Configuration configuration, TiffEncoderEntriesCollector entriesCollector)
             : base(image, memoryAllocator, configuration, entriesCollector)
        {
            using IQuantizer<TPixel> frameQuantizer = quantizer.CreatePixelSpecificQuantizer<TPixel>(this.Configuration);
            this.quantized = frameQuantizer.BuildPaletteAndQuantizeFrame(image, image.Bounds());

            this.AddTag(this.quantized);
        }

        /// <inheritdoc />
        public override int BitsPerPixel => 8;

        /// <inheritdoc />
        protected override void EncodeStrip(int y, int height, TiffBaseCompressor compressor)
        {
            Span<byte> pixels = GetStripPixels(((IPixelSource)this.quantized).PixelBuffer, y, height);
            compressor.CompressStrip(pixels, height);
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing) => this.quantized?.Dispose();

        private void AddTag(IndexedImageFrame<TPixel> quantized)
        {
            using IMemoryOwner<byte> colorPaletteBuffer = this.MemoryAllocator.AllocateManagedByteBuffer(ColorPaletteBytes);
            Span<byte> colorPalette = colorPaletteBuffer.GetSpan();

            ReadOnlySpan<TPixel> quantizedColors = quantized.Palette.Span;
            int quantizedColorBytes = quantizedColors.Length * 3 * 2;

            // In the ColorMap, black is represented by 0,0,0 and white is represented by 65535, 65535, 65535.
            Span<Rgb48> quantizedColorRgb48 = MemoryMarshal.Cast<byte, Rgb48>(colorPalette.Slice(0, quantizedColorBytes));
            PixelOperations<TPixel>.Instance.ToRgb48(this.Configuration, quantizedColors, quantizedColorRgb48);

            // It can happen that the quantized colors are less than the expected 256 per channel.
            var diffToMaxColors = ColorsPerChannel - quantizedColors.Length;

            // In a TIFF ColorMap, all the Red values come first, followed by the Green values,
            // then the Blue values. Convert the quantized palette to this format.
            var palette = new ushort[ColorPaletteSize];
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
