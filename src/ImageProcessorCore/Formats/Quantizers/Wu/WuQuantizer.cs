// <copyright file="WuAlphaColorQuantizer.cs" company="Jérémy Ansel">
// Copyright (c) 2014-2015 Jérémy Ansel
// </copyright>
// <license>
// Licensed under the MIT license. See LICENSE.txt
// </license>

namespace ImageProcessorCore.Formats
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Threading.Tasks;

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
    /// Algorithm: Greedy orthogonal bipartition of RGB space for variance
    /// minimization aided by inclusion-exclusion tricks.
    /// For speed no nearest neighbor search is done. Slightly
    /// better performance can be expected by more sophisticated
    /// but more expensive versions.
    /// </para>
    /// </remarks>
    [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "Wu", Justification = "Reviewed")]
    public sealed class WuQuantizer : IQuantizer
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
        /// Maximum allowed color depth
        /// </summary>
        private readonly int maxColors;

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
        private double[] m2;

        /// <summary>
        /// Color space tag.
        /// </summary>
        private byte[] tag;

        /// <summary>
        /// Initializes a new instance of the <see cref="WuQuantizer"/> class.
        /// </summary>
        public WuQuantizer()
            : this(256)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="WuQuantizer"/> class.
        /// </summary>
        /// <param name="maxColors">The maximum number of colors to return</param>
        public WuQuantizer(int maxColors)
        {
            Guard.MustBeBetweenOrEqualTo(maxColors, 1, 256, nameof(maxColors));

            this.maxColors = maxColors;
            this.vwt = new long[TableLength];
            this.vmr = new long[TableLength];
            this.vmg = new long[TableLength];
            this.vmb = new long[TableLength];
            this.vma = new long[TableLength];
            this.m2 = new double[TableLength];
            this.tag = new byte[TableLength];
        }

        /// <inheritdoc/>
        public QuantizedImage Quantize(ImageBase image)
        {
            Guard.NotNull(image, nameof(image));

            int colorCount = this.maxColors;

            this.Clear();

            this.Build3DHistogram(image);
            this.M3d();

            Box[] cube;
            this.BuildCube(out cube, ref colorCount);

            return this.GenerateResult(image, colorCount, cube);
        }

        /// <summary>
        /// Gets an index.
        /// </summary>
        /// <param name="r">The red value.</param>
        /// <param name="g">The green value.</param>
        /// <param name="b">The blue value.</param>
        /// <param name="a">The alpha value.</param>
        /// <returns>The index.</returns>
        private static int Ind(int r, int g, int b, int a)
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
        private static double Volume(Box cube, long[] moment)
        {
            return moment[Ind(cube.R1, cube.G1, cube.B1, cube.A1)]
                - moment[Ind(cube.R1, cube.G1, cube.B1, cube.A0)]
                - moment[Ind(cube.R1, cube.G1, cube.B0, cube.A1)]
                + moment[Ind(cube.R1, cube.G1, cube.B0, cube.A0)]
                - moment[Ind(cube.R1, cube.G0, cube.B1, cube.A1)]
                + moment[Ind(cube.R1, cube.G0, cube.B1, cube.A0)]
                + moment[Ind(cube.R1, cube.G0, cube.B0, cube.A1)]
                - moment[Ind(cube.R1, cube.G0, cube.B0, cube.A0)]
                - moment[Ind(cube.R0, cube.G1, cube.B1, cube.A1)]
                + moment[Ind(cube.R0, cube.G1, cube.B1, cube.A0)]
                + moment[Ind(cube.R0, cube.G1, cube.B0, cube.A1)]
                - moment[Ind(cube.R0, cube.G1, cube.B0, cube.A0)]
                + moment[Ind(cube.R0, cube.G0, cube.B1, cube.A1)]
                - moment[Ind(cube.R0, cube.G0, cube.B1, cube.A0)]
                - moment[Ind(cube.R0, cube.G0, cube.B0, cube.A1)]
                + moment[Ind(cube.R0, cube.G0, cube.B0, cube.A0)];
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
                    return -moment[Ind(cube.R0, cube.G1, cube.B1, cube.A1)]
                        + moment[Ind(cube.R0, cube.G1, cube.B1, cube.A0)]
                        + moment[Ind(cube.R0, cube.G1, cube.B0, cube.A1)]
                        - moment[Ind(cube.R0, cube.G1, cube.B0, cube.A0)]
                        + moment[Ind(cube.R0, cube.G0, cube.B1, cube.A1)]
                        - moment[Ind(cube.R0, cube.G0, cube.B1, cube.A0)]
                        - moment[Ind(cube.R0, cube.G0, cube.B0, cube.A1)]
                        + moment[Ind(cube.R0, cube.G0, cube.B0, cube.A0)];

                // Green
                case 1:
                    return -moment[Ind(cube.R1, cube.G0, cube.B1, cube.A1)]
                        + moment[Ind(cube.R1, cube.G0, cube.B1, cube.A0)]
                        + moment[Ind(cube.R1, cube.G0, cube.B0, cube.A1)]
                        - moment[Ind(cube.R1, cube.G0, cube.B0, cube.A0)]
                        + moment[Ind(cube.R0, cube.G0, cube.B1, cube.A1)]
                        - moment[Ind(cube.R0, cube.G0, cube.B1, cube.A0)]
                        - moment[Ind(cube.R0, cube.G0, cube.B0, cube.A1)]
                        + moment[Ind(cube.R0, cube.G0, cube.B0, cube.A0)];

                // Blue
                case 2:
                    return -moment[Ind(cube.R1, cube.G1, cube.B0, cube.A1)]
                        + moment[Ind(cube.R1, cube.G1, cube.B0, cube.A0)]
                        + moment[Ind(cube.R1, cube.G0, cube.B0, cube.A1)]
                        - moment[Ind(cube.R1, cube.G0, cube.B0, cube.A0)]
                        + moment[Ind(cube.R0, cube.G1, cube.B0, cube.A1)]
                        - moment[Ind(cube.R0, cube.G1, cube.B0, cube.A0)]
                        - moment[Ind(cube.R0, cube.G0, cube.B0, cube.A1)]
                        + moment[Ind(cube.R0, cube.G0, cube.B0, cube.A0)];

                // Alpha
                case 3:
                    return -moment[Ind(cube.R1, cube.G1, cube.B1, cube.A0)]
                        + moment[Ind(cube.R1, cube.G1, cube.B0, cube.A0)]
                        + moment[Ind(cube.R1, cube.G0, cube.B1, cube.A0)]
                        - moment[Ind(cube.R1, cube.G0, cube.B0, cube.A0)]
                        + moment[Ind(cube.R0, cube.G1, cube.B1, cube.A0)]
                        - moment[Ind(cube.R0, cube.G1, cube.B0, cube.A0)]
                        - moment[Ind(cube.R0, cube.G0, cube.B1, cube.A0)]
                        + moment[Ind(cube.R0, cube.G0, cube.B0, cube.A0)];

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
                    return moment[Ind(position, cube.G1, cube.B1, cube.A1)]
                        - moment[Ind(position, cube.G1, cube.B1, cube.A0)]
                        - moment[Ind(position, cube.G1, cube.B0, cube.A1)]
                        + moment[Ind(position, cube.G1, cube.B0, cube.A0)]
                        - moment[Ind(position, cube.G0, cube.B1, cube.A1)]
                        + moment[Ind(position, cube.G0, cube.B1, cube.A0)]
                        + moment[Ind(position, cube.G0, cube.B0, cube.A1)]
                        - moment[Ind(position, cube.G0, cube.B0, cube.A0)];

                // Green
                case 1:
                    return moment[Ind(cube.R1, position, cube.B1, cube.A1)]
                        - moment[Ind(cube.R1, position, cube.B1, cube.A0)]
                        - moment[Ind(cube.R1, position, cube.B0, cube.A1)]
                        + moment[Ind(cube.R1, position, cube.B0, cube.A0)]
                        - moment[Ind(cube.R0, position, cube.B1, cube.A1)]
                        + moment[Ind(cube.R0, position, cube.B1, cube.A0)]
                        + moment[Ind(cube.R0, position, cube.B0, cube.A1)]
                        - moment[Ind(cube.R0, position, cube.B0, cube.A0)];

                // Blue
                case 2:
                    return moment[Ind(cube.R1, cube.G1, position, cube.A1)]
                        - moment[Ind(cube.R1, cube.G1, position, cube.A0)]
                        - moment[Ind(cube.R1, cube.G0, position, cube.A1)]
                        + moment[Ind(cube.R1, cube.G0, position, cube.A0)]
                        - moment[Ind(cube.R0, cube.G1, position, cube.A1)]
                        + moment[Ind(cube.R0, cube.G1, position, cube.A0)]
                        + moment[Ind(cube.R0, cube.G0, position, cube.A1)]
                        - moment[Ind(cube.R0, cube.G0, position, cube.A0)];

                // Alpha
                case 3:
                    return moment[Ind(cube.R1, cube.G1, cube.B1, position)]
                        - moment[Ind(cube.R1, cube.G1, cube.B0, position)]
                        - moment[Ind(cube.R1, cube.G0, cube.B1, position)]
                        + moment[Ind(cube.R1, cube.G0, cube.B0, position)]
                        - moment[Ind(cube.R0, cube.G1, cube.B1, position)]
                        + moment[Ind(cube.R0, cube.G1, cube.B0, position)]
                        + moment[Ind(cube.R0, cube.G0, cube.B1, position)]
                        - moment[Ind(cube.R0, cube.G0, cube.B0, position)];

                default:
                    throw new ArgumentOutOfRangeException(nameof(direction));
            }
        }

        /// <summary>
        /// Clears the tables.
        /// </summary>
        private void Clear()
        {
            Array.Clear(this.vwt, 0, TableLength);
            Array.Clear(this.vmr, 0, TableLength);
            Array.Clear(this.vmg, 0, TableLength);
            Array.Clear(this.vmb, 0, TableLength);
            Array.Clear(this.vma, 0, TableLength);
            Array.Clear(this.m2, 0, TableLength);

            Array.Clear(this.tag, 0, TableLength);
        }

        /// <summary>
        /// Builds a 3-D color histogram of <c>counts, r/g/b, c^2</c>.
        /// </summary>
        /// <param name="image">The image.</param>
        private void Build3DHistogram(ImageBase image)
        {


            // TODO: Parallel
            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    Bgra32 color = image[x, y];

                    byte r = color.R;
                    byte g = color.G;
                    byte b = color.B;
                    byte a = color.A;

                    int inr = r >> (8 - IndexBits);
                    int ing = g >> (8 - IndexBits);
                    int inb = b >> (8 - IndexBits);
                    int ina = a >> (8 - IndexAlphaBits);

                    int ind = Ind(inr + 1, ing + 1, inb + 1, ina + 1);

                    this.vwt[ind]++;
                    this.vmr[ind] += r;
                    this.vmg[ind] += g;
                    this.vmb[ind] += b;
                    this.vma[ind] += a;
                    this.m2[ind] += (r * r) + (g * g) + (b * b) + (a * a);
                }
            }
        }

        /// <summary>
        /// Converts the histogram into moments so that we can rapidly calculate
        /// the sums of the above quantities over any desired box.
        /// </summary>
        private void M3d()
        {
            long[] volume = new long[IndexCount * IndexAlphaCount];
            long[] volume_r = new long[IndexCount * IndexAlphaCount];
            long[] volume_g = new long[IndexCount * IndexAlphaCount];
            long[] volume_b = new long[IndexCount * IndexAlphaCount];
            long[] volume_a = new long[IndexCount * IndexAlphaCount];
            double[] volume2 = new double[IndexCount * IndexAlphaCount];

            long[] area = new long[IndexAlphaCount];
            long[] area_r = new long[IndexAlphaCount];
            long[] area_g = new long[IndexAlphaCount];
            long[] area_b = new long[IndexAlphaCount];
            long[] area_a = new long[IndexAlphaCount];
            double[] area2 = new double[IndexAlphaCount];

            for (int r = 1; r < IndexCount; r++)
            {
                Array.Clear(volume, 0, IndexCount * IndexAlphaCount);
                Array.Clear(volume_r, 0, IndexCount * IndexAlphaCount);
                Array.Clear(volume_g, 0, IndexCount * IndexAlphaCount);
                Array.Clear(volume_b, 0, IndexCount * IndexAlphaCount);
                Array.Clear(volume_a, 0, IndexCount * IndexAlphaCount);
                Array.Clear(volume2, 0, IndexCount * IndexAlphaCount);

                for (int g = 1; g < IndexCount; g++)
                {
                    Array.Clear(area, 0, IndexAlphaCount);
                    Array.Clear(area_r, 0, IndexAlphaCount);
                    Array.Clear(area_g, 0, IndexAlphaCount);
                    Array.Clear(area_b, 0, IndexAlphaCount);
                    Array.Clear(area_a, 0, IndexAlphaCount);
                    Array.Clear(area2, 0, IndexAlphaCount);

                    for (int b = 1; b < IndexCount; b++)
                    {
                        long line = 0;
                        long line_r = 0;
                        long line_g = 0;
                        long line_b = 0;
                        long line_a = 0;
                        double line2 = 0;

                        for (int a = 1; a < IndexAlphaCount; a++)
                        {
                            int ind1 = Ind(r, g, b, a);

                            line += this.vwt[ind1];
                            line_r += this.vmr[ind1];
                            line_g += this.vmg[ind1];
                            line_b += this.vmb[ind1];
                            line_a += this.vma[ind1];
                            line2 += this.m2[ind1];

                            area[a] += line;
                            area_r[a] += line_r;
                            area_g[a] += line_g;
                            area_b[a] += line_b;
                            area_a[a] += line_a;
                            area2[a] += line2;

                            int inv = (b * IndexAlphaCount) + a;

                            volume[inv] += area[a];
                            volume_r[inv] += area_r[a];
                            volume_g[inv] += area_g[a];
                            volume_b[inv] += area_b[a];
                            volume_a[inv] += area_a[a];
                            volume2[inv] += area2[a];

                            int ind2 = ind1 - Ind(1, 0, 0, 0);

                            this.vwt[ind1] = this.vwt[ind2] + volume[inv];
                            this.vmr[ind1] = this.vmr[ind2] + volume_r[inv];
                            this.vmg[ind1] = this.vmg[ind2] + volume_g[inv];
                            this.vmb[ind1] = this.vmb[ind2] + volume_b[inv];
                            this.vma[ind1] = this.vma[ind2] + volume_a[inv];
                            this.m2[ind1] = this.m2[ind2] + volume2[inv];
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Computes the weighted variance of a box.
        /// </summary>
        /// <param name="cube">The cube.</param>
        /// <returns>The result.</returns>
        private double Var(Box cube)
        {
            double dr = Volume(cube, this.vmr);
            double dg = Volume(cube, this.vmg);
            double db = Volume(cube, this.vmb);
            double da = Volume(cube, this.vma);

            double xx =
                this.m2[Ind(cube.R1, cube.G1, cube.B1, cube.A1)]
                - this.m2[Ind(cube.R1, cube.G1, cube.B1, cube.A0)]
                - this.m2[Ind(cube.R1, cube.G1, cube.B0, cube.A1)]
                + this.m2[Ind(cube.R1, cube.G1, cube.B0, cube.A0)]
                - this.m2[Ind(cube.R1, cube.G0, cube.B1, cube.A1)]
                + this.m2[Ind(cube.R1, cube.G0, cube.B1, cube.A0)]
                + this.m2[Ind(cube.R1, cube.G0, cube.B0, cube.A1)]
                - this.m2[Ind(cube.R1, cube.G0, cube.B0, cube.A0)]
                - this.m2[Ind(cube.R0, cube.G1, cube.B1, cube.A1)]
                + this.m2[Ind(cube.R0, cube.G1, cube.B1, cube.A0)]
                + this.m2[Ind(cube.R0, cube.G1, cube.B0, cube.A1)]
                - this.m2[Ind(cube.R0, cube.G1, cube.B0, cube.A0)]
                + this.m2[Ind(cube.R0, cube.G0, cube.B1, cube.A1)]
                - this.m2[Ind(cube.R0, cube.G0, cube.B1, cube.A0)]
                - this.m2[Ind(cube.R0, cube.G0, cube.B0, cube.A1)]
                + this.m2[Ind(cube.R0, cube.G0, cube.B0, cube.A0)];

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
        /// <param name="whole_r">The whole red.</param>
        /// <param name="whole_g">The whole green.</param>
        /// <param name="whole_b">The whole blue.</param>
        /// <param name="whole_a">The whole alpha.</param>
        /// <param name="whole_w">The whole weight.</param>
        /// <returns>The result.</returns>
        private double Maximize(Box cube, int direction, int first, int last, out int cut, double whole_r, double whole_g, double whole_b, double whole_a, double whole_w)
        {
            long base_r = Bottom(cube, direction, this.vmr);
            long base_g = Bottom(cube, direction, this.vmg);
            long base_b = Bottom(cube, direction, this.vmb);
            long base_a = Bottom(cube, direction, this.vma);
            long base_w = Bottom(cube, direction, this.vwt);

            double max = 0.0;
            cut = -1;

            for (int i = first; i < last; i++)
            {
                double half_r = base_r + Top(cube, direction, i, this.vmr);
                double half_g = base_g + Top(cube, direction, i, this.vmg);
                double half_b = base_b + Top(cube, direction, i, this.vmb);
                double half_a = base_a + Top(cube, direction, i, this.vma);
                double half_w = base_w + Top(cube, direction, i, this.vwt);

                double temp;

                // TODO: Epsilon
                if (Math.Abs(half_w) < 0.001)
                {
                    continue;
                }
                else
                {
                    temp = ((half_r * half_r) + (half_g * half_g) + (half_b * half_b) + (half_a * half_a)) / half_w;
                }

                half_r = whole_r - half_r;
                half_g = whole_g - half_g;
                half_b = whole_b - half_b;
                half_a = whole_a - half_a;
                half_w = whole_w - half_w;

                // TODO: Epsilon
                if (Math.Abs(half_w) < 0.001)
                {
                    continue;
                }
                else
                {
                    temp += ((half_r * half_r) + (half_g * half_g) + (half_b * half_b) + (half_a * half_a)) / half_w;
                }

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
            double whole_r = Volume(set1, this.vmr);
            double whole_g = Volume(set1, this.vmg);
            double whole_b = Volume(set1, this.vmb);
            double whole_a = Volume(set1, this.vma);
            double whole_w = Volume(set1, this.vwt);

            int cutr;
            int cutg;
            int cutb;
            int cuta;

            double maxr = this.Maximize(set1, 0, set1.R0 + 1, set1.R1, out cutr, whole_r, whole_g, whole_b, whole_a, whole_w);
            double maxg = this.Maximize(set1, 1, set1.G0 + 1, set1.G1, out cutg, whole_r, whole_g, whole_b, whole_a, whole_w);
            double maxb = this.Maximize(set1, 2, set1.B0 + 1, set1.B1, out cutb, whole_r, whole_g, whole_b, whole_a, whole_w);
            double maxa = this.Maximize(set1, 3, set1.A0 + 1, set1.A1, out cuta, whole_r, whole_g, whole_b, whole_a, whole_w);

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
                            this.tag[Ind(r, g, b, a)] = label;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Builds the cube.
        /// </summary>
        /// <param name="cube">The cube.</param>
        /// <param name="colorCount">The color count.</param>
        private void BuildCube(out Box[] cube, ref int colorCount)
        {
            cube = new Box[colorCount];
            double[] vv = new double[colorCount];

            for (int i = 0; i < colorCount; i++)
            {
                cube[i] = new Box();
            }

            cube[0].R0 = cube[0].G0 = cube[0].B0 = cube[0].A0 = 0;
            cube[0].R1 = cube[0].G1 = cube[0].B1 = IndexCount - 1;
            cube[0].A1 = IndexAlphaCount - 1;

            int next = 0;

            for (int i = 1; i < colorCount; i++)
            {
                if (this.Cut(cube[next], cube[i]))
                {
                    vv[next] = cube[next].Volume > 1 ? this.Var(cube[next]) : 0.0;
                    vv[i] = cube[i].Volume > 1 ? this.Var(cube[i]) : 0.0;
                }
                else
                {
                    vv[next] = 0.0;
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

                if (temp <= 0.0)
                {
                    colorCount = i + 1;
                    break;
                }
            }
        }

        /// <summary>
        /// Generates the quantized result.
        /// </summary>
        /// <param name="image">The image.</param>
        /// <param name="colorCount">The color count.</param>
        /// <param name="cube">The cube.</param>
        /// <returns>The result.</returns>
        private QuantizedImage GenerateResult(ImageBase image, int colorCount, Box[] cube)
        {
            List<Bgra32> pallette = new List<Bgra32>();
            byte[] pixels = new byte[image.Width * image.Height];

            // Can't make this parallel.
            for (int k = 0; k < colorCount; k++)
            {
                this.Mark(cube[k], (byte)k);

                double weight = Volume(cube[k], this.vwt);

                // TODO: Epsilon
                if (Math.Abs(weight) > .0001)
                {
                    byte r = (byte)(Volume(cube[k], this.vmr) / weight);
                    byte g = (byte)(Volume(cube[k], this.vmg) / weight);
                    byte b = (byte)(Volume(cube[k], this.vmb) / weight);
                    byte a = (byte)(Volume(cube[k], this.vma) / weight);

                    pallette.Add(new Bgra32(b, g, r, a));
                }
                else
                {
                    pallette.Add(new Bgra32(0, 0, 0));
                }
            }

            // TODO: Optimize here.
            int i = 0;
            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    Bgra32 color = image[x, y];
                    int a = color.A >> (8 - IndexAlphaBits);
                    int r = color.R >> (8 - IndexBits);
                    int g = color.G >> (8 - IndexBits);
                    int b = color.B >> (8 - IndexBits);

                    int ind = Ind(r + 1, g + 1, b + 1, a + 1);
                    pixels[i++] = this.tag[ind];
                }
            }

            //for (int i = 0; i < image.Length / 4; i++)
            //{
            //    int a = image[(i * 4) + 3] >> (8 - WuAlphaColorQuantizer.IndexAlphaBits);
            //    int r = image[(i * 4) + 2] >> (8 - WuAlphaColorQuantizer.IndexBits);
            //    int g = image[(i * 4) + 1] >> (8 - WuAlphaColorQuantizer.IndexBits);
            //    int b = image[i * 4] >> (8 - WuAlphaColorQuantizer.IndexBits);

            //    int ind = WuAlphaColorQuantizer.Ind(r + 1, g + 1, b + 1, a + 1);

            //    pixels[i] = this.tag[ind];
            //}

            return new QuantizedImage(image.Width, image.Height, pallette.ToArray(), pixels);
        }
    }
}