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
        /// Calculates the output values with a curve set
        /// </summary>
        /// <param name="element">The curve set to use</param>
        /// <param name="values">The input color values to convert</param>
        /// <returns>The converted output color values</returns>
        protected float[] CalculateMpeCurveSet(IccCurveSetProcessElement element, float[] values)
        {
            float[] result = new float[values.Length];
            for (int i = 0; i < element.Curves.Length; i++)
            {
                result[i] = this.CalculateOneDimensionalCurve(element.Curves[i], values[i]);
            }

            return result;
        }

        /// <summary>
        /// Calculates the output values with a matrix
        /// </summary>
        /// <param name="element">The matrix data to use</param>
        /// <param name="values">The input color values to convert</param>
        /// <returns>The converted output color values</returns>
        protected float[] CalculateMpeMatrix(IccMatrixProcessElement element, float[] values)
        {
            float[] multiplied = this.MultiplyMatrix(element.MatrixIxO, values);
            return this.AddMatrix(multiplied, element.MatrixOx1);
        }

        /// <summary>
        /// Calculates the output values with a color lookup table
        /// </summary>
        /// <param name="element">The color lookup table to use</param>
        /// <param name="values">The input color values to convert</param>
        /// <returns>The converted output color values</returns>
        protected float[] CalculateMpeClut(IccClutProcessElement element, float[] values)
        {
            return this.CalculateClut(element.ClutValue, values);
        }

        private float[] MultiplyMatrix(Fast2DArray<float> matrixIxO, float[] values)
        {
            if (matrixIxO.Height != values.Length)
            {
                throw new ArgumentException("Matrix sizes do not match");
            }

            float[] result = new float[matrixIxO.Width];
            for (int i = 0; i < matrixIxO.Width; ++i)
            {
                for (int k = 0; k < matrixIxO.Height; ++k)
                {
                    result[i] += matrixIxO[i, k] * values[k];
                }
            }

            return result;
        }

        private float[] AddMatrix(float[] values, float[] matrixOx1)
        {
            if (values.Length != matrixOx1.Length)
            {
                throw new ArgumentException("Matrix sizes do not match");
            }

            float[] result = new float[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                result[i] = values[i] + matrixOx1[i];
            }

            return result;
        }

        private float CalculateOneDimensionalCurve(IccOneDimensionalCurve curve, float value)
        {
            int length = curve.BreakPoints.Length;

            if (length == 0)
            {
                throw new InvalidIccProfileException();
            }
            else if (length > 1)
            {
                int scopeStart = 0;
                int scopeEnd = curve.BreakPoints.Length - 1;
                int foundIndex = 0;
                while (scopeEnd > scopeStart)
                {
                    foundIndex = (scopeStart + scopeEnd) / 2;
                    if (value > curve.BreakPoints[foundIndex])
                    {
                        scopeStart = foundIndex + 1;
                    }
                    else
                    {
                        scopeEnd = foundIndex;
                    }
                }

                return this.CalculateCurveSegment(curve.Segments[foundIndex], value);
            }
            else
            {
                return this.CalculateCurveSegment(curve.Segments[0], value);
            }
        }

        private float CalculateCurveSegment(IccCurveSegment segment, float value)
        {
            switch (segment)
            {
                case IccFormulaCurveElement formula:
                    return this.CalculateFormulaCurveSegment(formula, value);

                case IccSampledCurveElement sampled:
                    return this.CalculateSampledCurveSegment(sampled, value);

                default:
                    throw new InvalidIccProfileException();
            }
        }

        private float CalculateFormulaCurveSegment(IccFormulaCurveElement segment, float value)
        {
            switch (segment.Type)
            {
                case IccFormulaCurveType.Type1:
                    return this.CalculateFormulaCurveSegmentType1(segment, value);
                case IccFormulaCurveType.Type2:
                    return this.CalculateFormulaCurveSegmentType2(segment, value);
                case IccFormulaCurveType.Type3:
                    return this.CalculateFormulaCurveSegmentType3(segment, value);
                default:
                    throw new InvalidIccProfileException("FormulaCurveElement has an unknown type.");
            }
        }

        private float CalculateFormulaCurveSegmentType1(IccFormulaCurveElement segment, float value)
        {
            return (float)Math.Pow((value * segment.A) + segment.B, segment.Gamma) + segment.C;
        }

        private float CalculateFormulaCurveSegmentType2(IccFormulaCurveElement segment, float value)
        {
            return ((float)Math.Log10((segment.B * (float)Math.Pow(value, segment.Gamma)) + segment.C) * segment.A) + segment.D;
        }

        private float CalculateFormulaCurveSegmentType3(IccFormulaCurveElement segment, float value)
        {
            return ((float)Math.Pow(segment.B, (value * segment.C) + segment.D) * segment.A) + segment.E;
        }

        private float CalculateSampledCurveSegment(IccSampledCurveElement segment, float value)
        {
            int length = segment.CurveEntries.Length;

            if (length == 0)
            {
                throw new InvalidIccProfileException();
            }
            else if (length > 1)
            {
                int scopeStart = 0;
                int scopeEnd = segment.CurveEntries.Length - 1;
                int currentIndex = 0;
                float currentValue;
                while (scopeEnd > scopeStart)
                {
                    currentIndex = (scopeStart + scopeEnd) / 2;
                    currentValue = segment.CurveEntries[currentIndex];
                    if (value > currentValue)
                    {
                        scopeStart = currentIndex + 1;
                    }
                    else
                    {
                        scopeEnd = currentIndex;
                    }
                }

                return segment.CurveEntries[currentIndex];
            }
            else
            {
                return segment.CurveEntries[0];
            }
        }
    }
}
