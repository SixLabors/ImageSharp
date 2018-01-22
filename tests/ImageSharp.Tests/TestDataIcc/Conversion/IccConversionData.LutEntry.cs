// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.ImageSharp.MetaData.Profiles.Icc;

namespace SixLabors.ImageSharp.Tests.Colorspaces.Icc
{
    public class IccConversionDataLutEntry
    {
        private static IccLut Lut256 = CreateLut(256);
        private static IccLut Lut32 = CreateLut(32);
        private static IccLut LutIdentity = CreateIdentityLut(0, 1);

        private static IccLut8TagDataEntry Lut8 = new IccLut8TagDataEntry(
            new IccLut[] { Lut256, Lut256, Lut256 },
            IccConversionDataClut.Clut3x2,
            new IccLut[] { Lut256, Lut256 });

        private static IccLut16TagDataEntry Lut16 = new IccLut16TagDataEntry(
            new IccLut[] { Lut32, Lut32, Lut32 },
            IccConversionDataClut.Clut3x2,
            new IccLut[] { LutIdentity, LutIdentity });

        private static IccLut8TagDataEntry Lut8Matrix = new IccLut8TagDataEntry(
            IccConversionDataMatrix.Matrix,
            new IccLut[] { Lut256, Lut256, Lut256 },
            IccConversionDataClut.Clut3x2,
            new IccLut[] { Lut256, Lut256 });

        private static IccLut16TagDataEntry Lut16Matrix = new IccLut16TagDataEntry(
            IccConversionDataMatrix.Matrix,
            new IccLut[] { Lut32, Lut32, Lut32 },
            IccConversionDataClut.Clut3x2,
            new IccLut[] { LutIdentity, LutIdentity });

        private static IccLut CreateLut(int length)
        {
            float[] values = new float[length];
            for (int i = 0; i < values.Length; i++)
            {
                values[i] = 0.1f + i / (float)length;
            }
            return new IccLut(values);
        }

        private static IccLut CreateIdentityLut(float min, float max)
        {
            return new IccLut(new float[] { min, max });
        }

        public static object[][] Lut8ConversionTestData =
        {
            new object[] { Lut8, new Vector4(0.2f, 0.3f, 0.4f, 0), new Vector4(0, 0, 0, 0) },
            new object[] { Lut8Matrix, new Vector4(0.21f, 0.31f, 0.41f, 0), new Vector4(0, 0, 0, 0) },
        };

        public static object[][] Lut16ConversionTestData =
        {
            new object[] { Lut16, new Vector4(0.2f, 0.3f, 0.4f, 0), new Vector4(0, 0, 0, 0) },
            new object[] { Lut16Matrix, new Vector4(0.21f, 0.31f, 0.41f, 0), new Vector4(0, 0, 0, 0) },
        };
    }
}
