// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.ImageSharp.MetaData.Profiles.Icc;

namespace SixLabors.ImageSharp.ColorSpaces.Conversion.Implementation.Icc.Calculators
{
    internal class TrcCalculator : IVector4Calculator
    {
        private ISingleCalculator[] calculators;

        public TrcCalculator(IccTagDataEntry[] entries, bool inverted)
        {
            Guard.NotNull(entries, nameof(entries));

            this.calculators = new ISingleCalculator[entries.Length];
            for (int i = 0; i < entries.Length; i++)
            {
                switch (entries[i])
                {
                    case IccCurveTagDataEntry curve:
                        this.calculators[i] = new CurveCalculator(curve, inverted);
                        break;

                    case IccParametricCurveTagDataEntry parametricCurve:
                        this.calculators[i] = new ParametricCurveCalculator(parametricCurve, inverted);
                        break;

                    default:
                        throw new InvalidIccProfileException();
                }
            }
        }

        public unsafe Vector4 Calculate(Vector4 value)
        {
            float* valuePointer = (float*)&value;
            for (int i = 0; i < this.calculators.Length; i++)
            {
                valuePointer[i] = this.calculators[i].Calculate(valuePointer[i]);
            }

            return value;
        }
    }
}
