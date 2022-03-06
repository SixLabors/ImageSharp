// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
#if SUPPORTS_RUNTIME_INTRINSICS
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
#endif
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

            if (RgbToYCbCrConverterVectorized.IsSupported)
            {
                ConvertAvx(this.rgbSpan, ref redBlock, ref greenBlock, ref blueBlock);
            }
            else
            {
                ConvertScalar(this.rgbSpan, ref redBlock, ref greenBlock, ref blueBlock);
            }
        }

        /// <summary>
        /// Converts 8x8 RGB24 pixel matrix to 8x8 Block of floats using Avx2 Intrinsics.
        /// </summary>
        /// <param name="rgbSpan">Span of Rgb24 pixels with size of 64</param>
        /// <param name="rBlock">8x8 destination matrix of Red converted data</param>
        /// <param name="gBlock">8x8 destination matrix of Blue converted data</param>
        /// <param name="bBlock">8x8 destination matrix of Green converted data</param>
        private static void ConvertAvx(Span<Rgb24> rgbSpan, ref Block8x8F rBlock, ref Block8x8F gBlock, ref Block8x8F bBlock)
        {
            Debug.Assert(RgbToYCbCrConverterVectorized.IsSupported, "AVX2 is required to run this converter");

#if SUPPORTS_RUNTIME_INTRINSICS
            ref Vector256<byte> rgbByteSpan = ref Unsafe.As<Rgb24, Vector256<byte>>(ref MemoryMarshal.GetReference(rgbSpan));
            ref Vector256<float> redRef = ref rBlock.V0;
            ref Vector256<float> greenRef = ref gBlock.V0;
            ref Vector256<float> blueRef = ref bBlock.V0;
            var zero = Vector256.Create(0).AsByte();

            var extractToLanesMask = Unsafe.As<byte, Vector256<uint>>(ref MemoryMarshal.GetReference(RgbToYCbCrConverterVectorized.MoveFirst24BytesToSeparateLanes));
            var extractRgbMask = Unsafe.As<byte, Vector256<byte>>(ref MemoryMarshal.GetReference(RgbToYCbCrConverterVectorized.ExtractRgb));
            Vector256<byte> rgb, rg, bx;

            const int bytesPerRgbStride = 24;
            for (int i = 0; i < 8; i++)
            {
                rgb = Avx2.PermuteVar8x32(Unsafe.AddByteOffset(ref rgbByteSpan, (IntPtr)(bytesPerRgbStride * i)).AsUInt32(), extractToLanesMask).AsByte();

                rgb = Avx2.Shuffle(rgb, extractRgbMask);

                rg = Avx2.UnpackLow(rgb, zero);
                bx = Avx2.UnpackHigh(rgb, zero);

                Unsafe.Add(ref redRef, i) = Avx.ConvertToVector256Single(Avx2.UnpackLow(rg, zero).AsInt32());
                Unsafe.Add(ref greenRef, i) = Avx.ConvertToVector256Single(Avx2.UnpackHigh(rg, zero).AsInt32());
                Unsafe.Add(ref blueRef, i) = Avx.ConvertToVector256Single(Avx2.UnpackLow(bx, zero).AsInt32());
            }
#endif
        }

        private static void ConvertScalar(Span<Rgb24> rgbSpan, ref Block8x8F redBlock, ref Block8x8F greenBlock, ref Block8x8F blueBlock)
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
