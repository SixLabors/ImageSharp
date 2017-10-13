// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Quantizers.Base;

namespace SixLabors.ImageSharp.Quantizers
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
    public class WuQuantizer<TPixel> : QuantizerBase<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// The index bits.
        /// </summary>
        private const int IndexBits = 6;

        /// <summary>
        /// The index alpha bits.
        /// </summary>
        private const int IndexAlphaBits = 3;

        /// <summary>
        /// The index count.
        /// </summary>
        private const int IndexCount = (1 << IndexBits) + 1;

        /// <summary>
        /// The index alpha count.
        /// </summary>
        private const int IndexAlphaCount = (1 << IndexAlphaBits) + 1;

        /// <summary>
        /// The table length.
        /// </summary>
        private const int TableLength = IndexCount * IndexCount * IndexCount * IndexAlphaCount;

        /// <summary>
        /// A buffer for storing pixels
        /// </summary>
        private readonly byte[] rgbaBuffer = new byte[4];

        /// <summary>
        /// A lookup table for colors
        /// </summary>
        private readonly Dictionary<TPixel, byte> colorMap = new Dictionary<TPixel, byte>();

        /// <summary>
        /// Moment of <c>P(c)</c>.
        /// </summary>
        private long[] vwt;

        /// <summary>
        /// Moment of <c>r*P(c)</c>.
        /// </summary>
        private long[] vmr;

        /// <summary>
        /// Moment of <c>g*P(c)</c>.
        /// </summary>
        private long[] vmg;

        /// <summary>
        /// Moment of <c>b*P(c)</c>.
        /// </summary>
        private long[] vmb;

        /// <summary>
        /// Moment of <c>a*P(c)</c>.
        /// </summary>
        private long[] vma;

        /// <summary>
        /// Moment of <c>c^2*P(c)</c>.
        /// </summary>
        private float[] m2;

        /// <summary>
        /// Color space tag.
        /// </summary>
        private byte[] tag;

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
        /// Initializes a new instance of the <see cref="WuQuantizer{TPixel}"/> class.
        /// </summary>
        /// <remarks>
        /// The Wu quantizer is a two pass algorithm. The initial pass sets up the 3-D color histogram,
        /// the second pass quantizes a color based on the position in the histogram.
        /// </remarks>
        public WuQuantizer()
            : base(false)
        {
        }

        /// <inheritdoc/>
        public override QuantizedImage<TPixel> Quantize(ImageFrame<TPixel> image, int maxColors)
        {
            Guard.NotNull(image, nameof(image));

            this.colors = maxColors.Clamp(1, 255);
            this.palette = null;
            this.colorMap.Clear();

            try
            {
                this.vwt = WuArrayPool.LongPool.Rent(TableLength);
                this.vmr = WuArrayPool.LongPool.Rent(TableLength);
                this.vmg = WuArrayPool.LongPool.Rent(TableLength);
                this.vmb = WuArrayPool.LongPool.Rent(TableLength);
                this.vma = WuArrayPool.LongPool.Rent(TableLength);
                this.m2 = WuArrayPool.FloatPool.Rent(TableLength);
                this.tag = WuArrayPool.BytePool.Rent(TableLength);

                return base.Quantize(image, this.colors);
            }
            finally
            {
                WuArrayPool.LongPool.Return(this.vwt, true);
                WuArrayPool.LongPool.Return(this.vmr, true);
                WuArrayPool.LongPool.Return(this.vmg, true);
                WuArrayPool.LongPool.Return(this.vmb, true);
                WuArrayPool.LongPool.Return(this.vma, true);
                WuArrayPool.FloatPool.Return(this.m2, true);
                WuArrayPool.BytePool.Return(this.tag, true);
            }
        }

        /// <inheritdoc/>
        protected override TPixel[] GetPalette()
        {
            if (this.palette == null)
            {
                this.palette = new TPixel[this.colors];
                for (int k = 0; k < this.colors; k++)
                {
                    this.Mark(this.colorCube[k], (byte)k);

                    float weight = Volume(this.colorCube[k], this.vwt);

                    if (MathF.Abs(weight) > Constants.Epsilon)
                    {
                        float r = Volume(this.colorCube[k], this.vmr) / weight;
                        float g = Volume(this.colorCube[k], this.vmg) / weight;
                        float b = Volume(this.colorCube[k], this.vmb) / weight;
                        float a = Volume(this.colorCube[k], this.vma) / weight;

                        var color = default(TPixel);
                        color.PackFromVector4(new Vector4(r, g, b, a) / 255F);
                        this.palette[k] = color;
                    }
                }
            }

            return this.palette;
        }

        /// <inheritdoc/>
        protected override void InitialQuantizePixel(TPixel pixel)
        {
            // Add the color to a 3-D color histogram.
            // Colors are expected in r->g->b->a format
            pixel.ToXyzwBytes(this.rgbaBuffer, 0);

            byte r = this.rgbaBuffer[0];
            byte g = this.rgbaBuffer[1];
            byte b = this.rgbaBuffer[2];
            byte a = this.rgbaBuffer[3];

            int inr = r >> (8 - IndexBits);
            int ing = g >> (8 - IndexBits);
            int inb = b >> (8 - IndexBits);
            int ina = a >> (8 - IndexAlphaBits);

            int ind = GetPaletteIndex(inr + 1, ing + 1, inb + 1, ina + 1);

            this.vwt[ind]++;
            this.vmr[ind] += r;
            this.vmg[ind] += g;
            this.vmb[ind] += b;
            this.vma[ind] += a;
            this.m2[ind] += (r * r) + (g * g) + (b * b) + (a * a);
        }

        /// <inheritdoc/>
        protected override void FirstPass(ImageFrame<TPixel> source, int width, int height)
        {
            // Build up the 3-D color histogram
            // Loop through each row
            for (int y = 0; y < height; y++)
            {
                // And loop through each column
                for (int x = 0; x < width; x++)
                {
                    // Now I have the pixel, call the FirstPassQuantize function...
                    this.InitialQuantizePixel(source[x, y]);
                }
            }

            this.Get3DMoments();
            this.BuildCube();
        }

        /// <inheritdoc/>
        protected override void SecondPass(ImageFrame<TPixel> source, byte[] output, int width, int height)
        {
            // Load up the values for the first pixel. We can use these to speed up the second
            // pass of the algorithm by avoiding transforming rows of identical color.
            TPixel sourcePixel = source[0, 0];
            TPixel previousPixel = sourcePixel;
            byte pixelValue = this.QuantizePixel(sourcePixel);
            TPixel[] colorPalette = this.GetPalette();
            TPixel transformedPixel = colorPalette[pixelValue];

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
                        pixelValue = this.QuantizePixel(sourcePixel);

                        // And setup the previous pointer
                        previousPixel = sourcePixel;

                        if (this.Dither)
                        {
                            transformedPixel = colorPalette[pixelValue];
                        }
                    }

                    if (this.Dither)
                    {
                        // Apply the dithering matrix. We have to reapply the value now as the original has changed.
                        this.DitherType.Dither(source, sourcePixel, transformedPixel, x, y, 0, 0, width, height, false);
                    }

                    output[(y * source.Width) + x] = pixelValue;
                }
            }
        }

        /// <summary>
        /// Gets the index index of the given color in the palette.
        /// </summary>
        /// <param name="r">The red value.</param>
        /// <param name="g">The green value.</param>
        /// <param name="b">The blue value.</param>
        /// <param name="a">The alpha value.</param>
        /// <returns>The index.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetPaletteIndex(int r, int g, int b, int a)
        {
            return (r << ((IndexBits * 2) + IndexAlphaBits)) + (r << (IndexBits + IndexAlphaBits + 1))
                   + (g << (IndexBits + IndexAlphaBits)) + (r << (IndexBits * 2)) + (r << (IndexBits + 1))
                   + (g << IndexBits) + ((r + g + b) << IndexAlphaBits) + r + g + b + a;
        }

        /// <summary>
        /// Computes sum over a box of any given statistic.
        /// </summary>
        /// <param name="cube">The cube.</param>
        /// <param name="moment">The moment.</param>
        /// <returns>The result.</returns>
        private static float Volume(Box cube, long[] moment)
        {
            return moment[GetPaletteIndex(cube.R1, cube.G1, cube.B1, cube.A1)]
                   - moment[GetPaletteIndex(cube.R1, cube.G1, cube.B1, cube.A0)]
                   - moment[GetPaletteIndex(cube.R1, cube.G1, cube.B0, cube.A1)]
                   + moment[GetPaletteIndex(cube.R1, cube.G1, cube.B0, cube.A0)]
                   - moment[GetPaletteIndex(cube.R1, cube.G0, cube.B1, cube.A1)]
                   + moment[GetPaletteIndex(cube.R1, cube.G0, cube.B1, cube.A0)]
                   + moment[GetPaletteIndex(cube.R1, cube.G0, cube.B0, cube.A1)]
                   - moment[GetPaletteIndex(cube.R1, cube.G0, cube.B0, cube.A0)]
                   - moment[GetPaletteIndex(cube.R0, cube.G1, cube.B1, cube.A1)]
                   + moment[GetPaletteIndex(cube.R0, cube.G1, cube.B1, cube.A0)]
                   + moment[GetPaletteIndex(cube.R0, cube.G1, cube.B0, cube.A1)]
                   - moment[GetPaletteIndex(cube.R0, cube.G1, cube.B0, cube.A0)]
                   + moment[GetPaletteIndex(cube.R0, cube.G0, cube.B1, cube.A1)]
                   - moment[GetPaletteIndex(cube.R0, cube.G0, cube.B1, cube.A0)]
                   - moment[GetPaletteIndex(cube.R0, cube.G0, cube.B0, cube.A1)]
                   + moment[GetPaletteIndex(cube.R0, cube.G0, cube.B0, cube.A0)];
        }

        /// <summary>
        /// Computes part of Volume(cube, moment) that doesn't depend on r1, g1, or b1 (depending on direction).
        /// </summary>
        /// <param name="cube">The cube.</param>
        /// <param name="direction">The direction.</param>
        /// <param name="moment">The moment.</param>
        /// <returns>The result.</returns>
        private static long Bottom(Box cube, int direction, long[] moment)
        {
            switch (direction)
            {
                // Red
                case 0:
                    return -moment[GetPaletteIndex(cube.R0, cube.G1, cube.B1, cube.A1)]
                           + moment[GetPaletteIndex(cube.R0, cube.G1, cube.B1, cube.A0)]
                           + moment[GetPaletteIndex(cube.R0, cube.G1, cube.B0, cube.A1)]
                           - moment[GetPaletteIndex(cube.R0, cube.G1, cube.B0, cube.A0)]
                           + moment[GetPaletteIndex(cube.R0, cube.G0, cube.B1, cube.A1)]
                           - moment[GetPaletteIndex(cube.R0, cube.G0, cube.B1, cube.A0)]
                           - moment[GetPaletteIndex(cube.R0, cube.G0, cube.B0, cube.A1)]
                           + moment[GetPaletteIndex(cube.R0, cube.G0, cube.B0, cube.A0)];

                // Green
                case 1:
                    return -moment[GetPaletteIndex(cube.R1, cube.G0, cube.B1, cube.A1)]
                           + moment[GetPaletteIndex(cube.R1, cube.G0, cube.B1, cube.A0)]
                           + moment[GetPaletteIndex(cube.R1, cube.G0, cube.B0, cube.A1)]
                           - moment[GetPaletteIndex(cube.R1, cube.G0, cube.B0, cube.A0)]
                           + moment[GetPaletteIndex(cube.R0, cube.G0, cube.B1, cube.A1)]
                           - moment[GetPaletteIndex(cube.R0, cube.G0, cube.B1, cube.A0)]
                           - moment[GetPaletteIndex(cube.R0, cube.G0, cube.B0, cube.A1)]
                           + moment[GetPaletteIndex(cube.R0, cube.G0, cube.B0, cube.A0)];

                // Blue
                case 2:
                    return -moment[GetPaletteIndex(cube.R1, cube.G1, cube.B0, cube.A1)]
                           + moment[GetPaletteIndex(cube.R1, cube.G1, cube.B0, cube.A0)]
                           + moment[GetPaletteIndex(cube.R1, cube.G0, cube.B0, cube.A1)]
                           - moment[GetPaletteIndex(cube.R1, cube.G0, cube.B0, cube.A0)]
                           + moment[GetPaletteIndex(cube.R0, cube.G1, cube.B0, cube.A1)]
                           - moment[GetPaletteIndex(cube.R0, cube.G1, cube.B0, cube.A0)]
                           - moment[GetPaletteIndex(cube.R0, cube.G0, cube.B0, cube.A1)]
                           + moment[GetPaletteIndex(cube.R0, cube.G0, cube.B0, cube.A0)];

                // Alpha
                case 3:
                    return -moment[GetPaletteIndex(cube.R1, cube.G1, cube.B1, cube.A0)]
                           + moment[GetPaletteIndex(cube.R1, cube.G1, cube.B0, cube.A0)]
                           + moment[GetPaletteIndex(cube.R1, cube.G0, cube.B1, cube.A0)]
                           - moment[GetPaletteIndex(cube.R1, cube.G0, cube.B0, cube.A0)]
                           + moment[GetPaletteIndex(cube.R0, cube.G1, cube.B1, cube.A0)]
                           - moment[GetPaletteIndex(cube.R0, cube.G1, cube.B0, cube.A0)]
                           - moment[GetPaletteIndex(cube.R0, cube.G0, cube.B1, cube.A0)]
                           + moment[GetPaletteIndex(cube.R0, cube.G0, cube.B0, cube.A0)];

                default:
                    throw new ArgumentOutOfRangeException(nameof(direction));
            }
        }

        /// <summary>
        /// Computes remainder of Volume(cube, moment), substituting position for r1, g1, or b1 (depending on direction).
        /// </summary>
        /// <param name="cube">The cube.</param>
        /// <param name="direction">The direction.</param>
        /// <param name="position">The position.</param>
        /// <param name="moment">The moment.</param>
        /// <returns>The result.</returns>
        private static long Top(Box cube, int direction, int position, long[] moment)
        {
            switch (direction)
            {
                // Red
                case 0:
                    return moment[GetPaletteIndex(position, cube.G1, cube.B1, cube.A1)]
                           - moment[GetPaletteIndex(position, cube.G1, cube.B1, cube.A0)]
                           - moment[GetPaletteIndex(position, cube.G1, cube.B0, cube.A1)]
                           + moment[GetPaletteIndex(position, cube.G1, cube.B0, cube.A0)]
                           - moment[GetPaletteIndex(position, cube.G0, cube.B1, cube.A1)]
                           + moment[GetPaletteIndex(position, cube.G0, cube.B1, cube.A0)]
                           + moment[GetPaletteIndex(position, cube.G0, cube.B0, cube.A1)]
                           - moment[GetPaletteIndex(position, cube.G0, cube.B0, cube.A0)];

                // Green
                case 1:
                    return moment[GetPaletteIndex(cube.R1, position, cube.B1, cube.A1)]
                           - moment[GetPaletteIndex(cube.R1, position, cube.B1, cube.A0)]
                           - moment[GetPaletteIndex(cube.R1, position, cube.B0, cube.A1)]
                           + moment[GetPaletteIndex(cube.R1, position, cube.B0, cube.A0)]
                           - moment[GetPaletteIndex(cube.R0, position, cube.B1, cube.A1)]
                           + moment[GetPaletteIndex(cube.R0, position, cube.B1, cube.A0)]
                           + moment[GetPaletteIndex(cube.R0, position, cube.B0, cube.A1)]
                           - moment[GetPaletteIndex(cube.R0, position, cube.B0, cube.A0)];

                // Blue
                case 2:
                    return moment[GetPaletteIndex(cube.R1, cube.G1, position, cube.A1)]
                           - moment[GetPaletteIndex(cube.R1, cube.G1, position, cube.A0)]
                           - moment[GetPaletteIndex(cube.R1, cube.G0, position, cube.A1)]
                           + moment[GetPaletteIndex(cube.R1, cube.G0, position, cube.A0)]
                           - moment[GetPaletteIndex(cube.R0, cube.G1, position, cube.A1)]
                           + moment[GetPaletteIndex(cube.R0, cube.G1, position, cube.A0)]
                           + moment[GetPaletteIndex(cube.R0, cube.G0, position, cube.A1)]
                           - moment[GetPaletteIndex(cube.R0, cube.G0, position, cube.A0)];

                // Alpha
                case 3:
                    return moment[GetPaletteIndex(cube.R1, cube.G1, cube.B1, position)]
                           - moment[GetPaletteIndex(cube.R1, cube.G1, cube.B0, position)]
                           - moment[GetPaletteIndex(cube.R1, cube.G0, cube.B1, position)]
                           + moment[GetPaletteIndex(cube.R1, cube.G0, cube.B0, position)]
                           - moment[GetPaletteIndex(cube.R0, cube.G1, cube.B1, position)]
                           + moment[GetPaletteIndex(cube.R0, cube.G1, cube.B0, position)]
                           + moment[GetPaletteIndex(cube.R0, cube.G0, cube.B1, position)]
                           - moment[GetPaletteIndex(cube.R0, cube.G0, cube.B0, position)];

                default:
                    throw new ArgumentOutOfRangeException(nameof(direction));
            }
        }

        /// <summary>
        /// Converts the histogram into moments so that we can rapidly calculate the sums of the above quantities over any desired box.
        /// </summary>
        private void Get3DMoments()
        {
            long[] volume = ArrayPool<long>.Shared.Rent(IndexCount * IndexAlphaCount);
            long[] volumeR = ArrayPool<long>.Shared.Rent(IndexCount * IndexAlphaCount);
            long[] volumeG = ArrayPool<long>.Shared.Rent(IndexCount * IndexAlphaCount);
            long[] volumeB = ArrayPool<long>.Shared.Rent(IndexCount * IndexAlphaCount);
            long[] volumeA = ArrayPool<long>.Shared.Rent(IndexCount * IndexAlphaCount);
            float[] volume2 = ArrayPool<float>.Shared.Rent(IndexCount * IndexAlphaCount);

            long[] area = ArrayPool<long>.Shared.Rent(IndexAlphaCount);
            long[] areaR = ArrayPool<long>.Shared.Rent(IndexAlphaCount);
            long[] areaG = ArrayPool<long>.Shared.Rent(IndexAlphaCount);
            long[] areaB = ArrayPool<long>.Shared.Rent(IndexAlphaCount);
            long[] areaA = ArrayPool<long>.Shared.Rent(IndexAlphaCount);
            float[] area2 = ArrayPool<float>.Shared.Rent(IndexAlphaCount);

            try
            {
                for (int r = 1; r < IndexCount; r++)
                {
                    Array.Clear(volume, 0, IndexCount * IndexAlphaCount);
                    Array.Clear(volumeR, 0, IndexCount * IndexAlphaCount);
                    Array.Clear(volumeG, 0, IndexCount * IndexAlphaCount);
                    Array.Clear(volumeB, 0, IndexCount * IndexAlphaCount);
                    Array.Clear(volumeA, 0, IndexCount * IndexAlphaCount);
                    Array.Clear(volume2, 0, IndexCount * IndexAlphaCount);

                    for (int g = 1; g < IndexCount; g++)
                    {
                        Array.Clear(area, 0, IndexAlphaCount);
                        Array.Clear(areaR, 0, IndexAlphaCount);
                        Array.Clear(areaG, 0, IndexAlphaCount);
                        Array.Clear(areaB, 0, IndexAlphaCount);
                        Array.Clear(areaA, 0, IndexAlphaCount);
                        Array.Clear(area2, 0, IndexAlphaCount);

                        for (int b = 1; b < IndexCount; b++)
                        {
                            long line = 0;
                            long lineR = 0;
                            long lineG = 0;
                            long lineB = 0;
                            long lineA = 0;
                            float line2 = 0;

                            for (int a = 1; a < IndexAlphaCount; a++)
                            {
                                int ind1 = GetPaletteIndex(r, g, b, a);

                                line += this.vwt[ind1];
                                lineR += this.vmr[ind1];
                                lineG += this.vmg[ind1];
                                lineB += this.vmb[ind1];
                                lineA += this.vma[ind1];
                                line2 += this.m2[ind1];

                                area[a] += line;
                                areaR[a] += lineR;
                                areaG[a] += lineG;
                                areaB[a] += lineB;
                                areaA[a] += lineA;
                                area2[a] += line2;

                                int inv = (b * IndexAlphaCount) + a;

                                volume[inv] += area[a];
                                volumeR[inv] += areaR[a];
                                volumeG[inv] += areaG[a];
                                volumeB[inv] += areaB[a];
                                volumeA[inv] += areaA[a];
                                volume2[inv] += area2[a];

                                int ind2 = ind1 - GetPaletteIndex(1, 0, 0, 0);

                                this.vwt[ind1] = this.vwt[ind2] + volume[inv];
                                this.vmr[ind1] = this.vmr[ind2] + volumeR[inv];
                                this.vmg[ind1] = this.vmg[ind2] + volumeG[inv];
                                this.vmb[ind1] = this.vmb[ind2] + volumeB[inv];
                                this.vma[ind1] = this.vma[ind2] + volumeA[inv];
                                this.m2[ind1] = this.m2[ind2] + volume2[inv];
                            }
                        }
                    }
                }
            }
            finally
            {
                ArrayPool<long>.Shared.Return(volume);
                ArrayPool<long>.Shared.Return(volumeR);
                ArrayPool<long>.Shared.Return(volumeG);
                ArrayPool<long>.Shared.Return(volumeB);
                ArrayPool<long>.Shared.Return(volumeA);
                ArrayPool<float>.Shared.Return(volume2);

                ArrayPool<long>.Shared.Return(area);
                ArrayPool<long>.Shared.Return(areaR);
                ArrayPool<long>.Shared.Return(areaG);
                ArrayPool<long>.Shared.Return(areaB);
                ArrayPool<long>.Shared.Return(areaA);
                ArrayPool<float>.Shared.Return(area2);
            }
        }

        /// <summary>
        /// Computes the weighted variance of a box cube.
        /// </summary>
        /// <param name="cube">The cube.</param>
        /// <returns>The <see cref="float"/>.</returns>
        private float Variance(Box cube)
        {
            float dr = Volume(cube, this.vmr);
            float dg = Volume(cube, this.vmg);
            float db = Volume(cube, this.vmb);
            float da = Volume(cube, this.vma);

            float xx =
                this.m2[GetPaletteIndex(cube.R1, cube.G1, cube.B1, cube.A1)]
                - this.m2[GetPaletteIndex(cube.R1, cube.G1, cube.B1, cube.A0)]
                - this.m2[GetPaletteIndex(cube.R1, cube.G1, cube.B0, cube.A1)]
                + this.m2[GetPaletteIndex(cube.R1, cube.G1, cube.B0, cube.A0)]
                - this.m2[GetPaletteIndex(cube.R1, cube.G0, cube.B1, cube.A1)]
                + this.m2[GetPaletteIndex(cube.R1, cube.G0, cube.B1, cube.A0)]
                + this.m2[GetPaletteIndex(cube.R1, cube.G0, cube.B0, cube.A1)]
                - this.m2[GetPaletteIndex(cube.R1, cube.G0, cube.B0, cube.A0)]
                - this.m2[GetPaletteIndex(cube.R0, cube.G1, cube.B1, cube.A1)]
                + this.m2[GetPaletteIndex(cube.R0, cube.G1, cube.B1, cube.A0)]
                + this.m2[GetPaletteIndex(cube.R0, cube.G1, cube.B0, cube.A1)]
                - this.m2[GetPaletteIndex(cube.R0, cube.G1, cube.B0, cube.A0)]
                + this.m2[GetPaletteIndex(cube.R0, cube.G0, cube.B1, cube.A1)]
                - this.m2[GetPaletteIndex(cube.R0, cube.G0, cube.B1, cube.A0)]
                - this.m2[GetPaletteIndex(cube.R0, cube.G0, cube.B0, cube.A1)]
                + this.m2[GetPaletteIndex(cube.R0, cube.G0, cube.B0, cube.A0)];

            return xx - (((dr * dr) + (dg * dg) + (db * db) + (da * da)) / Volume(cube, this.vwt));
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
        private float Maximize(Box cube, int direction, int first, int last, out int cut, float wholeR, float wholeG, float wholeB, float wholeA, float wholeW)
        {
            long baseR = Bottom(cube, direction, this.vmr);
            long baseG = Bottom(cube, direction, this.vmg);
            long baseB = Bottom(cube, direction, this.vmb);
            long baseA = Bottom(cube, direction, this.vma);
            long baseW = Bottom(cube, direction, this.vwt);

            float max = 0F;
            cut = -1;

            for (int i = first; i < last; i++)
            {
                float halfR = baseR + Top(cube, direction, i, this.vmr);
                float halfG = baseG + Top(cube, direction, i, this.vmg);
                float halfB = baseB + Top(cube, direction, i, this.vmb);
                float halfA = baseA + Top(cube, direction, i, this.vma);
                float halfW = baseW + Top(cube, direction, i, this.vwt);

                float temp;

                if (MathF.Abs(halfW) < Constants.Epsilon)
                {
                    continue;
                }

                temp = ((halfR * halfR) + (halfG * halfG) + (halfB * halfB) + (halfA * halfA)) / halfW;

                halfR = wholeR - halfR;
                halfG = wholeG - halfG;
                halfB = wholeB - halfB;
                halfA = wholeA - halfA;
                halfW = wholeW - halfW;

                if (MathF.Abs(halfW) < Constants.Epsilon)
                {
                    continue;
                }

                temp += ((halfR * halfR) + (halfG * halfG) + (halfB * halfB) + (halfA * halfA)) / halfW;

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
        private bool Cut(Box set1, Box set2)
        {
            float wholeR = Volume(set1, this.vmr);
            float wholeG = Volume(set1, this.vmg);
            float wholeB = Volume(set1, this.vmb);
            float wholeA = Volume(set1, this.vma);
            float wholeW = Volume(set1, this.vwt);

            float maxr = this.Maximize(set1, 0, set1.R0 + 1, set1.R1, out int cutr, wholeR, wholeG, wholeB, wholeA, wholeW);
            float maxg = this.Maximize(set1, 1, set1.G0 + 1, set1.G1, out int cutg, wholeR, wholeG, wholeB, wholeA, wholeW);
            float maxb = this.Maximize(set1, 2, set1.B0 + 1, set1.B1, out int cutb, wholeR, wholeG, wholeB, wholeA, wholeW);
            float maxa = this.Maximize(set1, 3, set1.A0 + 1, set1.A1, out int cuta, wholeR, wholeG, wholeB, wholeA, wholeW);

            int dir;

            if ((maxr >= maxg) && (maxr >= maxb) && (maxr >= maxa))
            {
                dir = 0;

                if (cutr < 0)
                {
                    return false;
                }
            }
            else if ((maxg >= maxr) && (maxg >= maxb) && (maxg >= maxa))
            {
                dir = 1;
            }
            else if ((maxb >= maxr) && (maxb >= maxg) && (maxb >= maxa))
            {
                dir = 2;
            }
            else
            {
                dir = 3;
            }

            set2.R1 = set1.R1;
            set2.G1 = set1.G1;
            set2.B1 = set1.B1;
            set2.A1 = set1.A1;

            switch (dir)
            {
                // Red
                case 0:
                    set2.R0 = set1.R1 = cutr;
                    set2.G0 = set1.G0;
                    set2.B0 = set1.B0;
                    set2.A0 = set1.A0;
                    break;

                // Green
                case 1:
                    set2.G0 = set1.G1 = cutg;
                    set2.R0 = set1.R0;
                    set2.B0 = set1.B0;
                    set2.A0 = set1.A0;
                    break;

                // Blue
                case 2:
                    set2.B0 = set1.B1 = cutb;
                    set2.R0 = set1.R0;
                    set2.G0 = set1.G0;
                    set2.A0 = set1.A0;
                    break;

                // Alpha
                case 3:
                    set2.A0 = set1.A1 = cuta;
                    set2.R0 = set1.R0;
                    set2.G0 = set1.G0;
                    set2.B0 = set1.B0;
                    break;
            }

            set1.Volume = (set1.R1 - set1.R0) * (set1.G1 - set1.G0) * (set1.B1 - set1.B0) * (set1.A1 - set1.A0);
            set2.Volume = (set2.R1 - set2.R0) * (set2.G1 - set2.G0) * (set2.B1 - set2.B0) * (set2.A1 - set2.A0);

            return true;
        }

        /// <summary>
        /// Marks a color space tag.
        /// </summary>
        /// <param name="cube">The cube.</param>
        /// <param name="label">A label.</param>
        private void Mark(Box cube, byte label)
        {
            for (int r = cube.R0 + 1; r <= cube.R1; r++)
            {
                for (int g = cube.G0 + 1; g <= cube.G1; g++)
                {
                    for (int b = cube.B0 + 1; b <= cube.B1; b++)
                    {
                        for (int a = cube.A0 + 1; a <= cube.A1; a++)
                        {
                            this.tag[GetPaletteIndex(r, g, b, a)] = label;
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
            float[] vv = new float[this.colors];

            for (int i = 0; i < this.colors; i++)
            {
                this.colorCube[i] = new Box();
            }

            this.colorCube[0].R0 = this.colorCube[0].G0 = this.colorCube[0].B0 = this.colorCube[0].A0 = 0;
            this.colorCube[0].R1 = this.colorCube[0].G1 = this.colorCube[0].B1 = IndexCount - 1;
            this.colorCube[0].A1 = IndexAlphaCount - 1;

            int next = 0;

            for (int i = 1; i < this.colors; i++)
            {
                if (this.Cut(this.colorCube[next], this.colorCube[i]))
                {
                    vv[next] = this.colorCube[next].Volume > 1 ? this.Variance(this.colorCube[next]) : 0F;
                    vv[i] = this.colorCube[i].Volume > 1 ? this.Variance(this.colorCube[i]) : 0F;
                }
                else
                {
                    vv[next] = 0F;
                    i--;
                }

                next = 0;

                float temp = vv[0];
                for (int k = 1; k <= i; k++)
                {
                    if (vv[k] > temp)
                    {
                        temp = vv[k];
                        next = k;
                    }
                }

                if (temp <= 0.0)
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
        private byte QuantizePixel(TPixel pixel)
        {
            if (this.Dither)
            {
                // The colors have changed so we need to use Euclidean distance caclulation to find the closest value.
                // This palette can never be null here.
                return this.GetClosestPixel(pixel, this.palette, this.colorMap);
            }

            // Expected order r->g->b->a
            pixel.ToXyzwBytes(this.rgbaBuffer, 0);

            int r = this.rgbaBuffer[0] >> (8 - IndexBits);
            int g = this.rgbaBuffer[1] >> (8 - IndexBits);
            int b = this.rgbaBuffer[2] >> (8 - IndexBits);
            int a = this.rgbaBuffer[3] >> (8 - IndexAlphaBits);

            return this.tag[GetPaletteIndex(r + 1, g + 1, b + 1, a + 1)];
        }
    }
}