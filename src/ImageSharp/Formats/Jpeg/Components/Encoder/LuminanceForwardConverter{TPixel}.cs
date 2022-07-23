// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
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
        /// Gets size of sampling area from given frame pixel buffer.
        /// </summary>
        private static Size SampleSize => new(8, 8);

        /// <summary>
        /// Converts a 8x8 image area inside 'pixels' at position (x,y) placing the result members of the structure (<see cref="Y"/>)
        /// </summary>
        public void Convert(int x, int y, ref RowOctet<TPixel> currentRows)
        {
            YCbCrForwardConverter<TPixel>.LoadAndStretchEdges(currentRows, this.pixelSpan, new Point(x, y), SampleSize, this.samplingAreaSize);

            PixelOperations<TPixel>.Instance.ToL8(this.config, this.pixelSpan, this.l8Span);

            ref Block8x8F yBlock = ref this.Y;
            ref L8 l8Start = ref MemoryMarshal.GetReference(this.l8Span);

            if (RgbToYCbCrConverterVectorized.IsSupported)
            {
                ConvertAvx(ref l8Start, ref yBlock);
            }
            else
            {
                ConvertScalar(ref l8Start, ref yBlock);
            }
        }

        /// <summary>
        /// Converts 8x8 L8 pixel matrix to 8x8 Block of floats using Avx2 Intrinsics.
        /// </summary>
        /// <param name="l8Start">Start of span of L8 pixels with size of 64</param>
        /// <param name="yBlock">8x8 destination matrix of Luminance(Y) converted data</param>
        private static void ConvertAvx(ref L8 l8Start, ref Block8x8F yBlock)
        {
            Debug.Assert(RgbToYCbCrConverterVectorized.IsSupported, "AVX2 is required to run this converter");

            ref Vector128<byte> l8ByteSpan = ref Unsafe.As<L8, Vector128<byte>>(ref l8Start);
            ref Vector256<float> destRef = ref yBlock.V0;

            const int bytesPerL8Stride = 8;
            for (nint i = 0; i < 8; i++)
            {
                Unsafe.Add(ref destRef, i) = Avx.ConvertToVector256Single(Avx2.ConvertToVector256Int32(Unsafe.AddByteOffset(ref l8ByteSpan, bytesPerL8Stride * i)));
            }
        }

        /// <summary>
        /// Converts 8x8 L8 pixel matrix to 8x8 Block of floats.
        /// </summary>
        /// <param name="l8Start">Start of span of L8 pixels with size of 64</param>
        /// <param name="yBlock">8x8 destination matrix of Luminance(Y) converted data</param>
        private static void ConvertScalar(ref L8 l8Start, ref Block8x8F yBlock)
        {
            for (int i = 0; i < Block8x8F.Size; i++)
            {
                ref L8 c = ref Unsafe.Add(ref l8Start, i);
                yBlock[i] = c.PackedValue;
            }
        }
    }
}
