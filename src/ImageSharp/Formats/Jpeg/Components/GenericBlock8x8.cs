// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Memory;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Formats.Jpeg.Components
{
    /// <summary>
    /// A generic 8x8 block implementation, useful for manipulating custom 8x8 pixel data.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal unsafe partial struct GenericBlock8x8<T>
        where T : struct
    {
        public const int Size = 64;

        /// <summary>
        /// FOR TESTING ONLY!
        /// Gets or sets a <see cref="Rgb24"/> value at the given index
        /// </summary>
        /// <param name="idx">The index</param>
        /// <returns>The value</returns>
        public T this[int idx]
        {
            get
            {
                ref T selfRef = ref Unsafe.As<GenericBlock8x8<T>, T>(ref this);
                return Unsafe.Add(ref selfRef, idx);
            }

            set
            {
                ref T selfRef = ref Unsafe.As<GenericBlock8x8<T>, T>(ref this);
                Unsafe.Add(ref selfRef, idx) = value;
            }
        }

        /// <summary>
        /// FOR TESTING ONLY!
        /// Gets or sets a value in a row+coulumn of the 8x8 block
        /// </summary>
        /// <param name="x">The x position index in the row</param>
        /// <param name="y">The column index</param>
        /// <returns>The value</returns>
        public T this[int x, int y]
        {
            get => this[(y * 8) + x];
            set => this[(y * 8) + x] = value;
        }

        public void LoadAndStretchEdges<TPixel>(IPixelSource<TPixel> source, int sourceX, int sourceY)
            where TPixel : struct, IPixel<TPixel>
        {
            if (source.PixelBuffer is Buffer2D<T> buffer)
            {
                this.LoadAndStretchEdges(buffer, sourceX, sourceY);
            }
            else
            {
                throw new InvalidOperationException("LoadAndStretchEdges<TPixels>() is only valid for TPixel == T !");
            }
        }

        /// <summary>
        /// Load a 8x8 region of an image into the block.
        /// The "outlying" area of the block will be stretched out with pixels on the right and bottom edge of the image.
        /// </summary>
        public void LoadAndStretchEdges(Buffer2D<T> source, int sourceX, int sourceY)
        {
            int width = Math.Min(8, source.Width - sourceX);
            int height = Math.Min(8, source.Height - sourceY);

            if (width <= 0 || height <= 0)
            {
                return;
            }

            uint byteWidth = (uint)width * (uint)Unsafe.SizeOf<T>();
            int remainderXCount = 8 - width;

            ref byte blockStart = ref Unsafe.As<GenericBlock8x8<T>, byte>(ref this);
            ref byte imageStart = ref Unsafe.As<T, byte>(
                                      ref Unsafe.Add(ref MemoryMarshal.GetReference(source.GetRowSpan(sourceY)), sourceX));

            int blockRowSizeInBytes = 8 * Unsafe.SizeOf<T>();
            int imageRowSizeInBytes = source.Width * Unsafe.SizeOf<T>();

            for (int y = 0; y < height; y++)
            {
                ref byte s = ref Unsafe.Add(ref imageStart, y * imageRowSizeInBytes);
                ref byte d = ref Unsafe.Add(ref blockStart, y * blockRowSizeInBytes);

                Unsafe.CopyBlock(ref d, ref s, byteWidth);

                ref T last = ref Unsafe.Add(ref Unsafe.As<byte, T>(ref d), width - 1);

                for (int x = 1; x <= remainderXCount; x++)
                {
                    Unsafe.Add(ref last, x) = last;
                }
            }

            int remainderYCount = 8 - height;

            if (remainderYCount == 0)
            {
                return;
            }

            ref byte lastRowStart = ref Unsafe.Add(ref blockStart, (height - 1) * blockRowSizeInBytes);

            for (int y = 1; y <= remainderYCount; y++)
            {
                ref byte remStart = ref Unsafe.Add(ref lastRowStart, blockRowSizeInBytes * y);
                Unsafe.CopyBlock(ref remStart, ref lastRowStart, (uint)blockRowSizeInBytes);
            }
        }

        /// <summary>
        /// Only for on-stack instances!
        /// </summary>
        public Span<T> AsSpanUnsafe() => new Span<T>(Unsafe.AsPointer(ref this), Size);
    }
}