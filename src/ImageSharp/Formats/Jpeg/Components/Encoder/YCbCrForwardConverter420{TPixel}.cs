// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

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
    internal ref struct YCbCrForwardConverter420<TPixel>
        where TPixel : unmanaged, IPixel<TPixel>
    {
        /// <summary>
        /// Number of pixels processed per single <see cref="Convert(int, int, ref RowOctet{TPixel}, int)"/> call
        /// </summary>
        private const int PixelsPerSample = 16 * 8;

        /// <summary>
        /// Total byte size of processed pixels converted from TPixel to <see cref="Rgb24"/>
        /// </summary>
        private const int RgbSpanByteSize = PixelsPerSample * 3;

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
        private readonly Span<TPixel> pixelSpan;

        /// <summary>
        /// Temporal RGB block
        /// </summary>
        private readonly Span<Rgb24> rgbSpan;

        /// <summary>
        /// Sampled pixel buffer size
        /// </summary>
        private readonly Size samplingAreaSize;

        /// <summary>
        /// <see cref="Configuration"/> for internal operations
        /// </summary>
        private readonly Configuration config;

        public YCbCrForwardConverter420(ImageFrame<TPixel> frame)
        {
            // matrices would be filled during convert calls
            this.YLeft = default;
            this.YRight = default;
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
        /// Gets size of sampling area from given frame pixel buffer.
        /// </summary>
        private static Size SampleSize => new(16, 8);

        public void Convert(int x, int y, ref RowOctet<TPixel> currentRows, int idx)
        {
            YCbCrForwardConverter<TPixel>.LoadAndStretchEdges(currentRows, this.pixelSpan, new Point(x, y), SampleSize, this.samplingAreaSize);

            PixelOperations<TPixel>.Instance.ToRgb24(this.config, this.pixelSpan, this.rgbSpan);

            if (RgbToYCbCrConverterVectorized.IsSupported)
            {
                RgbToYCbCrConverterVectorized.Convert420(this.rgbSpan, ref this.YLeft, ref this.YRight, ref this.Cb, ref this.Cr, idx);
            }
            else
            {
                this.colorTables.Convert420(this.rgbSpan, ref this.YLeft, ref this.YRight, ref this.Cb, ref this.Cr, idx);
            }
        }
    }
}
