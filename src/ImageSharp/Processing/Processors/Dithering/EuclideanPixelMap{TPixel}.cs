// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Concurrent;
using System.Numerics;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Dithering
{
    /// <summary>
    /// Gets the closest color to the supplied color based upon the Eucladean distance.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal sealed class EuclideanPixelMap<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        private readonly ReadOnlyMemory<TPixel> palette;
        private readonly ConcurrentDictionary<int, Vector4> vectorCache = new ConcurrentDictionary<int, Vector4>();
        private readonly ConcurrentDictionary<TPixel, byte> distanceCache = new ConcurrentDictionary<TPixel, byte>();

        public EuclideanPixelMap(ReadOnlyMemory<TPixel> palette)
        {
            this.palette = palette;
            ReadOnlySpan<TPixel> paletteSpan = this.palette.Span;

            for (int i = 0; i < paletteSpan.Length; i++)
            {
                this.vectorCache[i] = paletteSpan[i].ToScaledVector4();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte GetClosestColor(TPixel color, out TPixel match)
        {
            ReadOnlySpan<TPixel> paletteSpan = this.palette.Span;

            // Check if the color is in the lookup table
            if (this.distanceCache.TryGetValue(color, out byte index))
            {
                match = paletteSpan[index];
                return index;
            }

            return this.GetClosestColorSlow(color, paletteSpan, out match);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private byte GetClosestColorSlow(TPixel color, ReadOnlySpan<TPixel> palette, out TPixel match)
        {
            // Loop through the palette and find the nearest match.
            int index = 0;
            float leastDistance = float.MaxValue;
            Vector4 vector = color.ToScaledVector4();

            for (int i = 0; i < palette.Length; i++)
            {
                Vector4 candidate = this.vectorCache[i];
                float distance = Vector4.DistanceSquared(vector, candidate);

                // Greater... Move on.
                if (leastDistance < distance)
                {
                    continue;
                }

                index = i;
                leastDistance = distance;

                // And if it's an exact match, exit the loop
                if (distance == 0)
                {
                    break;
                }
            }

            // Now I have the index, pop it into the cache for next time
            var result = (byte)index;
            this.distanceCache[color] = result;
            match = palette[index];
            return result;
        }
    }
}
