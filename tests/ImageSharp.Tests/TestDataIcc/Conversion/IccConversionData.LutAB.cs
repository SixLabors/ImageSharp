// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;

namespace SixLabors.ImageSharp.Tests.Colorspaces.Icc
{
    public class IccConversionDataLutAB
    {
        private static IccLutAToBTagDataEntry lutAtoB_SingleCurve = new IccLutAToBTagDataEntry(
           new IccTagDataEntry[]
           {
               IccConversionDataTrc.IdentityCurve,
               IccConversionDataTrc.IdentityCurve,
               IccConversionDataTrc.IdentityCurve
           },
           null, null, null, null, null);

        // also need:
        // # CurveM + matrix
        // # CurveA + CLUT + CurveB
        // # CurveA + CLUT + CurveM + Matrix + CurveB

        private static IccLutBToATagDataEntry lutBtoA_SingleCurve = new IccLutBToATagDataEntry(
           new IccTagDataEntry[]
           {
               IccConversionDataTrc.IdentityCurve,
               IccConversionDataTrc.IdentityCurve,
               IccConversionDataTrc.IdentityCurve
           },
           null, null, null, null, null);

        public static object[][] LutAToBConversionTestData =
        {
            new object[] { lutAtoB_SingleCurve, new Vector4(0.2f, 0.3f, 0.4f, 0), new Vector4(0.2f, 0.3f, 0.4f, 0) },
        };

        public static object[][] LutBToAConversionTestData =
        {
            new object[] { lutBtoA_SingleCurve, new Vector4(0.2f, 0.3f, 0.4f, 0), new Vector4(0.2f, 0.3f, 0.4f, 0) },
        };
    }
}
