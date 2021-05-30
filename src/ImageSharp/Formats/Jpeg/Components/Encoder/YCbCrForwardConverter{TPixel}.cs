// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Encoder
{
    /// <summary>
    /// On-stack worker struct to efficiently encapsulate the TPixel -> Rgb24 -> YCbCr conversion chain of 8x8 pixel blocks.
    /// </summary>
    /// <typeparam name="TPixel">The pixel type to work on</typeparam>
    internal ref struct YCbCrForwardConverter<TPixel>
        where TPixel : unmanaged, IPixel<TPixel>
    {
        /// <summary>
        /// The Y component
        /// </summary>
        public Block8x8F Y;

        /// <summary>
        /// The Cb component
        /// </summary>
        public Block8x8F Cb;

        /// <summary>
        /// The Cr component
        /// </summary>
        public Block8x8F Cr;

        /// <summary>
        /// The color conversion tables
        /// </summary>
        private RgbToYCbCrConverterLut colorTables;

        /// <summary>
        /// Temporal 8x8 block to hold TPixel data
        /// </summary>
        private Span<TPixel> pixelSpan;

        /// <summary>
        /// Temporal RGB block
        /// </summary>
        private Span<Rgb24> rgbSpan;

        public Span<Block8x8F> twinBlocksY;

        public static YCbCrForwardConverter<TPixel> Create()
        {
            var result = default(YCbCrForwardConverter<TPixel>);

            // creating rgb pixel bufferr
            // TODO: this is subject to discuss
            const int twoBlocksByteSizeWithPadding = 384 + 8; // converter.Convert comments for +8 padding
            result.rgbSpan = MemoryMarshal.Cast<byte, Rgb24>(new byte[twoBlocksByteSizeWithPadding].AsSpan());
            // TODO: this size should be configurable
            result.pixelSpan = new TPixel[128].AsSpan();

            result.twinBlocksY = new Block8x8F[2].AsSpan();

            // Avoid creating lookup tables, when vectorized converter is supported
            if (!RgbToYCbCrConverterVectorized.IsSupported)
            {
                result.colorTables = RgbToYCbCrConverterLut.Create();
            }

            return result;
        }

        /// <summary>
        /// Converts a 8x8 image area inside 'pixels' at position (x,y) placing the result members of the structure (<see cref="Y"/>, <see cref="Cb"/>, <see cref="Cr"/>)
        /// </summary>
        public void Convert(ImageFrame<TPixel> frame, int x, int y, ref RowOctet<TPixel> currentRows)
        {
            Memory.Buffer2D<TPixel> buffer = frame.PixelBuffer;
            LoadAndStretchEdges(currentRows, this.pixelSpan, x, y, new Size(8), new Size(buffer.Width, buffer.Height));

            PixelOperations<TPixel>.Instance.ToRgb24(frame.GetConfiguration(), this.pixelSpan, this.rgbSpan);

            ref Block8x8F yBlock = ref this.Y;
            ref Block8x8F cbBlock = ref this.Cb;
            ref Block8x8F crBlock = ref this.Cr;

            if (RgbToYCbCrConverterVectorized.IsSupported)
            {
                RgbToYCbCrConverterVectorized.Convert(this.rgbSpan, ref yBlock, ref cbBlock, ref crBlock);
            }
            else
            {
                this.colorTables.Convert(this.rgbSpan, ref yBlock, ref cbBlock, ref crBlock);
            }
        }

        /// <summary>
        /// Converts a 8x8 image area inside 'pixels' at position (x,y) placing the result members of the structure (<see cref="Y"/>, <see cref="Cb"/>, <see cref="Cr"/>)
        /// </summary>
        public void Convert420(ImageFrame<TPixel> frame, int x, int y, ref RowOctet<TPixel> currentRows, int idx)
        {
            Memory.Buffer2D<TPixel> buffer = frame.PixelBuffer;
            LoadAndStretchEdges(currentRows, this.pixelSpan, x, y, new Size(16, 8), new Size(buffer.Width, buffer.Height));

            PixelOperations<TPixel>.Instance.ToRgb24(frame.GetConfiguration(), this.pixelSpan, this.rgbSpan);

            if (RgbToYCbCrConverterVectorized.IsSupported)
            {
                RgbToYCbCrConverterVectorized.Convert420_16x8(this.rgbSpan, this.twinBlocksY, ref this.Cb, ref this.Cr, idx);
            }
            else
            {
                throw new NotSupportedException("This is not yet implemented");
                //this.colorTables.Convert(this.rgbSpan, ref yBlock, ref cbBlock, ref crBlock);
            }
        }


        private static void LoadAndStretchEdges(RowOctet<TPixel> source, Span<TPixel> dest, int startX, int startY, Size areaSize, Size borders)
        {
            int width = Math.Min(areaSize.Width, borders.Width - startX);
            int height = Math.Min(areaSize.Height, borders.Height - startY);

            uint byteWidth = (uint)(width * Unsafe.SizeOf<TPixel>());
            int remainderXCount = areaSize.Width - width;

            ref byte blockStart = ref MemoryMarshal.GetReference(MemoryMarshal.Cast<TPixel, byte>(dest));
            int rowSizeInBytes = areaSize.Width * Unsafe.SizeOf<TPixel>();

            for (int y = 0; y < height; y++)
            {
                Span<TPixel> row = source[y];

                ref byte s = ref Unsafe.As<TPixel, byte>(ref row[startX]);
                ref byte d = ref Unsafe.Add(ref blockStart, y * rowSizeInBytes);

                Unsafe.CopyBlock(ref d, ref s, byteWidth);

                ref TPixel last = ref Unsafe.Add(ref Unsafe.As<byte, TPixel>(ref d), width - 1);

                for (int x = 1; x <= remainderXCount; x++)
                {
                    Unsafe.Add(ref last, x) = last;
                }
            }

            int remainderYCount = areaSize.Height - height;

            if (remainderYCount == 0)
            {
                return;
            }

            ref byte lastRowStart = ref Unsafe.Add(ref blockStart, (height - 1) * rowSizeInBytes);

            for (int y = 1; y <= remainderYCount; y++)
            {
                ref byte remStart = ref Unsafe.Add(ref lastRowStart, rowSizeInBytes * y);
                Unsafe.CopyBlock(ref remStart, ref lastRowStart, (uint)rowSizeInBytes);
            }
        }
    }
}
