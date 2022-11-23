// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;

namespace SixLabors.ImageSharp.ColorSpaces.Conversion.Icc
{
    internal class ParametricCurveCalculator : ISingleCalculator
    {
        private IccParametricCurve curve;
        private IccParametricCurveType type;
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
        {
            switch (this.type)
            {
                case IccParametricCurveType.Type1:
                    return this.CalculateGamma(value);
                case IccParametricCurveType.Cie122_1996:
                    return this.CalculateCie122(value);
                case IccParametricCurveType.Iec61966_3:
                    return this.CalculateIec61966(value);
                case IccParametricCurveType.SRgb:
                    return this.CalculateSRgb(value);
                case IccParametricCurveType.Type5:
                    return this.CalculateType5(value);

                case IccParametricCurveType.Type1 | InvertedFlag:
                    return this.CalculateInvertedGamma(value);
                case IccParametricCurveType.Cie122_1996 | InvertedFlag:
                    return this.CalculateInvertedCie122(value);
                case IccParametricCurveType.Iec61966_3 | InvertedFlag:
                    return this.CalculateInvertedIec61966(value);
                case IccParametricCurveType.SRgb | InvertedFlag:
                    return this.CalculateInvertedSRgb(value);
                case IccParametricCurveType.Type5 | InvertedFlag:
                    return this.CalculateInvertedType5(value);

                default:
                    throw new InvalidIccProfileException("ParametricCurve");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float CalculateGamma(float value)
        {
            return MathF.Pow(value, this.curve.G);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float CalculateCie122(float value)
        {
            if (value >= -this.curve.B / this.curve.A)
            {
                return MathF.Pow((this.curve.A * value) + this.curve.B, this.curve.G);
            }
            else
            {
                return 0;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float CalculateIec61966(float value)
        {
            if (value >= -this.curve.B / this.curve.A)
            {
                return MathF.Pow((this.curve.A * value) + this.curve.B, this.curve.G) + this.curve.C;
            }
            else
            {
                return this.curve.C;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float CalculateSRgb(float value)
        {
            if (value >= this.curve.D)
            {
                return MathF.Pow((this.curve.A * value) + this.curve.B, this.curve.G);
            }
            else
            {
                return this.curve.C * value;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float CalculateType5(float value)
        {
            if (value >= this.curve.D)
            {
                return MathF.Pow((this.curve.A * value) + this.curve.B, this.curve.G) + this.curve.E;
            }
            else
            {
                return (this.curve.C * value) + this.curve.F;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float CalculateInvertedGamma(float value)
        {
            return MathF.Pow(value, 1 / this.curve.G);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float CalculateInvertedCie122(float value)
        {
            return (MathF.Pow(value, 1 / this.curve.G) - this.curve.B) / this.curve.A;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float CalculateInvertedIec61966(float value)
        {
            if (value >= this.curve.C)
            {
                return (MathF.Pow(value - this.curve.C, 1 / this.curve.G) - this.curve.B) / this.curve.A;
            }
            else
            {
                return -this.curve.B / this.curve.A;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float CalculateInvertedSRgb(float value)
        {
            if (value >= MathF.Pow((this.curve.A * this.curve.D) + this.curve.B, this.curve.G))
            {
                return (MathF.Pow(value, 1 / this.curve.G) - this.curve.B) / this.curve.A;
            }
            else
            {
                return value / this.curve.C;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float CalculateInvertedType5(float value)
        {
            if (value >= (this.curve.C * this.curve.D) + this.curve.F)
            {
                return (MathF.Pow(value - this.curve.E, 1 / this.curve.G) - this.curve.B) / this.curve.A;
            }
            else
            {
                return (value - this.curve.F) / this.curve.C;
            }
        }
    }
}
