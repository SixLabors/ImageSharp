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

    public static JxlOptimizeArray<T> OptimizeWithScaledConjugateGradientMethod<T>(
        JxlOptimizeArrayFactory<T> factory,
        JxlOptimizeFunction<T> function,
        JxlOptimizeArray<T> w0,
        T gradientNormalThreshold,
        int maxIterations)
        where T : unmanaged, INumber<T>,
                            IBinaryFloatingPointIeee754<T>
    {
        // These variable names look cryptic, but they're part of
        // the Scaled Conjugate Gradient method. See the reference
        // implementation:
        //    https://github.com/libjxl/libjxl/blob/main/lib/jxl/enc_optimize.h#L81-L188
        int n = w0.length;
        T rsqThreshold = gradientNormalThreshold * gradientNormalThreshold;

        T sigma0 = T.CreateSaturating(0.0001);
        T lMin = T.CreateSaturating(1.0e-15);
        T lMax = T.CreateSaturating(1.0e15);

        JxlOptimizeArray<T> w = w0;

        JxlOptimizeArray<T> wp = factory.CreateArray();
        JxlOptimizeArray<T> r = factory.CreateArray();
        JxlOptimizeArray<T> rt = factory.CreateArray();
        JxlOptimizeArray<T> e = factory.CreateArray();
        JxlOptimizeArray<T> p = factory.CreateArray();

        T psq = default;
        T fp;
        T D = default;
        T d;
        T m = default;
        T a;
        T b;
        T s;
        T t = default;

        T fw = function(w, ref r);
        T rsq = r.DotProduct(r);
        e = r;
        p = r;

        T l = T.CreateSaturating(1.0);
        bool success = true;
        long nSuccess = 0;
        long k = 0;

        // Hot loop
        while (k++ < maxIterations)
        {
            if (success)
            {
                m = -p.DotProduct(r);

                if (m >= T.Zero)
                {
                    p = r;
                    m = -p.DotProduct(r);
                }

                psq = p.DotProduct(p);
                s = sigma0 / T.Sqrt(psq);
                _ = function(w + (s * p), ref rt);
                t = p.DotProduct(r - rt) / s;
            }

            d = t + (l * psq);
            if (d <= T.Zero)
            {
                d = l * psq;
                l -= t / psq;
            }

            a = -m / d;
            wp = w + (a * p);
            fp = function(wp, ref rt);

            D = T.CreateSaturating(2.0) * (fp - fw) / (a * m);
            if (D >= T.Zero)
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

            if (D < T.CreateSaturating(0.25))
            {
                l = T.Min(T.CreateSaturating(4) * l, lMax);
            }
            else if (D > T.CreateSaturating(0.75))
            {
                l = T.Max(T.CreateSaturating(0.25) * l, lMax);
            }

            if ((nSuccess % n) == 0)
            {
                p = r;
                l = T.CreateSaturating(1.0);
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
