// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation
{
    /// <summary>
    /// Implements the 'BlackIsZero' photometric interpretation (optimized for bilevel images).
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class BlackIsZero1TiffColor<TPixel> : TiffBaseColorDecoder<TPixel>
        where TPixel : unmanaged, IPixel<TPixel>
    {
        /// <inheritdoc/>
        public override void Decode(ReadOnlySpan<byte> data, Buffer2D<TPixel> pixels, int left, int top, int width, int height)
        {
            nint offset = 0;
            var colorBlack = default(TPixel);
            var colorWhite = default(TPixel);

            colorBlack.FromRgba32(Color.Black);
            colorWhite.FromRgba32(Color.White);
            ref byte dataRef = ref MemoryMarshal.GetReference(data);
            for (nint y = top; y < top + height; y++)
            {
                Span<TPixel> pixelRowSpan = pixels.DangerousGetRowSpan((int)y);
                ref TPixel pixelRowRef = ref MemoryMarshal.GetReference(pixelRowSpan);
                for (nint x = left; x < left + width; x += 8)
                {
                    byte b = Unsafe.Add(ref dataRef, offset++);
                    nint maxShift = Math.Min(left + width - x, 8);

                    for (int shift = 0; shift < maxShift; shift++)
                    {
                        int bit = (b >> (7 - shift)) & 1;

                        ref TPixel pixel = ref Unsafe.Add(ref pixelRowRef, x + shift);
                        pixel = bit == 0 ? colorBlack : colorWhite;
                    }
                }
            }
        }
    }
}
