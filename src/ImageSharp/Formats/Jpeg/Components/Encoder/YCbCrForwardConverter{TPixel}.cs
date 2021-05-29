// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
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
        private GenericBlock8x8<TPixel> pixelBlock;

        /// <summary>
        /// Temporal RGB block
        /// </summary>
        private Span<Rgb24> rgbSpan;

        public static YCbCrForwardConverter<TPixel> Create()
        {
            var result = default(YCbCrForwardConverter<TPixel>);

            // creating rgb pixel bufferr
            // TODO: this is subject to discuss
            result.rgbSpan = MemoryMarshal.Cast<byte, Rgb24>(new byte[200].AsSpan());

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
            this.pixelBlock.LoadAndStretchEdges(frame.PixelBuffer, x, y, ref currentRows);

            PixelOperations<TPixel>.Instance.ToRgb24(frame.GetConfiguration(), this.pixelBlock.AsSpanUnsafe(), this.rgbSpan);

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
            this.pixelBlock.LoadAndStretchEdges(frame.PixelBuffer, x, y, ref currentRows);

            PixelOperations<TPixel>.Instance.ToRgb24(frame.GetConfiguration(), this.pixelBlock.AsSpanUnsafe(), this.rgbSpan);

            if (RgbToYCbCrConverterVectorized.IsSupported)
            {
                RgbToYCbCrConverterVectorized.Convert420(this.rgbSpan, ref this.Y, ref this.Cb, ref this.Cr, idx);
            }
            else
            {
                throw new NotSupportedException("This is not yet implemented");
                //this.colorTables.Convert(this.rgbSpan, ref yBlock, ref cbBlock, ref crBlock);
            }
        }
    }
}
