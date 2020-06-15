// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Metadata.Profiles.Icc;

namespace SixLabors.ImageSharp.Tests
{
    internal static class IccTestDataLut
    {
        public static readonly IccLut LUT8_ValGrad = CreateLUT8Val();
        public static readonly byte[] LUT8_Grad = CreateLUT8();

        private static IccLut CreateLUT8Val()
        {
            float[] result = new float[256];
            for (int i = 0; i < 256; i++)
            {
                result[i] = i / 255f;
            }

            return new IccLut(result);
        }

        private static byte[] CreateLUT8()
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
            new object[] { LUT8_Grad, LUT8_ValGrad },
        };

        public static readonly IccLut LUT16_ValGrad = new IccLut(new float[]
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

        public static readonly byte[] LUT16_Grad = ArrayHelper.Concat(
            IccTestDataPrimitives.UInt16_1,
            IccTestDataPrimitives.UInt16_2,
            IccTestDataPrimitives.UInt16_3,
            IccTestDataPrimitives.UInt16_4,
            IccTestDataPrimitives.UInt16_5,
            IccTestDataPrimitives.UInt16_6,
            IccTestDataPrimitives.UInt16_7,
            IccTestDataPrimitives.UInt16_8,
            IccTestDataPrimitives.UInt16_9,
            IccTestDataPrimitives.UInt16_32768,
            IccTestDataPrimitives.UInt16_Max);

        public static readonly object[][] Lut16TestData =
        {
            new object[] { LUT16_Grad, LUT16_ValGrad, 11 },
        };

        public static readonly IccClut CLUT8_ValGrad = new IccClut(
            new float[][]
            {
                new float[] { 1f / byte.MaxValue, 2f / byte.MaxValue, 3f / byte.MaxValue },
                new float[] { 4f / byte.MaxValue, 5f / byte.MaxValue, 6f / byte.MaxValue },
                new float[] { 7f / byte.MaxValue, 8f / byte.MaxValue, 9f / byte.MaxValue },

                new float[] { 10f / byte.MaxValue, 11f / byte.MaxValue, 12f / byte.MaxValue },
                new float[] { 13f / byte.MaxValue, 14f / byte.MaxValue, 15f / byte.MaxValue },
                new float[] { 16f / byte.MaxValue, 17f / byte.MaxValue, 18f / byte.MaxValue },

                new float[] { 19f / byte.MaxValue, 20f / byte.MaxValue, 21f / byte.MaxValue },
                new float[] { 22f / byte.MaxValue, 23f / byte.MaxValue, 24f / byte.MaxValue },
                new float[] { 25f / byte.MaxValue, 26f / byte.MaxValue, 27f / byte.MaxValue },
            },
            new byte[] { 3, 3 },
            IccClutDataType.UInt8);

        /// <summary>
        /// <para>Input Channel Count: 2</para>
        /// <para>Output Channel Count: 3</para>
        /// <para>Grid-point Count: { 3, 3 }</para>
        /// </summary>
        public static readonly byte[] CLUT8_Grad =
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
            new object[] { CLUT8_Grad, CLUT8_ValGrad, 2, 3, new byte[] { 3, 3 } },
        };

        public static readonly IccClut CLUT16_ValGrad = new IccClut(
            new float[][]
            {
                new float[] { 1f / ushort.MaxValue, 2f / ushort.MaxValue, 3f / ushort.MaxValue },
                new float[] { 4f / ushort.MaxValue, 5f / ushort.MaxValue, 6f / ushort.MaxValue },
                new float[] { 7f / ushort.MaxValue, 8f / ushort.MaxValue, 9f / ushort.MaxValue },

                new float[] { 10f / ushort.MaxValue, 11f / ushort.MaxValue, 12f / ushort.MaxValue },
                new float[] { 13f / ushort.MaxValue, 14f / ushort.MaxValue, 15f / ushort.MaxValue },
                new float[] { 16f / ushort.MaxValue, 17f / ushort.MaxValue, 18f / ushort.MaxValue },

                new float[] { 19f / ushort.MaxValue, 20f / ushort.MaxValue, 21f / ushort.MaxValue },
                new float[] { 22f / ushort.MaxValue, 23f / ushort.MaxValue, 24f / ushort.MaxValue },
                new float[] { 25f / ushort.MaxValue, 26f / ushort.MaxValue, 27f / ushort.MaxValue },
            },
            new byte[] { 3, 3 },
            IccClutDataType.UInt16);

        /// <summary>
        /// <para>Input Channel Count: 2</para>
        /// <para>Output Channel Count: 3</para>
        /// <para>Grid-point Count: { 3, 3 }</para>
        /// </summary>
        public static readonly byte[] CLUT16_Grad =
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
            new object[] { CLUT16_Grad, CLUT16_ValGrad, 2, 3, new byte[] { 3, 3 } },
        };

        public static readonly IccClut CLUTf32_ValGrad = new IccClut(
            new float[][]
            {
                new float[] { 1f, 2f, 3f },
                new float[] { 4f, 5f, 6f },
                new float[] { 7f, 8f, 9f },

                new float[] { 1f, 2f, 3f },
                new float[] { 4f, 5f, 6f },
                new float[] { 7f, 8f, 9f },

                new float[] { 1f, 2f, 3f },
                new float[] { 4f, 5f, 6f },
                new float[] { 7f, 8f, 9f },
            },
            new byte[] { 3, 3 },
            IccClutDataType.Float);

        /// <summary>
        /// <para>Input Channel Count: 2</para>
        /// <para>Output Channel Count: 3</para>
        /// <para>Grid-point Count: { 3, 3 }</para>
        /// </summary>
        public static readonly byte[] CLUTf32_Grad = ArrayHelper.Concat(
            IccTestDataPrimitives.Single_1,
            IccTestDataPrimitives.Single_2,
            IccTestDataPrimitives.Single_3,
            IccTestDataPrimitives.Single_4,
            IccTestDataPrimitives.Single_5,
            IccTestDataPrimitives.Single_6,
            IccTestDataPrimitives.Single_7,
            IccTestDataPrimitives.Single_8,
            IccTestDataPrimitives.Single_9,
            IccTestDataPrimitives.Single_1,
            IccTestDataPrimitives.Single_2,
            IccTestDataPrimitives.Single_3,
            IccTestDataPrimitives.Single_4,
            IccTestDataPrimitives.Single_5,
            IccTestDataPrimitives.Single_6,
            IccTestDataPrimitives.Single_7,
            IccTestDataPrimitives.Single_8,
            IccTestDataPrimitives.Single_9,
            IccTestDataPrimitives.Single_1,
            IccTestDataPrimitives.Single_2,
            IccTestDataPrimitives.Single_3,
            IccTestDataPrimitives.Single_4,
            IccTestDataPrimitives.Single_5,
            IccTestDataPrimitives.Single_6,
            IccTestDataPrimitives.Single_7,
            IccTestDataPrimitives.Single_8,
            IccTestDataPrimitives.Single_9);

        public static readonly object[][] ClutF32TestData =
        {
            new object[] { CLUTf32_Grad, CLUTf32_ValGrad, 2, 3, new byte[] { 3, 3 } },
        };

        public static readonly IccClut CLUT_Val8 = CLUT8_ValGrad;
        public static readonly IccClut CLUT_Val16 = CLUT16_ValGrad;
        public static readonly IccClut CLUT_Valf32 = CLUTf32_ValGrad;

        public static readonly byte[] CLUT_8 = ArrayHelper.Concat(
            new byte[16] { 0x03, 0x03, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 },
            new byte[4] { 0x01, 0x00, 0x00, 0x00 },
            CLUT8_Grad);

        public static readonly byte[] CLUT_16 = ArrayHelper.Concat(
            new byte[16] { 0x03, 0x03, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 },
            new byte[4] { 0x02, 0x00, 0x00, 0x00 },
            CLUT16_Grad);

        public static readonly byte[] CLUT_f32 = ArrayHelper.Concat(
            new byte[16] { 0x03, 0x03, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 },
            CLUTf32_Grad);

        public static readonly object[][] ClutTestData =
        {
            new object[] { CLUT_8, CLUT_Val8, 2, 3, false },
            new object[] { CLUT_16, CLUT_Val16, 2, 3, false },
            new object[] { CLUT_f32, CLUT_Valf32, 2, 3, true },
        };
    }
}
