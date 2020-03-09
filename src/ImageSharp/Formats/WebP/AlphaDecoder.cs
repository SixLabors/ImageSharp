// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;

using SixLabors.ImageSharp.Formats.WebP.Filters;
using SixLabors.Memory;

namespace SixLabors.ImageSharp.Formats.WebP
{
    internal class AlphaDecoder
    {
        public int Width { get; }

        public int Height { get; }

        public WebPFilterBase Filter { get; }

        public WebPFilterType FilterType { get; }

        public int CropTop { get; }

        public int LastRow { get; set; }

        public Vp8LDecoder Vp8LDec { get; }

        public byte[] Alpha { get; }

        private int PreProcessing { get; }

        private bool Compressed { get; }

        private byte[] Data { get; }

        private WebPLosslessDecoder LosslessDecoder { get; }

        /// <summary>
        /// Although Alpha Channel requires only 1 byte per pixel,
        /// sometimes Vp8LDecoder may need to allocate
        /// 4 bytes per pixel internally during decode.
        /// </summary>
        public bool Use8BDecode { get; set; }

        public AlphaDecoder(int width, int height, byte[] data, byte alphaChunkHeader, MemoryAllocator memoryAllocator)
        {
            this.Width = width;
            this.Height = height;
            this.Data = data;
            this.LastRow = 0;

            // Compression method: Either 0 (no compression) or 1 (Compressed using the WebP lossless format)
            int method = alphaChunkHeader & 0x03;
            if (method < 0 || method > 1)
            {
                WebPThrowHelper.ThrowImageFormatException($"unexpected alpha compression method {method} found");
            }

            this.Compressed = !(method is 0);

            // The filtering method used. Only  values between 0 and 3 are valid.
            int filter = (alphaChunkHeader >> 2) & 0x03;
            if (filter < 0 || filter > 3)
            {
                WebPThrowHelper.ThrowImageFormatException($"unexpected alpha filter method {filter} found");
            }

            this.FilterType = (WebPFilterType)filter;

            // These INFORMATIVE bits are used to signal the pre-processing that has been performed during compression.
            // The decoder can use this information to e.g. dither the values or smooth the gradients prior to display.
            // 0: no pre-processing, 1: level reduction
            this.PreProcessing = (alphaChunkHeader >> 4) & 0x03;

            this.Vp8LDec = new Vp8LDecoder(width, height, memoryAllocator);

            // TODO: use memory allocator
            this.Alpha = new byte[width * height];

            if (this.Compressed)
            {
                var bitReader = new Vp8LBitReader(data);
                this.LosslessDecoder = new WebPLosslessDecoder(bitReader, memoryAllocator);
                this.LosslessDecoder.DecodeImageStream(this.Vp8LDec, width, height, true);
            }
        }

        private int PrevLineOffset { get; set; }

        public void Decode()
        {
            if (this.Compressed is false)
            {
                if (this.Data.Length < (this.Width * this.Height))
                {
                    WebPThrowHelper.ThrowImageFormatException("not enough data in the ALPH chunk");
                }

                if (this.FilterType == WebPFilterType.None)
                {
                    this.Data.AsSpan(0, this.Width * this.Height).CopyTo(this.Alpha);
                    return;
                }

                Span<byte> deltas = this.Data.AsSpan();
                Span<byte> dst = this.Alpha.AsSpan();
                Span<byte> prev = null;
                for (int y = 0; y < this.Height; ++y)
                {
                    switch (this.FilterType)
                    {
                        case WebPFilterType.Horizontal:
                            HorizontalUnfilter(prev, deltas, dst, this.Width);
                            break;
                        case WebPFilterType.Vertical:
                            VerticalUnfilter(prev, deltas, dst, this.Width);
                            break;
                        case WebPFilterType.Gradient:
                            GradientUnfilter(prev, deltas, dst, this.Width);
                            break;
                    }

                    prev = dst;
                    deltas = deltas.Slice(this.Width);
                    dst = dst.Slice(this.Width);
                }
            }
            else
            {
                this.LosslessDecoder.DecodeAlphaData(this);
            }
        }

        private static void HorizontalUnfilter(Span<byte> prev, Span<byte> input, Span<byte> dst, int width)
        {
            byte pred = (byte)(prev == null ? 0 : prev[0]);

            for (int i = 0; i < width; ++i)
            {
                dst[i] = (byte)(pred + input[i]);
                pred = dst[i];
            }
        }

        private static void VerticalUnfilter(Span<byte> prev, Span<byte> input, Span<byte> dst, int width)
        {
            if (prev == null)
            {
                HorizontalUnfilter(null, input, dst, width);
            }
            else
            {
                for (int i = 0; i < width; ++i)
                {
                    dst[i] = (byte)(prev[i] + input[i]);
                }
            }
        }

        private static void GradientUnfilter(Span<byte> prev, Span<byte> input, Span<byte> dst, int width)
        {
            if (prev == null)
            {
                HorizontalUnfilter(null, input, dst, width);
            }
            else
            {
                byte top = prev[0];
                byte topLeft = top;
                byte left = top;
                for (int i = 0; i < width; ++i)
                {
                    top = prev[i];
                    left = (byte)(input[i] + GradientPredictor(left, top, topLeft));
                    topLeft = top;
                    dst[i] = left;
                }
            }
        }

        private static int GradientPredictor(byte a, byte b, byte c)
        {
            int g = a + b - c;
            return ((g & ~0xff) is 0) ? g : (g < 0) ? 0 : 255;  // clip to 8bit
        }

        // Taken from vp8l_dec.c AlphaApplyFilter
        public void AlphaApplyFilter(
            int firstRow,
            int lastRow,
            Span<byte> prevLine,
            Span<byte> output,
            int outputOffset,
            int stride)
        {
            if (this.Filter is WebPFilterNone)
            {
                return;
            }

            int prevLineOffset = this.PrevLineOffset;

            for (int y = firstRow; y < lastRow; y++)
            {
                this.Filter
                    .Unfilter(
                        prevLine,
                        prevLineOffset,
                        output,
                        outputOffset,
                        output,
                        outputOffset,
                        stride);
                prevLineOffset = outputOffset;
                outputOffset += stride;
            }
        }
    }
}
