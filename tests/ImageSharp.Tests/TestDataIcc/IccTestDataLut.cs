// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Metadata.Profiles.Icc;

namespace SixLabors.ImageSharp.Tests.TestDataIcc;

internal static class IccTestDataLut
{
    public static readonly IccLut Lut8ValGrad = CreateLut8Val();
    public static readonly byte[] Lut8Grad = CreateLut8();

    private static IccLut CreateLut8Val()
    {
        float[] result = new float[256];
        for (int i = 0; i < 256; i++)
        {
            result[i] = i / 255f;
        }

        return new IccLut(result);
    }

    private static byte[] CreateLut8()
    {
        byte[] result = new byte[256];
        for (int i = 0; i < 256; i++)
        {
            result[i] = (byte)i;
        }

        return result;
    }

    public static readonly object[][] Lut8TestData =
    {
        new object[] { Lut8Grad, Lut8ValGrad },
    };

    public static readonly IccLut Lut16ValGrad = new(new[]
    {
        1f / ushort.MaxValue,
        2f / ushort.MaxValue,
        3f / ushort.MaxValue,
        4f / ushort.MaxValue,
        5f / ushort.MaxValue,
        6f / ushort.MaxValue,
        7f / ushort.MaxValue,
        8f / ushort.MaxValue,
        9f / ushort.MaxValue,
        32768f / ushort.MaxValue,
        1f
    });

    public static readonly byte[] Lut16Grad = ArrayHelper.Concat(
        IccTestDataPrimitives.UInt161,
        IccTestDataPrimitives.UInt162,
        IccTestDataPrimitives.UInt163,
        IccTestDataPrimitives.UInt164,
        IccTestDataPrimitives.UInt165,
        IccTestDataPrimitives.UInt166,
        IccTestDataPrimitives.UInt167,
        IccTestDataPrimitives.UInt168,
        IccTestDataPrimitives.UInt169,
        IccTestDataPrimitives.UInt1632768,
        IccTestDataPrimitives.UInt16Max);

    public static readonly object[][] Lut16TestData =
    {
        new object[] { Lut16Grad, Lut16ValGrad, 11 },
    };

    public static readonly IccClut Clut8ValGrad = new(
        new[]
        {
            1f / byte.MaxValue, 2f / byte.MaxValue, 3f / byte.MaxValue,
            4f / byte.MaxValue, 5f / byte.MaxValue, 6f / byte.MaxValue,
            7f / byte.MaxValue, 8f / byte.MaxValue, 9f / byte.MaxValue,

            10f / byte.MaxValue, 11f / byte.MaxValue, 12f / byte.MaxValue,
            13f / byte.MaxValue, 14f / byte.MaxValue, 15f / byte.MaxValue,
            16f / byte.MaxValue, 17f / byte.MaxValue, 18f / byte.MaxValue,

            19f / byte.MaxValue, 20f / byte.MaxValue, 21f / byte.MaxValue,
            22f / byte.MaxValue, 23f / byte.MaxValue, 24f / byte.MaxValue,
            25f / byte.MaxValue, 26f / byte.MaxValue, 27f / byte.MaxValue,
        },
        new byte[] { 3, 3 },
        IccClutDataType.UInt8,
        outputChannelCount: 3);

    /// <summary>
    /// <para>Input Channel Count: 2</para>
    /// <para>Output Channel Count: 3</para>
    /// <para>Grid-point Count: { 3, 3 }</para>
    /// </summary>
    public static readonly byte[] Clut8Grad =
    {
        0x01, 0x02, 0x03,
        0x04, 0x05, 0x06,
        0x07, 0x08, 0x09,

        0x0A, 0x0B, 0x0C,
        0x0D, 0x0E, 0x0F,
        0x10, 0x11, 0x12,

        0x13, 0x14, 0x15,
        0x16, 0x17, 0x18,
        0x19, 0x1A, 0x1B,
    };

    public static readonly object[][] Clut8TestData =
    {
        new object[] { Clut8Grad, Clut8ValGrad, 2, 3, new byte[] { 3, 3 } },
    };

    public static readonly IccClut Clut16ValGrad = new(
        new[]
        {
            1f / ushort.MaxValue, 2f / ushort.MaxValue, 3f / ushort.MaxValue,
            4f / ushort.MaxValue, 5f / ushort.MaxValue, 6f / ushort.MaxValue,
            7f / ushort.MaxValue, 8f / ushort.MaxValue, 9f / ushort.MaxValue,

            10f / ushort.MaxValue, 11f / ushort.MaxValue, 12f / ushort.MaxValue,
            13f / ushort.MaxValue, 14f / ushort.MaxValue, 15f / ushort.MaxValue,
            16f / ushort.MaxValue, 17f / ushort.MaxValue, 18f / ushort.MaxValue,

            19f / ushort.MaxValue, 20f / ushort.MaxValue, 21f / ushort.MaxValue,
            22f / ushort.MaxValue, 23f / ushort.MaxValue, 24f / ushort.MaxValue,
            25f / ushort.MaxValue, 26f / ushort.MaxValue, 27f / ushort.MaxValue,
        },
        new byte[] { 3, 3 },
        IccClutDataType.UInt16,
        outputChannelCount: 3);

    /// <summary>
    /// <para>Input Channel Count: 2</para>
    /// <para>Output Channel Count: 3</para>
    /// <para>Grid-point Count: { 3, 3 }</para>
    /// </summary>
    public static readonly byte[] Clut16Grad =
    {
        0x00, 0x01, 0x00, 0x02, 0x00, 0x03,
        0x00, 0x04, 0x00, 0x05, 0x00, 0x06,
        0x00, 0x07, 0x00, 0x08, 0x00, 0x09,

        0x00, 0x0A, 0x00, 0x0B, 0x00, 0x0C,
        0x00, 0x0D, 0x00, 0x0E, 0x00, 0x0F,
        0x00, 0x10, 0x00, 0x11, 0x00, 0x12,

        0x00, 0x13, 0x00, 0x14, 0x00, 0x15,
        0x00, 0x16, 0x00, 0x17, 0x00, 0x18,
        0x00, 0x19, 0x00, 0x1A, 0x00, 0x1B,
    };

    public static readonly object[][] Clut16TestData =
    {
        new object[] { Clut16Grad, Clut16ValGrad, 2, 3, new byte[] { 3, 3 } },
    };

    public static readonly IccClut CluTf32ValGrad = new(
        new[]
        {
            1f, 2f, 3f,
            4f, 5f, 6f,
            7f, 8f, 9f,

            1f, 2f, 3f,
            4f, 5f, 6f,
            7f, 8f, 9f,

            1f, 2f, 3f,
            4f, 5f, 6f,
            7f, 8f, 9f,
        },
        new byte[] { 3, 3 },
        IccClutDataType.Float,
        outputChannelCount: 3);

    /// <summary>
    /// <para>Input Channel Count: 2</para>
    /// <para>Output Channel Count: 3</para>
    /// <para>Grid-point Count: { 3, 3 }</para>
    /// </summary>
    public static readonly byte[] CluTf32Grad = ArrayHelper.Concat(
        IccTestDataPrimitives.Single1,
        IccTestDataPrimitives.Single2,
        IccTestDataPrimitives.Single3,
        IccTestDataPrimitives.Single4,
        IccTestDataPrimitives.Single5,
        IccTestDataPrimitives.Single6,
        IccTestDataPrimitives.Single7,
        IccTestDataPrimitives.Single8,
        IccTestDataPrimitives.Single9,
        IccTestDataPrimitives.Single1,
        IccTestDataPrimitives.Single2,
        IccTestDataPrimitives.Single3,
        IccTestDataPrimitives.Single4,
        IccTestDataPrimitives.Single5,
        IccTestDataPrimitives.Single6,
        IccTestDataPrimitives.Single7,
        IccTestDataPrimitives.Single8,
        IccTestDataPrimitives.Single9,
        IccTestDataPrimitives.Single1,
        IccTestDataPrimitives.Single2,
        IccTestDataPrimitives.Single3,
        IccTestDataPrimitives.Single4,
        IccTestDataPrimitives.Single5,
        IccTestDataPrimitives.Single6,
        IccTestDataPrimitives.Single7,
        IccTestDataPrimitives.Single8,
        IccTestDataPrimitives.Single9);

    public static readonly object[][] ClutF32TestData =
    {
        new object[] { CluTf32Grad, CluTf32ValGrad, 2, 3, new byte[] { 3, 3 } },
    };

    public static readonly IccClut ClutVal8 = Clut8ValGrad;
    public static readonly IccClut ClutVal16 = Clut16ValGrad;
    public static readonly IccClut ClutValf32 = CluTf32ValGrad;

    public static readonly byte[] Clut8 = ArrayHelper.Concat(
        new byte[16] { 0x03, 0x03, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 },
        new byte[4] { 0x01, 0x00, 0x00, 0x00 },
        Clut8Grad);

    public static readonly byte[] Clut16 = ArrayHelper.Concat(
        new byte[16] { 0x03, 0x03, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 },
        new byte[4] { 0x02, 0x00, 0x00, 0x00 },
        Clut16Grad);

    public static readonly byte[] ClutF32 = ArrayHelper.Concat(
        new byte[16] { 0x03, 0x03, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 },
        CluTf32Grad);

    public static readonly object[][] ClutTestData =
    {
        new object[] { Clut8, ClutVal8, 2, 3, false },
        new object[] { Clut16, ClutVal16, 2, 3, false },
        new object[] { ClutF32, ClutValf32, 2, 3, true },
    };
}
