// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.MetaData.Profiles.Icc;

namespace SixLabors.ImageSharp.ColorSpaces.Conversion.Implementation.Icc
{
    /// <summary>
    /// Color converter for ICC profiles
    /// </summary>
    internal abstract partial class IccConverterBase
    {
        /// <summary>
        /// Calculates the output values with curve tag data entries (one entry for each channel)
        /// </summary>
        /// <param name="entries">The curve tag data entries to use</param>
        /// <param name="inverted">True to use the inverse curve calculation; False otherwise</param>
        /// <param name="values">The input color values to convert</param>
        /// <returns>The converted output color values</returns>
        protected float[] CalculateCurve(IccTagDataEntry[] entries, bool inverted, float[] values)
        {
            float[] result = new float[values.Length];
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = this.CalculateCurve(entries[i], inverted, values[i]);
            }

            return result;
        }

        /// <summary>
        /// Calculates the output value with a curve a tag data entry
        /// </summary>
        /// <param name="curveEntry">The curve tag data entry to use</param>
        /// <param name="inverted">True to use the inverse curve calculation; False otherwise</param>
        /// <param name="value">The input color value to convert</param>
        /// <returns>The converted output color value</returns>
        protected float CalculateCurve(IccTagDataEntry curveEntry, bool inverted, float value)
        {
            switch (curveEntry)
            {
                case IccCurveTagDataEntry curve:
                    return inverted ? this.CalculateCurveInverted(curve, value) :
                        this.CalculateCurve(curve, value);

                case IccParametricCurveTagDataEntry pcurve:
                    return inverted ? this.CalculateParametricCurveInverted(pcurve, value) :
                        this.CalculateParametricCurve(pcurve, value);

                default:
                    throw new InvalidIccProfileException();
            }
        }

        private float CalculateCurve(IccCurveTagDataEntry curve, float value)
        {
            if (curve.IsIdentityResponse)
            {
                return value;
            }
            else if (curve.IsGamma)
            {
                return (float)Math.Pow(value, curve.Gamma);
            }
            else
            {
                int index = (int)((value * (curve.CurveData.Length - 1)) + 0.5);
                return curve.CurveData[index];
            }
        }

        private float CalculateCurveInverted(IccCurveTagDataEntry curve, float value)
        {
            if (curve.IsIdentityResponse)
            {
                return value;
            }
            else if (curve.IsGamma)
            {
                return (float)Math.Pow(value, 1 / curve.Gamma);
            }
            else
            {
                int idx = Array.BinarySearch(curve.CurveData, value);
                if (idx < 0)
                {
                    // Note that idx - 1 is never less than zero because BinarySearch always returns the index of the next
                    // bigger value and the array always has a length bigger than one because of the previous if conditions:
                    // IsIdentityResponse means it's a length of zero, IsGamma means it's a length of one.
                    idx = ~idx;
                    float higher = curve.CurveData[idx];
                    float lower = curve.CurveData[idx - 1];

                    if ((higher - value) > (value - lower))
                    {
                        idx -= 1;
                    }
                }

                return idx / (curve.CurveData.Length - 1f);
            }
        }

        private float CalculateParametricCurve(IccParametricCurveTagDataEntry data, float value)
        {
            IccParametricCurve curve = data.Curve;

            switch (curve.Type)
            {
                case IccParametricCurveType.Type1:
                    return this.CalculateParametricCurveType1(curve, value);
                case IccParametricCurveType.Cie122_1996:
                    return this.CalculateParametricCurveCie122(curve, value);
                case IccParametricCurveType.Iec61966_3:
                    return this.CalculateParametricCurveIec61966(curve, value);
                case IccParametricCurveType.SRgb:
                    return this.CalculateParametricCurveSRgb(curve, value);
                case IccParametricCurveType.Type5:
                    return this.CalculateParametricCurveType5(curve, value);

                default:
                    throw new InvalidIccProfileException("ParametricCurve");
            }
        }

        private float CalculateParametricCurveInverted(IccParametricCurveTagDataEntry data, float value)
        {
            IccParametricCurve curve = data.Curve;

            switch (curve.Type)
            {
                case IccParametricCurveType.Type1:
                    return this.CalculateParametricCurveInvertedType1(curve, value);
                case IccParametricCurveType.Cie122_1996:
                    return this.CalculateParametricCurveInvertedCie122(curve, value);
                case IccParametricCurveType.Iec61966_3:
                    return this.CalculateParametricCurveInvertedIec61966(curve, value);
                case IccParametricCurveType.SRgb:
                    return this.CalculateParametricCurveInvertedSRgb(curve, value);
                case IccParametricCurveType.Type5:
                    return this.CalculateParametricCurveInvertedType5(curve, value);

                default:
                    throw new InvalidIccProfileException("ParametricCurve");
            }
        }

        private float CalculateParametricCurveType1(IccParametricCurve curve, float value)
        {
            return (float)Math.Pow(value, curve.G);
        }

        private float CalculateParametricCurveCie122(IccParametricCurve curve, float value)
        {
            if (value >= -curve.B / curve.A)
            {
                return (float)Math.Pow((curve.A * value) + curve.B, curve.G);
            }
            else
            {
                return 0;
            }
        }

        private float CalculateParametricCurveIec61966(IccParametricCurve curve, float value)
        {
            if (value >= -curve.B / curve.A)
            {
                return (float)Math.Pow((curve.A * value) + curve.B, curve.G) + curve.C;
            }
            else
            {
                return curve.C;
            }
        }

        private float CalculateParametricCurveSRgb(IccParametricCurve curve, float value)
        {
            if (value >= curve.D)
            {
                return (float)Math.Pow((curve.A * value) + curve.B, curve.G);
            }
            else
            {
                return curve.C * value;
            }
        }

        private float CalculateParametricCurveType5(IccParametricCurve curve, float value)
        {
            if (value >= curve.D)
            {
                return (float)Math.Pow((curve.A * value) + curve.B, curve.G) + curve.E;
            }
            else
            {
                return (curve.C * value) + curve.F;
            }
        }

        private float CalculateParametricCurveInvertedType1(IccParametricCurve curve, float value)
        {
            return (float)Math.Pow(value, 1 / curve.G);
        }

        private float CalculateParametricCurveInvertedCie122(IccParametricCurve curve, float value)
        {
            return ((float)Math.Pow(value, 1 / curve.G) - curve.B) / curve.A;
        }

        private float CalculateParametricCurveInvertedIec61966(IccParametricCurve curve, float value)
        {
            if (value >= curve.C)
            {
                return ((float)Math.Pow(value - curve.C, 1 / curve.G) - curve.B) / curve.A;
            }
            else
            {
                return -curve.B / curve.A;
            }
        }

        private float CalculateParametricCurveInvertedSRgb(IccParametricCurve curve, float value)
        {
            if (value >= Math.Pow((curve.A * curve.D) + curve.B, curve.G))
            {
                return ((float)Math.Pow(value, 1 / curve.G) - curve.B) / curve.A;
            }
            else
            {
                return value / curve.C;
            }
        }

        private float CalculateParametricCurveInvertedType5(IccParametricCurve curve, float value)
        {
            if (value >= (curve.C * curve.D) + curve.F)
            {
                return ((float)Math.Pow(value - curve.E, 1 / curve.G) - curve.B) / curve.A;
            }
            else
            {
                return (value - curve.F) / curve.C;
            }
        }
    }
}
