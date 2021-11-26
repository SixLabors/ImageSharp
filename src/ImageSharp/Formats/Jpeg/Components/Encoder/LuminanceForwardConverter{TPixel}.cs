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
    /// On-stack worker struct to efficiently encapsulate the TPixel -> L8 -> Y conversion chain of 8x8 pixel blocks.
    /// </summary>
    /// <typeparam name="TPixel">The pixel type to work on</typeparam>
    internal ref struct LuminanceForwardConverter<TPixel>
        where TPixel : unmanaged, IPixel<TPixel>
    {
        /// <summary>
        /// Number of pixels processed per single <see cref="Convert(int, int, ref RowOctet{TPixel})"/> call
        /// </summary>
        private const int PixelsPerSample = 8 * 8;

        /// <summary>
        /// <see cref="Size"/> of sampling area from given frame pixel buffer.
        /// </summary>
        private static readonly Size SampleSize = new Size(8, 8);

        /// <summary>
        /// The Y component
        /// </summary>
        public Block8x8F Y;

        /// <summary>
        /// Temporal 64-pixel span to hold unconverted TPixel data.
        /// </summary>
        private readonly Span<TPixel> pixelSpan;

        /// <summary>
        /// Temporal 64-byte span to hold converted <see cref="L8"/> data.
        /// </summary>
        private readonly Span<L8> l8Span;

        /// <summary>
        /// Sampled pixel buffer size.
        /// </summary>
        private readonly Size samplingAreaSize;

        /// <summary>
        /// <see cref="Configuration"/> for internal operations.
        /// </summary>
        private readonly Configuration config;

        public LuminanceForwardConverter(ImageFrame<TPixel> frame)
        {
            this.Y = default;

            this.pixelSpan = new TPixel[PixelsPerSample].AsSpan();
            this.l8Span = new L8[PixelsPerSample].AsSpan();

            this.samplingAreaSize = new Size(frame.Width, frame.Height);
            this.config = frame.GetConfiguration();
        }

        /// <summary>
        /// Converts a 8x8 image area inside 'pixels' at position (x,y) placing the result members of the structure (<see cref="Y"/>)
        /// </summary>
        public void Convert(int x, int y, ref RowOctet<TPixel> currentRows)
        {
            YCbCrForwardConverter<TPixel>.LoadAndStretchEdges(currentRows, this.pixelSpan, new Point(x, y), SampleSize, this.samplingAreaSize);

            PixelOperations<TPixel>.Instance.ToL8(this.config, this.pixelSpan, this.l8Span);

            ref Block8x8F yBlock = ref this.Y;
            ref L8 l8Start = ref MemoryMarshal.GetReference(this.l8Span);

            for (int i = 0; i < Block8x8F.Size; i++)
            {
                ref L8 c = ref Unsafe.Add(ref l8Start, i);
                yBlock[i] = c.PackedValue;
            }
        }
    }
}
