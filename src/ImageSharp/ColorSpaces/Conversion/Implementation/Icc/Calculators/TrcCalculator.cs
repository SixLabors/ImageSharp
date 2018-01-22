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

        public Vector4 Calculate(Vector4 value)
        {
            value.X = this.calculators[0].Calculate(value.X);

            if (this.calculators.Length > 1)
            {
                value.Y = this.calculators[1].Calculate(value.Y);

                if (this.calculators.Length > 2)
                {
                    value.Z = this.calculators[2].Calculate(value.Z);

                    if (this.calculators.Length > 3)
                    {
                        value.W = this.calculators[3].Calculate(value.W);
                    }
                }
            }

            return value;
        }
    }
}
