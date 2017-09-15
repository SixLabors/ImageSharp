// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.ColorSpaces.Conversion.Implementation.Icc;
using SixLabors.ImageSharp.MetaData.Profiles.Icc;

namespace SixLabors.ImageSharp.Tests.Colorspaces.Icc
{
    internal class IccConverterBaseMock : IccConverterBase
    {
        public new float[] CalculateMpeCurveSet(IccCurveSetProcessElement element, float[] values)
        {
            return base.CalculateMpeCurveSet(element, values);
        }

        public new float[] CalculateMpeMatrix(IccMatrixProcessElement element, float[] values)
        {
            return base.CalculateMpeMatrix(element, values);
        }

        public new float[] CalculateMpeClut(IccClutProcessElement element, float[] values)
        {
            return base.CalculateMpeClut(element, values);
        }


        public new float[] CalculateLut(IccLut8TagDataEntry lut, float[] values)
        {
            return base.CalculateLut(lut, values);
        }

        public new float[] CalculateLut(IccLut16TagDataEntry lut, float[] values)
        {
            return base.CalculateLut(lut, values);
        }

        public new float[] CalculateLutAToB(IccLutAToBTagDataEntry entry, float[] values)
        {
            return base.CalculateLutAToB(entry, values);
        }

        public new float[] CalculateLutBToA(IccLutBToATagDataEntry entry, float[] values)
        {
            return base.CalculateLutBToA(entry, values);
        }


        public new float[] CalculateCurve(IccTagDataEntry[] entries, bool inverted, float[] values)
        {
            return base.CalculateCurve(entries, inverted, values);
        }

        public new float CalculateCurve(IccTagDataEntry curveEntry, bool inverted, float value)
        {
            return base.CalculateCurve(curveEntry, inverted, value);
        }
    }
}
