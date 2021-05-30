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
    internal ref struct YCbCrForwardConverter420<TPixel>
        where TPixel : unmanaged, IPixel<TPixel>
    {
        /// <summary>
        /// The left Y component
        /// </summary>
        public Block8x8F YLeft;

        /// <summary>
        /// The left Y component
        /// </summary>
        public Block8x8F YRight;

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
        /// Temporal 16x8 block to hold TPixel data
        /// </summary>
        private Span<TPixel> pixelSpan;

        /// <summary>
        /// Temporal RGB block
        /// </summary>
        private Span<Rgb24> rgbSpan;

        public static YCbCrForwardConverter420<TPixel> Create()
        {
            var result = default(YCbCrForwardConverter420<TPixel>);

            // TODO: this is subject to discuss
            const int twoBlocksByteSizeWithPadding = 384 + 8; // converter.Convert comments for +8 padding
            result.rgbSpan = MemoryMarshal.Cast<byte, Rgb24>(new byte[twoBlocksByteSizeWithPadding].AsSpan());

            // TODO: this size should be configurable
            result.pixelSpan = new TPixel[128].AsSpan();

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
        public void Convert(ImageFrame<TPixel> frame, int x, int y, ref RowOctet<TPixel> currentRows, int idx)
        {
            Memory.Buffer2D<TPixel> buffer = frame.PixelBuffer;
            YCbCrForwardConverter<TPixel>.LoadAndStretchEdges(currentRows, this.pixelSpan, new Point(x, y), new Size(16, 8), new Size(buffer.Width, buffer.Height));

            PixelOperations<TPixel>.Instance.ToRgb24(frame.GetConfiguration(), this.pixelSpan, this.rgbSpan);

            if (RgbToYCbCrConverterVectorized.IsSupported)
            {
                RgbToYCbCrConverterVectorized.Convert420_16x8(this.rgbSpan, ref this.YLeft, ref this.YRight, ref this.Cb, ref this.Cr, idx);
            }
            else
            {
                throw new NotSupportedException("This is not yet implemented");
                //this.colorTables.Convert(this.rgbSpan, ref yBlock, ref cbBlock, ref crBlock);
            }
        }
    }
}
