// <copyright file="IccConverter.MultiProcessElement.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Colors.Spaces.Conversion.Implementation.Icc
{
    using System;
    using ImageSharp.Memory;

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
        private float[] CalculateMpeCurveSet(IccCurveSetProcessElement element, float[] values)
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
        private float[] CalculateMpeMatrix(IccMatrixProcessElement element, float[] values)
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
        private float[] CalculateMpeClut(IccClutProcessElement element, float[] values)
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
    }
}
