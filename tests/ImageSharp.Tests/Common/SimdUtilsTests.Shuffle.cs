// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Common;

public partial class SimdUtilsTests
{
    public static readonly TheoryData<byte, byte> MMShuffleData = new()
    {
        { SimdUtils.Shuffle.MMShuffle(0, 0, 0, 0), SimdUtils.Shuffle.MMShuffle0000 },
        { SimdUtils.Shuffle.MMShuffle(0, 0, 0, 1), SimdUtils.Shuffle.MMShuffle0001 },
        { SimdUtils.Shuffle.MMShuffle(0, 0, 0, 2), SimdUtils.Shuffle.MMShuffle0002 },
        { SimdUtils.Shuffle.MMShuffle(0, 0, 0, 3), SimdUtils.Shuffle.MMShuffle0003 },
        { SimdUtils.Shuffle.MMShuffle(0, 0, 1, 0), SimdUtils.Shuffle.MMShuffle0010 },
        { SimdUtils.Shuffle.MMShuffle(0, 0, 1, 1), SimdUtils.Shuffle.MMShuffle0011 },
        { SimdUtils.Shuffle.MMShuffle(0, 0, 1, 2), SimdUtils.Shuffle.MMShuffle0012 },
        { SimdUtils.Shuffle.MMShuffle(0, 0, 1, 3), SimdUtils.Shuffle.MMShuffle0013 },
        { SimdUtils.Shuffle.MMShuffle(0, 0, 2, 0), SimdUtils.Shuffle.MMShuffle0020 },
        { SimdUtils.Shuffle.MMShuffle(0, 0, 2, 1), SimdUtils.Shuffle.MMShuffle0021 },
        { SimdUtils.Shuffle.MMShuffle(0, 0, 2, 2), SimdUtils.Shuffle.MMShuffle0022 },
        { SimdUtils.Shuffle.MMShuffle(0, 0, 2, 3), SimdUtils.Shuffle.MMShuffle0023 },
        { SimdUtils.Shuffle.MMShuffle(0, 0, 3, 0), SimdUtils.Shuffle.MMShuffle0030 },
        { SimdUtils.Shuffle.MMShuffle(0, 0, 3, 1), SimdUtils.Shuffle.MMShuffle0031 },
        { SimdUtils.Shuffle.MMShuffle(0, 0, 3, 2), SimdUtils.Shuffle.MMShuffle0032 },
        { SimdUtils.Shuffle.MMShuffle(0, 0, 3, 3), SimdUtils.Shuffle.MMShuffle0033 },
        { SimdUtils.Shuffle.MMShuffle(0, 1, 0, 0), SimdUtils.Shuffle.MMShuffle0100 },
        { SimdUtils.Shuffle.MMShuffle(0, 1, 0, 1), SimdUtils.Shuffle.MMShuffle0101 },
        { SimdUtils.Shuffle.MMShuffle(0, 1, 0, 2), SimdUtils.Shuffle.MMShuffle0102 },
        { SimdUtils.Shuffle.MMShuffle(0, 1, 0, 3), SimdUtils.Shuffle.MMShuffle0103 },
        { SimdUtils.Shuffle.MMShuffle(0, 1, 1, 0), SimdUtils.Shuffle.MMShuffle0110 },
        { SimdUtils.Shuffle.MMShuffle(0, 1, 1, 1), SimdUtils.Shuffle.MMShuffle0111 },
        { SimdUtils.Shuffle.MMShuffle(0, 1, 1, 2), SimdUtils.Shuffle.MMShuffle0112 },
        { SimdUtils.Shuffle.MMShuffle(0, 1, 1, 3), SimdUtils.Shuffle.MMShuffle0113 },
        { SimdUtils.Shuffle.MMShuffle(0, 1, 2, 0), SimdUtils.Shuffle.MMShuffle0120 },
        { SimdUtils.Shuffle.MMShuffle(0, 1, 2, 1), SimdUtils.Shuffle.MMShuffle0121 },
        { SimdUtils.Shuffle.MMShuffle(0, 1, 2, 2), SimdUtils.Shuffle.MMShuffle0122 },
        { SimdUtils.Shuffle.MMShuffle(0, 1, 2, 3), SimdUtils.Shuffle.MMShuffle0123 },
        { SimdUtils.Shuffle.MMShuffle(0, 1, 3, 0), SimdUtils.Shuffle.MMShuffle0130 },
        { SimdUtils.Shuffle.MMShuffle(0, 1, 3, 1), SimdUtils.Shuffle.MMShuffle0131 },
        { SimdUtils.Shuffle.MMShuffle(0, 1, 3, 2), SimdUtils.Shuffle.MMShuffle0132 },
        { SimdUtils.Shuffle.MMShuffle(0, 1, 3, 3), SimdUtils.Shuffle.MMShuffle0133 },
        { SimdUtils.Shuffle.MMShuffle(0, 2, 0, 0), SimdUtils.Shuffle.MMShuffle0200 },
        { SimdUtils.Shuffle.MMShuffle(0, 2, 0, 1), SimdUtils.Shuffle.MMShuffle0201 },
        { SimdUtils.Shuffle.MMShuffle(0, 2, 0, 2), SimdUtils.Shuffle.MMShuffle0202 },
        { SimdUtils.Shuffle.MMShuffle(0, 2, 0, 3), SimdUtils.Shuffle.MMShuffle0203 },
        { SimdUtils.Shuffle.MMShuffle(0, 2, 1, 0), SimdUtils.Shuffle.MMShuffle0210 },
        { SimdUtils.Shuffle.MMShuffle(0, 2, 1, 1), SimdUtils.Shuffle.MMShuffle0211 },
        { SimdUtils.Shuffle.MMShuffle(0, 2, 1, 2), SimdUtils.Shuffle.MMShuffle0212 },
        { SimdUtils.Shuffle.MMShuffle(0, 2, 1, 3), SimdUtils.Shuffle.MMShuffle0213 },
        { SimdUtils.Shuffle.MMShuffle(0, 2, 2, 0), SimdUtils.Shuffle.MMShuffle0220 },
        { SimdUtils.Shuffle.MMShuffle(0, 2, 2, 1), SimdUtils.Shuffle.MMShuffle0221 },
        { SimdUtils.Shuffle.MMShuffle(0, 2, 2, 2), SimdUtils.Shuffle.MMShuffle0222 },
        { SimdUtils.Shuffle.MMShuffle(0, 2, 2, 3), SimdUtils.Shuffle.MMShuffle0223 },
        { SimdUtils.Shuffle.MMShuffle(0, 2, 3, 0), SimdUtils.Shuffle.MMShuffle0230 },
        { SimdUtils.Shuffle.MMShuffle(0, 2, 3, 1), SimdUtils.Shuffle.MMShuffle0231 },
        { SimdUtils.Shuffle.MMShuffle(0, 2, 3, 2), SimdUtils.Shuffle.MMShuffle0232 },
        { SimdUtils.Shuffle.MMShuffle(0, 2, 3, 3), SimdUtils.Shuffle.MMShuffle0233 },
        { SimdUtils.Shuffle.MMShuffle(0, 3, 0, 0), SimdUtils.Shuffle.MMShuffle0300 },
        { SimdUtils.Shuffle.MMShuffle(0, 3, 0, 1), SimdUtils.Shuffle.MMShuffle0301 },
        { SimdUtils.Shuffle.MMShuffle(0, 3, 0, 2), SimdUtils.Shuffle.MMShuffle0302 },
        { SimdUtils.Shuffle.MMShuffle(0, 3, 0, 3), SimdUtils.Shuffle.MMShuffle0303 },
        { SimdUtils.Shuffle.MMShuffle(0, 3, 1, 0), SimdUtils.Shuffle.MMShuffle0310 },
        { SimdUtils.Shuffle.MMShuffle(0, 3, 1, 1), SimdUtils.Shuffle.MMShuffle0311 },
        { SimdUtils.Shuffle.MMShuffle(0, 3, 1, 2), SimdUtils.Shuffle.MMShuffle0312 },
        { SimdUtils.Shuffle.MMShuffle(0, 3, 1, 3), SimdUtils.Shuffle.MMShuffle0313 },
        { SimdUtils.Shuffle.MMShuffle(0, 3, 2, 0), SimdUtils.Shuffle.MMShuffle0320 },
        { SimdUtils.Shuffle.MMShuffle(0, 3, 2, 1), SimdUtils.Shuffle.MMShuffle0321 },
        { SimdUtils.Shuffle.MMShuffle(0, 3, 2, 2), SimdUtils.Shuffle.MMShuffle0322 },
        { SimdUtils.Shuffle.MMShuffle(0, 3, 2, 3), SimdUtils.Shuffle.MMShuffle0323 },
        { SimdUtils.Shuffle.MMShuffle(0, 3, 3, 0), SimdUtils.Shuffle.MMShuffle0330 },
        { SimdUtils.Shuffle.MMShuffle(0, 3, 3, 1), SimdUtils.Shuffle.MMShuffle0331 },
        { SimdUtils.Shuffle.MMShuffle(0, 3, 3, 2), SimdUtils.Shuffle.MMShuffle0332 },
        { SimdUtils.Shuffle.MMShuffle(0, 3, 3, 3), SimdUtils.Shuffle.MMShuffle0333 },
        { SimdUtils.Shuffle.MMShuffle(1, 0, 0, 0), SimdUtils.Shuffle.MMShuffle1000 },
        { SimdUtils.Shuffle.MMShuffle(1, 0, 0, 1), SimdUtils.Shuffle.MMShuffle1001 },
        { SimdUtils.Shuffle.MMShuffle(1, 0, 0, 2), SimdUtils.Shuffle.MMShuffle1002 },
        { SimdUtils.Shuffle.MMShuffle(1, 0, 0, 3), SimdUtils.Shuffle.MMShuffle1003 },
        { SimdUtils.Shuffle.MMShuffle(1, 0, 1, 0), SimdUtils.Shuffle.MMShuffle1010 },
        { SimdUtils.Shuffle.MMShuffle(1, 0, 1, 1), SimdUtils.Shuffle.MMShuffle1011 },
        { SimdUtils.Shuffle.MMShuffle(1, 0, 1, 2), SimdUtils.Shuffle.MMShuffle1012 },
        { SimdUtils.Shuffle.MMShuffle(1, 0, 1, 3), SimdUtils.Shuffle.MMShuffle1013 },
        { SimdUtils.Shuffle.MMShuffle(1, 0, 2, 0), SimdUtils.Shuffle.MMShuffle1020 },
        { SimdUtils.Shuffle.MMShuffle(1, 0, 2, 1), SimdUtils.Shuffle.MMShuffle1021 },
        { SimdUtils.Shuffle.MMShuffle(1, 0, 2, 2), SimdUtils.Shuffle.MMShuffle1022 },
        { SimdUtils.Shuffle.MMShuffle(1, 0, 2, 3), SimdUtils.Shuffle.MMShuffle1023 },
        { SimdUtils.Shuffle.MMShuffle(1, 0, 3, 0), SimdUtils.Shuffle.MMShuffle1030 },
        { SimdUtils.Shuffle.MMShuffle(1, 0, 3, 1), SimdUtils.Shuffle.MMShuffle1031 },
        { SimdUtils.Shuffle.MMShuffle(1, 0, 3, 2), SimdUtils.Shuffle.MMShuffle1032 },
        { SimdUtils.Shuffle.MMShuffle(1, 0, 3, 3), SimdUtils.Shuffle.MMShuffle1033 },
        { SimdUtils.Shuffle.MMShuffle(1, 1, 0, 0), SimdUtils.Shuffle.MMShuffle1100 },
        { SimdUtils.Shuffle.MMShuffle(1, 1, 0, 1), SimdUtils.Shuffle.MMShuffle1101 },
        { SimdUtils.Shuffle.MMShuffle(1, 1, 0, 2), SimdUtils.Shuffle.MMShuffle1102 },
        { SimdUtils.Shuffle.MMShuffle(1, 1, 0, 3), SimdUtils.Shuffle.MMShuffle1103 },
        { SimdUtils.Shuffle.MMShuffle(1, 1, 1, 0), SimdUtils.Shuffle.MMShuffle1110 },
        { SimdUtils.Shuffle.MMShuffle(1, 1, 1, 1), SimdUtils.Shuffle.MMShuffle1111 },
        { SimdUtils.Shuffle.MMShuffle(1, 1, 1, 2), SimdUtils.Shuffle.MMShuffle1112 },
        { SimdUtils.Shuffle.MMShuffle(1, 1, 1, 3), SimdUtils.Shuffle.MMShuffle1113 },
        { SimdUtils.Shuffle.MMShuffle(1, 1, 2, 0), SimdUtils.Shuffle.MMShuffle1120 },
        { SimdUtils.Shuffle.MMShuffle(1, 1, 2, 1), SimdUtils.Shuffle.MMShuffle1121 },
        { SimdUtils.Shuffle.MMShuffle(1, 1, 2, 2), SimdUtils.Shuffle.MMShuffle1122 },
        { SimdUtils.Shuffle.MMShuffle(1, 1, 2, 3), SimdUtils.Shuffle.MMShuffle1123 },
        { SimdUtils.Shuffle.MMShuffle(1, 1, 3, 0), SimdUtils.Shuffle.MMShuffle1130 },
        { SimdUtils.Shuffle.MMShuffle(1, 1, 3, 1), SimdUtils.Shuffle.MMShuffle1131 },
        { SimdUtils.Shuffle.MMShuffle(1, 1, 3, 2), SimdUtils.Shuffle.MMShuffle1132 },
        { SimdUtils.Shuffle.MMShuffle(1, 1, 3, 3), SimdUtils.Shuffle.MMShuffle1133 },
        { SimdUtils.Shuffle.MMShuffle(1, 2, 0, 0), SimdUtils.Shuffle.MMShuffle1200 },
        { SimdUtils.Shuffle.MMShuffle(1, 2, 0, 1), SimdUtils.Shuffle.MMShuffle1201 },
        { SimdUtils.Shuffle.MMShuffle(1, 2, 0, 2), SimdUtils.Shuffle.MMShuffle1202 },
        { SimdUtils.Shuffle.MMShuffle(1, 2, 0, 3), SimdUtils.Shuffle.MMShuffle1203 },
        { SimdUtils.Shuffle.MMShuffle(1, 2, 1, 0), SimdUtils.Shuffle.MMShuffle1210 },
        { SimdUtils.Shuffle.MMShuffle(1, 2, 1, 1), SimdUtils.Shuffle.MMShuffle1211 },
        { SimdUtils.Shuffle.MMShuffle(1, 2, 1, 2), SimdUtils.Shuffle.MMShuffle1212 },
        { SimdUtils.Shuffle.MMShuffle(1, 2, 1, 3), SimdUtils.Shuffle.MMShuffle1213 },
        { SimdUtils.Shuffle.MMShuffle(1, 2, 2, 0), SimdUtils.Shuffle.MMShuffle1220 },
        { SimdUtils.Shuffle.MMShuffle(1, 2, 2, 1), SimdUtils.Shuffle.MMShuffle1221 },
        { SimdUtils.Shuffle.MMShuffle(1, 2, 2, 2), SimdUtils.Shuffle.MMShuffle1222 },
        { SimdUtils.Shuffle.MMShuffle(1, 2, 2, 3), SimdUtils.Shuffle.MMShuffle1223 },
        { SimdUtils.Shuffle.MMShuffle(1, 2, 3, 0), SimdUtils.Shuffle.MMShuffle1230 },
        { SimdUtils.Shuffle.MMShuffle(1, 2, 3, 1), SimdUtils.Shuffle.MMShuffle1231 },
        { SimdUtils.Shuffle.MMShuffle(1, 2, 3, 2), SimdUtils.Shuffle.MMShuffle1232 },
        { SimdUtils.Shuffle.MMShuffle(1, 2, 3, 3), SimdUtils.Shuffle.MMShuffle1233 },
        { SimdUtils.Shuffle.MMShuffle(1, 3, 0, 0), SimdUtils.Shuffle.MMShuffle1300 },
        { SimdUtils.Shuffle.MMShuffle(1, 3, 0, 1), SimdUtils.Shuffle.MMShuffle1301 },
        { SimdUtils.Shuffle.MMShuffle(1, 3, 0, 2), SimdUtils.Shuffle.MMShuffle1302 },
        { SimdUtils.Shuffle.MMShuffle(1, 3, 0, 3), SimdUtils.Shuffle.MMShuffle1303 },
        { SimdUtils.Shuffle.MMShuffle(1, 3, 1, 0), SimdUtils.Shuffle.MMShuffle1310 },
        { SimdUtils.Shuffle.MMShuffle(1, 3, 1, 1), SimdUtils.Shuffle.MMShuffle1311 },
        { SimdUtils.Shuffle.MMShuffle(1, 3, 1, 2), SimdUtils.Shuffle.MMShuffle1312 },
        { SimdUtils.Shuffle.MMShuffle(1, 3, 1, 3), SimdUtils.Shuffle.MMShuffle1313 },
        { SimdUtils.Shuffle.MMShuffle(1, 3, 2, 0), SimdUtils.Shuffle.MMShuffle1320 },
        { SimdUtils.Shuffle.MMShuffle(1, 3, 2, 1), SimdUtils.Shuffle.MMShuffle1321 },
        { SimdUtils.Shuffle.MMShuffle(1, 3, 2, 2), SimdUtils.Shuffle.MMShuffle1322 },
        { SimdUtils.Shuffle.MMShuffle(1, 3, 2, 3), SimdUtils.Shuffle.MMShuffle1323 },
        { SimdUtils.Shuffle.MMShuffle(1, 3, 3, 0), SimdUtils.Shuffle.MMShuffle1330 },
        { SimdUtils.Shuffle.MMShuffle(1, 3, 3, 1), SimdUtils.Shuffle.MMShuffle1331 },
        { SimdUtils.Shuffle.MMShuffle(1, 3, 3, 2), SimdUtils.Shuffle.MMShuffle1332 },
        { SimdUtils.Shuffle.MMShuffle(1, 3, 3, 3), SimdUtils.Shuffle.MMShuffle1333 },
        { SimdUtils.Shuffle.MMShuffle(2, 0, 0, 0), SimdUtils.Shuffle.MMShuffle2000 },
        { SimdUtils.Shuffle.MMShuffle(2, 0, 0, 1), SimdUtils.Shuffle.MMShuffle2001 },
        { SimdUtils.Shuffle.MMShuffle(2, 0, 0, 2), SimdUtils.Shuffle.MMShuffle2002 },
        { SimdUtils.Shuffle.MMShuffle(2, 0, 0, 3), SimdUtils.Shuffle.MMShuffle2003 },
        { SimdUtils.Shuffle.MMShuffle(2, 0, 1, 0), SimdUtils.Shuffle.MMShuffle2010 },
        { SimdUtils.Shuffle.MMShuffle(2, 0, 1, 1), SimdUtils.Shuffle.MMShuffle2011 },
        { SimdUtils.Shuffle.MMShuffle(2, 0, 1, 2), SimdUtils.Shuffle.MMShuffle2012 },
        { SimdUtils.Shuffle.MMShuffle(2, 0, 1, 3), SimdUtils.Shuffle.MMShuffle2013 },
        { SimdUtils.Shuffle.MMShuffle(2, 0, 2, 0), SimdUtils.Shuffle.MMShuffle2020 },
        { SimdUtils.Shuffle.MMShuffle(2, 0, 2, 1), SimdUtils.Shuffle.MMShuffle2021 },
        { SimdUtils.Shuffle.MMShuffle(2, 0, 2, 2), SimdUtils.Shuffle.MMShuffle2022 },
        { SimdUtils.Shuffle.MMShuffle(2, 0, 2, 3), SimdUtils.Shuffle.MMShuffle2023 },
        { SimdUtils.Shuffle.MMShuffle(2, 0, 3, 0), SimdUtils.Shuffle.MMShuffle2030 },
        { SimdUtils.Shuffle.MMShuffle(2, 0, 3, 1), SimdUtils.Shuffle.MMShuffle2031 },
        { SimdUtils.Shuffle.MMShuffle(2, 0, 3, 2), SimdUtils.Shuffle.MMShuffle2032 },
        { SimdUtils.Shuffle.MMShuffle(2, 0, 3, 3), SimdUtils.Shuffle.MMShuffle2033 },
        { SimdUtils.Shuffle.MMShuffle(2, 1, 0, 0), SimdUtils.Shuffle.MMShuffle2100 },
        { SimdUtils.Shuffle.MMShuffle(2, 1, 0, 1), SimdUtils.Shuffle.MMShuffle2101 },
        { SimdUtils.Shuffle.MMShuffle(2, 1, 0, 2), SimdUtils.Shuffle.MMShuffle2102 },
        { SimdUtils.Shuffle.MMShuffle(2, 1, 0, 3), SimdUtils.Shuffle.MMShuffle2103 },
        { SimdUtils.Shuffle.MMShuffle(2, 1, 1, 0), SimdUtils.Shuffle.MMShuffle2110 },
        { SimdUtils.Shuffle.MMShuffle(2, 1, 1, 1), SimdUtils.Shuffle.MMShuffle2111 },
        { SimdUtils.Shuffle.MMShuffle(2, 1, 1, 2), SimdUtils.Shuffle.MMShuffle2112 },
        { SimdUtils.Shuffle.MMShuffle(2, 1, 1, 3), SimdUtils.Shuffle.MMShuffle2113 },
        { SimdUtils.Shuffle.MMShuffle(2, 1, 2, 0), SimdUtils.Shuffle.MMShuffle2120 },
        { SimdUtils.Shuffle.MMShuffle(2, 1, 2, 1), SimdUtils.Shuffle.MMShuffle2121 },
        { SimdUtils.Shuffle.MMShuffle(2, 1, 2, 2), SimdUtils.Shuffle.MMShuffle2122 },
        { SimdUtils.Shuffle.MMShuffle(2, 1, 2, 3), SimdUtils.Shuffle.MMShuffle2123 },
        { SimdUtils.Shuffle.MMShuffle(2, 1, 3, 0), SimdUtils.Shuffle.MMShuffle2130 },
        { SimdUtils.Shuffle.MMShuffle(2, 1, 3, 1), SimdUtils.Shuffle.MMShuffle2131 },
        { SimdUtils.Shuffle.MMShuffle(2, 1, 3, 2), SimdUtils.Shuffle.MMShuffle2132 },
        { SimdUtils.Shuffle.MMShuffle(2, 1, 3, 3), SimdUtils.Shuffle.MMShuffle2133 },
        { SimdUtils.Shuffle.MMShuffle(2, 2, 0, 0), SimdUtils.Shuffle.MMShuffle2200 },
        { SimdUtils.Shuffle.MMShuffle(2, 2, 0, 1), SimdUtils.Shuffle.MMShuffle2201 },
        { SimdUtils.Shuffle.MMShuffle(2, 2, 0, 2), SimdUtils.Shuffle.MMShuffle2202 },
        { SimdUtils.Shuffle.MMShuffle(2, 2, 0, 3), SimdUtils.Shuffle.MMShuffle2203 },
        { SimdUtils.Shuffle.MMShuffle(2, 2, 1, 0), SimdUtils.Shuffle.MMShuffle2210 },
        { SimdUtils.Shuffle.MMShuffle(2, 2, 1, 1), SimdUtils.Shuffle.MMShuffle2211 },
        { SimdUtils.Shuffle.MMShuffle(2, 2, 1, 2), SimdUtils.Shuffle.MMShuffle2212 },
        { SimdUtils.Shuffle.MMShuffle(2, 2, 1, 3), SimdUtils.Shuffle.MMShuffle2213 },
        { SimdUtils.Shuffle.MMShuffle(2, 2, 2, 0), SimdUtils.Shuffle.MMShuffle2220 },
        { SimdUtils.Shuffle.MMShuffle(2, 2, 2, 1), SimdUtils.Shuffle.MMShuffle2221 },
        { SimdUtils.Shuffle.MMShuffle(2, 2, 2, 2), SimdUtils.Shuffle.MMShuffle2222 },
        { SimdUtils.Shuffle.MMShuffle(2, 2, 2, 3), SimdUtils.Shuffle.MMShuffle2223 },
        { SimdUtils.Shuffle.MMShuffle(2, 2, 3, 0), SimdUtils.Shuffle.MMShuffle2230 },
        { SimdUtils.Shuffle.MMShuffle(2, 2, 3, 1), SimdUtils.Shuffle.MMShuffle2231 },
        { SimdUtils.Shuffle.MMShuffle(2, 2, 3, 2), SimdUtils.Shuffle.MMShuffle2232 },
        { SimdUtils.Shuffle.MMShuffle(2, 2, 3, 3), SimdUtils.Shuffle.MMShuffle2233 },
        { SimdUtils.Shuffle.MMShuffle(2, 3, 0, 0), SimdUtils.Shuffle.MMShuffle2300 },
        { SimdUtils.Shuffle.MMShuffle(2, 3, 0, 1), SimdUtils.Shuffle.MMShuffle2301 },
        { SimdUtils.Shuffle.MMShuffle(2, 3, 0, 2), SimdUtils.Shuffle.MMShuffle2302 },
        { SimdUtils.Shuffle.MMShuffle(2, 3, 0, 3), SimdUtils.Shuffle.MMShuffle2303 },
        { SimdUtils.Shuffle.MMShuffle(2, 3, 1, 0), SimdUtils.Shuffle.MMShuffle2310 },
        { SimdUtils.Shuffle.MMShuffle(2, 3, 1, 1), SimdUtils.Shuffle.MMShuffle2311 },
        { SimdUtils.Shuffle.MMShuffle(2, 3, 1, 2), SimdUtils.Shuffle.MMShuffle2312 },
        { SimdUtils.Shuffle.MMShuffle(2, 3, 1, 3), SimdUtils.Shuffle.MMShuffle2313 },
        { SimdUtils.Shuffle.MMShuffle(2, 3, 2, 0), SimdUtils.Shuffle.MMShuffle2320 },
        { SimdUtils.Shuffle.MMShuffle(2, 3, 2, 1), SimdUtils.Shuffle.MMShuffle2321 },
        { SimdUtils.Shuffle.MMShuffle(2, 3, 2, 2), SimdUtils.Shuffle.MMShuffle2322 },
        { SimdUtils.Shuffle.MMShuffle(2, 3, 2, 3), SimdUtils.Shuffle.MMShuffle2323 },
        { SimdUtils.Shuffle.MMShuffle(2, 3, 3, 0), SimdUtils.Shuffle.MMShuffle2330 },
        { SimdUtils.Shuffle.MMShuffle(2, 3, 3, 1), SimdUtils.Shuffle.MMShuffle2331 },
        { SimdUtils.Shuffle.MMShuffle(2, 3, 3, 2), SimdUtils.Shuffle.MMShuffle2332 },
        { SimdUtils.Shuffle.MMShuffle(2, 3, 3, 3), SimdUtils.Shuffle.MMShuffle2333 },
        { SimdUtils.Shuffle.MMShuffle(3, 0, 0, 0), SimdUtils.Shuffle.MMShuffle3000 },
        { SimdUtils.Shuffle.MMShuffle(3, 0, 0, 1), SimdUtils.Shuffle.MMShuffle3001 },
        { SimdUtils.Shuffle.MMShuffle(3, 0, 0, 2), SimdUtils.Shuffle.MMShuffle3002 },
        { SimdUtils.Shuffle.MMShuffle(3, 0, 0, 3), SimdUtils.Shuffle.MMShuffle3003 },
        { SimdUtils.Shuffle.MMShuffle(3, 0, 1, 0), SimdUtils.Shuffle.MMShuffle3010 },
        { SimdUtils.Shuffle.MMShuffle(3, 0, 1, 1), SimdUtils.Shuffle.MMShuffle3011 },
        { SimdUtils.Shuffle.MMShuffle(3, 0, 1, 2), SimdUtils.Shuffle.MMShuffle3012 },
        { SimdUtils.Shuffle.MMShuffle(3, 0, 1, 3), SimdUtils.Shuffle.MMShuffle3013 },
        { SimdUtils.Shuffle.MMShuffle(3, 0, 2, 0), SimdUtils.Shuffle.MMShuffle3020 },
        { SimdUtils.Shuffle.MMShuffle(3, 0, 2, 1), SimdUtils.Shuffle.MMShuffle3021 },
        { SimdUtils.Shuffle.MMShuffle(3, 0, 2, 2), SimdUtils.Shuffle.MMShuffle3022 },
        { SimdUtils.Shuffle.MMShuffle(3, 0, 2, 3), SimdUtils.Shuffle.MMShuffle3023 },
        { SimdUtils.Shuffle.MMShuffle(3, 0, 3, 0), SimdUtils.Shuffle.MMShuffle3030 },
        { SimdUtils.Shuffle.MMShuffle(3, 0, 3, 1), SimdUtils.Shuffle.MMShuffle3031 },
        { SimdUtils.Shuffle.MMShuffle(3, 0, 3, 2), SimdUtils.Shuffle.MMShuffle3032 },
        { SimdUtils.Shuffle.MMShuffle(3, 0, 3, 3), SimdUtils.Shuffle.MMShuffle3033 },
        { SimdUtils.Shuffle.MMShuffle(3, 1, 0, 0), SimdUtils.Shuffle.MMShuffle3100 },
        { SimdUtils.Shuffle.MMShuffle(3, 1, 0, 1), SimdUtils.Shuffle.MMShuffle3101 },
        { SimdUtils.Shuffle.MMShuffle(3, 1, 0, 2), SimdUtils.Shuffle.MMShuffle3102 },
        { SimdUtils.Shuffle.MMShuffle(3, 1, 0, 3), SimdUtils.Shuffle.MMShuffle3103 },
        { SimdUtils.Shuffle.MMShuffle(3, 1, 1, 0), SimdUtils.Shuffle.MMShuffle3110 },
        { SimdUtils.Shuffle.MMShuffle(3, 1, 1, 1), SimdUtils.Shuffle.MMShuffle3111 },
        { SimdUtils.Shuffle.MMShuffle(3, 1, 1, 2), SimdUtils.Shuffle.MMShuffle3112 },
        { SimdUtils.Shuffle.MMShuffle(3, 1, 1, 3), SimdUtils.Shuffle.MMShuffle3113 },
        { SimdUtils.Shuffle.MMShuffle(3, 1, 2, 0), SimdUtils.Shuffle.MMShuffle3120 },
        { SimdUtils.Shuffle.MMShuffle(3, 1, 2, 1), SimdUtils.Shuffle.MMShuffle3121 },
        { SimdUtils.Shuffle.MMShuffle(3, 1, 2, 2), SimdUtils.Shuffle.MMShuffle3122 },
        { SimdUtils.Shuffle.MMShuffle(3, 1, 2, 3), SimdUtils.Shuffle.MMShuffle3123 },
        { SimdUtils.Shuffle.MMShuffle(3, 1, 3, 0), SimdUtils.Shuffle.MMShuffle3130 },
        { SimdUtils.Shuffle.MMShuffle(3, 1, 3, 1), SimdUtils.Shuffle.MMShuffle3131 },
        { SimdUtils.Shuffle.MMShuffle(3, 1, 3, 2), SimdUtils.Shuffle.MMShuffle3132 },
        { SimdUtils.Shuffle.MMShuffle(3, 1, 3, 3), SimdUtils.Shuffle.MMShuffle3133 },
        { SimdUtils.Shuffle.MMShuffle(3, 2, 0, 0), SimdUtils.Shuffle.MMShuffle3200 },
        { SimdUtils.Shuffle.MMShuffle(3, 2, 0, 1), SimdUtils.Shuffle.MMShuffle3201 },
        { SimdUtils.Shuffle.MMShuffle(3, 2, 0, 2), SimdUtils.Shuffle.MMShuffle3202 },
        { SimdUtils.Shuffle.MMShuffle(3, 2, 0, 3), SimdUtils.Shuffle.MMShuffle3203 },
        { SimdUtils.Shuffle.MMShuffle(3, 2, 1, 0), SimdUtils.Shuffle.MMShuffle3210 },
        { SimdUtils.Shuffle.MMShuffle(3, 2, 1, 1), SimdUtils.Shuffle.MMShuffle3211 },
        { SimdUtils.Shuffle.MMShuffle(3, 2, 1, 2), SimdUtils.Shuffle.MMShuffle3212 },
        { SimdUtils.Shuffle.MMShuffle(3, 2, 1, 3), SimdUtils.Shuffle.MMShuffle3213 },
        { SimdUtils.Shuffle.MMShuffle(3, 2, 2, 0), SimdUtils.Shuffle.MMShuffle3220 },
        { SimdUtils.Shuffle.MMShuffle(3, 2, 2, 1), SimdUtils.Shuffle.MMShuffle3221 },
        { SimdUtils.Shuffle.MMShuffle(3, 2, 2, 2), SimdUtils.Shuffle.MMShuffle3222 },
        { SimdUtils.Shuffle.MMShuffle(3, 2, 2, 3), SimdUtils.Shuffle.MMShuffle3223 },
        { SimdUtils.Shuffle.MMShuffle(3, 2, 3, 0), SimdUtils.Shuffle.MMShuffle3230 },
        { SimdUtils.Shuffle.MMShuffle(3, 2, 3, 1), SimdUtils.Shuffle.MMShuffle3231 },
        { SimdUtils.Shuffle.MMShuffle(3, 2, 3, 2), SimdUtils.Shuffle.MMShuffle3232 },
        { SimdUtils.Shuffle.MMShuffle(3, 2, 3, 3), SimdUtils.Shuffle.MMShuffle3233 },
        { SimdUtils.Shuffle.MMShuffle(3, 3, 0, 0), SimdUtils.Shuffle.MMShuffle3300 },
        { SimdUtils.Shuffle.MMShuffle(3, 3, 0, 1), SimdUtils.Shuffle.MMShuffle3301 },
        { SimdUtils.Shuffle.MMShuffle(3, 3, 0, 2), SimdUtils.Shuffle.MMShuffle3302 },
        { SimdUtils.Shuffle.MMShuffle(3, 3, 0, 3), SimdUtils.Shuffle.MMShuffle3303 },
        { SimdUtils.Shuffle.MMShuffle(3, 3, 1, 0), SimdUtils.Shuffle.MMShuffle3310 },
        { SimdUtils.Shuffle.MMShuffle(3, 3, 1, 1), SimdUtils.Shuffle.MMShuffle3311 },
        { SimdUtils.Shuffle.MMShuffle(3, 3, 1, 2), SimdUtils.Shuffle.MMShuffle3312 },
        { SimdUtils.Shuffle.MMShuffle(3, 3, 1, 3), SimdUtils.Shuffle.MMShuffle3313 },
        { SimdUtils.Shuffle.MMShuffle(3, 3, 2, 0), SimdUtils.Shuffle.MMShuffle3320 },
        { SimdUtils.Shuffle.MMShuffle(3, 3, 2, 1), SimdUtils.Shuffle.MMShuffle3321 },
        { SimdUtils.Shuffle.MMShuffle(3, 3, 2, 2), SimdUtils.Shuffle.MMShuffle3322 },
        { SimdUtils.Shuffle.MMShuffle(3, 3, 2, 3), SimdUtils.Shuffle.MMShuffle3323 },
        { SimdUtils.Shuffle.MMShuffle(3, 3, 3, 0), SimdUtils.Shuffle.MMShuffle3330 },
        { SimdUtils.Shuffle.MMShuffle(3, 3, 3, 1), SimdUtils.Shuffle.MMShuffle3331 },
        { SimdUtils.Shuffle.MMShuffle(3, 3, 3, 2), SimdUtils.Shuffle.MMShuffle3332 },
        { SimdUtils.Shuffle.MMShuffle(3, 3, 3, 3), SimdUtils.Shuffle.MMShuffle3333 }
    };

    [Theory]
    [MemberData(nameof(MMShuffleData))]
    public void MMShuffleConstantsAreCorrect(byte expected, byte actual)
        => Assert.Equal(expected, actual);

    [Theory]
    [MemberData(nameof(ArraySizesDivisibleBy4))]
    public void BulkShuffleFloat4Channel(int count)
    {
        static void RunTest(string serialized)
        {
            // No need to test multiple shuffle controls as the
            // pipeline is always the same.
            int size = FeatureTestRunner.Deserialize<int>(serialized);
            const byte control = SimdUtils.Shuffle.MMShuffle0123;

            TestShuffleFloat4Channel(
                size,
                (s, d) => SimdUtils.Shuffle4(s.Span, d.Span, control),
                control);
        }

        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunTest,
            count,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);
    }

    [Theory]
    [MemberData(nameof(ArraySizesDivisibleBy4))]
    public void BulkShuffleByte4Channel(int count)
    {
        static void RunTest(string serialized)
        {
            int size = FeatureTestRunner.Deserialize<int>(serialized);

            // These cannot be expressed as a theory as you cannot
            // use RemoteExecutor within generic methods nor pass
            // IShuffle4 to the generic utils method.
            WXYZShuffle4 wxyz = default;
            TestShuffleByte4Channel(
                size,
                (s, d) => SimdUtils.Shuffle4(s.Span, d.Span, wxyz),
                SimdUtils.Shuffle.MMShuffle2103);

            WZYXShuffle4 wzyx = default;
            TestShuffleByte4Channel(
                size,
                (s, d) => SimdUtils.Shuffle4(s.Span, d.Span, wzyx),
                SimdUtils.Shuffle.MMShuffle0123);

            YZWXShuffle4 yzwx = default;
            TestShuffleByte4Channel(
                size,
                (s, d) => SimdUtils.Shuffle4(s.Span, d.Span, yzwx),
                SimdUtils.Shuffle.MMShuffle0321);

            ZYXWShuffle4 zyxw = default;
            TestShuffleByte4Channel(
                size,
                (s, d) => SimdUtils.Shuffle4(s.Span, d.Span, zyxw),
                SimdUtils.Shuffle.MMShuffle3012);

            DefaultShuffle4 xwyz = new(SimdUtils.Shuffle.MMShuffle2130);
            TestShuffleByte4Channel(
                size,
                (s, d) => SimdUtils.Shuffle4(s.Span, d.Span, xwyz),
                xwyz.Control);

            DefaultShuffle4 yyyy = new(SimdUtils.Shuffle.MMShuffle1111);
            TestShuffleByte4Channel(
                size,
                (s, d) => SimdUtils.Shuffle4(s.Span, d.Span, yyyy),
                yyyy.Control);

            DefaultShuffle4 wwww = new(SimdUtils.Shuffle.MMShuffle3333);
            TestShuffleByte4Channel(
                size,
                (s, d) => SimdUtils.Shuffle4(s.Span, d.Span, wwww),
                wwww.Control);
        }

        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunTest,
            count,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX2 | HwIntrinsics.DisableHWIntrinsic);
    }

    [Theory]
    [MemberData(nameof(ArraySizesDivisibleBy3))]
    public void BulkShuffleByte3Channel(int count)
    {
        static void RunTest(string serialized)
        {
            int size = FeatureTestRunner.Deserialize<int>(serialized);

            // These cannot be expressed as a theory as you cannot
            // use RemoteExecutor within generic methods nor pass
            // IShuffle3 to the generic utils method.
            DefaultShuffle3 zyx = new(SimdUtils.Shuffle.MMShuffle3012);
            TestShuffleByte3Channel(
                size,
                (s, d) => SimdUtils.Shuffle3(s.Span, d.Span, zyx),
                zyx.Control);

            DefaultShuffle3 xyz = new(SimdUtils.Shuffle.MMShuffle3210);
            TestShuffleByte3Channel(
                size,
                (s, d) => SimdUtils.Shuffle3(s.Span, d.Span, xyz),
                xyz.Control);

            DefaultShuffle3 yyy = new(SimdUtils.Shuffle.MMShuffle3111);
            TestShuffleByte3Channel(
                size,
                (s, d) => SimdUtils.Shuffle3(s.Span, d.Span, yyy),
                yyy.Control);

            DefaultShuffle3 zzz = new(SimdUtils.Shuffle.MMShuffle3222);
            TestShuffleByte3Channel(
                size,
                (s, d) => SimdUtils.Shuffle3(s.Span, d.Span, zzz),
                zzz.Control);
        }

        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunTest,
            count,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX2 | HwIntrinsics.DisableHWIntrinsic);
    }

    [Theory]
    [MemberData(nameof(ArraySizesDivisibleBy3))]
    public void BulkPad3Shuffle4Channel(int count)
    {
        static void RunTest(string serialized)
        {
            int size = FeatureTestRunner.Deserialize<int>(serialized);

            // These cannot be expressed as a theory as you cannot
            // use RemoteExecutor within generic methods nor pass
            // IPad3Shuffle4 to the generic utils method.
            XYZWPad3Shuffle4 xyzw = default;
            TestPad3Shuffle4Channel(
                size,
                (s, d) => SimdUtils.Pad3Shuffle4(s.Span, d.Span, xyzw),
                SimdUtils.Shuffle.MMShuffle3210);

            DefaultPad3Shuffle4 xwyz = new(SimdUtils.Shuffle.MMShuffle2130);
            TestPad3Shuffle4Channel(
                size,
                (s, d) => SimdUtils.Pad3Shuffle4(s.Span, d.Span, xwyz),
                xwyz.Control);

            DefaultPad3Shuffle4 yyyy = new(SimdUtils.Shuffle.MMShuffle1111);
            TestPad3Shuffle4Channel(
                size,
                (s, d) => SimdUtils.Pad3Shuffle4(s.Span, d.Span, yyyy),
                yyyy.Control);

            DefaultPad3Shuffle4 wwww = new(SimdUtils.Shuffle.MMShuffle3333);
            TestPad3Shuffle4Channel(
                size,
                (s, d) => SimdUtils.Pad3Shuffle4(s.Span, d.Span, wwww),
                wwww.Control);
        }

        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunTest,
            count,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX2 | HwIntrinsics.DisableHWIntrinsic);
    }

    [Theory]
    [MemberData(nameof(ArraySizesDivisibleBy4))]
    public void BulkShuffle4Slice3Channel(int count)
    {
        static void RunTest(string serialized)
        {
            int size = FeatureTestRunner.Deserialize<int>(serialized);

            // These cannot be expressed as a theory as you cannot
            // use RemoteExecutor within generic methods nor pass
            // IShuffle4Slice3 to the generic utils method.
            XYZWShuffle4Slice3 xyzw = default;
            TestShuffle4Slice3Channel(
                size,
                (s, d) => SimdUtils.Shuffle4Slice3(s.Span, d.Span, xyzw),
                SimdUtils.Shuffle.MMShuffle3210);

            DefaultShuffle4Slice3 xwyz = new(SimdUtils.Shuffle.MMShuffle2130);
            TestShuffle4Slice3Channel(
                size,
                (s, d) => SimdUtils.Shuffle4Slice3(s.Span, d.Span, xwyz),
                xwyz.Control);

            DefaultShuffle4Slice3 yyyy = new(SimdUtils.Shuffle.MMShuffle1111);
            TestShuffle4Slice3Channel(
                size,
                (s, d) => SimdUtils.Shuffle4Slice3(s.Span, d.Span, yyyy),
                yyyy.Control);

            DefaultShuffle4Slice3 wwww = new(SimdUtils.Shuffle.MMShuffle3333);
            TestShuffle4Slice3Channel(
                size,
                (s, d) => SimdUtils.Shuffle4Slice3(s.Span, d.Span, wwww),
                wwww.Control);
        }

        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunTest,
            count,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);
    }

    private static void TestShuffleFloat4Channel(
        int count,
        Action<Memory<float>, Memory<float>> convert,
        byte control)
    {
        float[] source = new Random(count).GenerateRandomFloatArray(count, 0, 256);
        float[] result = new float[count];

        float[] expected = new float[count];

        SimdUtils.Shuffle.InverseMMShuffle(
            control,
            out uint p3,
            out uint p2,
            out uint p1,
            out uint p0);

        for (int i = 0; i < expected.Length; i += 4)
        {
            expected[i] = source[p0 + i];
            expected[i + 1] = source[p1 + i];
            expected[i + 2] = source[p2 + i];
            expected[i + 3] = source[p3 + i];
        }

        convert(source, result);

        Assert.Equal(expected, result, new ApproximateFloatComparer(1e-5F));
    }

    private static void TestShuffleByte4Channel(
        int count,
        Action<Memory<byte>, Memory<byte>> convert,
        byte control)
    {
        byte[] source = new byte[count];
        new Random(count).NextBytes(source);
        byte[] result = new byte[count];

        byte[] expected = new byte[count];

        SimdUtils.Shuffle.InverseMMShuffle(
            control,
            out uint p3,
            out uint p2,
            out uint p1,
            out uint p0);

        for (int i = 0; i < expected.Length; i += 4)
        {
            expected[i] = source[p0 + i];
            expected[i + 1] = source[p1 + i];
            expected[i + 2] = source[p2 + i];
            expected[i + 3] = source[p3 + i];
        }

        convert(source, result);

        Assert.Equal(expected, result);
    }

    private static void TestShuffleByte3Channel(
        int count,
        Action<Memory<byte>, Memory<byte>> convert,
        byte control)
    {
        byte[] source = new byte[count];
        new Random(count).NextBytes(source);
        byte[] result = new byte[count];

        byte[] expected = new byte[count];

        SimdUtils.Shuffle.InverseMMShuffle(
            control,
            out uint _,
            out uint p2,
            out uint p1,
            out uint p0);

        for (int i = 0; i < expected.Length; i += 3)
        {
            expected[i] = source[p0 + i];
            expected[i + 1] = source[p1 + i];
            expected[i + 2] = source[p2 + i];
        }

        convert(source, result);

        Assert.Equal(expected, result);
    }

    private static void TestPad3Shuffle4Channel(
        int count,
        Action<Memory<byte>, Memory<byte>> convert,
        byte control)
    {
        byte[] source = new byte[count];
        new Random(count).NextBytes(source);

        byte[] result = new byte[count * 4 / 3];

        byte[] expected = new byte[result.Length];

        SimdUtils.Shuffle.InverseMMShuffle(
            control,
            out uint p3,
            out uint p2,
            out uint p1,
            out uint p0);

        for (int i = 0, j = 0; i < expected.Length; i += 4, j += 3)
        {
            expected[p0 + i] = source[j];
            expected[p1 + i] = source[j + 1];
            expected[p2 + i] = source[j + 2];
            expected[p3 + i] = byte.MaxValue;
        }

        Span<byte> temp = stackalloc byte[4];
        for (int i = 0, j = 0; i < expected.Length; i += 4, j += 3)
        {
            temp[0] = source[j];
            temp[1] = source[j + 1];
            temp[2] = source[j + 2];
            temp[3] = byte.MaxValue;

            expected[i] = temp[(int)p0];
            expected[i + 1] = temp[(int)p1];
            expected[i + 2] = temp[(int)p2];
            expected[i + 3] = temp[(int)p3];
        }

        convert(source, result);

        for (int i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i], result[i]);
        }

        Assert.Equal(expected, result);
    }

    private static void TestShuffle4Slice3Channel(
        int count,
        Action<Memory<byte>, Memory<byte>> convert,
        byte control)
    {
        byte[] source = new byte[count];
        new Random(count).NextBytes(source);

        byte[] result = new byte[count * 3 / 4];

        byte[] expected = new byte[result.Length];

        SimdUtils.Shuffle.InverseMMShuffle(
            control,
            out uint _,
            out uint p2,
            out uint p1,
            out uint p0);

        for (int i = 0, j = 0; i < expected.Length; i += 3, j += 4)
        {
            expected[i] = source[p0 + j];
            expected[i + 1] = source[p1 + j];
            expected[i + 2] = source[p2 + j];
        }

        convert(source, result);

        for (int i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i], result[i]);
        }

        Assert.Equal(expected, result);
    }
}
