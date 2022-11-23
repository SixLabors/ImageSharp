// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;

namespace SixLabors.ImageSharp.ColorSpaces.Conversion.Icc
{
    internal class LutEntryCalculator : IVector4Calculator
    {
        private LutCalculator[] inputCurve;
        private LutCalculator[] outputCurve;
        private ClutCalculator clutCalculator;
        private Matrix4x4 matrix;
        private bool doTransform;

        public LutEntryCalculator(IccLut8TagDataEntry lut)
        {
            Guard.NotNull(lut, nameof(lut));
            this.Init(lut.InputValues, lut.OutputValues, lut.ClutValues, lut.Matrix);
        }

        public LutEntryCalculator(IccLut16TagDataEntry lut)
        {
            Guard.NotNull(lut, nameof(lut));
            this.Init(lut.InputValues, lut.OutputValues, lut.ClutValues, lut.Matrix);
        }

        public Vector4 Calculate(Vector4 value)
        {
            if (this.doTransform)
            {
                value = Vector4.Transform(value, this.matrix);
            }

            value = this.CalculateLut(this.inputCurve, value);
            value = this.clutCalculator.Calculate(value);
            return this.CalculateLut(this.outputCurve, value);
        }

        private unsafe Vector4 CalculateLut(LutCalculator[] lut, Vector4 value)
        {
            value = Vector4.Clamp(value, Vector4.Zero, Vector4.One);

            float* valuePointer = (float*)&value;
            for (int i = 0; i < lut.Length; i++)
            {
                valuePointer[i] = lut[i].Calculate(valuePointer[i]);
            }

            return value;
        }

        private void Init(IccLut[] inputCurve, IccLut[] outputCurve, IccClut clut, Matrix4x4 matrix)
        {
            this.inputCurve = InitLut(inputCurve);
            this.outputCurve = InitLut(outputCurve);
            this.clutCalculator = new ClutCalculator(clut);
            this.matrix = matrix;

            this.doTransform = !matrix.IsIdentity && inputCurve.Length == 3;
        }

        private static LutCalculator[] InitLut(IccLut[] curves)
        {
            LutCalculator[] calculators = new LutCalculator[curves.Length];
            for (int i = 0; i < curves.Length; i++)
            {
                calculators[i] = new LutCalculator(curves[i].Values, false);
            }

            return calculators;
        }
    }
}
