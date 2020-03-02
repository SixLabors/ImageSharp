// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;

using SixLabors.ImageSharp.Formats.WebP.Filters;
using SixLabors.ImageSharp.Processing;

namespace SixLabors.ImageSharp.Formats.WebP
{
    internal class AlphaDecoder
    {
        public int Width { get; set; }

        public int Height { get; set; }

        public WebPFilterBase Filter { get; set; }

        private WebPFilterType FilterType { get; }

        private int PreProcessing { get; }

        private bool Compressed { get; }

        private byte[] Data { get; }

        private Vp8LDecoder Vp8LDec { get; set; }

        /// <summary>
        /// Although Alpha Channel requires only 1 byte per pixel,
        /// sometimes Vp8LDecoder may need to allocate
        /// 4 bytes per pixel internally during decode.
        /// </summary>
        public bool Use8BDecode { get; set; }

        public AlphaDecoder(int width, int height, byte[] data)
        {
            this.Width = width;
            this.Height = height;
            this.Data = data;

            // Compression method: Either 0 (no compression) or 1 (Compressed using the WebP lossless format)
            int method = data[0] & 0x03;
            if (method < 0 || method > 1)
            {
                WebPThrowHelper.ThrowImageFormatException($"unexpected alpha compression method {method} found");
            }

            this.Compressed = !(method is 0);

            // The filtering method used. Only  values between 0 and 3 are valid.
            int filter = (data[0] >> 2) & 0x03;
            if (filter < 0 || filter > 3)
            {
                WebPThrowHelper.ThrowImageFormatException($"unexpected alpha filter method {filter} found");
            }

            this.FilterType = (WebPFilterType)filter;

            // These INFORMATIVE bits are used to signal the pre-processing that has been performed during compression.
            // The decoder can use this information to e.g. dither the values or smooth the gradients prior to display.
            // 0: no pre-processing, 1: level reduction
            this.PreProcessing = (data[0] >> 4) & 0x03;
        }

        private int PrevLineOffset { get; set; }

        public void Decode(Vp8Decoder dec, Span<byte> dst)
        {
            if (this.Compressed is false)
            {
                this.Data.AsSpan(1, this.Width * this.Height).CopyTo(dst);
            }
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
            if (!(this.Filter is WebPFilterNone))
            {
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
}
