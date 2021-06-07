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
        // TODO: docs
        private const int PixelsPerSample = 8 * 8;

        // TODO: docs
        private const int RgbSpanByteSize = PixelsPerSample * 3;

        // TODO: docs
        private static readonly Size SampleSize = new Size(8, 8);


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

        // TODO: docs
        private Size samplingAreaSize;

        // TODO: docs
        private readonly Configuration config;

        public YCbCrForwardConverter444(ImageFrame<TPixel> frame)
        {
            // matrices would be filled during convert calls
            this.Y = default;
            this.Cb = default;
            this.Cr = default;

            // temporal pixel buffers
            this.pixelSpan = new TPixel[PixelsPerSample].AsSpan();
            this.rgbSpan = MemoryMarshal.Cast<byte, Rgb24>(new byte[RgbSpanByteSize + RgbToYCbCrConverterVectorized.AvxCompatibilityPadding].AsSpan());

            // frame data
            this.samplingAreaSize = new Size(frame.Width, frame.Height);
            this.config = frame.GetConfiguration();

            // conversion vector fallback data
            if (!RgbToYCbCrConverterVectorized.IsSupported)
            {
                this.colorTables = RgbToYCbCrConverterLut.Create();
            }
            else
            {
                this.colorTables = default;
            }
        }

        /// <summary>
        /// Converts a 8x8 image area inside 'pixels' at position (x,y) placing the result members of the structure (<see cref="Y"/>, <see cref="Cb"/>, <see cref="Cr"/>)
        /// </summary>
        public void Convert(int x, int y, ref RowOctet<TPixel> currentRows)
        {
            YCbCrForwardConverter<TPixel>.LoadAndStretchEdges(currentRows, this.pixelSpan, new Point(x, y), SampleSize, this.samplingAreaSize);

            PixelOperations<TPixel>.Instance.ToRgb24(this.config, this.pixelSpan, this.rgbSpan);

            ref Block8x8F yBlock = ref this.Y;
            ref Block8x8F cbBlock = ref this.Cb;
            ref Block8x8F crBlock = ref this.Cr;

            if (RgbToYCbCrConverterVectorized.IsSupported)
            {
                RgbToYCbCrConverterVectorized.Convert444(this.rgbSpan, ref yBlock, ref cbBlock, ref crBlock);
            }
            else
            {
                this.colorTables.Convert444(this.rgbSpan, ref yBlock, ref cbBlock, ref crBlock);
            }
        }
    }
}
