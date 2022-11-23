// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;

namespace SixLabors.ImageSharp.ColorSpaces.Conversion.Icc
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
                        throw new InvalidIccProfileException("Invalid Entry.");
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
