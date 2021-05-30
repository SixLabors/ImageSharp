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
    internal ref struct YCbCrForwardConverter444<TPixel>
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // TODO: documentation
        private const int RgbSpanByteSize = 8 * 8 * 3;
        // TODO: documentation
        private const int PixelSpanSize = 8 * 8;


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
        /// Temporal 64-byte span to hold unconverted TPixel data
        /// </summary>
        private Span<TPixel> pixelSpan;

        /// <summary>
        /// Temporal 64-byte span to hold converted Rgb24 data
        /// </summary>
        private Span<Rgb24> rgbSpan;

        public static YCbCrForwardConverter444<TPixel> Create()
        {
            var result = default(YCbCrForwardConverter444<TPixel>);

            // creating rgb pixel bufferr
            // TODO: this is subject to discuss
            // converter.Convert comments for +8 padding
            result.rgbSpan = MemoryMarshal.Cast<byte, Rgb24>(new byte[RgbSpanByteSize + 8].AsSpan());

            // TODO: this is subject to discuss
            result.pixelSpan = new TPixel[PixelSpanSize].AsSpan();

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
            YCbCrForwardConverter<TPixel>.LoadAndStretchEdges(currentRows, this.pixelSpan, new Point(x, y), new Size(8), new Size(buffer.Width, buffer.Height));

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
    }
}
