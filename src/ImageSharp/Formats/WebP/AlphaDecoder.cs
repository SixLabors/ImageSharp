// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Collections.Generic;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.WebP
{
    /// <summary>
    /// Implements decoding for lossy alpha chunks which may be compressed.
    /// </summary>
    internal class AlphaDecoder : IDisposable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AlphaDecoder"/> class.
        /// </summary>
        /// <param name="width">The width of the image.</param>
        /// <param name="height">The height of the image.</param>
        /// <param name="data">The (maybe compressed) alpha data.</param>
        /// <param name="alphaChunkHeader">The first byte of the alpha image stream contains information on ow to decode the stream.</param>
        /// <param name="memoryAllocator">Used for allocating memory during decoding.</param>
        /// <param name="configuration">The configuration.</param>
        public AlphaDecoder(int width, int height, byte[] data, byte alphaChunkHeader, MemoryAllocator memoryAllocator, Configuration configuration)
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

            this.AlphaFilterType = (WebPAlphaFilterType)filter;

            // These INFORMATIVE bits are used to signal the pre-processing that has been performed during compression.
            // The decoder can use this information to e.g. dither the values or smooth the gradients prior to display.
            // 0: no pre-processing, 1: level reduction
            this.PreProcessing = (alphaChunkHeader >> 4) & 0x03;

            this.Vp8LDec = new Vp8LDecoder(width, height, memoryAllocator);

            this.Alpha = memoryAllocator.Allocate<byte>(width * height);

            if (this.Compressed)
            {
                var bitReader = new Vp8LBitReader(data);
                this.LosslessDecoder = new WebPLosslessDecoder(bitReader, memoryAllocator, configuration);
                this.LosslessDecoder.DecodeImageStream(this.Vp8LDec, width, height, true);
            }
        }

        /// <summary>
        /// Gets the the width of the image.
        /// </summary>
        public int Width { get; }

        /// <summary>
        /// Gets the height of the image.
        /// </summary>
        public int Height { get; }

        /// <summary>
        /// Gets the used filter type.
        /// </summary>
        public WebPAlphaFilterType AlphaFilterType { get; }

        /// <summary>
        /// Gets or sets the last decoded row.
        /// </summary>
        public int LastRow { get; set; }

        /// <summary>
        /// Gets or sets the row before the last decoded row.
        /// </summary>
        public int PrevRow { get; set; }

        /// <summary>
        /// Gets information for decoding Vp8L compressed alpha data.
        /// </summary>
        public Vp8LDecoder Vp8LDec { get; }

        /// <summary>
        /// Gets the decoded alpha data.
        /// </summary>
        public IMemoryOwner<byte> Alpha { get; }

        public int CropTop { get; }

        /// <summary>
        /// Gets a value indicating whether pre-processing was used during compression.
        /// 0: no pre-processing, 1: level reduction.
        /// </summary>
        private int PreProcessing { get; }

        /// <summary>
        /// Gets a value indicating whether the alpha channel uses compression.
        /// </summary>
        private bool Compressed { get; }

        /// <summary>
        /// Gets the (maybe compressed) alpha data.
        /// </summary>
        private byte[] Data { get; }

        /// <summary>
        /// Gets the Vp8L decoder which is used to de compress the alpha channel, if needed.
        /// </summary>
        private WebPLosslessDecoder LosslessDecoder { get; }

        /// <summary>
        /// Gets or sets a value indicating whether the decoding needs 1 byte per pixel for decoding.
        /// Although Alpha Channel requires only 1 byte per pixel, sometimes Vp8LDecoder may need to allocate
        /// 4 bytes per pixel internally during decode.
        /// </summary>
        public bool Use8BDecode { get; set; }

        /// <summary>
        /// Decodes and filters the maybe compressed alpha data.
        /// </summary>
        public void Decode()
        {
            if (this.Compressed is false)
            {
                if (this.Data.Length < (this.Width * this.Height))
                {
                    WebPThrowHelper.ThrowImageFormatException("not enough data in the ALPH chunk");
                }

                Span<byte> alphaSpan = this.Alpha.Memory.Span;
                if (this.AlphaFilterType == WebPAlphaFilterType.None)
                {
                    this.Data.AsSpan(0, this.Width * this.Height).CopyTo(alphaSpan);
                    return;
                }

                Span<byte> deltas = this.Data.AsSpan();
                Span<byte> dst = alphaSpan;
                Span<byte> prev = null;
                for (int y = 0; y < this.Height; ++y)
                {
                    switch (this.AlphaFilterType)
                    {
                        case WebPAlphaFilterType.Horizontal:
                            HorizontalUnfilter(prev, deltas, dst, this.Width);
                            break;
                        case WebPAlphaFilterType.Vertical:
                            VerticalUnfilter(prev, deltas, dst, this.Width);
                            break;
                        case WebPAlphaFilterType.Gradient:
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

        /// <summary>
        /// Applies filtering to a set of rows.
        /// </summary>
        /// <param name="firstRow">The first row index to start filtering.</param>
        /// <param name="lastRow">The last row index for filtering.</param>
        /// <param name="dst">The destination to store the filtered data.</param>
        /// <param name="stride">The stride to use.</param>
        public void AlphaApplyFilter(int firstRow, int lastRow, Span<byte> dst, int stride)
        {
            if (this.AlphaFilterType is WebPAlphaFilterType.None)
            {
                return;
            }

            Span<byte> alphaSpan = this.Alpha.Memory.Span;
            Span<byte> prev = this.PrevRow == 0 ? null : alphaSpan.Slice(this.Width * this.PrevRow);
            for (int y = firstRow; y < lastRow; ++y)
            {
                switch (this.AlphaFilterType)
                {
                    case WebPAlphaFilterType.Horizontal:
                        HorizontalUnfilter(prev, dst, dst, this.Width);
                        break;
                    case WebPAlphaFilterType.Vertical:
                        VerticalUnfilter(prev, dst, dst, this.Width);
                        break;
                    case WebPAlphaFilterType.Gradient:
                        GradientUnfilter(prev, dst, dst, this.Width);
                        break;
                }

                prev = dst;
                dst = dst.Slice(stride);
            }

            this.PrevRow = lastRow - 1;
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

        private static bool Is8bOptimizable(Vp8LMetadata hdr)
        {
            if (hdr.ColorCacheSize > 0)
            {
                return false;
            }

            // When the Huffman tree contains only one symbol, we can skip the
            // call to ReadSymbol() for red/blue/alpha channels.
            for (int i = 0; i < hdr.NumHTreeGroups; ++i)
            {
                List<HuffmanCode[]> htrees = hdr.HTreeGroups[i].HTrees;
                if (htrees[HuffIndex.Red][0].Value > 0)
                {
                    return false;
                }

                if (htrees[HuffIndex.Blue][0].Value > 0)
                {
                    return false;
                }

                if (htrees[HuffIndex.Alpha][0].Value > 0)
                {
                    return false;
                }
            }

            return true;
        }

        private static int GradientPredictor(byte a, byte b, byte c)
        {
            int g = a + b - c;
            return ((g & ~0xff) is 0) ? g : (g < 0) ? 0 : 255;  // clip to 8bit
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.Vp8LDec?.Dispose();
            this.Alpha?.Dispose();
        }
    }
}
