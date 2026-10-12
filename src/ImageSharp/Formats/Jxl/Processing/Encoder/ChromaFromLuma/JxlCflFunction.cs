// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.ChromaFromLuma;

internal readonly ref struct JxlCflFunction
{
    private const float Coefficient = 1.0f / 3f;
    private const float Threshold = 100f;
    private const float InverseColorFactor = 1.0f / JxlChromaFromLuma.DefaultColorFactor;

    private readonly Span<float> valuesM;
    private readonly Span<float> valuesS;
    private readonly int num;
    private readonly float @base;
    private readonly float distanceMultiplier;

    public JxlCflFunction(
        Span<float> valuesM,
        Span<float> valuesS,
        int num,
        float @base,
        float distanceMultiplier)
    {
        this.valuesM = valuesM;
        this.valuesS = valuesS;
        this.num = num;
        this.@base = @base;
        this.distanceMultiplier = distanceMultiplier;
    }

    // Returns f'(x), where f is 1/3 * sum ((|color residual| + 1)^2-1) +
    // distance_mul * x^2 * num.
    public readonly float Compute(float x, float eps, ref float fpeps, ref float fmeps)
    {
        float firstDerivative = 2 * this.distanceMultiplier * this.num * x;
        float firstDerivativePeps = 2 * this.distanceMultiplier * this.num * (x + eps);
        float firstDerivativeMeps = 2 * this.distanceMultiplier * this.num * (x - eps);

        Vector<float> inverseColorFactor = Vector.Create(InverseColorFactor);
        Vector<float> thres = Vector.Create(Threshold);
        Vector<float> coeffx2 = Vector.Create(Coefficient * 2.0f);
        Vector<float> one = Vector<float>.One;
        Vector<float> zero = Vector<float>.Zero;
        Vector<float> baseV = Vector.Create(this.@base);
        Vector<float> xV = Vector.Create(x);
        Vector<float> xpeV = Vector.Create(x + eps);
        Vector<float> xmeV = Vector.Create(x - eps);
        Vector<float> fdV = Vector<float>.Zero;
        Vector<float> fdpeV = Vector<float>.Zero;
        Vector<float> fdmeV = Vector<float>.Zero;

        for (int i = 0; i <= this.num - Vector<float>.Count; i += Vector<float>.Count)
        {
            // color residual = ax + b
            Vector<float> a = inverseColorFactor * Vector.Create<float>(this.valuesM[i..]);
            Vector<float> b = (baseV * Vector.Create<float>(this.valuesM[i..])) - Vector.Create<float>(this.valuesS[i..]);

            Vector<float> v = Vector.FusedMultiplyAdd(a, xV, b);
            Vector<float> vpe = Vector.FusedMultiplyAdd(a, xpeV, b);
            Vector<float> vme = Vector.FusedMultiplyAdd(a, xmeV, b);

            // Abs of three above vectors
            Vector<float> av = Vector.Abs(v);
            Vector<float> avpe = Vector.Abs(vpe);
            Vector<float> avme = Vector.Abs(vme);

            Vector<float> acoeffx2 = coeffx2 * a;
            Vector<float> d = acoeffx2 * (av + one);
            Vector<float> dpe = acoeffx2 * (avpe + one);
            Vector<float> dme = acoeffx2 * (avme + one);

            // if (V < 0) { D = -D } else { D }
            d = Vector.ConditionalSelect(
                Vector.LessThan(v, zero),
                zero - d,
                d);
            dpe = Vector.ConditionalSelect(
                Vector.LessThan(vpe, zero),
                zero - dpe,
                dpe);
            dme = Vector.ConditionalSelect(
                Vector.LessThan(vme, zero),
                zero - dme,
                dme);

            Vector<int> above = Vector.GreaterThanOrEqual(av, thres);

            // F += above ? 0 : D
            fdV += Vector.ConditionalSelect(above, zero, d);
            fdpeV += Vector.ConditionalSelect(above, zero, dpe);
            fdmeV += Vector.ConditionalSelect(above, zero, dme);
        }

        fpeps = firstDerivativePeps + Vector.Sum(fdpeV);
        fmeps = firstDerivativeMeps + Vector.Sum(fdmeV);
        return firstDerivative + Vector.Sum(fdV);
    }
}
