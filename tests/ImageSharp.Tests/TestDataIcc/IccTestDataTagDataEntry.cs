// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Globalization;
using System.Numerics;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;

namespace SixLabors.ImageSharp.Tests
{
    internal static class IccTestDataTagDataEntry
    {
        public static readonly IccTypeSignature TagDataEntryHeader_UnknownVal = IccTypeSignature.Unknown;
        public static readonly byte[] TagDataEntryHeader_UnknownArr =
        {
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
        };

        public static readonly IccTypeSignature TagDataEntryHeader_MultiLocalizedUnicodeVal = IccTypeSignature.MultiLocalizedUnicode;
        public static readonly byte[] TagDataEntryHeader_MultiLocalizedUnicodeArr =
        {
            0x6D, 0x6C, 0x75, 0x63,
            0x00, 0x00, 0x00, 0x00,
        };

        public static readonly IccTypeSignature TagDataEntryHeader_CurveVal = IccTypeSignature.Curve;
        public static readonly byte[] TagDataEntryHeader_CurveArr =
        {
            0x63, 0x75, 0x72, 0x76,
            0x00, 0x00, 0x00, 0x00,
        };

        public static readonly object[][] TagDataEntryHeaderTestData =
        {
            new object[] { TagDataEntryHeader_UnknownArr, TagDataEntryHeader_UnknownVal },
            new object[] { TagDataEntryHeader_MultiLocalizedUnicodeArr, TagDataEntryHeader_MultiLocalizedUnicodeVal },
            new object[] { TagDataEntryHeader_CurveArr, TagDataEntryHeader_CurveVal },
        };

        public static readonly IccUnknownTagDataEntry Unknown_Val = new IccUnknownTagDataEntry(new byte[] { 0x00, 0x01, 0x02, 0x03 });
        public static readonly byte[] Unknown_Arr = { 0x00, 0x01, 0x02, 0x03 };

        public static readonly object[][] UnknownTagDataEntryTestData =
        {
            new object[] { Unknown_Arr, Unknown_Val, 12u },
        };

        public static readonly IccChromaticityTagDataEntry Chromaticity_Val1 = new IccChromaticityTagDataEntry(IccColorantEncoding.ItuRBt709_2);
        public static readonly byte[] Chromaticity_Arr1 = ArrayHelper.Concat(
            IccTestDataPrimitives.UInt16_3,
            IccTestDataPrimitives.UInt16_1,
            new byte[] { 0x00, 0x00, 0xA3, 0xD7 },  // 0.640
            new byte[] { 0x00, 0x00, 0x54, 0x7B },  // 0.330
            new byte[] { 0x00, 0x00, 0x4C, 0xCD },  // 0.300
            new byte[] { 0x00, 0x00, 0x99, 0x9A },  // 0.600
            new byte[] { 0x00, 0x00, 0x26, 0x66 },  // 0.150
            new byte[] { 0x00, 0x00, 0x0F, 0x5C }); // 0.060

        public static readonly IccChromaticityTagDataEntry Chromaticity_Val2 = new IccChromaticityTagDataEntry(
            new double[][]
            {
                new double[] { 1, 2 },
                new double[] { 3, 4 },
            });

        public static readonly byte[] Chromaticity_Arr2 = ArrayHelper.Concat(
            IccTestDataPrimitives.UInt16_2,
            IccTestDataPrimitives.UInt16_0,
            IccTestDataPrimitives.UFix16_1,
            IccTestDataPrimitives.UFix16_2,
            IccTestDataPrimitives.UFix16_3,
            IccTestDataPrimitives.UFix16_4);

        /// <summary>
        /// <see cref="InvalidIccProfileException"/>: channel count must be 3 for any enum other than <see cref="ColorantEncoding.Unknown"/>
        /// </summary>
        public static readonly byte[] Chromaticity_ArrInvalid1 = ArrayHelper.Concat(
            IccTestDataPrimitives.UInt16_5,
            IccTestDataPrimitives.UInt16_1);

        /// <summary>
        /// <see cref="InvalidIccProfileException"/>: invalid enum value
        /// </summary>
        public static readonly byte[] Chromaticity_ArrInvalid2 = ArrayHelper.Concat(
            IccTestDataPrimitives.UInt16_3,
            IccTestDataPrimitives.UInt16_9);

        public static readonly object[][] ChromaticityTagDataEntryTestData =
        {
            new object[] { Chromaticity_Arr1, Chromaticity_Val1 },
            new object[] { Chromaticity_Arr2, Chromaticity_Val2 },
        };

        public static readonly IccColorantOrderTagDataEntry ColorantOrder_Val = new IccColorantOrderTagDataEntry(new byte[] { 0x00, 0x01, 0x02 });
        public static readonly byte[] ColorantOrder_Arr = ArrayHelper.Concat(IccTestDataPrimitives.UInt32_3, new byte[] { 0x00, 0x01, 0x02 });

        public static readonly object[][] ColorantOrderTagDataEntryTestData =
        {
            new object[] { ColorantOrder_Arr, ColorantOrder_Val },
        };

        public static readonly IccColorantTableTagDataEntry ColorantTable_Val = new IccColorantTableTagDataEntry(
            new IccColorantTableEntry[]
            {
                IccTestDataNonPrimitives.ColorantTableEntry_ValRand1,
                IccTestDataNonPrimitives.ColorantTableEntry_ValRand2
            });

        public static readonly byte[] ColorantTable_Arr = ArrayHelper.Concat(
            IccTestDataPrimitives.UInt32_2,
            IccTestDataNonPrimitives.ColorantTableEntry_Rand1,
            IccTestDataNonPrimitives.ColorantTableEntry_Rand2);

        public static readonly object[][] ColorantTableTagDataEntryTestData =
        {
            new object[] { ColorantTable_Arr, ColorantTable_Val },
        };

        public static readonly IccCurveTagDataEntry Curve_Val_0 = new IccCurveTagDataEntry();
        public static readonly byte[] Curve_Arr_0 = IccTestDataPrimitives.UInt32_0;

        public static readonly IccCurveTagDataEntry Curve_Val_1 = new IccCurveTagDataEntry(1f);
        public static readonly byte[] Curve_Arr_1 = ArrayHelper.Concat(
            IccTestDataPrimitives.UInt32_1,
            IccTestDataPrimitives.UFix8_1);

        public static readonly IccCurveTagDataEntry Curve_Val_2 = new IccCurveTagDataEntry(new float[] { 1 / 65535f, 2 / 65535f, 3 / 65535f });
        public static readonly byte[] Curve_Arr_2 = ArrayHelper.Concat(
            IccTestDataPrimitives.UInt32_3,
            IccTestDataPrimitives.UInt16_1,
            IccTestDataPrimitives.UInt16_2,
            IccTestDataPrimitives.UInt16_3);

        public static readonly object[][] CurveTagDataEntryTestData =
        {
            new object[] { Curve_Arr_0, Curve_Val_0 },
            new object[] { Curve_Arr_1, Curve_Val_1 },
            new object[] { Curve_Arr_2, Curve_Val_2 },
        };

        public static readonly IccDataTagDataEntry Data_ValNoASCII = new IccDataTagDataEntry(
            new byte[] { 0x01, 0x02, 0x03, 0x04 },
            false);

        public static readonly byte[] Data_ArrNoASCII =
        {
            0x00, 0x00, 0x00, 0x00,
            0x01, 0x02, 0x03, 0x04
        };

        public static readonly IccDataTagDataEntry Data_ValASCII = new IccDataTagDataEntry(
            new byte[] { (byte)'A', (byte)'S', (byte)'C', (byte)'I', (byte)'I' },
            true);

        public static readonly byte[] Data_ArrASCII =
        {
            0x00, 0x00, 0x00, 0x01,
            (byte)'A', (byte)'S', (byte)'C', (byte)'I', (byte)'I'
        };

        public static readonly object[][] DataTagDataEntryTestData =
        {
            new object[] { Data_ArrNoASCII, Data_ValNoASCII, 16u },
            new object[] { Data_ArrASCII, Data_ValASCII, 17u },
        };

        public static readonly IccDateTimeTagDataEntry DateTime_Val = new IccDateTimeTagDataEntry(IccTestDataNonPrimitives.DateTime_ValRand1);
        public static readonly byte[] DateTime_Arr = IccTestDataNonPrimitives.DateTime_Rand1;

        public static readonly object[][] DateTimeTagDataEntryTestData =
        {
            new object[] { DateTime_Arr, DateTime_Val },
        };

        public static readonly IccLut16TagDataEntry Lut16_Val = new IccLut16TagDataEntry(
            new IccLut[] { IccTestDataLut.LUT16_ValGrad, IccTestDataLut.LUT16_ValGrad },
            IccTestDataLut.CLUT16_ValGrad,
            new IccLut[] { IccTestDataLut.LUT16_ValGrad, IccTestDataLut.LUT16_ValGrad, IccTestDataLut.LUT16_ValGrad });

        public static readonly byte[] Lut16_Arr = ArrayHelper.Concat(
            new byte[] { 0x02, 0x03, 0x03, 0x00 },
            IccTestDataMatrix.Fix16_2D_Identity,
            new byte[] { 0x00, (byte)IccTestDataLut.LUT16_ValGrad.Values.Length, 0x00, (byte)IccTestDataLut.LUT16_ValGrad.Values.Length },
            IccTestDataLut.LUT16_Grad,
            IccTestDataLut.LUT16_Grad,
            IccTestDataLut.CLUT16_Grad,
            IccTestDataLut.LUT16_Grad,
            IccTestDataLut.LUT16_Grad,
            IccTestDataLut.LUT16_Grad);

        public static readonly object[][] Lut16TagDataEntryTestData =
        {
            new object[] { Lut16_Arr, Lut16_Val },
        };

        public static readonly IccLut8TagDataEntry Lut8_Val = new IccLut8TagDataEntry(
            new IccLut[] { IccTestDataLut.LUT8_ValGrad, IccTestDataLut.LUT8_ValGrad },
            IccTestDataLut.CLUT8_ValGrad,
            new IccLut[] { IccTestDataLut.LUT8_ValGrad, IccTestDataLut.LUT8_ValGrad, IccTestDataLut.LUT8_ValGrad });

        public static readonly byte[] Lut8_Arr = ArrayHelper.Concat(
            new byte[] { 0x02, 0x03, 0x03, 0x00 },
            IccTestDataMatrix.Fix16_2D_Identity,
            IccTestDataLut.LUT8_Grad,
            IccTestDataLut.LUT8_Grad,
            IccTestDataLut.CLUT8_Grad,
            IccTestDataLut.LUT8_Grad,
            IccTestDataLut.LUT8_Grad,
            IccTestDataLut.LUT8_Grad);

        public static readonly object[][] Lut8TagDataEntryTestData =
        {
            new object[] { Lut8_Arr, Lut8_Val },
        };

        private static readonly byte[] CurveFull_0 = ArrayHelper.Concat(
            TagDataEntryHeader_CurveArr,
            Curve_Arr_0);

        private static readonly byte[] CurveFull_1 = ArrayHelper.Concat(
            TagDataEntryHeader_CurveArr,
            Curve_Arr_1);

        private static readonly byte[] CurveFull_2 = ArrayHelper.Concat(
            TagDataEntryHeader_CurveArr,
            Curve_Arr_2);

        public static readonly IccLutAToBTagDataEntry LutAToB_Val = new IccLutAToBTagDataEntry(
            new IccCurveTagDataEntry[]
            {
                Curve_Val_0,
                Curve_Val_1,
                Curve_Val_2,
            },
            IccTestDataMatrix.Single_2DArray_ValGrad,
            IccTestDataMatrix.Single_1DArray_ValGrad,
            new IccCurveTagDataEntry[] { Curve_Val_1,  Curve_Val_2, Curve_Val_0 },
            IccTestDataLut.CLUT_Val16,
            new IccCurveTagDataEntry[] { Curve_Val_2, Curve_Val_1 });

#pragma warning disable SA1115 // Parameter should follow comma
        public static readonly byte[] LutAToB_Arr = ArrayHelper.Concat(
            new byte[] { 0x02, 0x03, 0x00, 0x00 },
            new byte[] { 0x00, 0x00, 0x00, 0x20 },  // b:        32
            new byte[] { 0x00, 0x00, 0x00, 0x50 },  // matrix:   80
            new byte[] { 0x00, 0x00, 0x00, 0x80 },  // m:        128
            new byte[] { 0x00, 0x00, 0x00, 0xB0 },  // clut:     176
            new byte[] { 0x00, 0x00, 0x00, 0xFC },  // a:        252

            // B
            CurveFull_0,                // 12 bytes
            CurveFull_1,                // 14 bytes
            new byte[] { 0x00, 0x00 },  // Padding
            CurveFull_2,                // 18 bytes
            new byte[] { 0x00, 0x00 },  // Padding

            // Matrix
            IccTestDataMatrix.Fix16_2D_Grad,   // 36 bytes
            IccTestDataMatrix.Fix16_1D_Grad,   // 12 bytes

            // M
            CurveFull_1,                // 14 bytes
            new byte[] { 0x00, 0x00 },  // Padding
            CurveFull_2,                // 18 bytes
            new byte[] { 0x00, 0x00 },  // Padding
            CurveFull_0,                // 12 bytes

            // CLUT
            IccTestDataLut.CLUT_16,     // 74 bytes
            new byte[] { 0x00, 0x00 },  // Padding

            // A
            CurveFull_2,                // 18 bytes
            new byte[] { 0x00, 0x00 },  // Padding
            CurveFull_1,                // 14 bytes
            new byte[] { 0x00, 0x00 }); // Padding

#pragma warning restore SA1115 // Parameter should follow comma

        public static readonly object[][] LutAToBTagDataEntryTestData =
        {
            new object[] { LutAToB_Arr, LutAToB_Val },
        };

        public static readonly IccLutBToATagDataEntry LutBToA_Val = new IccLutBToATagDataEntry(
            new[]
            {
                Curve_Val_0,
                Curve_Val_1,
            },
            null,
            null,
            null,
            IccTestDataLut.CLUT_Val16,
            new[] { Curve_Val_2, Curve_Val_1, Curve_Val_0 });

#pragma warning disable SA1115 // Parameter should follow comma
        public static readonly byte[] LutBToA_Arr = ArrayHelper.Concat(
            new byte[] { 0x02, 0x03, 0x00, 0x00 },

            new byte[] { 0x00, 0x00, 0x00, 0x20 },  // b:        32
            new byte[] { 0x00, 0x00, 0x00, 0x00 },  // matrix:   0
            new byte[] { 0x00, 0x00, 0x00, 0x00 },  // m:        0
            new byte[] { 0x00, 0x00, 0x00, 0x3C },  // clut:     60
            new byte[] { 0x00, 0x00, 0x00, 0x88 },  // a:        136

            // B
            CurveFull_0,    // 12 bytes
            CurveFull_1,    // 14 bytes
            new byte[] { 0x00, 0x00 }, // Padding

            // CLUT
            IccTestDataLut.CLUT_16,     // 74 bytes
            new byte[] { 0x00, 0x00 },  // Padding

            // A
            CurveFull_2,                // 18 bytes
            new byte[] { 0x00, 0x00 },  // Padding
            CurveFull_1,                // 14 bytes
            new byte[] { 0x00, 0x00 },  // Padding
            CurveFull_0); // 12 bytes

#pragma warning restore SA1115 // Parameter should follow comma

        public static readonly object[][] LutBToATagDataEntryTestData =
        {
            new object[] { LutBToA_Arr, LutBToA_Val },
        };

        public static readonly IccMeasurementTagDataEntry Measurement_Val = new IccMeasurementTagDataEntry(
            IccStandardObserver.Cie1931Observer,
            IccTestDataNonPrimitives.XyzNumber_ValVar1,
            IccMeasurementGeometry.Degree0ToDOrDTo0,
            1f,
            IccStandardIlluminant.D50);

        public static readonly byte[] Measurement_Arr = ArrayHelper.Concat(
            IccTestDataPrimitives.UInt32_1,
            IccTestDataNonPrimitives.XyzNumber_Var1,
            IccTestDataPrimitives.UInt32_2,
            IccTestDataPrimitives.UFix16_1,
            IccTestDataPrimitives.UInt32_1);

        public static readonly object[][] MeasurementTagDataEntryTestData =
        {
            new object[] { Measurement_Arr, Measurement_Val },
        };

        private static readonly IccLocalizedString LocalizedString_Rand_enUS = CreateLocalizedString("en", "US", IccTestDataPrimitives.Unicode_ValRand2);
        private static readonly IccLocalizedString LocalizedString_Rand_deDE = CreateLocalizedString("de", "DE", IccTestDataPrimitives.Unicode_ValRand3);
        private static readonly IccLocalizedString LocalizedString_Rand2_deDE = CreateLocalizedString("de", "DE", IccTestDataPrimitives.Unicode_ValRand2);
        private static readonly IccLocalizedString LocalizedString_Rand2_esXL = CreateLocalizedString("es", "XL", IccTestDataPrimitives.Unicode_ValRand2);
        private static readonly IccLocalizedString LocalizedString_Rand2_xyXL = CreateLocalizedString("xy", "XL", IccTestDataPrimitives.Unicode_ValRand2);
        private static readonly IccLocalizedString LocalizedString_Rand_en = CreateLocalizedString("en", null, IccTestDataPrimitives.Unicode_ValRand2);
        private static readonly IccLocalizedString LocalizedString_Rand_Invariant = new IccLocalizedString(CultureInfo.InvariantCulture, IccTestDataPrimitives.Unicode_ValRand3);

        private static IccLocalizedString CreateLocalizedString(string language, string country, string text)
        {
            CultureInfo culture;
            if (country == null)
            {
                try
                {
                    culture = new CultureInfo(language);
                }
                catch (CultureNotFoundException)
                {
                    culture = CultureInfo.InvariantCulture;
                }
            }
            else
            {
                try
                {
                    culture = new CultureInfo($"{language}-{country}");
                }
                catch (CultureNotFoundException)
                {
                    return CreateLocalizedString(language, null, text);
                }
            }

            return new IccLocalizedString(culture, text);
        }

        private static readonly IccLocalizedString[] LocalizedString_RandArr_enUS_deDE = new IccLocalizedString[]
        {
            LocalizedString_Rand_enUS,
            LocalizedString_Rand_deDE,
        };

        private static readonly IccLocalizedString[] LocalizedString_RandArr_en_Invariant = new IccLocalizedString[]
        {
            LocalizedString_Rand_en,
            LocalizedString_Rand_Invariant,
        };

        private static readonly IccLocalizedString[] LocalizedString_SameArr_enUS_deDE_esXL_xyXL = new IccLocalizedString[]
        {
            LocalizedString_Rand_enUS,
            LocalizedString_Rand2_deDE,
            LocalizedString_Rand2_esXL,
            LocalizedString_Rand2_xyXL,
        };

        public static readonly IccMultiLocalizedUnicodeTagDataEntry MultiLocalizedUnicode_Val = new IccMultiLocalizedUnicodeTagDataEntry(LocalizedString_RandArr_enUS_deDE);
        public static readonly byte[] MultiLocalizedUnicode_Arr = ArrayHelper.Concat(
            IccTestDataPrimitives.UInt32_2,
            new byte[] { 0x00, 0x00, 0x00, 0x0C },  // 12
            new byte[] { (byte)'e', (byte)'n', (byte)'U', (byte)'S' },
            new byte[] { 0x00, 0x00, 0x00, 0x0C },  // 12
            new byte[] { 0x00, 0x00, 0x00, 0x28 },  // 40
            new byte[] { (byte)'d', (byte)'e', (byte)'D', (byte)'E' },
            new byte[] { 0x00, 0x00, 0x00, 0x0E },  // 14
            new byte[] { 0x00, 0x00, 0x00, 0x34 },  // 52
            IccTestDataPrimitives.Unicode_Rand2,
            IccTestDataPrimitives.Unicode_Rand3);

        public static readonly IccMultiLocalizedUnicodeTagDataEntry MultiLocalizedUnicode_Val2 = new IccMultiLocalizedUnicodeTagDataEntry(LocalizedString_RandArr_en_Invariant);
        public static readonly byte[] MultiLocalizedUnicode_Arr2_Read = ArrayHelper.Concat(
            IccTestDataPrimitives.UInt32_2,
            new byte[] { 0x00, 0x00, 0x00, 0x0C },  // 12
            new byte[] { (byte)'e', (byte)'n', 0x00, 0x00 },
            new byte[] { 0x00, 0x00, 0x00, 0x0C },  // 12
            new byte[] { 0x00, 0x00, 0x00, 0x28 },  // 40
            new byte[] { 0x00, 0x00, 0x00, 0x00 },
            new byte[] { 0x00, 0x00, 0x00, 0x0E },  // 14
            new byte[] { 0x00, 0x00, 0x00, 0x34 },  // 52
            IccTestDataPrimitives.Unicode_Rand2,
            IccTestDataPrimitives.Unicode_Rand3);

        public static readonly byte[] MultiLocalizedUnicode_Arr2_Write = ArrayHelper.Concat(
            IccTestDataPrimitives.UInt32_2,
            new byte[] { 0x00, 0x00, 0x00, 0x0C },  // 12
            new byte[] { (byte)'e', (byte)'n', 0x00, 0x00 },
            new byte[] { 0x00, 0x00, 0x00, 0x0C },  // 12
            new byte[] { 0x00, 0x00, 0x00, 0x28 },  // 40
            new byte[] { (byte)'x', (byte)'x', 0x00, 0x00 },
            new byte[] { 0x00, 0x00, 0x00, 0x0E },  // 14
            new byte[] { 0x00, 0x00, 0x00, 0x34 },  // 52
            IccTestDataPrimitives.Unicode_Rand2,
            IccTestDataPrimitives.Unicode_Rand3);

        public static readonly IccMultiLocalizedUnicodeTagDataEntry MultiLocalizedUnicode_Val3 = new IccMultiLocalizedUnicodeTagDataEntry(LocalizedString_SameArr_enUS_deDE_esXL_xyXL);
        public static readonly byte[] MultiLocalizedUnicode_Arr3 = ArrayHelper.Concat(
            IccTestDataPrimitives.UInt32_4,
            new byte[] { 0x00, 0x00, 0x00, 0x0C },  // 12
            new byte[] { (byte)'e', (byte)'n', (byte)'U', (byte)'S' },
            new byte[] { 0x00, 0x00, 0x00, 0x0C },  // 12
            new byte[] { 0x00, 0x00, 0x00, 0x40 },  // 64
            new byte[] { (byte)'d', (byte)'e', (byte)'D', (byte)'E' },
            new byte[] { 0x00, 0x00, 0x00, 0x0C },  // 12
            new byte[] { 0x00, 0x00, 0x00, 0x40 },  // 64
            new byte[] { (byte)'e', (byte)'s', (byte)'X', (byte)'L' },
            new byte[] { 0x00, 0x00, 0x00, 0x0C },  // 12
            new byte[] { 0x00, 0x00, 0x00, 0x40 },  // 64
            new byte[] { (byte)'x', (byte)'y', (byte)'X', (byte)'L' },
            new byte[] { 0x00, 0x00, 0x00, 0x0C },  // 12
            new byte[] { 0x00, 0x00, 0x00, 0x40 },  // 64
            IccTestDataPrimitives.Unicode_Rand2);

        public static readonly object[][] MultiLocalizedUnicodeTagDataEntryTestData_Read =
        {
            new object[] { MultiLocalizedUnicode_Arr, MultiLocalizedUnicode_Val },
            new object[] { MultiLocalizedUnicode_Arr2_Read, MultiLocalizedUnicode_Val2 },
            new object[] { MultiLocalizedUnicode_Arr3, MultiLocalizedUnicode_Val3 },
        };

        public static readonly object[][] MultiLocalizedUnicodeTagDataEntryTestData_Write =
        {
            new object[] { MultiLocalizedUnicode_Arr, MultiLocalizedUnicode_Val },
            new object[] { MultiLocalizedUnicode_Arr2_Write, MultiLocalizedUnicode_Val2 },
            new object[] { MultiLocalizedUnicode_Arr3, MultiLocalizedUnicode_Val3 },
        };

        public static readonly IccMultiProcessElementsTagDataEntry MultiProcessElements_Val = new IccMultiProcessElementsTagDataEntry(
            new IccMultiProcessElement[]
            {
                IccTestDataMultiProcessElements.MPE_ValCLUT,
                IccTestDataMultiProcessElements.MPE_ValCLUT,
            });

        public static readonly byte[] MultiProcessElements_Arr = ArrayHelper.Concat(
            IccTestDataPrimitives.UInt16_2,
            IccTestDataPrimitives.UInt16_3,
            IccTestDataPrimitives.UInt32_2,
            new byte[] { 0x00, 0x00, 0x00, 0x20 },  // 32
            new byte[] { 0x00, 0x00, 0x00, 0x84 },  // 132
            new byte[] { 0x00, 0x00, 0x00, 0xA4 },  // 164
            new byte[] { 0x00, 0x00, 0x00, 0x84 },  // 132
            IccTestDataMultiProcessElements.MPE_CLUT,
            IccTestDataMultiProcessElements.MPE_CLUT);

        public static readonly object[][] MultiProcessElementsTagDataEntryTestData =
        {
            new object[] { MultiProcessElements_Arr, MultiProcessElements_Val },
        };

        public static readonly IccNamedColor2TagDataEntry NamedColor2_Val = new IccNamedColor2TagDataEntry(
            16909060,
            ArrayHelper.Fill('A', 31),
            ArrayHelper.Fill('4', 31),
            new IccNamedColor[] { IccTestDataNonPrimitives.NamedColor_ValMin,  IccTestDataNonPrimitives.NamedColor_ValMin });

        public static readonly byte[] NamedColor2_Arr = ArrayHelper.Concat(
            new byte[] { 0x01, 0x02, 0x03, 0x04 },
            IccTestDataPrimitives.UInt32_2,
            IccTestDataPrimitives.UInt32_3,
            ArrayHelper.Fill((byte)0x41, 31),
            new byte[] { 0x00 },
            ArrayHelper.Fill((byte)0x34, 31),
            new byte[] { 0x00 },
            IccTestDataNonPrimitives.NamedColor_Min,
            IccTestDataNonPrimitives.NamedColor_Min);

        public static readonly object[][] NamedColor2TagDataEntryTestData =
        {
            new object[] { NamedColor2_Arr, NamedColor2_Val },
        };

        public static readonly IccParametricCurveTagDataEntry ParametricCurve_Val = new IccParametricCurveTagDataEntry(IccTestDataCurves.Parametric_ValVar1);
        public static readonly byte[] ParametricCurve_Arr = IccTestDataCurves.Parametric_Var1;

        public static readonly object[][] ParametricCurveTagDataEntryTestData =
        {
            new object[] { ParametricCurve_Arr, ParametricCurve_Val },
        };

        public static readonly IccProfileSequenceDescTagDataEntry ProfileSequenceDesc_Val = new IccProfileSequenceDescTagDataEntry(
            new IccProfileDescription[]
            {
                IccTestDataNonPrimitives.ProfileDescription_ValRand1,
                IccTestDataNonPrimitives.ProfileDescription_ValRand1
            });

        public static readonly byte[] ProfileSequenceDesc_Arr = ArrayHelper.Concat(
            IccTestDataPrimitives.UInt32_2,
            IccTestDataNonPrimitives.ProfileDescription_Rand1,
            IccTestDataNonPrimitives.ProfileDescription_Rand1);

        public static readonly object[][] ProfileSequenceDescTagDataEntryTestData =
        {
            new object[] { ProfileSequenceDesc_Arr, ProfileSequenceDesc_Val },
        };

        public static readonly IccProfileSequenceIdentifierTagDataEntry ProfileSequenceIdentifier_Val = new IccProfileSequenceIdentifierTagDataEntry(
            new IccProfileSequenceIdentifier[]
            {
                new IccProfileSequenceIdentifier(IccTestDataNonPrimitives.ProfileId_ValRand, LocalizedString_RandArr_enUS_deDE),
                new IccProfileSequenceIdentifier(IccTestDataNonPrimitives.ProfileId_ValRand, LocalizedString_RandArr_enUS_deDE),
            });

        public static readonly byte[] ProfileSequenceIdentifier_Arr = ArrayHelper.Concat(
            IccTestDataPrimitives.UInt32_2,
            new byte[] { 0x00, 0x00, 0x00, 0x1C },  // 28
            new byte[] { 0x00, 0x00, 0x00, 0x54 },  // 84
            new byte[] { 0x00, 0x00, 0x00, 0x70 },  // 112
            new byte[] { 0x00, 0x00, 0x00, 0x54 },  // 84
            IccTestDataNonPrimitives.ProfileId_Rand,        // 16 bytes
            TagDataEntryHeader_MultiLocalizedUnicodeArr,    // 8  bytes
            MultiLocalizedUnicode_Arr,                      // 58 bytes
            new byte[] { 0x00, 0x00 },                      // 2  bytes (padding)
            IccTestDataNonPrimitives.ProfileId_Rand,
            TagDataEntryHeader_MultiLocalizedUnicodeArr,
            MultiLocalizedUnicode_Arr,
            new byte[] { 0x00, 0x00 });

        public static readonly object[][] ProfileSequenceIdentifierTagDataEntryTestData =
        {
            new object[] { ProfileSequenceIdentifier_Arr, ProfileSequenceIdentifier_Val },
        };

        public static readonly IccResponseCurveSet16TagDataEntry ResponseCurveSet16_Val = new IccResponseCurveSet16TagDataEntry(
            new IccResponseCurve[]
            {
                IccTestDataCurves.Response_ValGrad,
                IccTestDataCurves.Response_ValGrad,
            });

        public static readonly byte[] ResponseCurveSet16_Arr = ArrayHelper.Concat(
            IccTestDataPrimitives.UInt16_3,
            IccTestDataPrimitives.UInt16_2,
            new byte[] { 0x00, 0x00, 0x00, 0x14 },  // 20
            new byte[] { 0x00, 0x00, 0x00, 0x6C },  // 108
            IccTestDataCurves.Response_Grad, // 88 bytes
            IccTestDataCurves.Response_Grad); // 88 bytes

        public static readonly object[][] ResponseCurveSet16TagDataEntryTestData =
        {
            new object[] { ResponseCurveSet16_Arr, ResponseCurveSet16_Val },
        };

        public static readonly IccFix16ArrayTagDataEntry Fix16Array_Val = new IccFix16ArrayTagDataEntry(new float[] { 1 / 256f, 2 / 256f, 3 / 256f });
        public static readonly byte[] Fix16Array_Arr = ArrayHelper.Concat(
            IccTestDataPrimitives.Fix16_1,
            IccTestDataPrimitives.Fix16_2,
            IccTestDataPrimitives.Fix16_3);

        public static readonly object[][] Fix16ArrayTagDataEntryTestData =
        {
            new object[] { Fix16Array_Arr, Fix16Array_Val, 20u },
        };

        public static readonly IccSignatureTagDataEntry Signature_Val = new IccSignatureTagDataEntry("ABCD");
        public static readonly byte[] Signature_Arr = { 0x41, 0x42, 0x43, 0x44, };

        public static readonly object[][] SignatureTagDataEntryTestData =
        {
            new object[] { Signature_Arr, Signature_Val },
        };

        public static readonly IccTextTagDataEntry Text_Val = new IccTextTagDataEntry("ABCD");
        public static readonly byte[] Text_Arr = { 0x41, 0x42, 0x43, 0x44 };

        public static readonly object[][] TextTagDataEntryTestData =
        {
            new object[] { Text_Arr, Text_Val, 12u },
        };

        public static readonly IccUFix16ArrayTagDataEntry UFix16Array_Val = new IccUFix16ArrayTagDataEntry(new float[] { 1, 2, 3 });
        public static readonly byte[] UFix16Array_Arr = ArrayHelper.Concat(
            IccTestDataPrimitives.UFix16_1,
            IccTestDataPrimitives.UFix16_2,
            IccTestDataPrimitives.UFix16_3);

        public static readonly object[][] UFix16ArrayTagDataEntryTestData =
        {
            new object[] { UFix16Array_Arr, UFix16Array_Val, 20u },
        };

        public static readonly IccUInt16ArrayTagDataEntry UInt16Array_Val = new IccUInt16ArrayTagDataEntry(new ushort[] { 1, 2, 3 });
        public static readonly byte[] UInt16Array_Arr = ArrayHelper.Concat(
            IccTestDataPrimitives.UInt16_1,
            IccTestDataPrimitives.UInt16_2,
            IccTestDataPrimitives.UInt16_3);

        public static readonly object[][] UInt16ArrayTagDataEntryTestData =
        {
            new object[] { UInt16Array_Arr, UInt16Array_Val, 14u },
        };

        public static readonly IccUInt32ArrayTagDataEntry UInt32Array_Val = new IccUInt32ArrayTagDataEntry(new uint[] { 1, 2, 3 });
        public static readonly byte[] UInt32Array_Arr = ArrayHelper.Concat(
            IccTestDataPrimitives.UInt32_1,
            IccTestDataPrimitives.UInt32_2,
            IccTestDataPrimitives.UInt32_3);

        public static readonly object[][] UInt32ArrayTagDataEntryTestData =
        {
            new object[] { UInt32Array_Arr, UInt32Array_Val, 20u },
        };

        public static readonly IccUInt64ArrayTagDataEntry UInt64Array_Val = new IccUInt64ArrayTagDataEntry(new ulong[] { 1, 2, 3 });
        public static readonly byte[] UInt64Array_Arr = ArrayHelper.Concat(
            IccTestDataPrimitives.UInt64_1,
            IccTestDataPrimitives.UInt64_2,
            IccTestDataPrimitives.UInt64_3);

        public static readonly object[][] UInt64ArrayTagDataEntryTestData =
        {
            new object[] { UInt64Array_Arr, UInt64Array_Val, 32u },
        };

        public static readonly IccUInt8ArrayTagDataEntry UInt8Array_Val = new IccUInt8ArrayTagDataEntry(new byte[] { 1, 2, 3 });
        public static readonly byte[] UInt8Array_Arr = { 1, 2, 3 };

        public static readonly object[][] UInt8ArrayTagDataEntryTestData =
        {
            new object[] { UInt8Array_Arr, UInt8Array_Val, 11u },
        };

        public static readonly IccViewingConditionsTagDataEntry ViewingConditions_Val = new IccViewingConditionsTagDataEntry(
            IccTestDataNonPrimitives.XyzNumber_ValVar1,
            IccTestDataNonPrimitives.XyzNumber_ValVar2,
            IccStandardIlluminant.D50);

        public static readonly byte[] ViewingConditions_Arr = ArrayHelper.Concat(
            IccTestDataNonPrimitives.XyzNumber_Var1,
            IccTestDataNonPrimitives.XyzNumber_Var2,
            IccTestDataPrimitives.UInt32_1);

        public static readonly object[][] ViewingConditionsTagDataEntryTestData =
        {
            new object[] { ViewingConditions_Arr, ViewingConditions_Val },
        };

        public static readonly IccXyzTagDataEntry XYZ_Val = new IccXyzTagDataEntry(new Vector3[]
        {
            IccTestDataNonPrimitives.XyzNumber_ValVar1,
            IccTestDataNonPrimitives.XyzNumber_ValVar2,
            IccTestDataNonPrimitives.XyzNumber_ValVar3,
        });

        public static readonly byte[] XYZ_Arr = ArrayHelper.Concat(
            IccTestDataNonPrimitives.XyzNumber_Var1,
            IccTestDataNonPrimitives.XyzNumber_Var2,
            IccTestDataNonPrimitives.XyzNumber_Var3);

        public static readonly object[][] XYZTagDataEntryTestData =
        {
            new object[] { XYZ_Arr, XYZ_Val, 44u },
        };

        public static readonly IccTextDescriptionTagDataEntry TextDescription_Val1 = new IccTextDescriptionTagDataEntry(
            IccTestDataPrimitives.Ascii_ValRand,
            IccTestDataPrimitives.Unicode_ValRand1,
            ArrayHelper.Fill('A', 66),
            1701729619,
            2);

        public static readonly byte[] TextDescription_Arr1 = ArrayHelper.Concat(
            new byte[] { 0x00, 0x00, 0x00, 0x0B }, // 11
            IccTestDataPrimitives.Ascii_Rand,
            new byte[] { 0x00 }, // Null terminator
            new byte[] { 0x65, 0x6E, 0x55, 0x53 }, // enUS
            new byte[] { 0x00, 0x00, 0x00, 0x0E }, // 14
            IccTestDataPrimitives.Unicode_Rand1,
            new byte[] { 0x00, 0x00 }, // Null terminator
            new byte[] { 0x00, 0x02, 0x43 }, // 2, 67
            ArrayHelper.Fill((byte)0x41, 66),
            new byte[] { 0x00 }); // Null terminator

        public static readonly IccTextDescriptionTagDataEntry TextDescription_Val2 = new IccTextDescriptionTagDataEntry(IccTestDataPrimitives.Ascii_ValRand, null, null, 0, 0);
        public static readonly byte[] TextDescription_Arr2 = ArrayHelper.Concat(
            new byte[] { 0x00, 0x00, 0x00, 0x0B },  // 11
            IccTestDataPrimitives.Ascii_Rand,
            new byte[] { 0x00 },                    // Null terminator
            IccTestDataPrimitives.UInt32_0,
            IccTestDataPrimitives.UInt32_0,
            new byte[] { 0x00, 0x00, 0x00 },        // 0, 0
            ArrayHelper.Fill((byte)0x00, 67));

        public static readonly object[][] TextDescriptionTagDataEntryTestData =
        {
            new object[] { TextDescription_Arr1, TextDescription_Val1 },
            new object[] { TextDescription_Arr2, TextDescription_Val2 },
        };

        public static readonly IccCrdInfoTagDataEntry CrdInfo_Val = new IccCrdInfoTagDataEntry(
            IccTestDataPrimitives.Ascii_ValRand4,
            IccTestDataPrimitives.Ascii_ValRand1,
            IccTestDataPrimitives.Ascii_ValRand2,
            IccTestDataPrimitives.Ascii_ValRand3,
            IccTestDataPrimitives.Ascii_ValRand4);

        public static readonly byte[] CrdInfo_Arr = ArrayHelper.Concat(
            IccTestDataPrimitives.UInt32_6,
            IccTestDataPrimitives.Ascii_Rand4,
            new byte[] { 0 },
            IccTestDataPrimitives.UInt32_6,
            IccTestDataPrimitives.Ascii_Rand1,
            new byte[] { 0 },
            IccTestDataPrimitives.UInt32_6,
            IccTestDataPrimitives.Ascii_Rand2,
            new byte[] { 0 },
            IccTestDataPrimitives.UInt32_6,
            IccTestDataPrimitives.Ascii_Rand3,
            new byte[] { 0 },
            IccTestDataPrimitives.UInt32_6,
            IccTestDataPrimitives.Ascii_Rand4,
            new byte[] { 0 });

        public static readonly object[][] CrdInfoTagDataEntryTestData =
        {
            new object[] { CrdInfo_Arr, CrdInfo_Val },
        };

        public static readonly IccScreeningTagDataEntry Screening_Val = new IccScreeningTagDataEntry(
            IccScreeningFlag.DefaultScreens | IccScreeningFlag.UnitLinesPerCm,
            new IccScreeningChannel[] { IccTestDataNonPrimitives.ScreeningChannel_ValRand1, IccTestDataNonPrimitives.ScreeningChannel_ValRand2 });

        public static readonly byte[] Screening_Arr = ArrayHelper.Concat(
            IccTestDataPrimitives.Int32_1,
            IccTestDataPrimitives.UInt32_2,
            IccTestDataNonPrimitives.ScreeningChannel_Rand1,
            IccTestDataNonPrimitives.ScreeningChannel_Rand2);

        public static readonly object[][] ScreeningTagDataEntryTestData =
        {
            new object[] { Screening_Arr, Screening_Val },
        };

        public static readonly IccUcrBgTagDataEntry UcrBg_Val = new IccUcrBgTagDataEntry(
            new ushort[] { 3, 4, 6 },
            new ushort[] { 9, 7, 2, 5 },
            IccTestDataPrimitives.Ascii_ValRand);

        public static readonly byte[] UcrBg_Arr = ArrayHelper.Concat(
            IccTestDataPrimitives.UInt32_3,
            IccTestDataPrimitives.UInt16_3,
            IccTestDataPrimitives.UInt16_4,
            IccTestDataPrimitives.UInt16_6,
            IccTestDataPrimitives.UInt32_4,
            IccTestDataPrimitives.UInt16_9,
            IccTestDataPrimitives.UInt16_7,
            IccTestDataPrimitives.UInt16_2,
            IccTestDataPrimitives.UInt16_5,
            IccTestDataPrimitives.Ascii_Rand,
            new byte[] { 0 });

        public static readonly object[][] UcrBgTagDataEntryTestData =
        {
            new object[] { UcrBg_Arr, UcrBg_Val, 41 },
        };

        public static readonly IccTagDataEntry TagDataEntry_CurveVal = Curve_Val_2;
        public static readonly byte[] TagDataEntry_CurveArr = ArrayHelper.Concat(
            TagDataEntryHeader_CurveArr,
            Curve_Arr_2,
            new byte[] { 0x00, 0x00 }); // padding

        public static readonly IccTagDataEntry TagDataEntry_MultiLocalizedUnicodeVal = MultiLocalizedUnicode_Val;
        public static readonly byte[] TagDataEntry_MultiLocalizedUnicodeArr = ArrayHelper.Concat(
            TagDataEntryHeader_MultiLocalizedUnicodeArr,
            MultiLocalizedUnicode_Arr,
            new byte[] { 0x00, 0x00 }); // padding

        public static readonly IccTagTableEntry TagDataEntry_MultiLocalizedUnicodeTable = new IccTagTableEntry(
            IccProfileTag.Unknown,
            0,
            (uint)TagDataEntry_MultiLocalizedUnicodeArr.Length - 2);

        public static readonly IccTagTableEntry TagDataEntry_CurveTable = new IccTagTableEntry(IccProfileTag.Unknown, 0, (uint)TagDataEntry_CurveArr.Length - 2);

        public static readonly object[][] TagDataEntryTestData =
        {
            new object[] { TagDataEntry_CurveArr, TagDataEntry_CurveVal },
            new object[] { TagDataEntry_MultiLocalizedUnicodeArr, TagDataEntry_MultiLocalizedUnicodeVal },
        };
    }
}
