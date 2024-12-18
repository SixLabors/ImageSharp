// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;

namespace SixLabors.ImageSharp.Tests.TestDataIcc.Conversion;

public class IccConversionDataLutEntry
{
    private static readonly IccLut Lut256 = CreateLut(256);
    private static readonly IccLut Lut32 = CreateLut(32);
    private static readonly IccLut LutIdentity = CreateIdentityLut(0, 1);

    private static readonly IccLut8TagDataEntry Lut8 = new(
        new[] { Lut256, Lut256 },
        IccConversionDataClut.Clut2x1,
        new[] { Lut256 });

    private static readonly IccLut16TagDataEntry Lut16 = new(
        new[] { Lut32, Lut32 },
        IccConversionDataClut.Clut2x1,
        new[] { LutIdentity });

    private static readonly IccLut8TagDataEntry Lut8Matrix = new(
        IccConversionDataMatrix.Matrix3x3Random,
        new[] { Lut256, Lut256, Lut256 },
        IccConversionDataClut.Clut3x1,
        new[] { Lut256 });

    private static readonly IccLut16TagDataEntry Lut16Matrix = new(
        IccConversionDataMatrix.Matrix3x3Random,
        new[] { Lut32, Lut32, Lut32 },
        IccConversionDataClut.Clut3x1,
        new[] { LutIdentity });

    private static IccLut CreateLut(int length)
    {
        float[] values = new float[length];
        for (int i = 0; i < values.Length; i++)
        {
            values[i] = 0.1f + (i / (float)length);
        }

        return new IccLut(values);
    }

    private static IccLut CreateIdentityLut(float min, float max) => new(new[] { min, max });

    public static object[][] Lut8ConversionTestData =
    {
        new object[] { Lut8, new Vector4(0.2f, 0.3f, 0, 0), new Vector4(0.4578933760499877f, 0, 0, 0) },
        new object[] { Lut8Matrix, new Vector4(0.21f, 0.31f, 0.41f, 0), new Vector4(0.40099657491875312f, 0, 0, 0) },
    };

    public static object[][] Lut16ConversionTestData =
    {
        new object[] { Lut16, new Vector4(0.2f, 0.3f, 0, 0), new Vector4(0.3543750030529918f, 0, 0, 0) },
        new object[] { Lut16Matrix, new Vector4(0.21f, 0.31f, 0.41f, 0), new Vector4(0.29739562389689594f, 0, 0, 0) },
    };
}
