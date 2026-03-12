// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Globalization;
using System.Numerics;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;

namespace SixLabors.ImageSharp.Tests.TestDataIcc;

internal static class IccTestDataNonPrimitives
{
    public static readonly DateTime DateTimeValMin = new(1, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    public static readonly DateTime DateTimeValMax = new(9999, 12, 31, 23, 59, 59, DateTimeKind.Utc);
    public static readonly DateTime DateTimeValRand1 = new(1990, 11, 26, 3, 19, 47, DateTimeKind.Utc);

    public static readonly byte[] DateTimeMin =
    [
        0x00, 0x01, // Year      1
        0x00, 0x01, // Month     1
        0x00, 0x01, // Day       1
        0x00, 0x00, // Hour      0
        0x00, 0x00, // Minute    0
        0x00, 0x00 // Second    0
    ];

    public static readonly byte[] DateTimeMax =
    [
        0x27, 0x0F, // Year      9999
            0x00, 0x0C, // Month     12
            0x00, 0x1F, // Day       31
            0x00, 0x17, // Hour      23
            0x00, 0x3B, // Minute    59
            0x00, 0x3B // Second    59
    ];

    public static readonly byte[] DateTimeInvalid =
    [
        0xFF, 0xFF, // Year      65535
            0x00, 0x0E, // Month     14
            0x00, 0x21, // Day       33
            0x00, 0x19, // Hour      25
            0x00, 0x3D, // Minute    61
            0x00, 0x3D // Second    61
    ];

    public static readonly byte[] DateTimeRand1 =
    [
        0x07, 0xC6, // Year      1990
            0x00, 0x0B, // Month     11
            0x00, 0x1A, // Day       26
            0x00, 0x03, // Hour      3
            0x00, 0x13, // Minute    19
            0x00, 0x2F // Second    47
    ];

    public static readonly object[][] DateTimeTestData =
    [
        [DateTimeMin, DateTimeValMin],
        [DateTimeMax, DateTimeValMax],
        [DateTimeRand1, DateTimeValRand1]
    ];

    public static readonly IccVersion VersionNumberValMin = new(0, 0, 0);
    public static readonly IccVersion VersionNumberVal211 = new(2, 1, 1);
    public static readonly IccVersion VersionNumberVal430 = new(4, 3, 0);
    public static readonly IccVersion VersionNumberValMax = new(255, 15, 15);

    public static readonly byte[] VersionNumberMin = [0x00, 0x00, 0x00, 0x00];
    public static readonly byte[] VersionNumber211 = [0x02, 0x11, 0x00, 0x00];
    public static readonly byte[] VersionNumber430 = [0x04, 0x30, 0x00, 0x00];
    public static readonly byte[] VersionNumberMax = [0xFF, 0xFF, 0x00, 0x00];

    public static readonly object[][] VersionNumberTestData =
    [
        [VersionNumberMin, VersionNumberValMin],
        [VersionNumber211, VersionNumberVal211],
        [VersionNumber430, VersionNumberVal430],
        [VersionNumberMax, VersionNumberValMax]
    ];

    public static readonly Vector3 XyzNumberValMin = new(IccTestDataPrimitives.Fix16ValMin, IccTestDataPrimitives.Fix16ValMin, IccTestDataPrimitives.Fix16ValMin);
    public static readonly Vector3 XyzNumberVal0 = new(0, 0, 0);
    public static readonly Vector3 XyzNumberVal1 = new(1, 1, 1);
    public static readonly Vector3 XyzNumberValVar1 = new(1, 2, 3);
    public static readonly Vector3 XyzNumberValVar2 = new(4, 5, 6);
    public static readonly Vector3 XyzNumberValVar3 = new(7, 8, 9);
    public static readonly Vector3 XyzNumberValMax = new(IccTestDataPrimitives.Fix16ValMax, IccTestDataPrimitives.Fix16ValMax, IccTestDataPrimitives.Fix16ValMax);

    public static readonly byte[] XyzNumberMin = ArrayHelper.Concat(IccTestDataPrimitives.Fix16Min, IccTestDataPrimitives.Fix16Min, IccTestDataPrimitives.Fix16Min);
    public static readonly byte[] XyzNumber0 = ArrayHelper.Concat(IccTestDataPrimitives.Fix160, IccTestDataPrimitives.Fix160, IccTestDataPrimitives.Fix160);
    public static readonly byte[] XyzNumber1 = ArrayHelper.Concat(IccTestDataPrimitives.Fix161, IccTestDataPrimitives.Fix161, IccTestDataPrimitives.Fix161);
    public static readonly byte[] XyzNumberVar1 = ArrayHelper.Concat(IccTestDataPrimitives.Fix161, IccTestDataPrimitives.Fix162, IccTestDataPrimitives.Fix163);
    public static readonly byte[] XyzNumberVar2 = ArrayHelper.Concat(IccTestDataPrimitives.Fix164, IccTestDataPrimitives.Fix165, IccTestDataPrimitives.Fix166);
    public static readonly byte[] XyzNumberVar3 = ArrayHelper.Concat(IccTestDataPrimitives.Fix167, IccTestDataPrimitives.Fix168, IccTestDataPrimitives.Fix169);
    public static readonly byte[] XyzNumberMax = ArrayHelper.Concat(IccTestDataPrimitives.Fix16Max, IccTestDataPrimitives.Fix16Max, IccTestDataPrimitives.Fix16Max);

    public static readonly object[][] XyzNumberTestData =
    [
        [XyzNumberMin, XyzNumberValMin],
        [XyzNumber0, XyzNumberVal0],
        [XyzNumberVar1, XyzNumberValVar1],
        [XyzNumberMax, XyzNumberValMax]
    ];

    public static readonly IccProfileId ProfileIdValMin = new(0, 0, 0, 0);
    public static readonly IccProfileId ProfileIdValRand = new(IccTestDataPrimitives.UInt32ValRand1, IccTestDataPrimitives.UInt32ValRand2, IccTestDataPrimitives.UInt32ValRand3, IccTestDataPrimitives.UInt32ValRand4);
    public static readonly IccProfileId ProfileIdValMax = new(uint.MaxValue, uint.MaxValue, uint.MaxValue, uint.MaxValue);

    public static readonly byte[] ProfileIdMin = ArrayHelper.Concat(IccTestDataPrimitives.UInt320, IccTestDataPrimitives.UInt320, IccTestDataPrimitives.UInt320, IccTestDataPrimitives.UInt320);
    public static readonly byte[] ProfileIdRand = ArrayHelper.Concat(IccTestDataPrimitives.UInt32Rand1, IccTestDataPrimitives.UInt32Rand2, IccTestDataPrimitives.UInt32Rand3, IccTestDataPrimitives.UInt32Rand4);
    public static readonly byte[] ProfileIdMax = ArrayHelper.Concat(IccTestDataPrimitives.UInt32Max, IccTestDataPrimitives.UInt32Max, IccTestDataPrimitives.UInt32Max, IccTestDataPrimitives.UInt32Max);

    public static readonly object[][] ProfileIdTestData =
    [
        [ProfileIdMin, ProfileIdValMin],
        [ProfileIdRand, ProfileIdValRand],
        [ProfileIdMax, ProfileIdValMax]
    ];

    public static readonly IccPositionNumber PositionNumberValMin = new(0, 0);
    public static readonly IccPositionNumber PositionNumberValRand = new(IccTestDataPrimitives.UInt32ValRand1, IccTestDataPrimitives.UInt32ValRand2);
    public static readonly IccPositionNumber PositionNumberValMax = new(uint.MaxValue, uint.MaxValue);

    public static readonly byte[] PositionNumberMin = ArrayHelper.Concat(IccTestDataPrimitives.UInt320, IccTestDataPrimitives.UInt320);
    public static readonly byte[] PositionNumberRand = ArrayHelper.Concat(IccTestDataPrimitives.UInt32Rand1, IccTestDataPrimitives.UInt32Rand2);
    public static readonly byte[] PositionNumberMax = ArrayHelper.Concat(IccTestDataPrimitives.UInt32Max, IccTestDataPrimitives.UInt32Max);

    public static readonly object[][] PositionNumberTestData =
    [
        [PositionNumberMin, PositionNumberValMin],
        [PositionNumberRand, PositionNumberValRand],
        [PositionNumberMax, PositionNumberValMax]
    ];

    public static readonly IccResponseNumber ResponseNumberValMin = new(0, IccTestDataPrimitives.Fix16ValMin);
    public static readonly IccResponseNumber ResponseNumberVal1 = new(1, 1);
    public static readonly IccResponseNumber ResponseNumberVal2 = new(2, 2);
    public static readonly IccResponseNumber ResponseNumberVal3 = new(3, 3);
    public static readonly IccResponseNumber ResponseNumberVal4 = new(4, 4);
    public static readonly IccResponseNumber ResponseNumberVal5 = new(5, 5);
    public static readonly IccResponseNumber ResponseNumberVal6 = new(6, 6);
    public static readonly IccResponseNumber ResponseNumberVal7 = new(7, 7);
    public static readonly IccResponseNumber ResponseNumberVal8 = new(8, 8);
    public static readonly IccResponseNumber ResponseNumberVal9 = new(9, 9);
    public static readonly IccResponseNumber ResponseNumberValMax = new(ushort.MaxValue, IccTestDataPrimitives.Fix16ValMax);

    public static readonly byte[] ResponseNumberMin = ArrayHelper.Concat(IccTestDataPrimitives.UInt160, IccTestDataPrimitives.Fix16Min);
    public static readonly byte[] ResponseNumber1 = ArrayHelper.Concat(IccTestDataPrimitives.UInt161, IccTestDataPrimitives.Fix161);
    public static readonly byte[] ResponseNumber2 = ArrayHelper.Concat(IccTestDataPrimitives.UInt162, IccTestDataPrimitives.Fix162);
    public static readonly byte[] ResponseNumber3 = ArrayHelper.Concat(IccTestDataPrimitives.UInt163, IccTestDataPrimitives.Fix163);
    public static readonly byte[] ResponseNumber4 = ArrayHelper.Concat(IccTestDataPrimitives.UInt164, IccTestDataPrimitives.Fix164);
    public static readonly byte[] ResponseNumber5 = ArrayHelper.Concat(IccTestDataPrimitives.UInt165, IccTestDataPrimitives.Fix165);
    public static readonly byte[] ResponseNumber6 = ArrayHelper.Concat(IccTestDataPrimitives.UInt166, IccTestDataPrimitives.Fix166);
    public static readonly byte[] ResponseNumber7 = ArrayHelper.Concat(IccTestDataPrimitives.UInt167, IccTestDataPrimitives.Fix167);
    public static readonly byte[] ResponseNumber8 = ArrayHelper.Concat(IccTestDataPrimitives.UInt168, IccTestDataPrimitives.Fix168);
    public static readonly byte[] ResponseNumber9 = ArrayHelper.Concat(IccTestDataPrimitives.UInt169, IccTestDataPrimitives.Fix169);
    public static readonly byte[] ResponseNumberMax = ArrayHelper.Concat(IccTestDataPrimitives.UInt16Max, IccTestDataPrimitives.Fix16Max);

    public static readonly object[][] ResponseNumberTestData =
    [
        [ResponseNumberMin, ResponseNumberValMin],
        [ResponseNumber1, ResponseNumberVal1],
        [ResponseNumber4, ResponseNumberVal4],
        [ResponseNumberMax, ResponseNumberValMax]
    ];

    public static readonly IccNamedColor NamedColorValMin = new(
        ArrayHelper.Fill('A', 31),
        [0, 0, 0],
        [0, 0, 0]);

    public static readonly IccNamedColor NamedColorValRand = new(
        ArrayHelper.Fill('5', 31),
        [10794, 10794, 10794],
        [17219, 17219, 17219, 17219, 17219]);

    public static readonly IccNamedColor NamedColorValMax = new(
        ArrayHelper.Fill('4', 31),
        [ushort.MaxValue, ushort.MaxValue, ushort.MaxValue],
        [ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue]);

    public static readonly byte[] NamedColorMin = CreateNamedColor(3, 0x41, 0x00, 0x00);
    public static readonly byte[] NamedColorRand = CreateNamedColor(5, 0x35, 42, 67);
    public static readonly byte[] NamedColorMax = CreateNamedColor(4, 0x34, 0xFF, 0xFF);

    private static byte[] CreateNamedColor(int devCoordCount, byte name, byte pCs, byte device)
    {
        byte[] data = new byte[32 + 6 + (devCoordCount * 2)];
        for (int i = 0; i < data.Length; i++)
        {
            if (i < 31)
            {
                data[i] = name; // Name
            }
            else if (i is 31)
            {
                data[i] = 0x00; // Name null terminator
            }
            else if (i < 32 + 6)
            {
                data[i] = pCs; // PCS Coordinates
            }
            else
            {
                data[i] = device; // Device Coordinates
            }
        }

        return data;
    }

    public static readonly object[][] NamedColorTestData =
    [
        [NamedColorMin, NamedColorValMin, 3u],
        [NamedColorRand, NamedColorValRand, 5u],
        [NamedColorMax, NamedColorValMax, 4u]
    ];

    private static readonly CultureInfo CultureEnUs = new("en-US");
    private static readonly CultureInfo CultureDeAt = new("de-AT");

    private static readonly IccLocalizedString LocalizedStringRand1 = new(CultureEnUs, IccTestDataPrimitives.UnicodeValRand2);
    private static readonly IccLocalizedString LocalizedStringRand2 = new(CultureDeAt, IccTestDataPrimitives.UnicodeValRand3);

    private static readonly IccLocalizedString[] LocalizedStringRandArr1 =
    [
        LocalizedStringRand1,
        LocalizedStringRand2
    ];

    private static readonly IccMultiLocalizedUnicodeTagDataEntry MultiLocalizedUnicodeVal = new(LocalizedStringRandArr1);
    private static readonly byte[] MultiLocalizedUnicodeArr = ArrayHelper.Concat(
        IccTestDataPrimitives.UInt322,
        new byte[] { 0x00, 0x00, 0x00, 0x0C }, // 12
        [(byte)'e', (byte)'n', (byte)'U', (byte)'S'],
        new byte[] { 0x00, 0x00, 0x00, 0x0C },  // 12
        new byte[] { 0x00, 0x00, 0x00, 0x28 },  // 40
        [(byte)'d', (byte)'e', (byte)'A', (byte)'T'],
        new byte[] { 0x00, 0x00, 0x00, 0x0E },  // 14
        new byte[] { 0x00, 0x00, 0x00, 0x34 },  // 52
        IccTestDataPrimitives.UnicodeRand2,
        IccTestDataPrimitives.UnicodeRand3);

    public static readonly IccTextDescriptionTagDataEntry TextDescriptionVal1 = new(
        IccTestDataPrimitives.AsciiValRand,
        IccTestDataPrimitives.UnicodeValRand1,
        ArrayHelper.Fill('A', 66),
        1701729619,
        2);

    public static readonly byte[] TextDescriptionArr1 = ArrayHelper.Concat(
        new byte[] { 0x00, 0x00, 0x00, 0x0B },  // 11
        IccTestDataPrimitives.AsciiRand,
        new byte[] { 0x00 },                    // Null terminator
        [(byte)'e', (byte)'n', (byte)'U', (byte)'S'],
        new byte[] { 0x00, 0x00, 0x00, 0x07 },  // 7
        IccTestDataPrimitives.UnicodeRand2,
        new byte[] { 0x00, 0x00 },              // Null terminator
        new byte[] { 0x00, 0x02, 0x43 },        // 2, 67
        ArrayHelper.Fill((byte)0x41, 66),
        new byte[] { 0x00 }); // Null terminator

    public static readonly IccProfileDescription ProfileDescriptionValRand1 = new(
        1,
        2,
        IccDeviceAttribute.ChromaBlackWhite | IccDeviceAttribute.ReflectivityMatte,
        IccProfileTag.ProfileDescription,
        MultiLocalizedUnicodeVal.Texts,
        MultiLocalizedUnicodeVal.Texts);

    public static readonly IccProfileDescription ProfileDescriptionValRand2 = new(
        1,
        2,
        IccDeviceAttribute.ChromaBlackWhite | IccDeviceAttribute.ReflectivityMatte,
        IccProfileTag.ProfileDescription,
        [LocalizedStringRand1],
        [LocalizedStringRand1]);

    public static readonly byte[] ProfileDescriptionRand1 = ArrayHelper.Concat(
        IccTestDataPrimitives.UInt321,
        IccTestDataPrimitives.UInt322,
        new byte[] { 0, 0, 0, 0, 0, 0, 0, 10 },
        new byte[] { 0x64, 0x65, 0x73, 0x63 },
        new byte[] { 0x6D, 0x6C, 0x75, 0x63 },
        new byte[] { 0x00, 0x00, 0x00, 0x00 },
        MultiLocalizedUnicodeArr,
        new byte[] { 0x6D, 0x6C, 0x75, 0x63 },
        new byte[] { 0x00, 0x00, 0x00, 0x00 },
        MultiLocalizedUnicodeArr);

    public static readonly byte[] ProfileDescriptionRand2 = ArrayHelper.Concat(
        IccTestDataPrimitives.UInt321,
        IccTestDataPrimitives.UInt322,
        new byte[] { 0, 0, 0, 0, 0, 0, 0, 10 },
        new byte[] { 0x64, 0x65, 0x73, 0x63 },
        new byte[] { 0x64, 0x65, 0x73, 0x63 },
        new byte[] { 0x00, 0x00, 0x00, 0x00 },
        TextDescriptionArr1,
        new byte[] { 0x64, 0x65, 0x73, 0x63 },
        new byte[] { 0x00, 0x00, 0x00, 0x00 },
        TextDescriptionArr1);

    public static readonly object[][] ProfileDescriptionReadTestData =
    [
        [ProfileDescriptionRand1, ProfileDescriptionValRand1],
        [ProfileDescriptionRand2, ProfileDescriptionValRand2]
    ];

    public static readonly object[][] ProfileDescriptionWriteTestData =
    [
        [ProfileDescriptionRand1, ProfileDescriptionValRand1]
    ];

    public static readonly IccColorantTableEntry ColorantTableEntryValRand1 = new(ArrayHelper.Fill('A', 31), 1, 2, 3);
    public static readonly IccColorantTableEntry ColorantTableEntryValRand2 = new(ArrayHelper.Fill('4', 31), 4, 5, 6);

    public static readonly byte[] ColorantTableEntryRand1 = ArrayHelper.Concat(
        ArrayHelper.Fill((byte)0x41, 31),
        new byte[1],    // null terminator
        IccTestDataPrimitives.UInt161,
        IccTestDataPrimitives.UInt162,
        IccTestDataPrimitives.UInt163);

    public static readonly byte[] ColorantTableEntryRand2 = ArrayHelper.Concat(
        ArrayHelper.Fill((byte)0x34, 31),
        new byte[1],    // null terminator
        IccTestDataPrimitives.UInt164,
        IccTestDataPrimitives.UInt165,
        IccTestDataPrimitives.UInt166);

    public static readonly object[][] ColorantTableEntryTestData =
    [
        [ColorantTableEntryRand1, ColorantTableEntryValRand1],
        [ColorantTableEntryRand2, ColorantTableEntryValRand2]
    ];

    public static readonly IccScreeningChannel ScreeningChannelValRand1 = new(4, 6, IccScreeningSpotType.Cross);
    public static readonly IccScreeningChannel ScreeningChannelValRand2 = new(8, 5, IccScreeningSpotType.Diamond);

    public static readonly byte[] ScreeningChannelRand1 = ArrayHelper.Concat(
        IccTestDataPrimitives.Fix164,
        IccTestDataPrimitives.Fix166,
        IccTestDataPrimitives.Int327);

    public static readonly byte[] ScreeningChannelRand2 = ArrayHelper.Concat(
        IccTestDataPrimitives.Fix168,
        IccTestDataPrimitives.Fix165,
        IccTestDataPrimitives.Int323);

    public static readonly object[][] ScreeningChannelTestData =
    [
        [ScreeningChannelRand1, ScreeningChannelValRand1],
        [ScreeningChannelRand2, ScreeningChannelValRand2]
    ];
}
