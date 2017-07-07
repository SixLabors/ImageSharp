// <copyright file="IccConverter.Lut.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Colors.Spaces.Conversion.Implementation.Icc
{
    using System;
    using System.Numerics;

    /// <summary>
    /// Color converter for ICC profiles
    /// </summary>
    internal abstract partial class IccConverterBase
    {
        /// <summary>
        /// Calculates the output values with an 8bit lookup table. Note that input and output channel count can be different.
        /// </summary>
        /// <param name="lut">The lookup table to use</param>
        /// <param name="values">The input color values to convert</param>
        /// <returns>The converted output color values</returns>
        private float[] CalculateLut(IccLut8TagDataEntry lut, float[] values)
        {
            return this.CalculateLut(lut.InputValues, lut.OutputValues, lut.ClutValues, lut.Matrix, values);
        }

        /// <summary>
        /// Calculates the output values with a 16bit lookup table. Note that input and output channel count can be different.
        /// </summary>
        /// <param name="lut">The lookup table to use</param>
        /// <param name="values">The input color values to convert</param>
        /// <returns>The converted output color values</returns>
        private float[] CalculateLut(IccLut16TagDataEntry lut, float[] values)
        {
            return this.CalculateLut(lut.InputValues, lut.OutputValues, lut.ClutValues, lut.Matrix, values);
        }

        /// <summary>
        /// Calculates the output values with an ICC A to B data type (PCS to Data). Note that input and output channel count can be different.
        /// </summary>
        /// <param name="entry">The conversion info to use</param>
        /// <param name="values">The input color values to convert</param>
        /// <returns>The converted output color values</returns>
        private float[] CalculateLutAToB(IccLutAToBTagDataEntry entry, float[] values)
        {
            bool ca = entry.CurveA != null;
            bool cb = entry.CurveB != null;
            bool cm = entry.CurveM != null;
            bool matrix = entry.Matrix3x1 != null && entry.Matrix3x3 != null;
            bool clut = entry.ClutValues != null;

            if (ca && clut && cm && matrix && cb)
            {
                float[] result = this.CalculateCurve(entry.CurveA, false, values);
                result = this.CalculateClut(entry.ClutValues, result);
                result = this.CalculateCurve(entry.CurveM, false, result);
                result = this.CalculateMatrix(entry.Matrix3x3.Value, entry.Matrix3x1.Value, result);
                return this.CalculateCurve(entry.CurveB, false, result);
            }
            else if (ca && clut && cb)
            {
                float[] result = this.CalculateCurve(entry.CurveA, false, values);
                result = this.CalculateClut(entry.ClutValues, result);
                return this.CalculateCurve(entry.CurveB, false, result);
            }
            else if (cm && matrix && cb)
            {
                float[] result = this.CalculateCurve(entry.CurveM, false, values);
                result = this.CalculateMatrix(entry.Matrix3x3.Value, entry.Matrix3x1.Value, result);
                return this.CalculateCurve(entry.CurveB, false, result);
            }
            else if (cb)
            {
                return this.CalculateCurve(entry.CurveB, false, values);
            }
            else
            {
                throw new InvalidIccProfileException("AToB tag has an invalid configuration");
            }
        }

        /// <summary>
        /// Calculates the output values with an ICC B to A data type (Data to PCS). Note that input and output channel count can be different.
        /// </summary>
        /// <param name="entry">The conversion info to use</param>
        /// <param name="values">The input color values to convert</param>
        /// <returns>The converted output color values</returns>
        private float[] CalculateLutBToA(IccLutBToATagDataEntry entry, float[] values)
        {
            bool ca = entry.CurveA != null;
            bool cb = entry.CurveB != null;
            bool cm = entry.CurveM != null;
            bool matrix = entry.Matrix3x1 != null && entry.Matrix3x3 != null;
            bool clut = entry.ClutValues != null;

            if (cb && matrix && cm && clut && ca)
            {
                float[] result = this.CalculateCurve(entry.CurveB, false, values);
                result = this.CalculateMatrix(entry.Matrix3x3.Value, entry.Matrix3x1.Value, result);
                result = this.CalculateCurve(entry.CurveM, false, result);
                result = this.CalculateClut(entry.ClutValues, result);
                return this.CalculateCurve(entry.CurveA, false, result);
            }
            else if (cb && clut && ca)
            {
                float[] result = this.CalculateCurve(entry.CurveB, false, values);
                result = this.CalculateClut(entry.ClutValues, result);
                return this.CalculateCurve(entry.CurveA, false, result);
            }
            else if (cb && matrix && cm)
            {
                float[] result = this.CalculateCurve(entry.CurveB, false, values);
                result = this.CalculateMatrix(entry.Matrix3x3.Value, entry.Matrix3x1.Value, result);
                return this.CalculateCurve(entry.CurveM, false, result);
            }
            else if (cb)
            {
                return this.CalculateCurve(entry.CurveB, false, values);
            }
            else
            {
                throw new InvalidIccProfileException("BToA tag has an invalid configuration");
            }
        }

        private float[] CalculateLut(IccLut[] inCurve, IccLut[] outCurve, IccClut clut, Matrix4x4 matrix, float[] values)
        {
            float[] inValues = new float[values.Length];
            if (values.Length == 3 && matrix != null)
            {
                var transformed = Vector3.Transform(new Vector3(values[0], values[1], values[2]), matrix);
                transformed.CopyTo(inValues);
            }
            else
            {
                Array.Copy(values, inValues, values.Length);
            }

            // Input LUT
            for (int i = 0; i < values.Length; i++)
            {
                inValues[i] = this.CalculateLut(inCurve[i], inValues[i]);
            }

            // CLUT
            float[] result = this.CalculateClut(clut, inValues);

            // Output LUT
            for (int i = 0; i < outCurve.Length; i++)
            {
                result[i] = this.CalculateLut(outCurve[i], result[i]);
            }

            return result;
        }

        private float CalculateLut(IccLut lut, float value)
        {
            float idx = value * (lut.Values.Length - 1);
            float low = lut.Values[(int)idx];
            float high = lut.Values[(int)idx + 1];
            return low + ((high - low) * (idx - (int)idx));
        }

        private float[] CalculateClut(IccClut clut, float[] values)
        {
            int gpc = 1;
            int idx = 0;
            for (int i = clut.InputChannelCount - 1; i >= 0; i--)
            {
                idx += (int)(values[i] * clut.GridPointCount[i]) * gpc;
                gpc *= clut.GridPointCount[i];
            }

            return clut.Values[idx];
        }

        private float[] CalculateMatrix(Matrix4x4 matrix3x3, Vector3 matrix3x1, float[] values)
        {
            var inVector = new Vector3(values[0], values[1], values[2]);
            var transformed = Vector3.Transform(inVector, matrix3x3);

            var resultVector = Vector3.Add(matrix3x1, transformed);
            return new float[] { resultVector.X, resultVector.Y, resultVector.Z };
        }
    }
}
