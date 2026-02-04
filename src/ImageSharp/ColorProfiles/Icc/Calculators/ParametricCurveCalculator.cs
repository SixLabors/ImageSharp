// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;

namespace SixLabors.ImageSharp.ColorProfiles.Icc.Calculators;

internal class ParametricCurveCalculator : ISingleCalculator
{
    private readonly IccParametricCurve curve;
    private readonly IccParametricCurveType type;
    private const IccParametricCurveType InvertedFlag = (IccParametricCurveType)(1 << 3);

    public ParametricCurveCalculator(IccParametricCurveTagDataEntry entry, bool inverted)
    {
        Guard.NotNull(entry, nameof(entry));
        this.curve = entry.Curve;
        this.type = entry.Curve.Type;

        if (inverted)
        {
            this.type |= InvertedFlag;
        }
    }

    public float Calculate(float value)
        => this.type switch
        {
            IccParametricCurveType.Type1 => this.CalculateGamma(value),
            IccParametricCurveType.Cie122_1996 => this.CalculateCie122(value),
            IccParametricCurveType.Iec61966_3 => this.CalculateIec61966(value),
            IccParametricCurveType.SRgb => this.CalculateSRgb(value),
            IccParametricCurveType.Type5 => this.CalculateType5(value),
            IccParametricCurveType.Type1 | InvertedFlag => this.CalculateInvertedGamma(value),
            IccParametricCurveType.Cie122_1996 | InvertedFlag => this.CalculateInvertedCie122(value),
            IccParametricCurveType.Iec61966_3 | InvertedFlag => this.CalculateInvertedIec61966(value),
            IccParametricCurveType.SRgb | InvertedFlag => this.CalculateInvertedSRgb(value),
            IccParametricCurveType.Type5 | InvertedFlag => this.CalculateInvertedType5(value),
            _ => throw new InvalidIccProfileException("ParametricCurve"),
        };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private float CalculateGamma(float value) => MathF.Pow(value, this.curve.G);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private float CalculateCie122(float value)
    {
        if (value >= -this.curve.B / this.curve.A)
        {
            return MathF.Pow((this.curve.A * value) + this.curve.B, this.curve.G);
        }

        return 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private float CalculateIec61966(float value)
    {
        if (value >= -this.curve.B / this.curve.A)
        {
            return MathF.Pow((this.curve.A * value) + this.curve.B, this.curve.G) + this.curve.C;
        }

        return this.curve.C;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private float CalculateSRgb(float value)
    {
        if (value >= this.curve.D)
        {
            return MathF.Pow((this.curve.A * value) + this.curve.B, this.curve.G);
        }

        return this.curve.C * value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private float CalculateType5(float value)
    {
        if (value >= this.curve.D)
        {
            return MathF.Pow((this.curve.A * value) + this.curve.B, this.curve.G) + this.curve.E;
        }

        return (this.curve.C * value) + this.curve.F;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private float CalculateInvertedGamma(float value)
        => MathF.Pow(value, 1 / this.curve.G);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private float CalculateInvertedCie122(float value)
        => (MathF.Pow(value, 1 / this.curve.G) - this.curve.B) / this.curve.A;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private float CalculateInvertedIec61966(float value)
    {
        if (value >= this.curve.C)
        {
            return (MathF.Pow(value - this.curve.C, 1 / this.curve.G) - this.curve.B) / this.curve.A;
        }

        return -this.curve.B / this.curve.A;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private float CalculateInvertedSRgb(float value)
    {
        if (value >= MathF.Pow((this.curve.A * this.curve.D) + this.curve.B, this.curve.G))
        {
            return (MathF.Pow(value, 1 / this.curve.G) - this.curve.B) / this.curve.A;
        }

        return value / this.curve.C;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private float CalculateInvertedType5(float value)
    {
        if (value >= (this.curve.C * this.curve.D) + this.curve.F)
        {
            return (MathF.Pow(value - this.curve.E, 1 / this.curve.G) - this.curve.B) / this.curve.A;
        }

        return (value - this.curve.F) / this.curve.C;
    }
}
