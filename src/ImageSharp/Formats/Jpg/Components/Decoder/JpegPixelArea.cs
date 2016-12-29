// <copyright file="JpegPixelArea.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>
namespace ImageSharp.Formats.Jpg
{
    using System.Buffers;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Represents an area of a Jpeg subimage (channel)
    /// </summary>
    internal struct JpegPixelArea
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="JpegPixelArea" /> struct from existing data.
        /// </summary>
        /// <param name="pixels">The pixel array</param>
        /// <param name="striede">The stride</param>
        /// <param name="offset">The offset</param>
        public JpegPixelArea(byte[] pixels, int striede, int offset)
        {
            this.Stride = striede;
            this.Pixels = pixels;
            this.Offset = offset;
        }

        /// <summary>
        /// Gets the pixels.
        /// </summary>
        public byte[] Pixels { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the instance has been initalized. (Is not default(JpegPixelArea))
        /// </summary>
        public bool IsInitialized => this.Pixels != null;

        /// <summary>
        /// Gets or the stride.
        /// </summary>
        public int Stride { get; }

        /// <summary>
        /// Gets or the offset.
        /// </summary>
        public int Offset { get; }

        /// <summary>
        /// Gets a <see cref="MutableSpan{T}" /> of bytes to the pixel area
        /// </summary>
        public MutableSpan<byte> Span => new MutableSpan<byte>(this.Pixels, this.Offset);

        /// <summary>
        /// Returns the pixel at (x, y)
        /// </summary>
        /// <param name="x">The x index</param>
        /// <param name="y">The y index</param>
        /// <returns>The pixel value</returns>
        public byte this[int x, int y]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return this.Pixels[(y * this.Stride) + x];
            }
        }

        /// <summary>
        /// Creates a new instance of the <see cref="JpegPixelArea" /> struct.
        /// Pixel array will be taken from a pool, this instance will be the owner of it's pixel data, therefore
        /// <see cref="ReturnPooled" /> should be called when the instance is no longer needed.
        /// </summary>
        /// <param name="width">The width.</param>
        /// <param name="height">The height.</param>
        /// <returns>A <see cref="JpegPixelArea" /> with pooled data</returns>
        public static JpegPixelArea CreatePooled(int width, int height)
        {
            int size = width * height;
            var pixels = BytePool.Rent(size);
            return new JpegPixelArea(pixels, width, 0);
        }

        /// <summary>
        /// Returns <see cref="Pixels" /> to the pool
        /// </summary>
        public void ReturnPooled()
        {
            if (this.Pixels == null)
            {
                return;
            }

            BytePool.Return(this.Pixels);
            this.Pixels = null;
        }

        private static ArrayPool<byte> BytePool => ArrayPool<byte>.Shared;

        /// <summary>
        /// Gets the subarea that belongs to the Block8x8 defined by block indices
        /// </summary>
        /// <param name="bx">The block X index</param>
        /// <param name="by">The block Y index</param>
        /// <returns>The subarea offseted by block indices</returns>
        public JpegPixelArea GetOffsetedSubAreaForBlock(int bx, int by)
        {
            int offset = this.Offset + (8 * ((by * this.Stride) + bx));
            return new JpegPixelArea(this.Pixels, this.Stride, offset);
        }

        /// <summary>
        /// Gets the row offset at the given position
        /// </summary>
        /// <param name="y">The y-coordinate of the image.</param>
        /// <returns>The <see cref="int" /></returns>
        public int GetRowOffset(int y)
        {
            return this.Offset + (y * this.Stride);
        }

        /// <summary>
        /// Load values to the pixel area from the given <see cref="Block8x8F" />.
        /// Level shift [-128.0, 128.0] floating point color values by +128, clip them to [0, 255], and convert them to
        /// <see cref="byte" /> values
        /// </summary>
        /// <param name="block">The block holding the color values</param>
        /// <param name="temp">Temporal block provided by the caller</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void LoadColorsFrom(Block8x8F* block, Block8x8F* temp)
        {
            // Level shift by +128, clip to [0, 255], and write to dst.
            block->CopyColorsTo(new MutableSpan<byte>(this.Pixels, this.Offset), this.Stride, temp);
        }
    }
}