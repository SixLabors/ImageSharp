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
using SixLabors.Memory;

// TODO: Isn't an AOS ("array of structures") layout more efficient & more readable than SOA ("structure of arrays") for this particular use case?
// (T, R, G, B, A, M2) could be grouped together! Investigate a ColorMoment struct.
namespace SixLabors.ImageSharp.Processing.Processors.Quantization
{
    /// <summary>
    /// An implementation of Wu's color quantizer with alpha channel.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Based on C Implementation of Xiaolin Wu's Color Quantizer (v. 2)
    /// (see Graphics Gems volume II, pages 126-133)
    /// (<see href="http://www.ece.mcmaster.ca/~xwu/cq.c"/>).
    /// </para>
    /// <para>
    /// This adaptation is based on the excellent JeremyAnsel.ColorQuant by Jérémy Ansel
    /// <see href="https://github.com/JeremyAnsel/JeremyAnsel.ColorQuant"/>
    /// </para>
    /// <para>
    /// Algorithm: Greedy orthogonal bipartition of RGB space for variance minimization aided by inclusion-exclusion tricks.
    /// For speed no nearest neighbor search is done. Slightly better performance can be expected by more sophisticated
    /// but more expensive versions.
    /// </para>
    /// </remarks>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal sealed class WuFrameQuantizer<TPixel> : FrameQuantizer<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
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

        /// <summary>
        /// Moment of <c>P(c)</c>.
        /// </summary>
        private IMemoryOwner<long> vwt;

        /// <summary>
        /// Moment of <c>r*P(c)</c>.
        /// </summary>
        private IMemoryOwner<long> vmr;

        /// <summary>
        /// Moment of <c>g*P(c)</c>.
        /// </summary>
        private IMemoryOwner<long> vmg;

        /// <summary>
        /// Moment of <c>b*P(c)</c>.
        /// </summary>
        private IMemoryOwner<long> vmb;

        /// <summary>
        /// Moment of <c>a*P(c)</c>.
        /// </summary>
        private IMemoryOwner<long> vma;

        /// <summary>
        /// Moment of <c>c^2*P(c)</c>.
        /// </summary>
        private IMemoryOwner<double> m2;

        /// <summary>
        /// Color space tag.
        /// </summary>
        private IMemoryOwner<byte> tag;

        /// <summary>
        /// Maximum allowed color depth
        /// </summary>
        private int colors;

        /// <summary>
        /// The reduced image palette
        /// </summary>
        private TPixel[] palette;

        /// <summary>
        /// The color cube representing the image palette
        /// </summary>
        private Box[] colorCube;

        /// <summary>
        /// Initializes a new instance of the <see cref="WuFrameQuantizer{TPixel}"/> class.
        /// </summary>
        /// <param name="memoryAllocator">The <see cref="MemoryAllocator"/>.</param>
        /// <param name="quantizer">The wu quantizer</param>
        /// <remarks>
        /// The Wu quantizer is a two pass algorithm. The initial pass sets up the 3-D color histogram,
        /// the second pass quantizes a color based on the position in the histogram.
        /// </remarks>
        public WuFrameQuantizer(MemoryAllocator memoryAllocator, WuQuantizer quantizer)
            : this(memoryAllocator, quantizer, quantizer.MaxColors)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="WuFrameQuantizer{TPixel}"/> class.
        /// </summary>
        /// <param name="memoryAllocator">The <see cref="MemoryAllocator"/>.</param>
        /// <param name="quantizer">The wu quantizer.</param>
        /// <param name="maxColors">The maximum number of colors to hold in the color palette.</param>
        /// <remarks>
        /// The Wu quantizer is a two pass algorithm. The initial pass sets up the 3-D color histogram,
        /// the second pass quantizes a color based on the position in the histogram.
        /// </remarks>
        public WuFrameQuantizer(MemoryAllocator memoryAllocator, WuQuantizer quantizer, int maxColors)
            : base(quantizer, false)
        {
            Guard.NotNull(memoryAllocator, nameof(memoryAllocator));

            this.vwt = memoryAllocator.Allocate<long>(TableLength, AllocationOptions.Clean);
            this.vmr = memoryAllocator.Allocate<long>(TableLength, AllocationOptions.Clean);
            this.vmg = memoryAllocator.Allocate<long>(TableLength, AllocationOptions.Clean);
            this.vmb = memoryAllocator.Allocate<long>(TableLength, AllocationOptions.Clean);
            this.vma = memoryAllocator.Allocate<long>(TableLength, AllocationOptions.Clean);
            this.m2 = memoryAllocator.Allocate<double>(TableLength, AllocationOptions.Clean);
            this.tag = memoryAllocator.Allocate<byte>(TableLength, AllocationOptions.Clean);

            this.colors = maxColors;
        }

        /// <inheritdoc/>
        public override void Dispose()
        {
            this.vwt?.Dispose();
            this.vmr?.Dispose();
            this.vmg?.Dispose();
            this.vmb?.Dispose();
            this.vma?.Dispose();
            this.m2?.Dispose();
            this.tag?.Dispose();

            this.vwt = null;
            this.vmr = null;
            this.vmg = null;
            this.vmb = null;
            this.vma = null;
            this.m2 = null;
            this.tag = null;
        }

        internal ReadOnlyMemory<TPixel> AotGetPalette() => this.GetPalette();

        /// <inheritdoc/>
        protected override ReadOnlyMemory<TPixel> GetPalette()
        {
            if (this.palette is null)
            {
                this.palette = new TPixel[this.colors];
                Span<long> vwtSpan = this.vwt.GetSpan();
                Span<long> vmrSpan = this.vmr.GetSpan();
                Span<long> vmgSpan = this.vmg.GetSpan();
                Span<long> vmbSpan = this.vmb.GetSpan();
                Span<long> vmaSpan = this.vma.GetSpan();

                for (int k = 0; k < this.colors; k++)
                {
                    this.Mark(ref this.colorCube[k], (byte)k);

                    float weight = Volume(ref this.colorCube[k], vwtSpan);

                    if (MathF.Abs(weight) > Constants.Epsilon)
                    {
                        float r = Volume(ref this.colorCube[k], vmrSpan);
                        float g = Volume(ref this.colorCube[k], vmgSpan);
                        float b = Volume(ref this.colorCube[k], vmbSpan);
                        float a = Volume(ref this.colorCube[k], vmaSpan);

                        ref TPixel color = ref this.palette[k];
                        color.FromScaledVector4(new Vector4(r, g, b, a) / weight / 255F);
                    }
                }
            }

            return this.palette;
        }

        /// <inheritdoc/>
        protected override void FirstPass(ImageFrame<TPixel> source, int width, int height)
        {
            this.Build3DHistogram(source, width, height);
            this.Get3DMoments(source.MemoryAllocator);
            this.BuildCube();
        }

        /// <inheritdoc/>
        protected override void SecondPass(ImageFrame<TPixel> source, Span<byte> output, ReadOnlySpan<TPixel> palette, int width, int height)
        {
            // Load up the values for the first pixel. We can use these to speed up the second
            // pass of the algorithm by avoiding transforming rows of identical color.
            TPixel sourcePixel = source[0, 0];
            TPixel previousPixel = sourcePixel;
            byte pixelValue = this.QuantizePixel(ref sourcePixel);
            TPixel transformedPixel = palette[pixelValue];

            for (int y = 0; y < height; y++)
            {
                Span<TPixel> row = source.GetPixelRowSpan(y);

                // And loop through each column
                for (int x = 0; x < width; x++)
                {
                    // Get the pixel.
                    sourcePixel = row[x];

                    // Check if this is the same as the last pixel. If so use that value
                    // rather than calculating it again. This is an inexpensive optimization.
                    if (!previousPixel.Equals(sourcePixel))
                    {
                        // Quantize the pixel
                        pixelValue = this.QuantizePixel(ref sourcePixel);

                        // And setup the previous pointer
                        previousPixel = sourcePixel;

                        if (this.Dither)
                        {
                            transformedPixel = palette[pixelValue];
                        }
                    }

                    if (this.Dither)
                    {
                        // Apply the dithering matrix. We have to reapply the value now as the original has changed.
                        this.Diffuser.Dither(source, sourcePixel, transformedPixel, x, y, 0, 0, width, height);
                    }

                    output[(y * source.Width) + x] = pixelValue;
                }
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
        /// <param name="moment">The moment.</param>
        /// <returns>The result.</returns>
        private static float Volume(ref Box cube, Span<long> moment)
        {
            return moment[GetPaletteIndex(cube.RMax, cube.GMax, cube.BMax, cube.AMax)]
                 - moment[GetPaletteIndex(cube.RMax, cube.GMax, cube.BMax, cube.AMin)]
                 - moment[GetPaletteIndex(cube.RMax, cube.GMax, cube.BMin, cube.AMax)]
                 + moment[GetPaletteIndex(cube.RMax, cube.GMax, cube.BMin, cube.AMin)]
                 - moment[GetPaletteIndex(cube.RMax, cube.GMin, cube.BMax, cube.AMax)]
                 + moment[GetPaletteIndex(cube.RMax, cube.GMin, cube.BMax, cube.AMin)]
                 + moment[GetPaletteIndex(cube.RMax, cube.GMin, cube.BMin, cube.AMax)]
                 - moment[GetPaletteIndex(cube.RMax, cube.GMin, cube.BMin, cube.AMin)]
                 - moment[GetPaletteIndex(cube.RMin, cube.GMax, cube.BMax, cube.AMax)]
                 + moment[GetPaletteIndex(cube.RMin, cube.GMax, cube.BMax, cube.AMin)]
                 + moment[GetPaletteIndex(cube.RMin, cube.GMax, cube.BMin, cube.AMax)]
                 - moment[GetPaletteIndex(cube.RMin, cube.GMax, cube.BMin, cube.AMin)]
                 + moment[GetPaletteIndex(cube.RMin, cube.GMin, cube.BMax, cube.AMax)]
                 - moment[GetPaletteIndex(cube.RMin, cube.GMin, cube.BMax, cube.AMin)]
                 - moment[GetPaletteIndex(cube.RMin, cube.GMin, cube.BMin, cube.AMax)]
                 + moment[GetPaletteIndex(cube.RMin, cube.GMin, cube.BMin, cube.AMin)];
        }

        /// <summary>
        /// Computes part of Volume(cube, moment) that doesn't depend on RMax, GMax, BMax, or AMax (depending on direction).
        /// </summary>
        /// <param name="cube">The cube.</param>
        /// <param name="direction">The direction.</param>
        /// <param name="moment">The moment.</param>
        /// <returns>The result.</returns>
        private static long Bottom(ref Box cube, int direction, Span<long> moment)
        {
            switch (direction)
            {
                // Red
                case 3:
                    return -moment[GetPaletteIndex(cube.RMin, cube.GMax, cube.BMax, cube.AMax)]
                         + moment[GetPaletteIndex(cube.RMin, cube.GMax, cube.BMax, cube.AMin)]
                         + moment[GetPaletteIndex(cube.RMin, cube.GMax, cube.BMin, cube.AMax)]
                         - moment[GetPaletteIndex(cube.RMin, cube.GMax, cube.BMin, cube.AMin)]
                         + moment[GetPaletteIndex(cube.RMin, cube.GMin, cube.BMax, cube.AMax)]
                         - moment[GetPaletteIndex(cube.RMin, cube.GMin, cube.BMax, cube.AMin)]
                         - moment[GetPaletteIndex(cube.RMin, cube.GMin, cube.BMin, cube.AMax)]
                         + moment[GetPaletteIndex(cube.RMin, cube.GMin, cube.BMin, cube.AMin)];

                // Green
                case 2:
                    return -moment[GetPaletteIndex(cube.RMax, cube.GMin, cube.BMax, cube.AMax)]
                         + moment[GetPaletteIndex(cube.RMax, cube.GMin, cube.BMax, cube.AMin)]
                         + moment[GetPaletteIndex(cube.RMax, cube.GMin, cube.BMin, cube.AMax)]
                         - moment[GetPaletteIndex(cube.RMax, cube.GMin, cube.BMin, cube.AMin)]
                         + moment[GetPaletteIndex(cube.RMin, cube.GMin, cube.BMax, cube.AMax)]
                         - moment[GetPaletteIndex(cube.RMin, cube.GMin, cube.BMax, cube.AMin)]
                         - moment[GetPaletteIndex(cube.RMin, cube.GMin, cube.BMin, cube.AMax)]
                         + moment[GetPaletteIndex(cube.RMin, cube.GMin, cube.BMin, cube.AMin)];

                // Blue
                case 1:
                    return -moment[GetPaletteIndex(cube.RMax, cube.GMax, cube.BMin, cube.AMax)]
                         + moment[GetPaletteIndex(cube.RMax, cube.GMax, cube.BMin, cube.AMin)]
                         + moment[GetPaletteIndex(cube.RMax, cube.GMin, cube.BMin, cube.AMax)]
                         - moment[GetPaletteIndex(cube.RMax, cube.GMin, cube.BMin, cube.AMin)]
                         + moment[GetPaletteIndex(cube.RMin, cube.GMax, cube.BMin, cube.AMax)]
                         - moment[GetPaletteIndex(cube.RMin, cube.GMax, cube.BMin, cube.AMin)]
                         - moment[GetPaletteIndex(cube.RMin, cube.GMin, cube.BMin, cube.AMax)]
                         + moment[GetPaletteIndex(cube.RMin, cube.GMin, cube.BMin, cube.AMin)];

                // Alpha
                case 0:
                    return -moment[GetPaletteIndex(cube.RMax, cube.GMax, cube.BMax, cube.AMin)]
                         + moment[GetPaletteIndex(cube.RMax, cube.GMax, cube.BMin, cube.AMin)]
                         + moment[GetPaletteIndex(cube.RMax, cube.GMin, cube.BMax, cube.AMin)]
                         - moment[GetPaletteIndex(cube.RMax, cube.GMin, cube.BMin, cube.AMin)]
                         + moment[GetPaletteIndex(cube.RMin, cube.GMax, cube.BMax, cube.AMin)]
                         - moment[GetPaletteIndex(cube.RMin, cube.GMax, cube.BMin, cube.AMin)]
                         - moment[GetPaletteIndex(cube.RMin, cube.GMin, cube.BMax, cube.AMin)]
                         + moment[GetPaletteIndex(cube.RMin, cube.GMin, cube.BMin, cube.AMin)];

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
        /// <param name="moment">The moment.</param>
        /// <returns>The result.</returns>
        private static long Top(ref Box cube, int direction, int position, Span<long> moment)
        {
            switch (direction)
            {
                // Red
                case 3:
                    return moment[GetPaletteIndex(position, cube.GMax, cube.BMax, cube.AMax)]
                         - moment[GetPaletteIndex(position, cube.GMax, cube.BMax, cube.AMin)]
                         - moment[GetPaletteIndex(position, cube.GMax, cube.BMin, cube.AMax)]
                         + moment[GetPaletteIndex(position, cube.GMax, cube.BMin, cube.AMin)]
                         - moment[GetPaletteIndex(position, cube.GMin, cube.BMax, cube.AMax)]
                         + moment[GetPaletteIndex(position, cube.GMin, cube.BMax, cube.AMin)]
                         + moment[GetPaletteIndex(position, cube.GMin, cube.BMin, cube.AMax)]
                         - moment[GetPaletteIndex(position, cube.GMin, cube.BMin, cube.AMin)];

                // Green
                case 2:
                    return moment[GetPaletteIndex(cube.RMax, position, cube.BMax, cube.AMax)]
                         - moment[GetPaletteIndex(cube.RMax, position, cube.BMax, cube.AMin)]
                         - moment[GetPaletteIndex(cube.RMax, position, cube.BMin, cube.AMax)]
                         + moment[GetPaletteIndex(cube.RMax, position, cube.BMin, cube.AMin)]
                         - moment[GetPaletteIndex(cube.RMin, position, cube.BMax, cube.AMax)]
                         + moment[GetPaletteIndex(cube.RMin, position, cube.BMax, cube.AMin)]
                         + moment[GetPaletteIndex(cube.RMin, position, cube.BMin, cube.AMax)]
                         - moment[GetPaletteIndex(cube.RMin, position, cube.BMin, cube.AMin)];

                // Blue
                case 1:
                    return moment[GetPaletteIndex(cube.RMax, cube.GMax, position, cube.AMax)]
                         - moment[GetPaletteIndex(cube.RMax, cube.GMax, position, cube.AMin)]
                         - moment[GetPaletteIndex(cube.RMax, cube.GMin, position, cube.AMax)]
                         + moment[GetPaletteIndex(cube.RMax, cube.GMin, position, cube.AMin)]
                         - moment[GetPaletteIndex(cube.RMin, cube.GMax, position, cube.AMax)]
                         + moment[GetPaletteIndex(cube.RMin, cube.GMax, position, cube.AMin)]
                         + moment[GetPaletteIndex(cube.RMin, cube.GMin, position, cube.AMax)]
                         - moment[GetPaletteIndex(cube.RMin, cube.GMin, position, cube.AMin)];

                // Alpha
                case 0:
                    return moment[GetPaletteIndex(cube.RMax, cube.GMax, cube.BMax, position)]
                         - moment[GetPaletteIndex(cube.RMax, cube.GMax, cube.BMin, position)]
                         - moment[GetPaletteIndex(cube.RMax, cube.GMin, cube.BMax, position)]
                         + moment[GetPaletteIndex(cube.RMax, cube.GMin, cube.BMin, position)]
                         - moment[GetPaletteIndex(cube.RMin, cube.GMax, cube.BMax, position)]
                         + moment[GetPaletteIndex(cube.RMin, cube.GMax, cube.BMin, position)]
                         + moment[GetPaletteIndex(cube.RMin, cube.GMin, cube.BMax, position)]
                         - moment[GetPaletteIndex(cube.RMin, cube.GMin, cube.BMin, position)];

                default:
                    throw new ArgumentOutOfRangeException(nameof(direction));
            }
        }

        /// <summary>
        /// Builds a 3-D color histogram of <c>counts, r/g/b, c^2</c>.
        /// </summary>
        /// <param name="source">The source data.</param>
        /// <param name="width">The width in pixels of the image.</param>
        /// <param name="height">The height in pixels of the image.</param>
        private void Build3DHistogram(ImageFrame<TPixel> source, int width, int height)
        {
            Span<long> vwtSpan = this.vwt.GetSpan();
            Span<long> vmrSpan = this.vmr.GetSpan();
            Span<long> vmgSpan = this.vmg.GetSpan();
            Span<long> vmbSpan = this.vmb.GetSpan();
            Span<long> vmaSpan = this.vma.GetSpan();
            Span<double> m2Span = this.m2.GetSpan();

            // Build up the 3-D color histogram
            // Loop through each row
            using (IMemoryOwner<Rgba32> rgbaBuffer = source.MemoryAllocator.Allocate<Rgba32>(source.Width))
            {
                for (int y = 0; y < height; y++)
                {
                    Span<TPixel> row = source.GetPixelRowSpan(y);
                    Span<Rgba32> rgbaSpan = rgbaBuffer.GetSpan();
                    PixelOperations<TPixel>.Instance.ToRgba32(source.Configuration, row, rgbaSpan);
                    ref Rgba32 scanBaseRef = ref MemoryMarshal.GetReference(rgbaSpan);

                    // And loop through each column
                    for (int x = 0; x < width; x++)
                    {
                        ref Rgba32 rgba = ref Unsafe.Add(ref scanBaseRef, x);

                        int r = rgba.R >> (8 - IndexBits);
                        int g = rgba.G >> (8 - IndexBits);
                        int b = rgba.B >> (8 - IndexBits);
                        int a = rgba.A >> (8 - IndexAlphaBits);

                        int index = GetPaletteIndex(r + 1, g + 1, b + 1, a + 1);

                        vwtSpan[index]++;
                        vmrSpan[index] += rgba.R;
                        vmgSpan[index] += rgba.G;
                        vmbSpan[index] += rgba.B;
                        vmaSpan[index] += rgba.A;

                        var vector = new Vector4(rgba.R, rgba.G, rgba.B, rgba.A);
                        m2Span[index] += Vector4.Dot(vector, vector);
                    }
                }
            }
        }

        /// <summary>
        /// Converts the histogram into moments so that we can rapidly calculate the sums of the above quantities over any desired box.
        /// </summary>
        /// <param name="memoryAllocator">The memory allocator used for allocating buffers.</param>
        private void Get3DMoments(MemoryAllocator memoryAllocator)
        {
            Span<long> vwtSpan = this.vwt.GetSpan();
            Span<long> vmrSpan = this.vmr.GetSpan();
            Span<long> vmgSpan = this.vmg.GetSpan();
            Span<long> vmbSpan = this.vmb.GetSpan();
            Span<long> vmaSpan = this.vma.GetSpan();
            Span<double> m2Span = this.m2.GetSpan();

            using (IMemoryOwner<long> volume = memoryAllocator.Allocate<long>(IndexCount * IndexAlphaCount))
            using (IMemoryOwner<long> volumeR = memoryAllocator.Allocate<long>(IndexCount * IndexAlphaCount))
            using (IMemoryOwner<long> volumeG = memoryAllocator.Allocate<long>(IndexCount * IndexAlphaCount))
            using (IMemoryOwner<long> volumeB = memoryAllocator.Allocate<long>(IndexCount * IndexAlphaCount))
            using (IMemoryOwner<long> volumeA = memoryAllocator.Allocate<long>(IndexCount * IndexAlphaCount))
            using (IMemoryOwner<double> volume2 = memoryAllocator.Allocate<double>(IndexCount * IndexAlphaCount))
            using (IMemoryOwner<long> area = memoryAllocator.Allocate<long>(IndexAlphaCount))
            using (IMemoryOwner<long> areaR = memoryAllocator.Allocate<long>(IndexAlphaCount))
            using (IMemoryOwner<long> areaG = memoryAllocator.Allocate<long>(IndexAlphaCount))
            using (IMemoryOwner<long> areaB = memoryAllocator.Allocate<long>(IndexAlphaCount))
            using (IMemoryOwner<long> areaA = memoryAllocator.Allocate<long>(IndexAlphaCount))
            using (IMemoryOwner<double> area2 = memoryAllocator.Allocate<double>(IndexAlphaCount))
            {
                Span<long> volumeSpan = volume.GetSpan();
                Span<long> volumeRSpan = volumeR.GetSpan();
                Span<long> volumeGSpan = volumeG.GetSpan();
                Span<long> volumeBSpan = volumeB.GetSpan();
                Span<long> volumeASpan = volumeA.GetSpan();
                Span<double> volume2Span = volume2.GetSpan();

                Span<long> areaSpan = area.GetSpan();
                Span<long> areaRSpan = areaR.GetSpan();
                Span<long> areaGSpan = areaG.GetSpan();
                Span<long> areaBSpan = areaB.GetSpan();
                Span<long> areaASpan = areaA.GetSpan();
                Span<double> area2Span = area2.GetSpan();

                for (int r = 1; r < IndexCount; r++)
                {
                    volume.Clear();
                    volumeR.Clear();
                    volumeG.Clear();
                    volumeB.Clear();
                    volumeA.Clear();
                    volume2.Clear();

                    for (int g = 1; g < IndexCount; g++)
                    {
                        area.Clear();
                        areaR.Clear();
                        areaG.Clear();
                        areaB.Clear();
                        areaA.Clear();
                        area2.Clear();

                        for (int b = 1; b < IndexCount; b++)
                        {
                            long line = 0;
                            long lineR = 0;
                            long lineG = 0;
                            long lineB = 0;
                            long lineA = 0;
                            double line2 = 0;

                            for (int a = 1; a < IndexAlphaCount; a++)
                            {
                                int ind1 = GetPaletteIndex(r, g, b, a);

                                line += vwtSpan[ind1];
                                lineR += vmrSpan[ind1];
                                lineG += vmgSpan[ind1];
                                lineB += vmbSpan[ind1];
                                lineA += vmaSpan[ind1];
                                line2 += m2Span[ind1];

                                areaSpan[a] += line;
                                areaRSpan[a] += lineR;
                                areaGSpan[a] += lineG;
                                areaBSpan[a] += lineB;
                                areaASpan[a] += lineA;
                                area2Span[a] += line2;

                                int inv = (b * IndexAlphaCount) + a;

                                volumeSpan[inv] += areaSpan[a];
                                volumeRSpan[inv] += areaRSpan[a];
                                volumeGSpan[inv] += areaGSpan[a];
                                volumeBSpan[inv] += areaBSpan[a];
                                volumeASpan[inv] += areaASpan[a];
                                volume2Span[inv] += area2Span[a];

                                int ind2 = ind1 - GetPaletteIndex(1, 0, 0, 0);

                                vwtSpan[ind1] = vwtSpan[ind2] + volumeSpan[inv];
                                vmrSpan[ind1] = vmrSpan[ind2] + volumeRSpan[inv];
                                vmgSpan[ind1] = vmgSpan[ind2] + volumeGSpan[inv];
                                vmbSpan[ind1] = vmbSpan[ind2] + volumeBSpan[inv];
                                vmaSpan[ind1] = vmaSpan[ind2] + volumeASpan[inv];
                                m2Span[ind1] = m2Span[ind2] + volume2Span[inv];
                            }
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
            float dr = Volume(ref cube, this.vmr.GetSpan());
            float dg = Volume(ref cube, this.vmg.GetSpan());
            float db = Volume(ref cube, this.vmb.GetSpan());
            float da = Volume(ref cube, this.vma.GetSpan());

            Span<double> m2Span = this.m2.GetSpan();

            double moment =
                  m2Span[GetPaletteIndex(cube.RMax, cube.GMax, cube.BMax, cube.AMax)]
                - m2Span[GetPaletteIndex(cube.RMax, cube.GMax, cube.BMax, cube.AMin)]
                - m2Span[GetPaletteIndex(cube.RMax, cube.GMax, cube.BMin, cube.AMax)]
                + m2Span[GetPaletteIndex(cube.RMax, cube.GMax, cube.BMin, cube.AMin)]
                - m2Span[GetPaletteIndex(cube.RMax, cube.GMin, cube.BMax, cube.AMax)]
                + m2Span[GetPaletteIndex(cube.RMax, cube.GMin, cube.BMax, cube.AMin)]
                + m2Span[GetPaletteIndex(cube.RMax, cube.GMin, cube.BMin, cube.AMax)]
                - m2Span[GetPaletteIndex(cube.RMax, cube.GMin, cube.BMin, cube.AMin)]
                - m2Span[GetPaletteIndex(cube.RMin, cube.GMax, cube.BMax, cube.AMax)]
                + m2Span[GetPaletteIndex(cube.RMin, cube.GMax, cube.BMax, cube.AMin)]
                + m2Span[GetPaletteIndex(cube.RMin, cube.GMax, cube.BMin, cube.AMax)]
                - m2Span[GetPaletteIndex(cube.RMin, cube.GMax, cube.BMin, cube.AMin)]
                + m2Span[GetPaletteIndex(cube.RMin, cube.GMin, cube.BMax, cube.AMax)]
                - m2Span[GetPaletteIndex(cube.RMin, cube.GMin, cube.BMax, cube.AMin)]
                - m2Span[GetPaletteIndex(cube.RMin, cube.GMin, cube.BMin, cube.AMax)]
                + m2Span[GetPaletteIndex(cube.RMin, cube.GMin, cube.BMin, cube.AMin)];

            var vector = new Vector4(dr, dg, db, da);
            return moment - (Vector4.Dot(vector, vector) / Volume(ref cube, this.vwt.GetSpan()));
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
        /// <param name="wholeR">The whole red.</param>
        /// <param name="wholeG">The whole green.</param>
        /// <param name="wholeB">The whole blue.</param>
        /// <param name="wholeA">The whole alpha.</param>
        /// <param name="wholeW">The whole weight.</param>
        /// <returns>The <see cref="float"/>.</returns>
        private float Maximize(ref Box cube, int direction, int first, int last, out int cut, float wholeR, float wholeG, float wholeB, float wholeA, float wholeW)
        {
            Span<long> vwtSpan = this.vwt.GetSpan();
            Span<long> vmrSpan = this.vmr.GetSpan();
            Span<long> vmgSpan = this.vmg.GetSpan();
            Span<long> vmbSpan = this.vmb.GetSpan();
            Span<long> vmaSpan = this.vma.GetSpan();

            long baseR = Bottom(ref cube, direction, vmrSpan);
            long baseG = Bottom(ref cube, direction, vmgSpan);
            long baseB = Bottom(ref cube, direction, vmbSpan);
            long baseA = Bottom(ref cube, direction, vmaSpan);
            long baseW = Bottom(ref cube, direction, vwtSpan);

            float max = 0F;
            cut = -1;

            for (int i = first; i < last; i++)
            {
                float halfR = baseR + Top(ref cube, direction, i, vmrSpan);
                float halfG = baseG + Top(ref cube, direction, i, vmgSpan);
                float halfB = baseB + Top(ref cube, direction, i, vmbSpan);
                float halfA = baseA + Top(ref cube, direction, i, vmaSpan);
                float halfW = baseW + Top(ref cube, direction, i, vwtSpan);

                if (MathF.Abs(halfW) < Constants.Epsilon)
                {
                    continue;
                }

                var vector = new Vector4(halfR, halfG, halfB, halfA);
                float temp = Vector4.Dot(vector, vector) / halfW;

                halfW = wholeW - halfW;

                if (MathF.Abs(halfW) < Constants.Epsilon)
                {
                    continue;
                }

                halfR = wholeR - halfR;
                halfG = wholeG - halfG;
                halfB = wholeB - halfB;
                halfA = wholeA - halfA;

                vector = new Vector4(halfR, halfG, halfB, halfA);

                temp += Vector4.Dot(vector, vector) / halfW;

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
            float wholeR = Volume(ref set1, this.vmr.GetSpan());
            float wholeG = Volume(ref set1, this.vmg.GetSpan());
            float wholeB = Volume(ref set1, this.vmb.GetSpan());
            float wholeA = Volume(ref set1, this.vma.GetSpan());
            float wholeW = Volume(ref set1, this.vwt.GetSpan());

            float maxr = this.Maximize(ref set1, 3, set1.RMin + 1, set1.RMax, out int cutr, wholeR, wholeG, wholeB, wholeA, wholeW);
            float maxg = this.Maximize(ref set1, 2, set1.GMin + 1, set1.GMax, out int cutg, wholeR, wholeG, wholeB, wholeA, wholeW);
            float maxb = this.Maximize(ref set1, 1, set1.BMin + 1, set1.BMax, out int cutb, wholeR, wholeG, wholeB, wholeA, wholeW);
            float maxa = this.Maximize(ref set1, 0, set1.AMin + 1, set1.AMax, out int cuta, wholeR, wholeG, wholeB, wholeA, wholeW);

            int dir;

            if ((maxr >= maxg) && (maxr >= maxb) && (maxr >= maxa))
            {
                dir = 3;

                if (cutr < 0)
                {
                    return false;
                }
            }
            else if ((maxg >= maxr) && (maxg >= maxb) && (maxg >= maxa))
            {
                dir = 2;
            }
            else if ((maxb >= maxr) && (maxb >= maxg) && (maxb >= maxa))
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
                    set2.RMin = set1.RMax = cutr;
                    set2.GMin = set1.GMin;
                    set2.BMin = set1.BMin;
                    set2.AMin = set1.AMin;
                    break;

                // Green
                case 2:
                    set2.GMin = set1.GMax = cutg;
                    set2.RMin = set1.RMin;
                    set2.BMin = set1.BMin;
                    set2.AMin = set1.AMin;
                    break;

                // Blue
                case 1:
                    set2.BMin = set1.BMax = cutb;
                    set2.RMin = set1.RMin;
                    set2.GMin = set1.GMin;
                    set2.AMin = set1.AMin;
                    break;

                // Alpha
                case 0:
                    set2.AMin = set1.AMax = cuta;
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
            this.colorCube = new Box[this.colors];
            var vv = new double[this.colors];

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
                    vv[next] = nextCube.Volume > 1 ? this.Variance(ref nextCube) : 0F;
                    vv[i] = currentCube.Volume > 1 ? this.Variance(ref currentCube) : 0F;
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

        /// <summary>
        /// Process the pixel in the second pass of the algorithm
        /// </summary>
        /// <param name="pixel">The pixel to quantize</param>
        /// <returns>
        /// The quantized value
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private byte QuantizePixel(ref TPixel pixel)
        {
            if (this.Dither)
            {
                // The colors have changed so we need to use Euclidean distance calculation to
                // find the closest value.
                return this.GetClosestPixel(ref pixel);
            }

            // Expected order r->g->b->a
            Rgba32 rgba = default;
            pixel.ToRgba32(ref rgba);

            int r = rgba.R >> (8 - IndexBits);
            int g = rgba.G >> (8 - IndexBits);
            int b = rgba.B >> (8 - IndexBits);
            int a = rgba.A >> (8 - IndexAlphaBits);

            Span<byte> tagSpan = this.tag.GetSpan();

            return tagSpan[GetPaletteIndex(r + 1, g + 1, b + 1, a + 1)];
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
            public override bool Equals(object obj) => obj is Box box && this.Equals(box);

            /// <inheritdoc/>
            public bool Equals(Box other) =>
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
            public override int GetHashCode()
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
