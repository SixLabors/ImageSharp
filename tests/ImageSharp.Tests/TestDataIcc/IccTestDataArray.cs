// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Tests
{
    internal static class IccTestDataArray
    {
        public static readonly byte[] UInt8 = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };

        public static readonly object[][] UInt8TestData =
        {
            new object[] { UInt8, UInt8 }
        };

        public static readonly ushort[] UInt16_Val = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };

        public static readonly byte[] UInt16_Arr = ArrayHelper.Concat(
            IccTestDataPrimitives.UInt16_0,
            IccTestDataPrimitives.UInt16_1,
            IccTestDataPrimitives.UInt16_2,
            IccTestDataPrimitives.UInt16_3,
            IccTestDataPrimitives.UInt16_4,
            IccTestDataPrimitives.UInt16_5,
            IccTestDataPrimitives.UInt16_6,
            IccTestDataPrimitives.UInt16_7,
            IccTestDataPrimitives.UInt16_8,
            IccTestDataPrimitives.UInt16_9);

        public static readonly object[][] UInt16TestData =
        {
            new object[] { UInt16_Arr, UInt16_Val }
        };

        public static readonly short[] Int16_Val = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };

        public static readonly byte[] Int16_Arr = ArrayHelper.Concat(
            IccTestDataPrimitives.Int16_0,
            IccTestDataPrimitives.Int16_1,
            IccTestDataPrimitives.Int16_2,
            IccTestDataPrimitives.Int16_3,
            IccTestDataPrimitives.Int16_4,
            IccTestDataPrimitives.Int16_5,
            IccTestDataPrimitives.Int16_6,
            IccTestDataPrimitives.Int16_7,
            IccTestDataPrimitives.Int16_8,
            IccTestDataPrimitives.Int16_9);

        public static readonly object[][] Int16TestData =
        {
            new object[] { Int16_Arr, Int16_Val }
        };

        public static readonly uint[] UInt32_Val = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };

        public static readonly byte[] UInt32_Arr = ArrayHelper.Concat(
            IccTestDataPrimitives.UInt32_0,
            IccTestDataPrimitives.UInt32_1,
            IccTestDataPrimitives.UInt32_2,
            IccTestDataPrimitives.UInt32_3,
            IccTestDataPrimitives.UInt32_4,
            IccTestDataPrimitives.UInt32_5,
            IccTestDataPrimitives.UInt32_6,
            IccTestDataPrimitives.UInt32_7,
            IccTestDataPrimitives.UInt32_8,
            IccTestDataPrimitives.UInt32_9);

        public static readonly object[][] UInt32TestData =
        {
            new object[] { UInt32_Arr, UInt32_Val }
        };

        public static readonly int[] Int32_Val = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };

        public static readonly byte[] Int32_Arr = ArrayHelper.Concat(
            IccTestDataPrimitives.Int32_0,
            IccTestDataPrimitives.Int32_1,
            IccTestDataPrimitives.Int32_2,
            IccTestDataPrimitives.Int32_3,
            IccTestDataPrimitives.Int32_4,
            IccTestDataPrimitives.Int32_5,
            IccTestDataPrimitives.Int32_6,
            IccTestDataPrimitives.Int32_7,
            IccTestDataPrimitives.Int32_8,
            IccTestDataPrimitives.Int32_9);

        public static readonly object[][] Int32TestData =
        {
            new object[] { Int32_Arr, Int32_Val }
        };

        public static readonly ulong[] UInt64_Val = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };

        public static readonly byte[] UInt64_Arr = ArrayHelper.Concat(
            IccTestDataPrimitives.UInt64_0,
            IccTestDataPrimitives.UInt64_1,
            IccTestDataPrimitives.UInt64_2,
            IccTestDataPrimitives.UInt64_3,
            IccTestDataPrimitives.UInt64_4,
            IccTestDataPrimitives.UInt64_5,
            IccTestDataPrimitives.UInt64_6,
            IccTestDataPrimitives.UInt64_7,
            IccTestDataPrimitives.UInt64_8,
            IccTestDataPrimitives.UInt64_9);

        public static readonly object[][] UInt64TestData =
        {
            new object[] { UInt64_Arr, UInt64_Val }
        };
    }
}
