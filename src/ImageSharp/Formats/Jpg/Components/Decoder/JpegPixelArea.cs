// <copyright file="JpegChannelArea.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats.Jpg
{
    
    using System;
    using System.Buffers;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Represents a grayscale image
    /// </summary>
    internal struct JpegPixelArea
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="JpegPixelArea" /> class.
        /// </summary>
        /// <param name="width">The width.</param>
        /// <param name="height">The height.</param>
        public static JpegPixelArea CreatePooled(int width, int height)
        {
            int size = width * height;
            //var pixels = ArrayPool<byte>.Shared.Rent(size);
            //Array.Clear(pixels, 0, size);
            var pixels = ArrayPoolManager<byte>.RentCleanArray(size);
            return new JpegPixelArea(pixels, width, 0);
        }

        public JpegPixelArea(byte[] pixels, int widthOrStride, int offset)
        {
            this.Stride = widthOrStride;
            this.Pixels = pixels;
            this.Offset = offset;
        }

        public void ReturnPooled()
        {
            if (this.Pixels == null) return;
            ArrayPoolManager<byte>.ReturnArray(this.Pixels);
            this.Pixels = null;
        }

        /// <summary>
        /// Gets or sets the pixels.
        /// </summary>
        public byte[] Pixels { get; private set; }

        public bool Created => this.Pixels != null;

        /// <summary>
        /// Gets or sets the width.
        /// </summary>
        public int Stride { get; private set; }

        /// <summary>
        /// Gets or sets the offset
        /// </summary>
        public int Offset { get; private set; }

        /// <summary>
        /// Gets an image made up of a subset of the originals pixels.
        /// </summary>
        /// <param name="x">The x-coordinate of the image.</param>
        /// <param name="y">The y-coordinate of the image.</param>
        /// <param name="width">The width.</param>
        /// <param name="height">The height.</param>
        /// <returns>
        /// The <see cref="JpegPixelArea"/>.
        /// </returns>
        public JpegPixelArea Subimage(int x, int y, int width, int height)
        {
            return new JpegPixelArea
            {
                Stride = width,
                Pixels = this.Pixels,
                Offset = (y * this.Stride) + x
            };
        }

        /// <summary>
        /// Get the subarea that belongs to the given block indices
        /// </summary>
        /// <param name="bx">The block X index</param>
        /// <param name="by">The block Y index</param>
        /// <returns></returns>
        public JpegPixelArea GetOffsetedAreaForBlock(int bx, int by)
        {
            int offset = this.Offset + 8 * (by * this.Stride + bx);
            return new JpegPixelArea(this.Pixels, this.Stride, offset);
        }

        public byte this[int x, int y] => this.Pixels[y * this.Stride + x];

        /// <summary>
        /// Gets the row offset at the given position
        /// </summary>
        /// <param name="y">The y-coordinate of the image.</param>
        /// <returns>The <see cref="int"/></returns>
        public int GetRowOffset(int y)
        {
            return this.Offset + (y * this.Stride);
        }

        public MutableSpan<byte> Span => new MutableSpan<byte>(this.Pixels, this.Offset);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void LoadColorsFrom(Block8x8F* block, Block8x8F* temp)
        {
            // Level shift by +128, clip to [0, 255], and write to dst.
            block->CopyColorsTo(new MutableSpan<byte>(this.Pixels, this.Offset), this.Stride, temp);
        }
    }
}
