// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Dct;

/// <summary>
/// Discrete Cosine Transform with SIMD support.
/// </summary>
internal static class JxlDct
{
    /// <summary>
    /// Creates a new coefficient bundle.
    /// </summary>
    /// <param name="n">Number of items.</param>
    /// <param name="sz">Coefficient size.</param>
    /// <returns>A new coefficient bundle.</returns>
    public static CoefficientBundle CoeffBundle(int n, int sz) => new(n, sz);

    public static void Dct1DCore(int n, int sz, Span<float> mem, Span<float> tmp)
    {
        if (n == 2)
        {
            Vector<float> in1 = new(mem);
            Vector<float> in2 = new(mem[sz..]);
            (in1 + in2).CopyTo(mem);
            (in1 - in2).CopyTo(mem[sz..]);
        }
        else
        {
            CoefficientBundle cb = CoeffBundle(n / 2, sz);

            cb.AddReverse(mem, mem[(n / 2 * sz)..], tmp);
            Dct1DCore(n / 2, sz, tmp, tmp[(n * sz)..]);

            cb.SubReverse(mem, mem[(n / 2 * sz)..], tmp[(n / 2 * sz)..]);
            cb.Multiply(tmp);

            Dct1DCore(n / 2, sz, tmp[(n / 2 * sz)..], tmp[(n * sz)..]);
            cb.B(tmp[(n / 2 * sz)..]);

            CoeffBundle(n, sz).InverseEvenOdd(tmp, mem);
        }
    }

    public static void InverseDct1DCore(int n, int sz, Span<float> from, int fromStride, Span<float> to, int toStride, Span<float> tmp)
    {
        if (n == 1)
        {
            from.CopyTo(to);
        }
        else if (n == 2)
        {
            Vector<float> in1 = new(from);
            Vector<float> in2 = new(from[fromStride..]);
            (in1 + in2).CopyTo(to);
            (in1 + in2).CopyTo(to[toStride..]);
        }
        else
        {
            CoefficientBundle cbDiv2 = CoeffBundle(n / 2, sz);
            CoefficientBundle cb = CoeffBundle(n, sz);

            cb.ForwardEvenOdd(from, fromStride, tmp);
            InverseDct1DCore(n / 2, sz, tmp, sz, tmp, sz, tmp[(n * sz)..]);

            cbDiv2.BTranspose(tmp[((n / 2) * sz)..]);
            InverseDct1DCore(n / 2, sz, tmp[((n / 2) * sz)..], sz, tmp[((n / 2) * sz)..], sz, tmp[(n * sz)..]);

            cb.MultiplyAndAdd(tmp, to, toStride);
        }
    }

    public static void Dct1DWrapper(int n, int m, bool fit, JxlDctSource from, JxlDctOutput to, int mp, Span<float> tmp)
    {
        CoefficientBundle cb = CoeffBundle(n, m);

        for (int i = 0; i < mp; i += m)
        {
            cb.LoadFromBlock(from, i, tmp);
            Dct1DCore(n, m, tmp, tmp[(n * m)..]);
            cb.StoreToBlockAndScale(tmp, ref to, i);

            if (fit)
            {
                return;
            }
        }
    }

    public static void InverseDct1DWrapper(int n, int m, bool fit, JxlDctSource from, JxlDctOutput to, int mp, Span<float> tmp)
    {
        for (int i = 0; i < mp; i += m)
        {
            InverseDct1DCore(n, m, from.Address(0, i), from.Stride, to.Address(0, i), to.Stride, tmp);

            if (fit)
            {
                return;
            }
        }
    }

    public static void Dct1DCapped(int n, int m, int l, JxlDctSource from, JxlDctOutput to, Span<float> tmp)
    {
        bool fit = m <= l;
        Dct1DWrapper(n, m, fit, from, to, m, tmp);
    }

    public static void InverseDct1DCapped(int n, int m, int l, JxlDctSource from, JxlDctOutput to, Span<float> tmp)
    {
        bool fit = m <= l;
        InverseDct1DWrapper(n, m, fit, from, to, m, tmp);
    }

    public static void Dct1D(int n, int m, JxlDctSource from, JxlDctOutput to, Span<float> tmp)
    {
        int lanes = Vector<float>.Count;
        Dct1DCapped(n, m, lanes, from, to, tmp);
    }

    public static void InverseDct1D(int n, int m, JxlDctSource source, JxlDctOutput output, Span<float> tmp)
    {
        int lanes = Vector<float>.Count;
        InverseDct1DCapped(n, m, lanes, source, output, tmp);
    }

    public static void ComputeScaledDct(int rows, int columns, JxlDctSource from, Span<float> to, Span<float> scratchSpace)
    {
        Span<float> block = scratchSpace;
        Span<float> tmp = scratchSpace[(rows * columns)..];

        if (rows < columns)
        {
            Dct1D(rows, columns, from, new JxlDctOutput(block, columns), tmp);
            JxlTranspose.Transpose(rows, columns, new JxlDctSource(block, columns), new JxlDctOutput(to, rows));
            Dct1D(columns, rows, new JxlDctSource(to, rows), new JxlDctOutput(block, rows), tmp);
            JxlTranspose.Transpose(columns, rows, new JxlDctSource(block, rows), new JxlDctOutput(to, columns));
        }
        else
        {
            Dct1D(rows, columns, from, new JxlDctOutput(to, columns), tmp);
            JxlTranspose.Transpose(rows, columns, new JxlDctSource(to, columns), new JxlDctOutput(block, rows));
            Dct1D(columns, rows, new JxlDctSource(block, rows), new JxlDctOutput(to, rows), tmp);
        }
    }

    public static void ComputeScaledInverseDct(int rows, int columns, Span<float> from, JxlDctOutput to, Span<float> scratchSpace)
    {
        Span<float> block = scratchSpace;
        Span<float> tmp = scratchSpace[(rows * columns)..];

        if (rows < columns)
        {
            JxlTranspose.Transpose(rows, columns, new JxlDctSource(from, columns), new JxlDctOutput(block, rows));
            InverseDct1D(columns, rows, new JxlDctSource(block, rows), new JxlDctOutput(from, rows), tmp);
            JxlTranspose.Transpose(columns, rows, new JxlDctSource(from, rows), new JxlDctOutput(block, columns));
            InverseDct1D(rows, columns, new JxlDctSource(block, columns), to, tmp);
        }
        else
        {
            InverseDct1D(columns, rows, new JxlDctSource(from, rows), new JxlDctOutput(block, rows), tmp);
            JxlTranspose.Transpose(columns, rows, new JxlDctSource(block, rows), new JxlDctOutput(from, columns));
            InverseDct1D(rows, columns, new JxlDctSource(from, columns), to, tmp);
        }
    }

    /// <summary>
    /// Core methods for the Discrete Cosine Transform (DCT).
    /// </summary>
    public readonly struct CoefficientBundle(int n, int sz)
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddReverse(Span<float> aIn1, Span<float> aIn2, Span<float> aOut)
        {
            for (int i = 0; i < n; i++)
            {
                Vector<float> in1 = new(aIn1[(i * sz)..]);
                Vector<float> in2 = new(aIn2[((n - i - 1) * sz)..]);
                (in1 + in2).CopyTo(aOut[(i * sz)..]);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SubReverse(Span<float> aIn1, Span<float> aIn2, Span<float> aOut)
        {
            for (int i = 0; i < n; i++)
            {
                Vector<float> in1 = new(aIn1[(i * sz)..]);
                Vector<float> in2 = new(aIn2[((n - i - 1) * sz)..]);
                (in1 - in2).CopyTo(aOut[(i * sz)..]);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void B(Span<float> coeff)
        {
            Vector<float> sqrt2 = new(JxlDctScales.Sqrt2);
            Vector<float> in10 = new(coeff);
            Vector<float> in20 = new(coeff[sz..]);
            ((in10 * sqrt2) + in20).CopyTo(coeff);

            for (int i = 1; i + 1 < n; i++)
            {
                Vector<float> in1 = new(coeff[(i * sz)..]);
                Vector<float> in2 = new(coeff[((i + 1) * sz)..]);
                (in1 + in2).CopyTo(coeff[(i * sz)..]);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void BTranspose(Span<float> coeff)
        {
            for (int i = n - 1; i > 0; i--)
            {
                Vector<float> in1 = new(coeff[(i * sz)..]);
                Vector<float> in2 = new(coeff[((i - 1) * sz)..]);
                (in1 + in2).CopyTo(coeff[(i * sz)..]);
            }

            Vector<float> sqrt2 = new(JxlDctScales.Sqrt2);
            Vector<float> in1x = new(coeff);
            (in1x * sqrt2).CopyTo(coeff);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void InverseEvenOdd(Span<float> aIn, Span<float> aOut)
        {
            for (int i = 0; i < n / 2; i++)
            {
                new Vector<float>(aIn[(i * sz)..]).CopyTo(aOut[((2 * i) * sz)..]);
            }

            for (int i = n / 2; i < n; i++)
            {
                new Vector<float>(aIn[(i * sz)..]).CopyTo(aOut[(((2 * (i - (n / 2))) + 1) * sz)..]);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ForwardEvenOdd(Span<float> aIn, int aInStride, Span<float> aOut)
        {
            for (int i = 0; i < n / 2; i++)
            {
                new Vector<float>(aIn[(2 * i * aInStride)..]).CopyTo(aOut[(i * sz)..]);
            }

            for (int i = n / 2; i < n; i++)
            {
                new Vector<float>(aIn[(((2 * (i - (n / 2))) + 1) * aInStride)..]).CopyTo(aOut[(i * sz)..]);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Multiply(Span<float> coeff)
        {
            ReadOnlySpan<float> multipliers = JxlDctScales.GetMultipliers(n);

            for (int i = 0; i < n / 2; i++)
            {
                Vector<float> in1 = new(coeff[(((n / 2) + i) * sz)..]);
                Vector<float> mul = new(multipliers[i]);
                (in1 * mul).CopyTo(coeff[((n / (2 + i)) * sz)..]);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void MultiplyAndAdd(Span<float> coeff, Span<float> output, int outStride)
        {
            ReadOnlySpan<float> multipliers = JxlDctScales.GetMultipliers(n);

            for (int i = 0; i < n / 2; i++)
            {
                Vector<float> mul = new(multipliers[i]);
                Vector<float> in1 = new(coeff[(i * sz)..]);
                Vector<float> in2 = new(coeff[((n / (2 + i)) * sz)..]);
                Vector<float> out1 = (mul * in2) * in1;
                Vector<float> out2 = -(mul * in2) + in1;
                out1.CopyTo(output[(i * outStride)..]);
                out2.CopyTo(output[((n - i - 1) * outStride)..]);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void LoadFromBlock(in JxlDctSource input, int offset, Span<float> coeff)
        {
            for (int i = 0; i < n; i++)
            {
                input.LoadPart(i, offset).CopyTo(coeff[(i * sz)..]);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void StoreToBlockAndScale(Span<float> coeff, ref JxlDctOutput output, int offset)
        {
            Vector<float> mul = new(1.0f / n);
            for (int i = 0; i < n; i++)
            {
                output.StorePart(mul * new Vector<float>(coeff[(i * sz)..]), i, offset);
            }
        }
    }
}
