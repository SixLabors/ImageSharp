// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Metadata.Profiles.Icc;

namespace SixLabors.ImageSharp.Tests.Colorspaces.Icc
{
    public class IccConversionDataMultiProcessElement
    {
        private static IccMatrixProcessElement Matrix = new IccMatrixProcessElement(new float[,]
        {
            { 2, 4, 6 },
            { 3, 5, 7 },
        }, new float[] { 3, 4, 5 });

        private static IccClut Clut = new IccClut(new float[][]
        {
            new float[] { 0.2f, 0.3f },
            new float[] { 0.4f, 0.5f },

            new float[] { 0.21f, 0.31f },
            new float[] { 0.41f, 0.51f },

            new float[] { 0.22f, 0.32f },
            new float[] { 0.42f, 0.52f },

            new float[] { 0.23f, 0.33f },
            new float[] { 0.43f, 0.53f },
        }, new byte[] { 2, 2, 2 }, IccClutDataType.Float);

        private static IccFormulaCurveElement FormulaCurveElement1 = new IccFormulaCurveElement(IccFormulaCurveType.Type1, 2.2f, 0.7f, 0.2f, 0.3f, 0, 0);
        private static IccFormulaCurveElement FormulaCurveElement2 = new IccFormulaCurveElement(IccFormulaCurveType.Type2, 2.2f, 0.9f, 0.9f, 0.02f, 0.1f, 0);
        private static IccFormulaCurveElement FormulaCurveElement3 = new IccFormulaCurveElement(IccFormulaCurveType.Type3, 0, 0.9f, 0.9f, 1.02f, 0.1f, 0.02f);

        private static IccCurveSetProcessElement CurveSet1DFormula1 = Create1DSingleCurveSet(FormulaCurveElement1);
        private static IccCurveSetProcessElement CurveSet1DFormula2 = Create1DSingleCurveSet(FormulaCurveElement2);
        private static IccCurveSetProcessElement CurveSet1DFormula3 = Create1DSingleCurveSet(FormulaCurveElement3);

        private static IccCurveSetProcessElement CurveSet1DFormula1And2 = Create1DMultiCurveSet(new float[] { 0.5f }, FormulaCurveElement1, FormulaCurveElement2);

        private static IccClutProcessElement ClutElement = new IccClutProcessElement(Clut);

        private static IccCurveSetProcessElement Create1DSingleCurveSet(IccCurveSegment segment)
        {
            var curve = new IccOneDimensionalCurve(new float[0], new IccCurveSegment[] { segment });
            return new IccCurveSetProcessElement(new IccOneDimensionalCurve[] { curve });
        }

        private static IccCurveSetProcessElement Create1DMultiCurveSet(float[] breakPoints, params IccCurveSegment[] segments)
        {
            var curve = new IccOneDimensionalCurve(breakPoints, segments);
            return new IccCurveSetProcessElement(new IccOneDimensionalCurve[] { curve });
        }


        public static object[][] MpeCurveConversionTestData =
        {
            new object[] { CurveSet1DFormula1, new float[] { 0.51f }, new float[] { 0.575982451f } },
            new object[] { CurveSet1DFormula2, new float[] { 0.52f }, new float[] { -0.4684991f } },
            new object[] { CurveSet1DFormula3, new float[] { 0.53f }, new float[] { 0.86126f } },

            new object[] { CurveSet1DFormula1And2, new float[] { 0.31f }, new float[] { 0.445982f } },
            new object[] { CurveSet1DFormula1And2, new float[] { 0.61f }, new float[] { -0.341274023f } },
        };

        public static object[][] MpeMatrixConversionTestData =
        {
            new object[] { Matrix, new float[] { 2, 4 }, new float[] { 19, 32, 45 } }
        };

        public static object[][] MpeClutConversionTestData =
        {
            new object[] { ClutElement, new float[] { 0.5f, 0.5f, 0.5f }, new float[] { 0.5f, 0.5f } }
        };
    }
}
