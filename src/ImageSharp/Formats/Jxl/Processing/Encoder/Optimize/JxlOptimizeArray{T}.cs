// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using System.Numerics.Tensors;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.Optimize;

internal struct JxlOptimizeArray<T> : IDisposable
    where T : unmanaged, INumber<T>
{
    private readonly IMemoryOwner<T> memoryOwner;
    private readonly int length;
    private readonly Configuration configuration;
    private bool isDisposed;

    public JxlOptimizeArray(Configuration configuration, int length, T value)
    {
        this.memoryOwner = configuration.MemoryAllocator.Allocate<T>(length);
        this.memoryOwner.Memory.Span.Fill(value);
        this.length = length;
        this.configuration = configuration;
    }

    public readonly Span<T> Span => this.memoryOwner.Memory.Span;

    public readonly ref T this[int index]
    {
        get
        {
            DebugGuard.MustBeLessThan(index, this.length, nameof(index));
            return ref this.memoryOwner.Memory.Span[index];
        }
    }

    public static JxlOptimizeArray<T> operator +(JxlOptimizeArray<T> a, JxlOptimizeArray<T> b)
    {
        JxlOptimizeArray<T> z = new(a.configuration, a.length, default);
        TensorPrimitives.Add(a.Span, b.Span, z.Span);
        return z;
    }

    public static JxlOptimizeArray<T> operator -(JxlOptimizeArray<T> a, JxlOptimizeArray<T> b)
    {
        JxlOptimizeArray<T> z = new(a.configuration, a.length, default);
        TensorPrimitives.Subtract(a.Span, b.Span, z.Span);
        return z;
    }

    public static JxlOptimizeArray<T> operator *(T a, JxlOptimizeArray<T> b)
    {
        JxlOptimizeArray<T> z = new(b.configuration, b.length, default);
        TensorPrimitives.Multiply(b.Span, a, z.Span);
        return z;
    }

    public readonly T DotProduct(JxlOptimizeArray<T> y) => TensorPrimitives.Dot<T>(this.Span, y.Span);

    /// <summary>
    /// Finds objective-function parameters with the scaled conjugate gradient method.
    /// </summary>
    /// <typeparam name="TFloat">The floating-point type used by the objective and the search.</typeparam>
    /// <param name="factory">The factory for search arrays.</param>
    /// <param name="function">The objective function.</param>
    /// <param name="w0">The initial parameter values.</param>
    /// <param name="gradientNormalThreshold">The gradient norm at which the search stops.</param>
    /// <param name="maxIterations">The maximum number of search iterations.</param>
    /// <returns>The parameter values at the end of the search.</returns>
    public static JxlOptimizeArray<TFloat> OptimizeWithScaledConjugateGradientMethod<TFloat>(
        JxlOptimizeArrayFactory<TFloat> factory,
        JxlOptimizeFunction<TFloat> function,
        JxlOptimizeArray<TFloat> w0,
        TFloat gradientNormalThreshold,
        int maxIterations)
        where TFloat : unmanaged, INumber<TFloat>, IBinaryFloatingPointIeee754<TFloat>
    {
        // These variable names look cryptic, but they're part of
        // the Scaled Conjugate Gradient method. See the reference
        // implementation:
        //    https://github.com/libjxl/libjxl/blob/main/lib/jxl/enc_optimize.h#L81-L188
        int n = w0.length;
        TFloat rsqThreshold = gradientNormalThreshold * gradientNormalThreshold;

        TFloat sigma0 = TFloat.CreateSaturating(0.0001);
        TFloat lMin = TFloat.CreateSaturating(1.0e-15);
        TFloat lMax = TFloat.CreateSaturating(1.0e15);

        JxlOptimizeArray<TFloat> w = w0;

        JxlOptimizeArray<TFloat> wp = factory.CreateArray();
        JxlOptimizeArray<TFloat> r = factory.CreateArray();
        JxlOptimizeArray<TFloat> rt = factory.CreateArray();
        JxlOptimizeArray<TFloat> e = factory.CreateArray();
        JxlOptimizeArray<TFloat> p = factory.CreateArray();

        TFloat psq = default;
        TFloat fp;
        TFloat D = default;
        TFloat d;
        TFloat m = default;
        TFloat a;
        TFloat b;
        TFloat s;
        TFloat t = default;

        TFloat fw = function(w, ref r);
        TFloat rsq = r.DotProduct(r);
        e = r;
        p = r;

        TFloat l = TFloat.CreateSaturating(1.0);
        bool success = true;
        long nSuccess = 0;
        long k = 0;

        // Hot loop
        while (k++ < maxIterations)
        {
            if (success)
            {
                m = -p.DotProduct(r);

                if (m >= TFloat.Zero)
                {
                    p = r;
                    m = -p.DotProduct(r);
                }

                psq = p.DotProduct(p);
                s = sigma0 / TFloat.Sqrt(psq);
                _ = function(w + (s * p), ref rt);
                t = p.DotProduct(r - rt) / s;
            }

            d = t + (l * psq);
            if (d <= TFloat.Zero)
            {
                d = l * psq;
                l -= t / psq;
            }

            a = -m / d;
            wp = w + (a * p);
            fp = function(wp, ref rt);

            D = TFloat.CreateSaturating(2.0) * (fp - fw) / (a * m);
            if (D >= TFloat.Zero)
            {
                success = true;
                nSuccess++;
                w = wp;
            }
            else
            {
                success = false;
            }

            if (success)
            {
                e = r;
                r = rt;
                rsq = r.DotProduct(r);
                fw = fp;

                if (rsq <= rsqThreshold)
                {
                    break;
                }
            }

            if (D < TFloat.CreateSaturating(0.25))
            {
                l = TFloat.Min(TFloat.CreateSaturating(4) * l, lMax);
            }
            else if (D > TFloat.CreateSaturating(0.75))
            {
                l = TFloat.Max(TFloat.CreateSaturating(0.25) * l, lMax);
            }

            if ((nSuccess % n) == 0)
            {
                p = r;
                l = TFloat.CreateSaturating(1.0);
            }
            else if (success)
            {
                b = (e - r).DotProduct(r) / m;
                p = (b * p) + r;
            }
        }

        // clean up
        wp.Dispose();
        r.Dispose();
        rt.Dispose();
        e.Dispose();
        p.Dispose();

        return w;
    }

    public void Dispose()
    {
        if (this.isDisposed)
        {
            return;
        }

        this.isDisposed = true;
        this.memoryOwner.Dispose();
    }
}
