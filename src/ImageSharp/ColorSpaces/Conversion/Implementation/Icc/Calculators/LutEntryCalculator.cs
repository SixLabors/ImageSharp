// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.ImageSharp.MetaData.Profiles.Icc;

namespace SixLabors.ImageSharp.ColorSpaces.Conversion.Implementation.Icc.Calculators
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

        private Vector4 CalculateLut(LutCalculator[] lut, Vector4 value)
        {
            value = Vector4.Clamp(value, Vector4.Zero, Vector4.One);

            value.X = lut[0].Calculate(value.X);

            if (lut.Length > 1)
            {
                value.Y = lut[1].Calculate(value.Y);

                if (lut.Length > 2)
                {
                    value.Z = lut[2].Calculate(value.Z);

                    if (lut.Length > 3)
                    {
                        value.W = lut[3].Calculate(value.W);
                    }
                }
            }

            return value;
        }

        private void Init(IccLut[] inputCurve, IccLut[] outputCurve, IccClut clut, Matrix4x4 matrix)
        {
            this.inputCurve = this.InitLut(inputCurve);
            this.outputCurve = this.InitLut(outputCurve);
            this.clutCalculator = new ClutCalculator(clut);
            this.matrix = matrix;

            this.doTransform = matrix != null && inputCurve.Length == 3;
        }

        private LutCalculator[] InitLut(IccLut[] curves)
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
