// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Quantization.PaletteLookup
{
    internal struct WuPaletteMap<TPixel> : IDisposable
        where TPixel : unmanaged, IPixel<TPixel>
    {
        private readonly MemoryAllocator memoryAllocator;

        // The following two variables determine the amount of bits to preserve when calculating the histogram.
        // Reducing the value of these numbers the granularity of the color maps produced, making it much faster
        // and using much less memory but potentially less accurate. Current results are very good though!

        /// <summary>
        /// The index bits. 6 in original code.
        /// </summary>
        private const int IndexBits = 5;

        /// <summary>
        /// The index alpha bits. 3 in original code.
        /// </summary>
        private const int IndexAlphaBits = 5;

        /// <summary>
        /// The index count.
        /// </summary>
        private const int IndexCount = (1 << IndexBits) + 1;

        /// <summary>
        /// The index alpha count.
        /// </summary>
        private const int IndexAlphaCount = (1 << IndexAlphaBits) + 1;

        /// <summary>
        /// The table length. Now 1185921. originally 2471625.
        /// </summary>
        private const int TableLength = IndexCount * IndexCount * IndexCount * IndexAlphaCount;

        private IMemoryOwner<Moment> moments;
        private IMemoryOwner<byte> tag;
        private IMemoryOwner<TPixel> palette;
        private int colors;
        private readonly Box[] colorCube;
        private bool isDisposed;

        public WuPaletteMap(Configuration configuration, int maxColors)
        {
            this.Configuration = configuration;
            this.colors = maxColors;
            this.memoryAllocator = this.Configuration.MemoryAllocator;
            this.moments = this.memoryAllocator.Allocate<Moment>(TableLength, AllocationOptions.Clean);
            this.tag = this.memoryAllocator.Allocate<byte>(TableLength, AllocationOptions.Clean);
            this.palette = this.memoryAllocator.Allocate<TPixel>(this.colors, AllocationOptions.Clean);

            this.colorCube = new Box[this.colors];
            this.isDisposed = false;
            this.DebugLog = null;
        }

        public Configuration Configuration { get; }

        public Action<string> DebugLog { get; set; }

        public ReadOnlySpan<TPixel> BuildPalette(ImageFrame<TPixel> source, Rectangle bounds)
        {
            BufferArea<TPixel> area = source.PixelBuffer.GetArea(bounds);
            return this.BuildPalette(area);
        }

        public ReadOnlySpan<TPixel> BuildPalette(ReadOnlySpan<TPixel> pixels)
        {
            using Buffer2D<TPixel> fakeBuffer = this.memoryAllocator.Allocate2D<TPixel>(pixels.Length, 1);
            pixels.CopyTo(fakeBuffer.GetRowSpan(0));

            return this.BuildPalette(fakeBuffer.GetArea());
        }

        private void Log(string str) => this.DebugLog?.Invoke(str);

        private unsafe ReadOnlySpan<TPixel> BuildPalette(BufferArea<TPixel> area)
        {
            int elementSize = sizeof(Moment) + 1;
            int totalBytes = TableLength * elementSize;
            double totalMegaBytes = totalBytes / 1e6;

            this.Log($"Total allocated MB: {totalMegaBytes}");

            this.Build3DHistogram(area);
            this.Get3DMoments(this.memoryAllocator);
            this.BuildCube();

            ReadOnlySpan<Moment> momentsSpan = this.moments.GetSpan();
            Span<TPixel> paletteSpan = this.palette.GetSpan();
            for (int k = 0; k < this.colors; k++)
            {
                this.Mark(ref this.colorCube[k], (byte)k);

                Moment moment = Volume(ref this.colorCube[k], momentsSpan);

                if (moment.Weight > 0)
                {
                    ref TPixel color = ref paletteSpan[k];
                    color.FromScaledVector4(moment.Normalize());
                }
            }

            paletteSpan = paletteSpan.Slice(0, this.colors);
            return paletteSpan;
        }

        public readonly byte GetQuantizedColor(TPixel color, out TPixel match)
        {
            Rgba32 rgba = default;
            color.ToRgba32(ref rgba);

            int r = rgba.R >> (8 - IndexBits);
            int g = rgba.G >> (8 - IndexBits);
            int b = rgba.B >> (8 - IndexBits);
            int a = rgba.A >> (8 - IndexAlphaBits);

            ReadOnlySpan<byte> tagSpan = this.tag.GetSpan();
            byte index = tagSpan[GetPaletteIndex(r + 1, g + 1, b + 1, a + 1)];
            ref TPixel paletteRef = ref MemoryMarshal.GetReference(this.palette.GetSpan());
            match = Unsafe.Add(ref paletteRef, index);
            return index;
        }

        public void Dispose()
        {
            if (!this.isDisposed)
            {
                this.isDisposed = true;
                this.moments?.Dispose();
                this.tag?.Dispose();
                this.palette?.Dispose();
                this.moments = null;
                this.tag = null;
                this.palette = null;
            }
        }

        /// <summary>
        /// Gets the index of the given color in the palette.
        /// </summary>
        /// <param name="r">The red value.</param>
        /// <param name="g">The green value.</param>
        /// <param name="b">The blue value.</param>
        /// <param name="a">The alpha value.</param>
        /// <returns>The index.</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        private static int GetPaletteIndex(int r, int g, int b, int a)
        {
            return (r << ((IndexBits * 2) + IndexAlphaBits))
                + (r << (IndexBits + IndexAlphaBits + 1))
                + (g << (IndexBits + IndexAlphaBits))
                + (r << (IndexBits * 2))
                + (r << (IndexBits + 1))
                + (g << IndexBits)
                + ((r + g + b) << IndexAlphaBits)
                + r + g + b + a;
        }

        /// <summary>
        /// Computes sum over a box of any given statistic.
        /// </summary>
        /// <param name="cube">The cube.</param>
        /// <param name="moments">The moment.</param>
        /// <returns>The result.</returns>
        private static Moment Volume(ref Box cube, ReadOnlySpan<Moment> moments)
        {
            return moments[GetPaletteIndex(cube.RMax, cube.GMax, cube.BMax, cube.AMax)]
                 - moments[GetPaletteIndex(cube.RMax, cube.GMax, cube.BMax, cube.AMin)]
                 - moments[GetPaletteIndex(cube.RMax, cube.GMax, cube.BMin, cube.AMax)]
                 + moments[GetPaletteIndex(cube.RMax, cube.GMax, cube.BMin, cube.AMin)]
                 - moments[GetPaletteIndex(cube.RMax, cube.GMin, cube.BMax, cube.AMax)]
                 + moments[GetPaletteIndex(cube.RMax, cube.GMin, cube.BMax, cube.AMin)]
                 + moments[GetPaletteIndex(cube.RMax, cube.GMin, cube.BMin, cube.AMax)]
                 - moments[GetPaletteIndex(cube.RMax, cube.GMin, cube.BMin, cube.AMin)]
                 - moments[GetPaletteIndex(cube.RMin, cube.GMax, cube.BMax, cube.AMax)]
                 + moments[GetPaletteIndex(cube.RMin, cube.GMax, cube.BMax, cube.AMin)]
                 + moments[GetPaletteIndex(cube.RMin, cube.GMax, cube.BMin, cube.AMax)]
                 - moments[GetPaletteIndex(cube.RMin, cube.GMax, cube.BMin, cube.AMin)]
                 + moments[GetPaletteIndex(cube.RMin, cube.GMin, cube.BMax, cube.AMax)]
                 - moments[GetPaletteIndex(cube.RMin, cube.GMin, cube.BMax, cube.AMin)]
                 - moments[GetPaletteIndex(cube.RMin, cube.GMin, cube.BMin, cube.AMax)]
                 + moments[GetPaletteIndex(cube.RMin, cube.GMin, cube.BMin, cube.AMin)];
        }

        /// <summary>
        /// Computes part of Volume(cube, moment) that doesn't depend on RMax, GMax, BMax, or AMax (depending on direction).
        /// </summary>
        /// <param name="cube">The cube.</param>
        /// <param name="direction">The direction.</param>
        /// <param name="moments">The moment.</param>
        /// <returns>The result.</returns>
        private static Moment Bottom(ref Box cube, int direction, ReadOnlySpan<Moment> moments)
        {
            switch (direction)
            {
                // Red
                case 3:
                    return -moments[GetPaletteIndex(cube.RMin, cube.GMax, cube.BMax, cube.AMax)]
                         + moments[GetPaletteIndex(cube.RMin, cube.GMax, cube.BMax, cube.AMin)]
                         + moments[GetPaletteIndex(cube.RMin, cube.GMax, cube.BMin, cube.AMax)]
                         - moments[GetPaletteIndex(cube.RMin, cube.GMax, cube.BMin, cube.AMin)]
                         + moments[GetPaletteIndex(cube.RMin, cube.GMin, cube.BMax, cube.AMax)]
                         - moments[GetPaletteIndex(cube.RMin, cube.GMin, cube.BMax, cube.AMin)]
                         - moments[GetPaletteIndex(cube.RMin, cube.GMin, cube.BMin, cube.AMax)]
                         + moments[GetPaletteIndex(cube.RMin, cube.GMin, cube.BMin, cube.AMin)];

                // Green
                case 2:
                    return -moments[GetPaletteIndex(cube.RMax, cube.GMin, cube.BMax, cube.AMax)]
                         + moments[GetPaletteIndex(cube.RMax, cube.GMin, cube.BMax, cube.AMin)]
                         + moments[GetPaletteIndex(cube.RMax, cube.GMin, cube.BMin, cube.AMax)]
                         - moments[GetPaletteIndex(cube.RMax, cube.GMin, cube.BMin, cube.AMin)]
                         + moments[GetPaletteIndex(cube.RMin, cube.GMin, cube.BMax, cube.AMax)]
                         - moments[GetPaletteIndex(cube.RMin, cube.GMin, cube.BMax, cube.AMin)]
                         - moments[GetPaletteIndex(cube.RMin, cube.GMin, cube.BMin, cube.AMax)]
                         + moments[GetPaletteIndex(cube.RMin, cube.GMin, cube.BMin, cube.AMin)];

                // Blue
                case 1:
                    return -moments[GetPaletteIndex(cube.RMax, cube.GMax, cube.BMin, cube.AMax)]
                         + moments[GetPaletteIndex(cube.RMax, cube.GMax, cube.BMin, cube.AMin)]
                         + moments[GetPaletteIndex(cube.RMax, cube.GMin, cube.BMin, cube.AMax)]
                         - moments[GetPaletteIndex(cube.RMax, cube.GMin, cube.BMin, cube.AMin)]
                         + moments[GetPaletteIndex(cube.RMin, cube.GMax, cube.BMin, cube.AMax)]
                         - moments[GetPaletteIndex(cube.RMin, cube.GMax, cube.BMin, cube.AMin)]
                         - moments[GetPaletteIndex(cube.RMin, cube.GMin, cube.BMin, cube.AMax)]
                         + moments[GetPaletteIndex(cube.RMin, cube.GMin, cube.BMin, cube.AMin)];

                // Alpha
                case 0:
                    return -moments[GetPaletteIndex(cube.RMax, cube.GMax, cube.BMax, cube.AMin)]
                         + moments[GetPaletteIndex(cube.RMax, cube.GMax, cube.BMin, cube.AMin)]
                         + moments[GetPaletteIndex(cube.RMax, cube.GMin, cube.BMax, cube.AMin)]
                         - moments[GetPaletteIndex(cube.RMax, cube.GMin, cube.BMin, cube.AMin)]
                         + moments[GetPaletteIndex(cube.RMin, cube.GMax, cube.BMax, cube.AMin)]
                         - moments[GetPaletteIndex(cube.RMin, cube.GMax, cube.BMin, cube.AMin)]
                         - moments[GetPaletteIndex(cube.RMin, cube.GMin, cube.BMax, cube.AMin)]
                         + moments[GetPaletteIndex(cube.RMin, cube.GMin, cube.BMin, cube.AMin)];

                default:
                    throw new ArgumentOutOfRangeException(nameof(direction));
            }
        }

        /// <summary>
        /// Computes remainder of Volume(cube, moment), substituting position for RMax, GMax, BMax, or AMax (depending on direction).
        /// </summary>
        /// <param name="cube">The cube.</param>
        /// <param name="direction">The direction.</param>
        /// <param name="position">The position.</param>
        /// <param name="moments">The moment.</param>
        /// <returns>The result.</returns>
        private static Moment Top(ref Box cube, int direction, int position, ReadOnlySpan<Moment> moments)
        {
            switch (direction)
            {
                // Red
                case 3:
                    return moments[GetPaletteIndex(position, cube.GMax, cube.BMax, cube.AMax)]
                         - moments[GetPaletteIndex(position, cube.GMax, cube.BMax, cube.AMin)]
                         - moments[GetPaletteIndex(position, cube.GMax, cube.BMin, cube.AMax)]
                         + moments[GetPaletteIndex(position, cube.GMax, cube.BMin, cube.AMin)]
                         - moments[GetPaletteIndex(position, cube.GMin, cube.BMax, cube.AMax)]
                         + moments[GetPaletteIndex(position, cube.GMin, cube.BMax, cube.AMin)]
                         + moments[GetPaletteIndex(position, cube.GMin, cube.BMin, cube.AMax)]
                         - moments[GetPaletteIndex(position, cube.GMin, cube.BMin, cube.AMin)];

                // Green
                case 2:
                    return moments[GetPaletteIndex(cube.RMax, position, cube.BMax, cube.AMax)]
                         - moments[GetPaletteIndex(cube.RMax, position, cube.BMax, cube.AMin)]
                         - moments[GetPaletteIndex(cube.RMax, position, cube.BMin, cube.AMax)]
                         + moments[GetPaletteIndex(cube.RMax, position, cube.BMin, cube.AMin)]
                         - moments[GetPaletteIndex(cube.RMin, position, cube.BMax, cube.AMax)]
                         + moments[GetPaletteIndex(cube.RMin, position, cube.BMax, cube.AMin)]
                         + moments[GetPaletteIndex(cube.RMin, position, cube.BMin, cube.AMax)]
                         - moments[GetPaletteIndex(cube.RMin, position, cube.BMin, cube.AMin)];

                // Blue
                case 1:
                    return moments[GetPaletteIndex(cube.RMax, cube.GMax, position, cube.AMax)]
                         - moments[GetPaletteIndex(cube.RMax, cube.GMax, position, cube.AMin)]
                         - moments[GetPaletteIndex(cube.RMax, cube.GMin, position, cube.AMax)]
                         + moments[GetPaletteIndex(cube.RMax, cube.GMin, position, cube.AMin)]
                         - moments[GetPaletteIndex(cube.RMin, cube.GMax, position, cube.AMax)]
                         + moments[GetPaletteIndex(cube.RMin, cube.GMax, position, cube.AMin)]
                         + moments[GetPaletteIndex(cube.RMin, cube.GMin, position, cube.AMax)]
                         - moments[GetPaletteIndex(cube.RMin, cube.GMin, position, cube.AMin)];

                // Alpha
                case 0:
                    return moments[GetPaletteIndex(cube.RMax, cube.GMax, cube.BMax, position)]
                         - moments[GetPaletteIndex(cube.RMax, cube.GMax, cube.BMin, position)]
                         - moments[GetPaletteIndex(cube.RMax, cube.GMin, cube.BMax, position)]
                         + moments[GetPaletteIndex(cube.RMax, cube.GMin, cube.BMin, position)]
                         - moments[GetPaletteIndex(cube.RMin, cube.GMax, cube.BMax, position)]
                         + moments[GetPaletteIndex(cube.RMin, cube.GMax, cube.BMin, position)]
                         + moments[GetPaletteIndex(cube.RMin, cube.GMin, cube.BMax, position)]
                         - moments[GetPaletteIndex(cube.RMin, cube.GMin, cube.BMin, position)];

                default:
                    throw new ArgumentOutOfRangeException(nameof(direction));
            }
        }

        /// <summary>
        /// Builds a 3-D color histogram of <c>counts, r/g/b, c^2</c>.
        /// </summary>
        private void Build3DHistogram(BufferArea<TPixel> area)
        {
            Span<Moment> momentSpan = this.moments.GetSpan();

            // Build up the 3-D color histogram
            using IMemoryOwner<Rgba32> buffer = this.memoryAllocator.Allocate<Rgba32>(area.Width);
            Span<Rgba32> bufferSpan = buffer.GetSpan();

            for (int y = 0; y < area.Height; y++)
            {
                Span<TPixel> row = area.GetRowSpan(y);
                PixelOperations<TPixel>.Instance.ToRgba32(this.Configuration, row, bufferSpan);

                for (int x = 0; x < bufferSpan.Length; x++)
                {
                    Rgba32 rgba = bufferSpan[x];

                    int r = (rgba.R >> (8 - IndexBits)) + 1;
                    int g = (rgba.G >> (8 - IndexBits)) + 1;
                    int b = (rgba.B >> (8 - IndexBits)) + 1;
                    int a = (rgba.A >> (8 - IndexAlphaBits)) + 1;

                    momentSpan[GetPaletteIndex(r, g, b, a)] += rgba;
                }
            }
        }

        /// <summary>
        /// Converts the histogram into moments so that we can rapidly calculate the sums of the above quantities over any desired box.
        /// </summary>
        /// <param name="allocator">The memory allocator used for allocating buffers.</param>
        private void Get3DMoments(MemoryAllocator allocator)
        {
            using IMemoryOwner<Moment> volume = allocator.Allocate<Moment>(IndexCount * IndexAlphaCount);
            using IMemoryOwner<Moment> area = allocator.Allocate<Moment>(IndexAlphaCount);

            Span<Moment> momentSpan = this.moments.GetSpan();
            Span<Moment> volumeSpan = volume.GetSpan();
            Span<Moment> areaSpan = area.GetSpan();
            int baseIndex = GetPaletteIndex(1, 0, 0, 0);

            for (int r = 1; r < IndexCount; r++)
            {
                volumeSpan.Clear();

                for (int g = 1; g < IndexCount; g++)
                {
                    areaSpan.Clear();

                    for (int b = 1; b < IndexCount; b++)
                    {
                        Moment line = default;

                        for (int a = 1; a < IndexAlphaCount; a++)
                        {
                            int ind1 = GetPaletteIndex(r, g, b, a);
                            line += momentSpan[ind1];

                            areaSpan[a] += line;

                            int inv = (b * IndexAlphaCount) + a;
                            volumeSpan[inv] += areaSpan[a];

                            int ind2 = ind1 - baseIndex;
                            momentSpan[ind1] = momentSpan[ind2] + volumeSpan[inv];
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Computes the weighted variance of a box cube.
        /// </summary>
        /// <param name="cube">The cube.</param>
        /// <returns>The <see cref="float"/>.</returns>
        private double Variance(ref Box cube)
        {
            ReadOnlySpan<Moment> momentSpan = this.moments.GetSpan();

            Moment volume = Volume(ref cube, momentSpan);
            Moment variance =
                  momentSpan[GetPaletteIndex(cube.RMax, cube.GMax, cube.BMax, cube.AMax)]
                - momentSpan[GetPaletteIndex(cube.RMax, cube.GMax, cube.BMax, cube.AMin)]
                - momentSpan[GetPaletteIndex(cube.RMax, cube.GMax, cube.BMin, cube.AMax)]
                + momentSpan[GetPaletteIndex(cube.RMax, cube.GMax, cube.BMin, cube.AMin)]
                - momentSpan[GetPaletteIndex(cube.RMax, cube.GMin, cube.BMax, cube.AMax)]
                + momentSpan[GetPaletteIndex(cube.RMax, cube.GMin, cube.BMax, cube.AMin)]
                + momentSpan[GetPaletteIndex(cube.RMax, cube.GMin, cube.BMin, cube.AMax)]
                - momentSpan[GetPaletteIndex(cube.RMax, cube.GMin, cube.BMin, cube.AMin)]
                - momentSpan[GetPaletteIndex(cube.RMin, cube.GMax, cube.BMax, cube.AMax)]
                + momentSpan[GetPaletteIndex(cube.RMin, cube.GMax, cube.BMax, cube.AMin)]
                + momentSpan[GetPaletteIndex(cube.RMin, cube.GMax, cube.BMin, cube.AMax)]
                - momentSpan[GetPaletteIndex(cube.RMin, cube.GMax, cube.BMin, cube.AMin)]
                + momentSpan[GetPaletteIndex(cube.RMin, cube.GMin, cube.BMax, cube.AMax)]
                - momentSpan[GetPaletteIndex(cube.RMin, cube.GMin, cube.BMax, cube.AMin)]
                - momentSpan[GetPaletteIndex(cube.RMin, cube.GMin, cube.BMin, cube.AMax)]
                + momentSpan[GetPaletteIndex(cube.RMin, cube.GMin, cube.BMin, cube.AMin)];

            var vector = new Vector4(volume.R, volume.G, volume.B, volume.A);
            return variance.Moment2 - (Vector4.Dot(vector, vector) / volume.Weight);
        }

        /// <summary>
        /// We want to minimize the sum of the variances of two sub-boxes.
        /// The sum(c^2) terms can be ignored since their sum over both sub-boxes
        /// is the same (the sum for the whole box) no matter where we split.
        /// The remaining terms have a minus sign in the variance formula,
        /// so we drop the minus sign and maximize the sum of the two terms.
        /// </summary>
        /// <param name="cube">The cube.</param>
        /// <param name="direction">The direction.</param>
        /// <param name="first">The first position.</param>
        /// <param name="last">The last position.</param>
        /// <param name="cut">The cutting point.</param>
        /// <param name="whole">The whole moment.</param>
        /// <returns>The <see cref="float"/>.</returns>
        private float Maximize(ref Box cube, int direction, int first, int last, out int cut, Moment whole)
        {
            ReadOnlySpan<Moment> momentSpan = this.moments.GetSpan();
            Moment bottom = Bottom(ref cube, direction, momentSpan);

            float max = 0F;
            cut = -1;

            for (int i = first; i < last; i++)
            {
                Moment half = bottom + Top(ref cube, direction, i, momentSpan);

                if (half.Weight == 0)
                {
                    continue;
                }

                var vector = new Vector4(half.R, half.G, half.B, half.A);
                float temp = Vector4.Dot(vector, vector) / half.Weight;

                half = whole - half;

                if (half.Weight == 0)
                {
                    continue;
                }

                vector = new Vector4(half.R, half.G, half.B, half.A);
                temp += Vector4.Dot(vector, vector) / half.Weight;

                if (temp > max)
                {
                    max = temp;
                    cut = i;
                }
            }

            return max;
        }

        /// <summary>
        /// Cuts a box.
        /// </summary>
        /// <param name="set1">The first set.</param>
        /// <param name="set2">The second set.</param>
        /// <returns>Returns a value indicating whether the box has been split.</returns>
        private bool Cut(ref Box set1, ref Box set2)
        {
            ReadOnlySpan<Moment> momentSpan = this.moments.GetSpan();
            Moment whole = Volume(ref set1, momentSpan);

            float maxR = this.Maximize(ref set1, 3, set1.RMin + 1, set1.RMax, out int cutR, whole);
            float maxG = this.Maximize(ref set1, 2, set1.GMin + 1, set1.GMax, out int cutG, whole);
            float maxB = this.Maximize(ref set1, 1, set1.BMin + 1, set1.BMax, out int cutB, whole);
            float maxA = this.Maximize(ref set1, 0, set1.AMin + 1, set1.AMax, out int cutA, whole);

            int dir;

            if ((maxR >= maxG) && (maxR >= maxB) && (maxR >= maxA))
            {
                dir = 3;

                if (cutR < 0)
                {
                    return false;
                }
            }
            else if ((maxG >= maxR) && (maxG >= maxB) && (maxG >= maxA))
            {
                dir = 2;
            }
            else if ((maxB >= maxR) && (maxB >= maxG) && (maxB >= maxA))
            {
                dir = 1;
            }
            else
            {
                dir = 0;
            }

            set2.RMax = set1.RMax;
            set2.GMax = set1.GMax;
            set2.BMax = set1.BMax;
            set2.AMax = set1.AMax;

            switch (dir)
            {
                // Red
                case 3:
                    set2.RMin = set1.RMax = cutR;
                    set2.GMin = set1.GMin;
                    set2.BMin = set1.BMin;
                    set2.AMin = set1.AMin;
                    break;

                // Green
                case 2:
                    set2.GMin = set1.GMax = cutG;
                    set2.RMin = set1.RMin;
                    set2.BMin = set1.BMin;
                    set2.AMin = set1.AMin;
                    break;

                // Blue
                case 1:
                    set2.BMin = set1.BMax = cutB;
                    set2.RMin = set1.RMin;
                    set2.GMin = set1.GMin;
                    set2.AMin = set1.AMin;
                    break;

                // Alpha
                case 0:
                    set2.AMin = set1.AMax = cutA;
                    set2.RMin = set1.RMin;
                    set2.GMin = set1.GMin;
                    set2.BMin = set1.BMin;
                    break;
            }

            set1.Volume = (set1.RMax - set1.RMin) * (set1.GMax - set1.GMin) * (set1.BMax - set1.BMin) * (set1.AMax - set1.AMin);
            set2.Volume = (set2.RMax - set2.RMin) * (set2.GMax - set2.GMin) * (set2.BMax - set2.BMin) * (set2.AMax - set2.AMin);

            return true;
        }

        /// <summary>
        /// Marks a color space tag.
        /// </summary>
        /// <param name="cube">The cube.</param>
        /// <param name="label">A label.</param>
        private void Mark(ref Box cube, byte label)
        {
            Span<byte> tagSpan = this.tag.GetSpan();

            for (int r = cube.RMin + 1; r <= cube.RMax; r++)
            {
                for (int g = cube.GMin + 1; g <= cube.GMax; g++)
                {
                    for (int b = cube.BMin + 1; b <= cube.BMax; b++)
                    {
                        for (int a = cube.AMin + 1; a <= cube.AMax; a++)
                        {
                            tagSpan[GetPaletteIndex(r, g, b, a)] = label;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Builds the cube.
        /// </summary>
        private void BuildCube()
        {
            Span<double> vv = stackalloc double[this.colors];

            ref Box cube = ref this.colorCube[0];
            cube.RMin = cube.GMin = cube.BMin = cube.AMin = 0;
            cube.RMax = cube.GMax = cube.BMax = IndexCount - 1;
            cube.AMax = IndexAlphaCount - 1;

            int next = 0;

            for (int i = 1; i < this.colors; i++)
            {
                ref Box nextCube = ref this.colorCube[next];
                ref Box currentCube = ref this.colorCube[i];
                if (this.Cut(ref nextCube, ref currentCube))
                {
                    vv[next] = nextCube.Volume > 1 ? this.Variance(ref nextCube) : 0D;
                    vv[i] = currentCube.Volume > 1 ? this.Variance(ref currentCube) : 0D;
                }
                else
                {
                    vv[next] = 0D;
                    i--;
                }

                next = 0;

                double temp = vv[0];
                for (int k = 1; k <= i; k++)
                {
                    if (vv[k] > temp)
                    {
                        temp = vv[k];
                        next = k;
                    }
                }

                if (temp <= 0D)
                {
                    this.colors = i + 1;
                    break;
                }
            }
        }

        private struct Moment
        {
            /// <summary>
            /// Moment of <c>r*P(c)</c>.
            /// </summary>
            public long R;

            /// <summary>
            /// Moment of <c>g*P(c)</c>.
            /// </summary>
            public long G;

            /// <summary>
            /// Moment of <c>b*P(c)</c>.
            /// </summary>
            public long B;

            /// <summary>
            /// Moment of <c>a*P(c)</c>.
            /// </summary>
            public long A;

            /// <summary>
            /// Moment of <c>P(c)</c>.
            /// </summary>
            public long Weight;

            /// <summary>
            /// Moment of <c>c^2*P(c)</c>.
            /// </summary>
            public double Moment2;

            [MethodImpl(InliningOptions.ShortMethod)]
            public static Moment operator +(Moment x, Moment y)
            {
                x.R += y.R;
                x.G += y.G;
                x.B += y.B;
                x.A += y.A;
                x.Weight += y.Weight;
                x.Moment2 += y.Moment2;
                return x;
            }

            [MethodImpl(InliningOptions.ShortMethod)]
            public static Moment operator -(Moment x, Moment y)
            {
                x.R -= y.R;
                x.G -= y.G;
                x.B -= y.B;
                x.A -= y.A;
                x.Weight -= y.Weight;
                x.Moment2 -= y.Moment2;
                return x;
            }

            [MethodImpl(InliningOptions.ShortMethod)]
            public static Moment operator -(Moment x)
            {
                x.R = -x.R;
                x.G = -x.G;
                x.B = -x.B;
                x.A = -x.A;
                x.Weight = -x.Weight;
                x.Moment2 = -x.Moment2;
                return x;
            }

            [MethodImpl(InliningOptions.ShortMethod)]
            public static Moment operator +(Moment x, Rgba32 y)
            {
                x.R += y.R;
                x.G += y.G;
                x.B += y.B;
                x.A += y.A;
                x.Weight++;

                var vector = new Vector4(y.R, y.G, y.B, y.A);
                x.Moment2 += Vector4.Dot(vector, vector);

                return x;
            }

            [MethodImpl(InliningOptions.ShortMethod)]
            public readonly Vector4 Normalize()
                => new Vector4(this.R, this.G, this.B, this.A) / this.Weight / 255F;
        }

        /// <summary>
        /// Represents a box color cube.
        /// </summary>
        private struct Box : IEquatable<Box>
        {
            /// <summary>
            /// Gets or sets the min red value, exclusive.
            /// </summary>
            public int RMin;

            /// <summary>
            /// Gets or sets the max red value, inclusive.
            /// </summary>
            public int RMax;

            /// <summary>
            /// Gets or sets the min green value, exclusive.
            /// </summary>
            public int GMin;

            /// <summary>
            /// Gets or sets the max green value, inclusive.
            /// </summary>
            public int GMax;

            /// <summary>
            /// Gets or sets the min blue value, exclusive.
            /// </summary>
            public int BMin;

            /// <summary>
            /// Gets or sets the max blue value, inclusive.
            /// </summary>
            public int BMax;

            /// <summary>
            /// Gets or sets the min alpha value, exclusive.
            /// </summary>
            public int AMin;

            /// <summary>
            /// Gets or sets the max alpha value, inclusive.
            /// </summary>
            public int AMax;

            /// <summary>
            /// Gets or sets the volume.
            /// </summary>
            public int Volume;

            /// <inheritdoc/>
            public readonly override bool Equals(object obj)
                => obj is Box box
                && this.Equals(box);

            /// <inheritdoc/>
            public readonly bool Equals(Box other) =>
                this.RMin == other.RMin
                && this.RMax == other.RMax
                && this.GMin == other.GMin
                && this.GMax == other.GMax
                && this.BMin == other.BMin
                && this.BMax == other.BMax
                && this.AMin == other.AMin
                && this.AMax == other.AMax
                && this.Volume == other.Volume;

            /// <inheritdoc/>
            public readonly override int GetHashCode()
            {
                HashCode hash = default;
                hash.Add(this.RMin);
                hash.Add(this.RMax);
                hash.Add(this.GMin);
                hash.Add(this.GMax);
                hash.Add(this.BMin);
                hash.Add(this.BMax);
                hash.Add(this.AMin);
                hash.Add(this.AMax);
                hash.Add(this.Volume);
                return hash.ToHashCode();
            }
        }
    }
}
