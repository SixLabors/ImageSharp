// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Globalization;
using System.Numerics;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;

namespace SixLabors.ImageSharp.Tests;

internal static class IccTestDataNonPrimitives
{
    public static readonly DateTime DateTime_ValMin = new(1, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    public static readonly DateTime DateTime_ValMax = new(9999, 12, 31, 23, 59, 59, DateTimeKind.Utc);
    public static readonly DateTime DateTime_ValRand1 = new(1990, 11, 26, 3, 19, 47, DateTimeKind.Utc);

    public static readonly byte[] DateTime_Min =
    [
        0x00, 0x01, // Year      1
        0x00, 0x01, // Month     1
        0x00, 0x01, // Day       1
        0x00, 0x00, // Hour      0
        0x00, 0x00, // Minute    0
        0x00, 0x00 // Second    0
    ];

    public static readonly byte[] DateTime_Max =
    [
        0x27, 0x0F, // Year      9999
            0x00, 0x0C, // Month     12
            0x00, 0x1F, // Day       31
            0x00, 0x17, // Hour      23
            0x00, 0x3B, // Minute    59
            0x00, 0x3B // Second    59
    ];

    public static readonly byte[] DateTime_Invalid =
    [
        0xFF, 0xFF, // Year      65535
            0x00, 0x0E, // Month     14
            0x00, 0x21, // Day       33
            0x00, 0x19, // Hour      25
            0x00, 0x3D, // Minute    61
            0x00, 0x3D // Second    61
    ];

    public static readonly byte[] DateTime_Rand1 =
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
        [DateTime_Min, DateTime_ValMin],
        [DateTime_Max, DateTime_ValMax],
        [DateTime_Rand1, DateTime_ValRand1]
    ];

    public static readonly IccVersion VersionNumber_ValMin = new(0, 0, 0);
    public static readonly IccVersion VersionNumber_Val211 = new(2, 1, 1);
    public static readonly IccVersion VersionNumber_Val430 = new(4, 3, 0);
    public static readonly IccVersion VersionNumber_ValMax = new(255, 15, 15);

    public static readonly byte[] VersionNumber_Min = [0x00, 0x00, 0x00, 0x00];
    public static readonly byte[] VersionNumber_211 = [0x02, 0x11, 0x00, 0x00];
    public static readonly byte[] VersionNumber_430 = [0x04, 0x30, 0x00, 0x00];
    public static readonly byte[] VersionNumber_Max = [0xFF, 0xFF, 0x00, 0x00];

    public static readonly object[][] VersionNumberTestData =
    [
        [VersionNumber_Min, VersionNumber_ValMin],
        [VersionNumber_211, VersionNumber_Val211],
        [VersionNumber_430, VersionNumber_Val430],
        [VersionNumber_Max, VersionNumber_ValMax]
    ];

    public static readonly Vector3 XyzNumber_ValMin = new(IccTestDataPrimitives.Fix16_ValMin, IccTestDataPrimitives.Fix16_ValMin, IccTestDataPrimitives.Fix16_ValMin);
    public static readonly Vector3 XyzNumber_Val0 = new(0, 0, 0);
    public static readonly Vector3 XyzNumber_Val1 = new(1, 1, 1);
    public static readonly Vector3 XyzNumber_ValVar1 = new(1, 2, 3);
    public static readonly Vector3 XyzNumber_ValVar2 = new(4, 5, 6);
    public static readonly Vector3 XyzNumber_ValVar3 = new(7, 8, 9);
    public static readonly Vector3 XyzNumber_ValMax = new(IccTestDataPrimitives.Fix16_ValMax, IccTestDataPrimitives.Fix16_ValMax, IccTestDataPrimitives.Fix16_ValMax);

    public static readonly byte[] XyzNumber_Min = ArrayHelper.Concat(IccTestDataPrimitives.Fix16_Min, IccTestDataPrimitives.Fix16_Min, IccTestDataPrimitives.Fix16_Min);
    public static readonly byte[] XyzNumber_0 = ArrayHelper.Concat(IccTestDataPrimitives.Fix16_0, IccTestDataPrimitives.Fix16_0, IccTestDataPrimitives.Fix16_0);
    public static readonly byte[] XyzNumber_1 = ArrayHelper.Concat(IccTestDataPrimitives.Fix16_1, IccTestDataPrimitives.Fix16_1, IccTestDataPrimitives.Fix16_1);
    public static readonly byte[] XyzNumber_Var1 = ArrayHelper.Concat(IccTestDataPrimitives.Fix16_1, IccTestDataPrimitives.Fix16_2, IccTestDataPrimitives.Fix16_3);
    public static readonly byte[] XyzNumber_Var2 = ArrayHelper.Concat(IccTestDataPrimitives.Fix16_4, IccTestDataPrimitives.Fix16_5, IccTestDataPrimitives.Fix16_6);
    public static readonly byte[] XyzNumber_Var3 = ArrayHelper.Concat(IccTestDataPrimitives.Fix16_7, IccTestDataPrimitives.Fix16_8, IccTestDataPrimitives.Fix16_9);
    public static readonly byte[] XyzNumber_Max = ArrayHelper.Concat(IccTestDataPrimitives.Fix16_Max, IccTestDataPrimitives.Fix16_Max, IccTestDataPrimitives.Fix16_Max);

    public static readonly object[][] XyzNumberTestData =
    [
        [XyzNumber_Min, XyzNumber_ValMin],
        [XyzNumber_0, XyzNumber_Val0],
        [XyzNumber_Var1, XyzNumber_ValVar1],
        [XyzNumber_Max, XyzNumber_ValMax]
    ];

    public static readonly IccProfileId ProfileId_ValMin = new(0, 0, 0, 0);
    public static readonly IccProfileId ProfileId_ValRand = new(IccTestDataPrimitives.UInt32_ValRand1, IccTestDataPrimitives.UInt32_ValRand2, IccTestDataPrimitives.UInt32_ValRand3, IccTestDataPrimitives.UInt32_ValRand4);
    public static readonly IccProfileId ProfileId_ValMax = new(uint.MaxValue, uint.MaxValue, uint.MaxValue, uint.MaxValue);

    public static readonly byte[] ProfileId_Min = ArrayHelper.Concat(IccTestDataPrimitives.UInt32_0, IccTestDataPrimitives.UInt32_0, IccTestDataPrimitives.UInt32_0, IccTestDataPrimitives.UInt32_0);
    public static readonly byte[] ProfileId_Rand = ArrayHelper.Concat(IccTestDataPrimitives.UInt32_Rand1, IccTestDataPrimitives.UInt32_Rand2, IccTestDataPrimitives.UInt32_Rand3, IccTestDataPrimitives.UInt32_Rand4);
    public static readonly byte[] ProfileId_Max = ArrayHelper.Concat(IccTestDataPrimitives.UInt32_Max, IccTestDataPrimitives.UInt32_Max, IccTestDataPrimitives.UInt32_Max, IccTestDataPrimitives.UInt32_Max);

    public static readonly object[][] ProfileIdTestData =
    [
        [ProfileId_Min, ProfileId_ValMin],
        [ProfileId_Rand, ProfileId_ValRand],
        [ProfileId_Max, ProfileId_ValMax]
    ];

    public static readonly IccPositionNumber PositionNumber_ValMin = new(0, 0);
    public static readonly IccPositionNumber PositionNumber_ValRand = new(IccTestDataPrimitives.UInt32_ValRand1, IccTestDataPrimitives.UInt32_ValRand2);
    public static readonly IccPositionNumber PositionNumber_ValMax = new(uint.MaxValue, uint.MaxValue);

    public static readonly byte[] PositionNumber_Min = ArrayHelper.Concat(IccTestDataPrimitives.UInt32_0, IccTestDataPrimitives.UInt32_0);
    public static readonly byte[] PositionNumber_Rand = ArrayHelper.Concat(IccTestDataPrimitives.UInt32_Rand1, IccTestDataPrimitives.UInt32_Rand2);
    public static readonly byte[] PositionNumber_Max = ArrayHelper.Concat(IccTestDataPrimitives.UInt32_Max, IccTestDataPrimitives.UInt32_Max);

    public static readonly object[][] PositionNumberTestData =
    [
        [PositionNumber_Min, PositionNumber_ValMin],
        [PositionNumber_Rand, PositionNumber_ValRand],
        [PositionNumber_Max, PositionNumber_ValMax]
    ];

    public static readonly IccResponseNumber ResponseNumber_ValMin = new(0, IccTestDataPrimitives.Fix16_ValMin);
    public static readonly IccResponseNumber ResponseNumber_Val1 = new(1, 1);
    public static readonly IccResponseNumber ResponseNumber_Val2 = new(2, 2);
    public static readonly IccResponseNumber ResponseNumber_Val3 = new(3, 3);
    public static readonly IccResponseNumber ResponseNumber_Val4 = new(4, 4);
    public static readonly IccResponseNumber ResponseNumber_Val5 = new(5, 5);
    public static readonly IccResponseNumber ResponseNumber_Val6 = new(6, 6);
    public static readonly IccResponseNumber ResponseNumber_Val7 = new(7, 7);
    public static readonly IccResponseNumber ResponseNumber_Val8 = new(8, 8);
    public static readonly IccResponseNumber ResponseNumber_Val9 = new(9, 9);
    public static readonly IccResponseNumber ResponseNumber_ValMax = new(ushort.MaxValue, IccTestDataPrimitives.Fix16_ValMax);

    public static readonly byte[] ResponseNumber_Min = ArrayHelper.Concat(IccTestDataPrimitives.UInt16_0, IccTestDataPrimitives.Fix16_Min);
    public static readonly byte[] ResponseNumber_1 = ArrayHelper.Concat(IccTestDataPrimitives.UInt16_1, IccTestDataPrimitives.Fix16_1);
    public static readonly byte[] ResponseNumber_2 = ArrayHelper.Concat(IccTestDataPrimitives.UInt16_2, IccTestDataPrimitives.Fix16_2);
    public static readonly byte[] ResponseNumber_3 = ArrayHelper.Concat(IccTestDataPrimitives.UInt16_3, IccTestDataPrimitives.Fix16_3);
    public static readonly byte[] ResponseNumber_4 = ArrayHelper.Concat(IccTestDataPrimitives.UInt16_4, IccTestDataPrimitives.Fix16_4);
    public static readonly byte[] ResponseNumber_5 = ArrayHelper.Concat(IccTestDataPrimitives.UInt16_5, IccTestDataPrimitives.Fix16_5);
    public static readonly byte[] ResponseNumber_6 = ArrayHelper.Concat(IccTestDataPrimitives.UInt16_6, IccTestDataPrimitives.Fix16_6);
    public static readonly byte[] ResponseNumber_7 = ArrayHelper.Concat(IccTestDataPrimitives.UInt16_7, IccTestDataPrimitives.Fix16_7);
    public static readonly byte[] ResponseNumber_8 = ArrayHelper.Concat(IccTestDataPrimitives.UInt16_8, IccTestDataPrimitives.Fix16_8);
    public static readonly byte[] ResponseNumber_9 = ArrayHelper.Concat(IccTestDataPrimitives.UInt16_9, IccTestDataPrimitives.Fix16_9);
    public static readonly byte[] ResponseNumber_Max = ArrayHelper.Concat(IccTestDataPrimitives.UInt16_Max, IccTestDataPrimitives.Fix16_Max);

    public static readonly object[][] ResponseNumberTestData =
    [
        [ResponseNumber_Min, ResponseNumber_ValMin],
        [ResponseNumber_1, ResponseNumber_Val1],
        [ResponseNumber_4, ResponseNumber_Val4],
        [ResponseNumber_Max, ResponseNumber_ValMax]
    ];

    public static readonly IccNamedColor NamedColor_ValMin = new(
        ArrayHelper.Fill('A', 31),
        [0, 0, 0],
        [0, 0, 0]);

    public static readonly IccNamedColor NamedColor_ValRand = new(
        ArrayHelper.Fill('5', 31),
        [10794, 10794, 10794],
        [17219, 17219, 17219, 17219, 17219]);

    public static readonly IccNamedColor NamedColor_ValMax = new(
        ArrayHelper.Fill('4', 31),
        [ushort.MaxValue, ushort.MaxValue, ushort.MaxValue],
        [ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue]);

    public static readonly byte[] NamedColor_Min = CreateNamedColor(3, 0x41, 0x00, 0x00);
    public static readonly byte[] NamedColor_Rand = CreateNamedColor(5, 0x35, 42, 67);
    public static readonly byte[] NamedColor_Max = CreateNamedColor(4, 0x34, 0xFF, 0xFF);

    private static byte[] CreateNamedColor(int devCoordCount, byte name, byte pCS, byte device)
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
                data[i] = pCS; // PCS Coordinates
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
        [NamedColor_Min, NamedColor_ValMin, 3u],
        [NamedColor_Rand, NamedColor_ValRand, 5u],
        [NamedColor_Max, NamedColor_ValMax, 4u]
    ];

    private static readonly CultureInfo CultureEnUs = new("en-US");
    private static readonly CultureInfo CultureDeAT = new("de-AT");

    private static readonly IccLocalizedString LocalizedString_Rand1 = new(CultureEnUs, IccTestDataPrimitives.Unicode_ValRand2);
    private static readonly IccLocalizedString LocalizedString_Rand2 = new(CultureDeAT, IccTestDataPrimitives.Unicode_ValRand3);

    private static readonly IccLocalizedString[] LocalizedString_RandArr1 =
    [
        LocalizedString_Rand1,
        LocalizedString_Rand2
    ];

    private static readonly IccMultiLocalizedUnicodeTagDataEntry MultiLocalizedUnicode_Val = new(LocalizedString_RandArr1);
    private static readonly byte[] MultiLocalizedUnicode_Arr = ArrayHelper.Concat(
        IccTestDataPrimitives.UInt32_2,
        new byte[] { 0x00, 0x00, 0x00, 0x0C }, // 12
        [(byte)'e', (byte)'n', (byte)'U', (byte)'S'],
        new byte[] { 0x00, 0x00, 0x00, 0x0C },  // 12
        new byte[] { 0x00, 0x00, 0x00, 0x28 },  // 40
        [(byte)'d', (byte)'e', (byte)'A', (byte)'T'],
        new byte[] { 0x00, 0x00, 0x00, 0x0E },  // 14
        new byte[] { 0x00, 0x00, 0x00, 0x34 },  // 52
        IccTestDataPrimitives.Unicode_Rand2,
        IccTestDataPrimitives.Unicode_Rand3);

    public static readonly IccTextDescriptionTagDataEntry TextDescription_Val1 = new(
        IccTestDataPrimitives.Ascii_ValRand,
        IccTestDataPrimitives.Unicode_ValRand1,
        ArrayHelper.Fill('A', 66),
        1701729619,
        2);

    public static readonly byte[] TextDescription_Arr1 = ArrayHelper.Concat(
        new byte[] { 0x00, 0x00, 0x00, 0x0B },  // 11
        IccTestDataPrimitives.Ascii_Rand,
        new byte[] { 0x00 },                    // Null terminator
        [(byte)'e', (byte)'n', (byte)'U', (byte)'S'],
        new byte[] { 0x00, 0x00, 0x00, 0x07 },  // 7
        IccTestDataPrimitives.Unicode_Rand2,
        new byte[] { 0x00, 0x00 },              // Null terminator
        new byte[] { 0x00, 0x02, 0x43 },        // 2, 67
        ArrayHelper.Fill((byte)0x41, 66),
        new byte[] { 0x00 }); // Null terminator

    public static readonly IccProfileDescription ProfileDescription_ValRand1 = new(
        1,
        2,
        IccDeviceAttribute.ChromaBlackWhite | IccDeviceAttribute.ReflectivityMatte,
        IccProfileTag.ProfileDescription,
        MultiLocalizedUnicode_Val.Texts,
        MultiLocalizedUnicode_Val.Texts);

    public static readonly IccProfileDescription ProfileDescription_ValRand2 = new(
        1,
        2,
        IccDeviceAttribute.ChromaBlackWhite | IccDeviceAttribute.ReflectivityMatte,
        IccProfileTag.ProfileDescription,
        [LocalizedString_Rand1],
        [LocalizedString_Rand1]);

    public static readonly byte[] ProfileDescription_Rand1 = ArrayHelper.Concat(
        IccTestDataPrimitives.UInt32_1,
        IccTestDataPrimitives.UInt32_2,
        new byte[] { 0, 0, 0, 0, 0, 0, 0, 10 },
        new byte[] { 0x64, 0x65, 0x73, 0x63 },
        new byte[] { 0x6D, 0x6C, 0x75, 0x63 },
        new byte[] { 0x00, 0x00, 0x00, 0x00 },
        MultiLocalizedUnicode_Arr,
        new byte[] { 0x6D, 0x6C, 0x75, 0x63 },
        new byte[] { 0x00, 0x00, 0x00, 0x00 },
        MultiLocalizedUnicode_Arr);

    public static readonly byte[] ProfileDescription_Rand2 = ArrayHelper.Concat(
        IccTestDataPrimitives.UInt32_1,
        IccTestDataPrimitives.UInt32_2,
        new byte[] { 0, 0, 0, 0, 0, 0, 0, 10 },
        new byte[] { 0x64, 0x65, 0x73, 0x63 },
        new byte[] { 0x64, 0x65, 0x73, 0x63 },
        new byte[] { 0x00, 0x00, 0x00, 0x00 },
        TextDescription_Arr1,
        new byte[] { 0x64, 0x65, 0x73, 0x63 },
        new byte[] { 0x00, 0x00, 0x00, 0x00 },
        TextDescription_Arr1);

    public static readonly object[][] ProfileDescriptionReadTestData =
    [
        [ProfileDescription_Rand1, ProfileDescription_ValRand1],
        [ProfileDescription_Rand2, ProfileDescription_ValRand2]
    ];

    public static readonly object[][] ProfileDescriptionWriteTestData =
    [
        [ProfileDescription_Rand1, ProfileDescription_ValRand1]
    ];

    public static readonly IccColorantTableEntry ColorantTableEntry_ValRand1 = new(ArrayHelper.Fill('A', 31), 1, 2, 3);
    public static readonly IccColorantTableEntry ColorantTableEntry_ValRand2 = new(ArrayHelper.Fill('4', 31), 4, 5, 6);

    public static readonly byte[] ColorantTableEntry_Rand1 = ArrayHelper.Concat(
        ArrayHelper.Fill((byte)0x41, 31),
        new byte[1],    // null terminator
        IccTestDataPrimitives.UInt16_1,
        IccTestDataPrimitives.UInt16_2,
        IccTestDataPrimitives.UInt16_3);

    public static readonly byte[] ColorantTableEntry_Rand2 = ArrayHelper.Concat(
        ArrayHelper.Fill((byte)0x34, 31),
        new byte[1],    // null terminator
        IccTestDataPrimitives.UInt16_4,
        IccTestDataPrimitives.UInt16_5,
        IccTestDataPrimitives.UInt16_6);

    public static readonly object[][] ColorantTableEntryTestData =
    [
        [ColorantTableEntry_Rand1, ColorantTableEntry_ValRand1],
        [ColorantTableEntry_Rand2, ColorantTableEntry_ValRand2]
    ];

    public static readonly IccScreeningChannel ScreeningChannel_ValRand1 = new(4, 6, IccScreeningSpotType.Cross);
    public static readonly IccScreeningChannel ScreeningChannel_ValRand2 = new(8, 5, IccScreeningSpotType.Diamond);

    public static readonly byte[] ScreeningChannel_Rand1 = ArrayHelper.Concat(
        IccTestDataPrimitives.Fix16_4,
        IccTestDataPrimitives.Fix16_6,
        IccTestDataPrimitives.Int32_7);

    public static readonly byte[] ScreeningChannel_Rand2 = ArrayHelper.Concat(
        IccTestDataPrimitives.Fix16_8,
        IccTestDataPrimitives.Fix16_5,
        IccTestDataPrimitives.Int32_3);

    public static readonly object[][] ScreeningChannelTestData =
    [
        [ScreeningChannel_Rand1, ScreeningChannel_ValRand1],
        [ScreeningChannel_Rand2, ScreeningChannel_ValRand2]
    ];
}
