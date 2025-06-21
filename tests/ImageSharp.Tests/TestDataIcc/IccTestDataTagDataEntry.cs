// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Globalization;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;

namespace SixLabors.ImageSharp.Tests.TestDataIcc;

internal static class IccTestDataTagDataEntry
{
    public static readonly IccTypeSignature TagDataEntryHeaderUnknownVal = IccTypeSignature.Unknown;
    public static readonly byte[] TagDataEntryHeaderUnknownArr =
    {
        0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00,
    };

    public static readonly IccTypeSignature TagDataEntryHeaderMultiLocalizedUnicodeVal = IccTypeSignature.MultiLocalizedUnicode;
    public static readonly byte[] TagDataEntryHeaderMultiLocalizedUnicodeArr =
    {
        0x6D, 0x6C, 0x75, 0x63,
        0x00, 0x00, 0x00, 0x00,
    };

    public static readonly IccTypeSignature TagDataEntryHeaderCurveVal = IccTypeSignature.Curve;
    public static readonly byte[] TagDataEntryHeaderCurveArr =
    {
        0x63, 0x75, 0x72, 0x76,
        0x00, 0x00, 0x00, 0x00,
    };

    public static readonly object[][] TagDataEntryHeaderTestData =
    {
        new object[] { TagDataEntryHeaderUnknownArr, TagDataEntryHeaderUnknownVal },
        new object[] { TagDataEntryHeaderMultiLocalizedUnicodeArr, TagDataEntryHeaderMultiLocalizedUnicodeVal },
        new object[] { TagDataEntryHeaderCurveArr, TagDataEntryHeaderCurveVal },
    };

    public static readonly IccUnknownTagDataEntry UnknownVal = new(new byte[] { 0x00, 0x01, 0x02, 0x03 });

    public static readonly byte[] UnknownArr = { 0x00, 0x01, 0x02, 0x03 };

    public static readonly object[][] UnknownTagDataEntryTestData =
    {
        new object[] { UnknownArr, UnknownVal, 12u },
    };

    public static readonly IccChromaticityTagDataEntry ChromaticityVal1 = new(IccColorantEncoding.ItuRBt709_2);
    public static readonly byte[] ChromaticityArr1 = ArrayHelper.Concat(
        IccTestDataPrimitives.UInt163,
        IccTestDataPrimitives.UInt161,
        new byte[] { 0x00, 0x00, 0xA3, 0xD7 },  // 0.640
        new byte[] { 0x00, 0x00, 0x54, 0x7B },  // 0.330
        new byte[] { 0x00, 0x00, 0x4C, 0xCD },  // 0.300
        new byte[] { 0x00, 0x00, 0x99, 0x9A },  // 0.600
        new byte[] { 0x00, 0x00, 0x26, 0x66 },  // 0.150
        new byte[] { 0x00, 0x00, 0x0F, 0x5C }); // 0.060

    public static readonly IccChromaticityTagDataEntry ChromaticityVal2 = new(
        new[]
        {
            new double[] { 1, 2 },
            new double[] { 3, 4 },
        });

    public static readonly byte[] ChromaticityArr2 = ArrayHelper.Concat(
        IccTestDataPrimitives.UInt162,
        IccTestDataPrimitives.UInt160,
        IccTestDataPrimitives.UFix161,
        IccTestDataPrimitives.UFix162,
        IccTestDataPrimitives.UFix163,
        IccTestDataPrimitives.UFix164);

    /// <summary>
    /// <see cref="InvalidIccProfileException"/>: channel count must be 3 for any enum other than <see cref="ColorantEncoding.Unknown"/>
    /// </summary>
    public static readonly byte[] ChromaticityArrInvalid1 = ArrayHelper.Concat(
        IccTestDataPrimitives.UInt165,
        IccTestDataPrimitives.UInt161);

    /// <summary>
    /// <see cref="InvalidIccProfileException"/>: invalid enum value
    /// </summary>
    public static readonly byte[] ChromaticityArrInvalid2 = ArrayHelper.Concat(
        IccTestDataPrimitives.UInt163,
        IccTestDataPrimitives.UInt169);

    public static readonly object[][] ChromaticityTagDataEntryTestData =
    {
        new object[] { ChromaticityArr1, ChromaticityVal1 },
        new object[] { ChromaticityArr2, ChromaticityVal2 },
    };

    public static readonly IccColorantOrderTagDataEntry ColorantOrderVal = new(new byte[] { 0x00, 0x01, 0x02 });
    public static readonly byte[] ColorantOrderArr = ArrayHelper.Concat(IccTestDataPrimitives.UInt323, new byte[] { 0x00, 0x01, 0x02 });

    public static readonly object[][] ColorantOrderTagDataEntryTestData =
    {
        new object[] { ColorantOrderArr, ColorantOrderVal },
    };

    public static readonly IccColorantTableTagDataEntry ColorantTableVal = new(
        new[]
        {
            IccTestDataNonPrimitives.ColorantTableEntryValRand1,
            IccTestDataNonPrimitives.ColorantTableEntryValRand2
        });

    public static readonly byte[] ColorantTableArr = ArrayHelper.Concat(
        IccTestDataPrimitives.UInt322,
        IccTestDataNonPrimitives.ColorantTableEntryRand1,
        IccTestDataNonPrimitives.ColorantTableEntryRand2);

    public static readonly object[][] ColorantTableTagDataEntryTestData =
    {
        new object[] { ColorantTableArr, ColorantTableVal },
    };

    public static readonly IccCurveTagDataEntry CurveVal0 = new();
    public static readonly byte[] CurveArr0 = IccTestDataPrimitives.UInt320;

    public static readonly IccCurveTagDataEntry CurveVal1 = new(1f);
    public static readonly byte[] CurveArr1 = ArrayHelper.Concat(
        IccTestDataPrimitives.UInt321,
        IccTestDataPrimitives.UFix81);

    public static readonly IccCurveTagDataEntry CurveVal2 = new(new float[] { 1 / 65535f, 2 / 65535f, 3 / 65535f });
    public static readonly byte[] CurveArr2 = ArrayHelper.Concat(
        IccTestDataPrimitives.UInt323,
        IccTestDataPrimitives.UInt161,
        IccTestDataPrimitives.UInt162,
        IccTestDataPrimitives.UInt163);

    public static readonly object[][] CurveTagDataEntryTestData =
    {
        new object[] { CurveArr0, CurveVal0 },
        new object[] { CurveArr1, CurveVal1 },
        new object[] { CurveArr2, CurveVal2 },
    };

    public static readonly IccDataTagDataEntry DataValNoAscii = new(new byte[] { 0x01, 0x02, 0x03, 0x04 }, false);

    public static readonly byte[] DataArrNoAscii =
    {
        0x00, 0x00, 0x00, 0x00,
        0x01, 0x02, 0x03, 0x04
    };

    public static readonly IccDataTagDataEntry DataValAscii = new(new[] { (byte)'A', (byte)'S', (byte)'C', (byte)'I', (byte)'I' }, true);

    public static readonly byte[] DataArrAscii =
    {
        0x00, 0x00, 0x00, 0x01,
        (byte)'A', (byte)'S', (byte)'C', (byte)'I', (byte)'I'
    };

    public static readonly object[][] DataTagDataEntryTestData =
    {
        new object[] { DataArrNoAscii, DataValNoAscii, 16u },
        new object[] { DataArrAscii, DataValAscii, 17u },
    };

    public static readonly IccDateTimeTagDataEntry DateTimeVal = new(IccTestDataNonPrimitives.DateTimeValRand1);
    public static readonly byte[] DateTimeArr = IccTestDataNonPrimitives.DateTimeRand1;

    public static readonly object[][] DateTimeTagDataEntryTestData =
    {
        new object[] { DateTimeArr, DateTimeVal },
    };

    public static readonly IccLut16TagDataEntry Lut16Val = new(
        new[] { IccTestDataLut.Lut16ValGrad, IccTestDataLut.Lut16ValGrad },
        IccTestDataLut.Clut16ValGrad,
        new[] { IccTestDataLut.Lut16ValGrad, IccTestDataLut.Lut16ValGrad, IccTestDataLut.Lut16ValGrad });

    public static readonly byte[] Lut16Arr = ArrayHelper.Concat(
        new byte[] { 0x02, 0x03, 0x03, 0x00 },
        IccTestDataMatrix.Fix162DIdentity,
        new byte[] { 0x00, (byte)IccTestDataLut.Lut16ValGrad.Values.Length, 0x00, (byte)IccTestDataLut.Lut16ValGrad.Values.Length },
        IccTestDataLut.Lut16Grad,
        IccTestDataLut.Lut16Grad,
        IccTestDataLut.Clut16Grad,
        IccTestDataLut.Lut16Grad,
        IccTestDataLut.Lut16Grad,
        IccTestDataLut.Lut16Grad);

    public static readonly object[][] Lut16TagDataEntryTestData =
    {
        new object[] { Lut16Arr, Lut16Val },
    };

    public static readonly IccLut8TagDataEntry Lut8Val = new(
        new IccLut[] { IccTestDataLut.Lut8ValGrad, IccTestDataLut.Lut8ValGrad },
        IccTestDataLut.Clut8ValGrad,
        new IccLut[] { IccTestDataLut.Lut8ValGrad, IccTestDataLut.Lut8ValGrad, IccTestDataLut.Lut8ValGrad });

    public static readonly byte[] Lut8Arr = ArrayHelper.Concat(
        new byte[] { 0x02, 0x03, 0x03, 0x00 },
        IccTestDataMatrix.Fix162DIdentity,
        IccTestDataLut.Lut8Grad,
        IccTestDataLut.Lut8Grad,
        IccTestDataLut.Clut8Grad,
        IccTestDataLut.Lut8Grad,
        IccTestDataLut.Lut8Grad,
        IccTestDataLut.Lut8Grad);

    public static readonly object[][] Lut8TagDataEntryTestData = { new object[] { Lut8Arr, Lut8Val }, };

    private static readonly byte[] CurveFull0 = ArrayHelper.Concat(TagDataEntryHeaderCurveArr, CurveArr0);

    private static readonly byte[] CurveFull1 = ArrayHelper.Concat(TagDataEntryHeaderCurveArr, CurveArr1);

    private static readonly byte[] CurveFull2 = ArrayHelper.Concat(TagDataEntryHeaderCurveArr, CurveArr2);

    public static readonly IccLutAToBTagDataEntry LutAToBVal
        = new(
            new[]
        {
            CurveVal0,
            CurveVal1,
            CurveVal2,
        },
            IccTestDataMatrix.Single2DArrayValGrad,
            IccTestDataMatrix.Single1DArrayValGrad,
            new[] { CurveVal1, CurveVal2, CurveVal0 },
            IccTestDataLut.ClutVal16,
            new[] { CurveVal2, CurveVal1 });

    public static readonly byte[] LutAToBArr = ArrayHelper.Concat(
        new byte[] { 0x02, 0x03, 0x00, 0x00 },
        new byte[] { 0x00, 0x00, 0x00, 0x20 },  // b:        32
        new byte[] { 0x00, 0x00, 0x00, 0x50 },  // matrix:   80
        new byte[] { 0x00, 0x00, 0x00, 0x80 },  // m:        128
        new byte[] { 0x00, 0x00, 0x00, 0xB0 },  // clut:     176
        new byte[] { 0x00, 0x00, 0x00, 0xFC },  // a:        252

        // B
        CurveFull0,                // 12 bytes
        CurveFull1,                // 14 bytes
        new byte[] { 0x00, 0x00 },  // Padding
        CurveFull2,                // 18 bytes
        new byte[] { 0x00, 0x00 },  // Padding

        // Matrix
        IccTestDataMatrix.Fix162DGrad,   // 36 bytes
        IccTestDataMatrix.Fix161DGrad,   // 12 bytes

        // M
        CurveFull1,                // 14 bytes
        new byte[] { 0x00, 0x00 },  // Padding
        CurveFull2,                // 18 bytes
        new byte[] { 0x00, 0x00 },  // Padding
        CurveFull0,                // 12 bytes

        // CLUT
        IccTestDataLut.Clut16,     // 74 bytes
        new byte[] { 0x00, 0x00 },  // Padding

        // A
        CurveFull2,                // 18 bytes
        new byte[] { 0x00, 0x00 },  // Padding
        CurveFull1,                // 14 bytes
        new byte[] { 0x00, 0x00 }); // Padding

    public static readonly object[][] LutAToBTagDataEntryTestData = { new object[] { LutAToBArr, LutAToBVal }, };

    public static readonly IccLutBToATagDataEntry LutBToAVal = new(
        new[]
        {
            CurveVal0,
            CurveVal1,
        },
        null,
        null,
        null,
        IccTestDataLut.ClutVal16,
        new[] { CurveVal2, CurveVal1, CurveVal0 });

    public static readonly byte[] LutBToAArr = ArrayHelper.Concat(
        new byte[] { 0x02, 0x03, 0x00, 0x00 },
        new byte[] { 0x00, 0x00, 0x00, 0x20 },  // b:        32
        new byte[] { 0x00, 0x00, 0x00, 0x00 },  // matrix:   0
        new byte[] { 0x00, 0x00, 0x00, 0x00 },  // m:        0
        new byte[] { 0x00, 0x00, 0x00, 0x3C },  // clut:     60
        new byte[] { 0x00, 0x00, 0x00, 0x88 },  // a:        136

        // B
        CurveFull0,    // 12 bytes
        CurveFull1,    // 14 bytes
        new byte[] { 0x00, 0x00 }, // Padding

        // CLUT
        IccTestDataLut.Clut16,     // 74 bytes
        new byte[] { 0x00, 0x00 },  // Padding

        // A
        CurveFull2,                // 18 bytes
        new byte[] { 0x00, 0x00 },  // Padding
        CurveFull1,                // 14 bytes
        new byte[] { 0x00, 0x00 },  // Padding
        CurveFull0); // 12 bytes

    public static readonly object[][] LutBToATagDataEntryTestData =
    {
        new object[] { LutBToAArr, LutBToAVal },
    };

    public static readonly IccMeasurementTagDataEntry MeasurementVal = new(
        IccStandardObserver.Cie1931Observer,
        IccTestDataNonPrimitives.XyzNumberValVar1,
        IccMeasurementGeometry.Degree0ToDOrDTo0,
        1f,
        IccStandardIlluminant.D50);

    public static readonly byte[] MeasurementArr = ArrayHelper.Concat(
        IccTestDataPrimitives.UInt321,
        IccTestDataNonPrimitives.XyzNumberVar1,
        IccTestDataPrimitives.UInt322,
        IccTestDataPrimitives.UFix161,
        IccTestDataPrimitives.UInt321);

    public static readonly object[][] MeasurementTagDataEntryTestData =
    {
        new object[] { MeasurementArr, MeasurementVal },
    };

    private static readonly IccLocalizedString LocalizedStringRandEnUs = CreateLocalizedString("en", "US", IccTestDataPrimitives.UnicodeValRand2);
    private static readonly IccLocalizedString LocalizedStringRandDeDe = CreateLocalizedString("de", "DE", IccTestDataPrimitives.UnicodeValRand3);
    private static readonly IccLocalizedString LocalizedStringRand2DeDe = CreateLocalizedString("de", "DE", IccTestDataPrimitives.UnicodeValRand2);
    private static readonly IccLocalizedString LocalizedStringRand2EsXl = CreateLocalizedString("es", "XL", IccTestDataPrimitives.UnicodeValRand2);
    private static readonly IccLocalizedString LocalizedStringRand2XyXl = CreateLocalizedString("xy", "XL", IccTestDataPrimitives.UnicodeValRand2);
    private static readonly IccLocalizedString LocalizedStringRandEn = CreateLocalizedString("en", null, IccTestDataPrimitives.UnicodeValRand2);
    private static readonly IccLocalizedString LocalizedStringRandInvariant = new(CultureInfo.InvariantCulture, IccTestDataPrimitives.UnicodeValRand3);

    private static IccLocalizedString CreateLocalizedString(string language, string country, string text)
    {
        CultureInfo culture;
        if (country == null)
        {
            try
            {
                culture = new(language);
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
                culture = new($"{language}-{country}");
            }
            catch (CultureNotFoundException)
            {
                return CreateLocalizedString(language, null, text);
            }
        }

        return new(culture, text);
    }

    private static readonly IccLocalizedString[] LocalizedStringRandArrEnUsDeDe = new[]
    {
        LocalizedStringRandEnUs,
        LocalizedStringRandDeDe,
    };

    private static readonly IccLocalizedString[] LocalizedStringRandArrEnInvariant = new[]
    {
        LocalizedStringRandEn,
        LocalizedStringRandInvariant,
    };

    private static readonly IccLocalizedString[] LocalizedStringSameArrEnUsDeDeEsXlXyXl = new[]
    {
        LocalizedStringRandEnUs,
        LocalizedStringRand2DeDe,
        LocalizedStringRand2EsXl,
        LocalizedStringRand2XyXl,
    };

    public static readonly IccMultiLocalizedUnicodeTagDataEntry MultiLocalizedUnicodeVal = new(LocalizedStringRandArrEnUsDeDe);
    public static readonly byte[] MultiLocalizedUnicodeArr = ArrayHelper.Concat(
        IccTestDataPrimitives.UInt322,
        new byte[] { 0x00, 0x00, 0x00, 0x0C },  // 12
        new byte[] { (byte)'e', (byte)'n', (byte)'U', (byte)'S' },
        new byte[] { 0x00, 0x00, 0x00, 0x0C },  // 12
        new byte[] { 0x00, 0x00, 0x00, 0x28 },  // 40
        new byte[] { (byte)'d', (byte)'e', (byte)'D', (byte)'E' },
        new byte[] { 0x00, 0x00, 0x00, 0x0E },  // 14
        new byte[] { 0x00, 0x00, 0x00, 0x34 },  // 52
        IccTestDataPrimitives.UnicodeRand2,
        IccTestDataPrimitives.UnicodeRand3);

    public static readonly IccMultiLocalizedUnicodeTagDataEntry MultiLocalizedUnicodeVal2 = new(LocalizedStringRandArrEnInvariant);
    public static readonly byte[] MultiLocalizedUnicodeArr2Read = ArrayHelper.Concat(
        IccTestDataPrimitives.UInt322,
        new byte[] { 0x00, 0x00, 0x00, 0x0C },  // 12
        new byte[] { (byte)'e', (byte)'n', 0x00, 0x00 },
        new byte[] { 0x00, 0x00, 0x00, 0x0C },  // 12
        new byte[] { 0x00, 0x00, 0x00, 0x28 },  // 40
        new byte[] { 0x00, 0x00, 0x00, 0x00 },
        new byte[] { 0x00, 0x00, 0x00, 0x0E },  // 14
        new byte[] { 0x00, 0x00, 0x00, 0x34 },  // 52
        IccTestDataPrimitives.UnicodeRand2,
        IccTestDataPrimitives.UnicodeRand3);

    public static readonly byte[] MultiLocalizedUnicodeArr2Write = ArrayHelper.Concat(
        IccTestDataPrimitives.UInt322,
        new byte[] { 0x00, 0x00, 0x00, 0x0C },  // 12
        new byte[] { (byte)'e', (byte)'n', 0x00, 0x00 },
        new byte[] { 0x00, 0x00, 0x00, 0x0C },  // 12
        new byte[] { 0x00, 0x00, 0x00, 0x28 },  // 40
        new byte[] { (byte)'x', (byte)'x', 0x00, 0x00 },
        new byte[] { 0x00, 0x00, 0x00, 0x0E },  // 14
        new byte[] { 0x00, 0x00, 0x00, 0x34 },  // 52
        IccTestDataPrimitives.UnicodeRand2,
        IccTestDataPrimitives.UnicodeRand3);

    public static readonly IccMultiLocalizedUnicodeTagDataEntry MultiLocalizedUnicodeVal3 = new(LocalizedStringSameArrEnUsDeDeEsXlXyXl);
    public static readonly byte[] MultiLocalizedUnicodeArr3 = ArrayHelper.Concat(
        IccTestDataPrimitives.UInt324,
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
        IccTestDataPrimitives.UnicodeRand2);

    public static readonly object[][] MultiLocalizedUnicodeTagDataEntryTestDataRead =
    {
        new object[] { MultiLocalizedUnicodeArr, MultiLocalizedUnicodeVal },
        new object[] { MultiLocalizedUnicodeArr2Read, MultiLocalizedUnicodeVal2 },
        new object[] { MultiLocalizedUnicodeArr3, MultiLocalizedUnicodeVal3 },
    };

    public static readonly object[][] MultiLocalizedUnicodeTagDataEntryTestDataWrite =
    {
        new object[] { MultiLocalizedUnicodeArr, MultiLocalizedUnicodeVal },
        new object[] { MultiLocalizedUnicodeArr2Write, MultiLocalizedUnicodeVal2 },
        new object[] { MultiLocalizedUnicodeArr3, MultiLocalizedUnicodeVal3 },
    };

    public static readonly IccMultiProcessElementsTagDataEntry MultiProcessElementsVal = new(
        new IccMultiProcessElement[]
        {
            IccTestDataMultiProcessElements.MpeValClut,
            IccTestDataMultiProcessElements.MpeValClut,
        });

    public static readonly byte[] MultiProcessElementsArr = ArrayHelper.Concat(
        IccTestDataPrimitives.UInt162,
        IccTestDataPrimitives.UInt163,
        IccTestDataPrimitives.UInt322,
        new byte[] { 0x00, 0x00, 0x00, 0x20 },  // 32
        new byte[] { 0x00, 0x00, 0x00, 0x84 },  // 132
        new byte[] { 0x00, 0x00, 0x00, 0xA4 },  // 164
        new byte[] { 0x00, 0x00, 0x00, 0x84 },  // 132
        IccTestDataMultiProcessElements.MpeClut,
        IccTestDataMultiProcessElements.MpeClut);

    public static readonly object[][] MultiProcessElementsTagDataEntryTestData =
    {
        new object[] { MultiProcessElementsArr, MultiProcessElementsVal },
    };

    public static readonly IccNamedColor2TagDataEntry NamedColor2Val = new(
        16909060,
        ArrayHelper.Fill('A', 31),
        ArrayHelper.Fill('4', 31),
        new IccNamedColor[] { IccTestDataNonPrimitives.NamedColorValMin, IccTestDataNonPrimitives.NamedColorValMin });

    public static readonly byte[] NamedColor2Arr = ArrayHelper.Concat(
        new byte[] { 0x01, 0x02, 0x03, 0x04 },
        IccTestDataPrimitives.UInt322,
        IccTestDataPrimitives.UInt323,
        ArrayHelper.Fill((byte)0x41, 31),
        new byte[] { 0x00 },
        ArrayHelper.Fill((byte)0x34, 31),
        new byte[] { 0x00 },
        IccTestDataNonPrimitives.NamedColorMin,
        IccTestDataNonPrimitives.NamedColorMin);

    public static readonly object[][] NamedColor2TagDataEntryTestData =
    {
        new object[] { NamedColor2Arr, NamedColor2Val },
    };

    public static readonly IccParametricCurveTagDataEntry ParametricCurveVal = new(IccTestDataCurves.ParametricValVar1);
    public static readonly byte[] ParametricCurveArr = IccTestDataCurves.ParametricVar1;

    public static readonly object[][] ParametricCurveTagDataEntryTestData =
    {
        new object[] { ParametricCurveArr, ParametricCurveVal },
    };

    public static readonly IccProfileSequenceDescTagDataEntry ProfileSequenceDescVal = new(
        new IccProfileDescription[]
        {
            IccTestDataNonPrimitives.ProfileDescriptionValRand1,
            IccTestDataNonPrimitives.ProfileDescriptionValRand1
        });

    public static readonly byte[] ProfileSequenceDescArr = ArrayHelper.Concat(
        IccTestDataPrimitives.UInt322,
        IccTestDataNonPrimitives.ProfileDescriptionRand1,
        IccTestDataNonPrimitives.ProfileDescriptionRand1);

    public static readonly object[][] ProfileSequenceDescTagDataEntryTestData =
    {
        new object[] { ProfileSequenceDescArr, ProfileSequenceDescVal },
    };

    public static readonly IccProfileSequenceIdentifierTagDataEntry ProfileSequenceIdentifier_Val = new(
        new[]
        {
            new IccProfileSequenceIdentifier(IccTestDataNonPrimitives.ProfileIdValRand, LocalizedStringRandArrEnUsDeDe),
            new IccProfileSequenceIdentifier(IccTestDataNonPrimitives.ProfileIdValRand, LocalizedStringRandArrEnUsDeDe),
        });

    public static readonly byte[] ProfileSequenceIdentifierArr = ArrayHelper.Concat(
        IccTestDataPrimitives.UInt322,
        new byte[] { 0x00, 0x00, 0x00, 0x1C },  // 28
        new byte[] { 0x00, 0x00, 0x00, 0x54 },  // 84
        new byte[] { 0x00, 0x00, 0x00, 0x70 },  // 112
        new byte[] { 0x00, 0x00, 0x00, 0x54 },  // 84
        IccTestDataNonPrimitives.ProfileIdRand,        // 16 bytes
        TagDataEntryHeaderMultiLocalizedUnicodeArr,    // 8  bytes
        MultiLocalizedUnicodeArr,                      // 58 bytes
        new byte[] { 0x00, 0x00 },                      // 2  bytes (padding)
        IccTestDataNonPrimitives.ProfileIdRand,
        TagDataEntryHeaderMultiLocalizedUnicodeArr,
        MultiLocalizedUnicodeArr,
        new byte[] { 0x00, 0x00 });

    public static readonly object[][] ProfileSequenceIdentifierTagDataEntryTestData =
    {
        new object[] { ProfileSequenceIdentifierArr, ProfileSequenceIdentifier_Val },
    };

    public static readonly IccResponseCurveSet16TagDataEntry ResponseCurveSet16Val = new(
        new[]
        {
            IccTestDataCurves.ResponseValGrad,
            IccTestDataCurves.ResponseValGrad,
        });

    public static readonly byte[] ResponseCurveSet16Arr = ArrayHelper.Concat(
        IccTestDataPrimitives.UInt163,
        IccTestDataPrimitives.UInt162,
        new byte[] { 0x00, 0x00, 0x00, 0x14 },  // 20
        new byte[] { 0x00, 0x00, 0x00, 0x6C },  // 108
        IccTestDataCurves.ResponseGrad, // 88 bytes
        IccTestDataCurves.ResponseGrad); // 88 bytes

    public static readonly object[][] ResponseCurveSet16TagDataEntryTestData =
    {
        new object[] { ResponseCurveSet16Arr, ResponseCurveSet16Val },
    };

    public static readonly IccFix16ArrayTagDataEntry Fix16ArrayVal = new(new[] { 1 / 256f, 2 / 256f, 3 / 256f });
    public static readonly byte[] Fix16ArrayArr = ArrayHelper.Concat(
        IccTestDataPrimitives.Fix161,
        IccTestDataPrimitives.Fix162,
        IccTestDataPrimitives.Fix163);

    public static readonly object[][] Fix16ArrayTagDataEntryTestData =
    {
        new object[] { Fix16ArrayArr, Fix16ArrayVal, 20u },
    };

    public static readonly IccSignatureTagDataEntry SignatureVal = new("ABCD");
    public static readonly byte[] SignatureArr = { 0x41, 0x42, 0x43, 0x44, };

    public static readonly object[][] SignatureTagDataEntryTestData =
    {
        new object[] { SignatureArr, SignatureVal },
    };

    public static readonly IccTextTagDataEntry TextVal = new("ABCD");
    public static readonly byte[] TextArr = { 0x41, 0x42, 0x43, 0x44 };

    public static readonly object[][] TextTagDataEntryTestData =
    {
        new object[] { TextArr, TextVal, 12u },
    };

    public static readonly IccUFix16ArrayTagDataEntry UFix16ArrayVal = new(new float[] { 1, 2, 3 });
    public static readonly byte[] UFix16ArrayArr = ArrayHelper.Concat(
        IccTestDataPrimitives.UFix161,
        IccTestDataPrimitives.UFix162,
        IccTestDataPrimitives.UFix163);

    public static readonly object[][] UFix16ArrayTagDataEntryTestData =
    {
        new object[] { UFix16ArrayArr, UFix16ArrayVal, 20u },
    };

    public static readonly IccUInt16ArrayTagDataEntry UInt16ArrayVal = new(new ushort[] { 1, 2, 3 });
    public static readonly byte[] UInt16ArrayArr = ArrayHelper.Concat(
        IccTestDataPrimitives.UInt161,
        IccTestDataPrimitives.UInt162,
        IccTestDataPrimitives.UInt163);

    public static readonly object[][] UInt16ArrayTagDataEntryTestData =
    {
        new object[] { UInt16ArrayArr, UInt16ArrayVal, 14u },
    };

    public static readonly IccUInt32ArrayTagDataEntry UInt32ArrayVal = new(new uint[] { 1, 2, 3 });
    public static readonly byte[] UInt32ArrayArr = ArrayHelper.Concat(
        IccTestDataPrimitives.UInt321,
        IccTestDataPrimitives.UInt322,
        IccTestDataPrimitives.UInt323);

    public static readonly object[][] UInt32ArrayTagDataEntryTestData =
    {
        new object[] { UInt32ArrayArr, UInt32ArrayVal, 20u },
    };

    public static readonly IccUInt64ArrayTagDataEntry UInt64ArrayVal = new(new ulong[] { 1, 2, 3 });
    public static readonly byte[] UInt64ArrayArr = ArrayHelper.Concat(
        IccTestDataPrimitives.UInt641,
        IccTestDataPrimitives.UInt642,
        IccTestDataPrimitives.UInt643);

    public static readonly object[][] UInt64ArrayTagDataEntryTestData =
    {
        new object[] { UInt64ArrayArr, UInt64ArrayVal, 32u },
    };

    public static readonly IccUInt8ArrayTagDataEntry UInt8ArrayVal = new(new byte[] { 1, 2, 3 });
    public static readonly byte[] UInt8ArrayArr = { 1, 2, 3 };

    public static readonly object[][] UInt8ArrayTagDataEntryTestData =
    {
        new object[] { UInt8ArrayArr, UInt8ArrayVal, 11u },
    };

    public static readonly IccViewingConditionsTagDataEntry ViewingConditionsVal = new(
        IccTestDataNonPrimitives.XyzNumberValVar1,
        IccTestDataNonPrimitives.XyzNumberValVar2,
        IccStandardIlluminant.D50);

    public static readonly byte[] ViewingConditionsArr = ArrayHelper.Concat(
        IccTestDataNonPrimitives.XyzNumberVar1,
        IccTestDataNonPrimitives.XyzNumberVar2,
        IccTestDataPrimitives.UInt321);

    public static readonly object[][] ViewingConditionsTagDataEntryTestData =
    {
        new object[] { ViewingConditionsArr, ViewingConditionsVal },
    };

    public static readonly IccXyzTagDataEntry XyzVal = new(new[]
    {
        IccTestDataNonPrimitives.XyzNumberValVar1,
        IccTestDataNonPrimitives.XyzNumberValVar2,
        IccTestDataNonPrimitives.XyzNumberValVar3,
    });

    public static readonly byte[] XyzArr = ArrayHelper.Concat(
        IccTestDataNonPrimitives.XyzNumberVar1,
        IccTestDataNonPrimitives.XyzNumberVar2,
        IccTestDataNonPrimitives.XyzNumberVar3);

    public static readonly object[][] XYZTagDataEntryTestData =
    {
        new object[] { XyzArr, XyzVal, 44u },
    };

    public static readonly IccTextDescriptionTagDataEntry TextDescriptionVal1 = new(
        IccTestDataPrimitives.AsciiValRand,
        IccTestDataPrimitives.UnicodeValRand1,
        ArrayHelper.Fill('A', 66),
        1701729619,
        2);

    public static readonly byte[] TextDescriptionArr1 = ArrayHelper.Concat(
        new byte[] { 0x00, 0x00, 0x00, 0x0B }, // 11
        IccTestDataPrimitives.AsciiRand,
        new byte[] { 0x00 }, // Null terminator
        new byte[] { 0x65, 0x6E, 0x55, 0x53 }, // enUS
        new byte[] { 0x00, 0x00, 0x00, 0x0E }, // 14
        IccTestDataPrimitives.UnicodeRand1,
        new byte[] { 0x00, 0x00 }, // Null terminator
        new byte[] { 0x00, 0x02, 0x43 }, // 2, 67
        ArrayHelper.Fill((byte)0x41, 66),
        new byte[] { 0x00 }); // Null terminator

    public static readonly IccTextDescriptionTagDataEntry TextDescriptionVal2 = new(IccTestDataPrimitives.AsciiValRand, null, null, 0, 0);
    public static readonly byte[] TextDescriptionArr2 = ArrayHelper.Concat(
        new byte[] { 0x00, 0x00, 0x00, 0x0B },  // 11
        IccTestDataPrimitives.AsciiRand,
        new byte[] { 0x00 },                    // Null terminator
        IccTestDataPrimitives.UInt320,
        IccTestDataPrimitives.UInt320,
        new byte[] { 0x00, 0x00, 0x00 },        // 0, 0
        ArrayHelper.Fill((byte)0x00, 67));

    public static readonly object[][] TextDescriptionTagDataEntryTestData =
    {
        new object[] { TextDescriptionArr1, TextDescriptionVal1 },
        new object[] { TextDescriptionArr2, TextDescriptionVal2 },
    };

    public static readonly IccCrdInfoTagDataEntry CrdInfoVal = new(
        IccTestDataPrimitives.AsciiValRand4,
        IccTestDataPrimitives.AsciiValRand1,
        IccTestDataPrimitives.AsciiValRand2,
        IccTestDataPrimitives.AsciiValRand3,
        IccTestDataPrimitives.AsciiValRand4);

    public static readonly byte[] CrdInfoArr = ArrayHelper.Concat(
        IccTestDataPrimitives.UInt326,
        IccTestDataPrimitives.AsciiRand4,
        new byte[] { 0 },
        IccTestDataPrimitives.UInt326,
        IccTestDataPrimitives.AsciiRand1,
        new byte[] { 0 },
        IccTestDataPrimitives.UInt326,
        IccTestDataPrimitives.AsciiRand2,
        new byte[] { 0 },
        IccTestDataPrimitives.UInt326,
        IccTestDataPrimitives.AsciiRand3,
        new byte[] { 0 },
        IccTestDataPrimitives.UInt326,
        IccTestDataPrimitives.AsciiRand4,
        new byte[] { 0 });

    public static readonly object[][] CrdInfoTagDataEntryTestData =
    {
        new object[] { CrdInfoArr, CrdInfoVal },
    };

    public static readonly IccScreeningTagDataEntry ScreeningVal = new(
        IccScreeningFlag.DefaultScreens | IccScreeningFlag.UnitLinesPerCm,
        new IccScreeningChannel[] { IccTestDataNonPrimitives.ScreeningChannelValRand1, IccTestDataNonPrimitives.ScreeningChannelValRand2 });

    public static readonly byte[] ScreeningArr = ArrayHelper.Concat(
        IccTestDataPrimitives.Int321,
        IccTestDataPrimitives.UInt322,
        IccTestDataNonPrimitives.ScreeningChannelRand1,
        IccTestDataNonPrimitives.ScreeningChannelRand2);

    public static readonly object[][] ScreeningTagDataEntryTestData =
    {
        new object[] { ScreeningArr, ScreeningVal },
    };

    public static readonly IccUcrBgTagDataEntry UcrBgVal = new(
        new ushort[] { 3, 4, 6 },
        new ushort[] { 9, 7, 2, 5 },
        IccTestDataPrimitives.AsciiValRand);

    public static readonly byte[] UcrBgArr = ArrayHelper.Concat(
        IccTestDataPrimitives.UInt323,
        IccTestDataPrimitives.UInt163,
        IccTestDataPrimitives.UInt164,
        IccTestDataPrimitives.UInt166,
        IccTestDataPrimitives.UInt324,
        IccTestDataPrimitives.UInt169,
        IccTestDataPrimitives.UInt167,
        IccTestDataPrimitives.UInt162,
        IccTestDataPrimitives.UInt165,
        IccTestDataPrimitives.AsciiRand,
        new byte[] { 0 });

    public static readonly object[][] UcrBgTagDataEntryTestData =
    {
        new object[] { UcrBgArr, UcrBgVal, 41 },
    };

    public static readonly IccTagDataEntry TagDataEntryCurveVal = CurveVal2;
    public static readonly byte[] TagDataEntryCurveArr = ArrayHelper.Concat(
        TagDataEntryHeaderCurveArr,
        CurveArr2,
        new byte[] { 0x00, 0x00 }); // padding

    public static readonly IccTagDataEntry TagDataEntryMultiLocalizedUnicodeVal = MultiLocalizedUnicodeVal;
    public static readonly byte[] TagDataEntryMultiLocalizedUnicodeArr = ArrayHelper.Concat(
        TagDataEntryHeaderMultiLocalizedUnicodeArr,
        MultiLocalizedUnicodeArr,
        new byte[] { 0x00, 0x00 }); // padding

    public static readonly IccTagTableEntry TagDataEntryMultiLocalizedUnicodeTable = new(
        IccProfileTag.Unknown,
        0,
        (uint)TagDataEntryMultiLocalizedUnicodeArr.Length - 2);

    public static readonly IccTagTableEntry TagDataEntryCurveTable = new(IccProfileTag.Unknown, 0, (uint)TagDataEntryCurveArr.Length - 2);

    public static readonly object[][] TagDataEntryTestData =
    {
        new object[] { TagDataEntryCurveArr, TagDataEntryCurveVal },
        new object[] { TagDataEntryMultiLocalizedUnicodeArr, TagDataEntryMultiLocalizedUnicodeVal },
    };
}
