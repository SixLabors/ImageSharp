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
    /// On-stack worker struct to convert TPixel -> Rgb24 of 8x8 pixel blocks.
    /// </summary>
    /// <typeparam name="TPixel">The pixel type to work on.</typeparam>
    internal ref struct RgbForwardConverter<TPixel>
        where TPixel : unmanaged, IPixel<TPixel>
    {
        /// <summary>
        /// Number of pixels processed per single <see cref="Convert(int, int, ref RowOctet{TPixel})"/> call
        /// </summary>
        private const int PixelsPerSample = 8 * 8;

        /// <summary>
        /// Total byte size of processed pixels converted from TPixel to <see cref="Rgb24"/>
        /// </summary>
        private const int RgbSpanByteSize = PixelsPerSample * 3;

        /// <summary>
        /// The Red component.
        /// </summary>
        public Block8x8F R;

        /// <summary>
        /// The Green component.
        /// </summary>
        public Block8x8F G;

        /// <summary>
        /// The Blue component.
        /// </summary>
        public Block8x8F B;

        /// <summary>
        /// Temporal 64-byte span to hold unconverted TPixel data.
        /// </summary>
        private readonly Span<TPixel> pixelSpan;

        /// <summary>
        /// Temporal 64-byte span to hold converted Rgb24 data.
        /// </summary>
        private readonly Span<Rgb24> rgbSpan;

        /// <summary>
        /// Sampled pixel buffer size.
        /// </summary>
        private readonly Size samplingAreaSize;

        /// <summary>
        /// <see cref="Configuration"/> for internal operations.
        /// </summary>
        private readonly Configuration config;

        public RgbForwardConverter(ImageFrame<TPixel> frame)
        {
            this.R = default;
            this.G = default;
            this.B = default;

            // temporal pixel buffers
            this.pixelSpan = new TPixel[PixelsPerSample].AsSpan();
            this.rgbSpan = MemoryMarshal.Cast<byte, Rgb24>(new byte[RgbSpanByteSize + RgbToYCbCrConverterVectorized.AvxCompatibilityPadding].AsSpan());

            // frame data
            this.samplingAreaSize = new Size(frame.Width, frame.Height);
            this.config = frame.GetConfiguration();
        }

        /// <summary>
        /// Gets size of sampling area from given frame pixel buffer.
        /// </summary>
        private static Size SampleSize => new(8, 8);

        /// <summary>
        /// Converts a 8x8 image area inside 'pixels' at position (x, y) to Rgb24.
        /// </summary>
        public void Convert(int x, int y, ref RowOctet<TPixel> currentRows)
        {
            YCbCrForwardConverter<TPixel>.LoadAndStretchEdges(currentRows, this.pixelSpan, new Point(x, y), SampleSize, this.samplingAreaSize);

            PixelOperations<TPixel>.Instance.ToRgb24(this.config, this.pixelSpan, this.rgbSpan);

            ref Block8x8F redBlock = ref this.R;
            ref Block8x8F greenBlock = ref this.G;
            ref Block8x8F blueBlock = ref this.B;

            CopyToBlock(this.rgbSpan, ref redBlock, ref greenBlock, ref blueBlock);
        }

        private static void CopyToBlock(Span<Rgb24> rgbSpan, ref Block8x8F redBlock, ref Block8x8F greenBlock, ref Block8x8F blueBlock)
        {
            ref Rgb24 rgbStart = ref MemoryMarshal.GetReference(rgbSpan);

            for (int i = 0; i < Block8x8F.Size; i++)
            {
                Rgb24 c = Unsafe.Add(ref rgbStart, (nint)(uint)i);

                redBlock[i] = c.R;
                greenBlock[i] = c.G;
                blueBlock[i] = c.B;
            }
        }
    }
}
