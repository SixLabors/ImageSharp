// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;

using SixLabors.ImageSharp.Formats.WebP.Filters;

namespace SixLabors.ImageSharp.Formats.WebP
{
    internal ref struct AlphaDecoder
    {
        public int Width { get; set; }

        public int Height { get; set; }

        public int Method { get; set; }

        public WebPFilterBase Filter { get; set; }

        public int PreProcessing { get; set; }

        public Vp8LDecoder Vp8LDec { get; set; }

        public Vp8Io Io { get; set; }

        /// <summary>
        /// Although Alpha Channel requires only 1 byte per pixel,
        /// sometimes Vp8LDecoder may need to allocate
        /// 4 bytes per pixel internally during decode.
        /// </summary>
        public bool Use8BDecode { get; set; }

        // last output row (or null)
        private Span<byte> PrevLine { get; set; }

        private int PrevLineOffset { get; set; }

        // Taken from vp8l_dec.c AlphaApplyFilter
        public void AlphaApplyFilter(
            int firstRow, int lastRow,
            Span<byte> output, int outputOffset,
            int stride)
        {
            if (!(this.Filter is WebPFilterNone))
            {
                Span<byte> prevLine = this.PrevLine;
                int prevLineOffset = this.PrevLineOffset;

                for (int y = firstRow; y < lastRow; y++)
                {
                    this.Filter
                        .Unfilter(
                            prevLine, prevLineOffset,
                            output, outputOffset,
                            output, outputOffset,
                            stride);
                    prevLineOffset = outputOffset;
                    outputOffset += stride;
                }

                this.PrevLine = prevLine;
            }
        }
    }
}
