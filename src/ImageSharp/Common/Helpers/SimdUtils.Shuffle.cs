// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp;

internal static partial class SimdUtils
{
    /// <summary>
    /// Shuffle single-precision (32-bit) floating-point elements in <paramref name="source"/>
    /// using the control and store the results in <paramref name="dest"/>.
    /// </summary>
    /// <param name="source">The source span of floats.</param>
    /// <param name="dest">The destination span of floats.</param>
    /// <param name="control">The byte control.</param>
    [MethodImpl(InliningOptions.ShortMethod)]
    public static void Shuffle4(
        ReadOnlySpan<float> source,
        Span<float> dest,
        byte control)
    {
        VerifyShuffle4SpanInput(source, dest);

        HwIntrinsics.Shuffle4Reduce(ref source, ref dest, control);

        // Deal with the remainder:
        if (source.Length > 0)
        {
            Shuffle4Remainder(source, dest, control);
        }
    }

    /// <summary>
    /// Shuffle 8-bit integers within 128-bit lanes in <paramref name="source"/>
    /// using the control and store the results in <paramref name="dest"/>.
    /// </summary>
    /// <typeparam name="TShuffle">The type of shuffle struct.</typeparam>
    /// <param name="source">The source span of bytes.</param>
    /// <param name="dest">The destination span of bytes.</param>
    /// <param name="shuffle">The type of shuffle to perform.</param>
    [MethodImpl(InliningOptions.ShortMethod)]
    public static void Shuffle4<TShuffle>(
        ReadOnlySpan<byte> source,
        Span<byte> dest,
        TShuffle shuffle)
        where TShuffle : struct, IShuffle4
    {
        VerifyShuffle4SpanInput(source, dest);

        shuffle.ShuffleReduce(ref source, ref dest);

        // Deal with the remainder:
        if (source.Length > 0)
        {
            shuffle.RunFallbackShuffle(source, dest);
        }
    }

    /// <summary>
    /// Shuffle 8-bit integer triplets within 128-bit lanes in <paramref name="source"/>
    /// using the control and store the results in <paramref name="dest"/>.
    /// </summary>
    /// <typeparam name="TShuffle">The type of shuffle struct.</typeparam>
    /// <param name="source">The source span of bytes.</param>
    /// <param name="dest">The destination span of bytes.</param>
    /// <param name="shuffle">The type of shuffle to perform.</param>
    [MethodImpl(InliningOptions.ShortMethod)]
    public static void Shuffle3<TShuffle>(
        ReadOnlySpan<byte> source,
        Span<byte> dest,
        TShuffle shuffle)
        where TShuffle : struct, IShuffle3
    {
        // Source length should be smaller than dest length, and divisible by 3.
        VerifyShuffle3SpanInput(source, dest);

        shuffle.ShuffleReduce(ref source, ref dest);

        // Deal with the remainder:
        if (source.Length > 0)
        {
            shuffle.RunFallbackShuffle(source, dest);
        }
    }

    /// <summary>
    /// Pads then shuffles 8-bit integers within 128-bit lanes in <paramref name="source"/>
    /// using the control and store the results in <paramref name="dest"/>.
    /// </summary>
    /// <typeparam name="TShuffle">The type of shuffle struct.</typeparam>
    /// <param name="source">The source span of bytes.</param>
    /// <param name="dest">The destination span of bytes.</param>
    /// <param name="shuffle">The type of shuffle to perform.</param>
    [MethodImpl(InliningOptions.ShortMethod)]
    public static void Pad3Shuffle4<TShuffle>(
        ReadOnlySpan<byte> source,
        Span<byte> dest,
        TShuffle shuffle)
        where TShuffle : struct, IPad3Shuffle4
    {
        VerifyPad3Shuffle4SpanInput(source, dest);

        shuffle.ShuffleReduce(ref source, ref dest);

        // Deal with the remainder:
        if (source.Length > 0)
        {
            shuffle.RunFallbackShuffle(source, dest);
        }
    }

    /// <summary>
    /// Shuffles then slices 8-bit integers within 128-bit lanes in <paramref name="source"/>
    /// using the control and store the results in <paramref name="dest"/>.
    /// </summary>
    /// <typeparam name="TShuffle">The type of shuffle struct.</typeparam>
    /// <param name="source">The source span of bytes.</param>
    /// <param name="dest">The destination span of bytes.</param>
    /// <param name="shuffle">The type of shuffle to perform.</param>
    [MethodImpl(InliningOptions.ShortMethod)]
    public static void Shuffle4Slice3<TShuffle>(
        ReadOnlySpan<byte> source,
        Span<byte> dest,
        TShuffle shuffle)
        where TShuffle : struct, IShuffle4Slice3
    {
        VerifyShuffle4Slice3SpanInput(source, dest);

        shuffle.ShuffleReduce(ref source, ref dest);

        // Deal with the remainder:
        if (source.Length > 0)
        {
            shuffle.RunFallbackShuffle(source, dest);
        }
    }

    private static void Shuffle4Remainder(
        ReadOnlySpan<float> source,
        Span<float> dest,
        byte control)
    {
        ref float sBase = ref MemoryMarshal.GetReference(source);
        ref float dBase = ref MemoryMarshal.GetReference(dest);
        Shuffle.InverseMMShuffle(control, out int p3, out int p2, out int p1, out int p0);

        for (int i = 0; i < source.Length; i += 4)
        {
            Unsafe.Add(ref dBase, i) = Unsafe.Add(ref sBase, p0 + i);
            Unsafe.Add(ref dBase, i + 1) = Unsafe.Add(ref sBase, p1 + i);
            Unsafe.Add(ref dBase, i + 2) = Unsafe.Add(ref sBase, p2 + i);
            Unsafe.Add(ref dBase, i + 3) = Unsafe.Add(ref sBase, p3 + i);
        }
    }

    [Conditional("DEBUG")]
    internal static void VerifyShuffle4SpanInput<T>(ReadOnlySpan<T> source, Span<T> dest)
        where T : struct
    {
        DebugGuard.IsTrue(
            source.Length == dest.Length,
            nameof(source),
            "Input spans must be of same length!");

        DebugGuard.IsTrue(
            source.Length % 4 == 0,
            nameof(source),
            "Input spans must be divisable by 4!");
    }

    [Conditional("DEBUG")]
    private static void VerifyShuffle3SpanInput<T>(ReadOnlySpan<T> source, Span<T> dest)
        where T : struct
    {
        DebugGuard.IsTrue(
            source.Length <= dest.Length,
            nameof(source),
            "Source should fit into dest!");

        DebugGuard.IsTrue(
            source.Length % 3 == 0,
            nameof(source),
            "Input spans must be divisable by 3!");
    }

    [Conditional("DEBUG")]
    private static void VerifyPad3Shuffle4SpanInput(ReadOnlySpan<byte> source, Span<byte> dest)
    {
        DebugGuard.IsTrue(
            source.Length % 3 == 0,
            nameof(source),
            "Input span must be divisable by 3!");

        DebugGuard.IsTrue(
            dest.Length % 4 == 0,
            nameof(dest),
            "Output span must be divisable by 4!");

        DebugGuard.IsTrue(
            source.Length == dest.Length * 3 / 4,
            nameof(source),
            "Input span must be 3/4 the length of the output span!");
    }

    [Conditional("DEBUG")]
    private static void VerifyShuffle4Slice3SpanInput(ReadOnlySpan<byte> source, Span<byte> dest)
    {
        DebugGuard.IsTrue(
            source.Length % 4 == 0,
            nameof(source),
            "Input span must be divisable by 4!");

        DebugGuard.IsTrue(
            dest.Length % 3 == 0,
            nameof(dest),
            "Output span must be divisable by 3!");

        DebugGuard.IsTrue(
            dest.Length >= source.Length * 3 / 4,
            nameof(source),
            "Output span must be at least 3/4 the length of the input span!");
    }

    public static class Shuffle
    {
        public const byte MMShuffle0000 = 0b00000000;
        public const byte MMShuffle0001 = 0b00000001;
        public const byte MMShuffle0002 = 0b00000010;
        public const byte MMShuffle0003 = 0b00000011;
        public const byte MMShuffle0010 = 0b00000100;
        public const byte MMShuffle0011 = 0b00000101;
        public const byte MMShuffle0012 = 0b00000110;
        public const byte MMShuffle0013 = 0b00000111;
        public const byte MMShuffle0020 = 0b00001000;
        public const byte MMShuffle0021 = 0b00001001;
        public const byte MMShuffle0022 = 0b00001010;
        public const byte MMShuffle0023 = 0b00001011;
        public const byte MMShuffle0030 = 0b00001100;
        public const byte MMShuffle0031 = 0b00001101;
        public const byte MMShuffle0032 = 0b00001110;
        public const byte MMShuffle0033 = 0b00001111;
        public const byte MMShuffle0100 = 0b00010000;
        public const byte MMShuffle0101 = 0b00010001;
        public const byte MMShuffle0102 = 0b00010010;
        public const byte MMShuffle0103 = 0b00010011;
        public const byte MMShuffle0110 = 0b00010100;
        public const byte MMShuffle0111 = 0b00010101;
        public const byte MMShuffle0112 = 0b00010110;
        public const byte MMShuffle0113 = 0b00010111;
        public const byte MMShuffle0120 = 0b00011000;
        public const byte MMShuffle0121 = 0b00011001;
        public const byte MMShuffle0122 = 0b00011010;
        public const byte MMShuffle0123 = 0b00011011;
        public const byte MMShuffle0130 = 0b00011100;
        public const byte MMShuffle0131 = 0b00011101;
        public const byte MMShuffle0132 = 0b00011110;
        public const byte MMShuffle0133 = 0b00011111;
        public const byte MMShuffle0200 = 0b00100000;
        public const byte MMShuffle0201 = 0b00100001;
        public const byte MMShuffle0202 = 0b00100010;
        public const byte MMShuffle0203 = 0b00100011;
        public const byte MMShuffle0210 = 0b00100100;
        public const byte MMShuffle0211 = 0b00100101;
        public const byte MMShuffle0212 = 0b00100110;
        public const byte MMShuffle0213 = 0b00100111;
        public const byte MMShuffle0220 = 0b00101000;
        public const byte MMShuffle0221 = 0b00101001;
        public const byte MMShuffle0222 = 0b00101010;
        public const byte MMShuffle0223 = 0b00101011;
        public const byte MMShuffle0230 = 0b00101100;
        public const byte MMShuffle0231 = 0b00101101;
        public const byte MMShuffle0232 = 0b00101110;
        public const byte MMShuffle0233 = 0b00101111;
        public const byte MMShuffle0300 = 0b00110000;
        public const byte MMShuffle0301 = 0b00110001;
        public const byte MMShuffle0302 = 0b00110010;
        public const byte MMShuffle0303 = 0b00110011;
        public const byte MMShuffle0310 = 0b00110100;
        public const byte MMShuffle0311 = 0b00110101;
        public const byte MMShuffle0312 = 0b00110110;
        public const byte MMShuffle0313 = 0b00110111;
        public const byte MMShuffle0320 = 0b00111000;
        public const byte MMShuffle0321 = 0b00111001;
        public const byte MMShuffle0322 = 0b00111010;
        public const byte MMShuffle0323 = 0b00111011;
        public const byte MMShuffle0330 = 0b00111100;
        public const byte MMShuffle0331 = 0b00111101;
        public const byte MMShuffle0332 = 0b00111110;
        public const byte MMShuffle0333 = 0b00111111;
        public const byte MMShuffle1000 = 0b01000000;
        public const byte MMShuffle1001 = 0b01000001;
        public const byte MMShuffle1002 = 0b01000010;
        public const byte MMShuffle1003 = 0b01000011;
        public const byte MMShuffle1010 = 0b01000100;
        public const byte MMShuffle1011 = 0b01000101;
        public const byte MMShuffle1012 = 0b01000110;
        public const byte MMShuffle1013 = 0b01000111;
        public const byte MMShuffle1020 = 0b01001000;
        public const byte MMShuffle1021 = 0b01001001;
        public const byte MMShuffle1022 = 0b01001010;
        public const byte MMShuffle1023 = 0b01001011;
        public const byte MMShuffle1030 = 0b01001100;
        public const byte MMShuffle1031 = 0b01001101;
        public const byte MMShuffle1032 = 0b01001110;
        public const byte MMShuffle1033 = 0b01001111;
        public const byte MMShuffle1100 = 0b01010000;
        public const byte MMShuffle1101 = 0b01010001;
        public const byte MMShuffle1102 = 0b01010010;
        public const byte MMShuffle1103 = 0b01010011;
        public const byte MMShuffle1110 = 0b01010100;
        public const byte MMShuffle1111 = 0b01010101;
        public const byte MMShuffle1112 = 0b01010110;
        public const byte MMShuffle1113 = 0b01010111;
        public const byte MMShuffle1120 = 0b01011000;
        public const byte MMShuffle1121 = 0b01011001;
        public const byte MMShuffle1122 = 0b01011010;
        public const byte MMShuffle1123 = 0b01011011;
        public const byte MMShuffle1130 = 0b01011100;
        public const byte MMShuffle1131 = 0b01011101;
        public const byte MMShuffle1132 = 0b01011110;
        public const byte MMShuffle1133 = 0b01011111;
        public const byte MMShuffle1200 = 0b01100000;
        public const byte MMShuffle1201 = 0b01100001;
        public const byte MMShuffle1202 = 0b01100010;
        public const byte MMShuffle1203 = 0b01100011;
        public const byte MMShuffle1210 = 0b01100100;
        public const byte MMShuffle1211 = 0b01100101;
        public const byte MMShuffle1212 = 0b01100110;
        public const byte MMShuffle1213 = 0b01100111;
        public const byte MMShuffle1220 = 0b01101000;
        public const byte MMShuffle1221 = 0b01101001;
        public const byte MMShuffle1222 = 0b01101010;
        public const byte MMShuffle1223 = 0b01101011;
        public const byte MMShuffle1230 = 0b01101100;
        public const byte MMShuffle1231 = 0b01101101;
        public const byte MMShuffle1232 = 0b01101110;
        public const byte MMShuffle1233 = 0b01101111;
        public const byte MMShuffle1300 = 0b01110000;
        public const byte MMShuffle1301 = 0b01110001;
        public const byte MMShuffle1302 = 0b01110010;
        public const byte MMShuffle1303 = 0b01110011;
        public const byte MMShuffle1310 = 0b01110100;
        public const byte MMShuffle1311 = 0b01110101;
        public const byte MMShuffle1312 = 0b01110110;
        public const byte MMShuffle1313 = 0b01110111;
        public const byte MMShuffle1320 = 0b01111000;
        public const byte MMShuffle1321 = 0b01111001;
        public const byte MMShuffle1322 = 0b01111010;
        public const byte MMShuffle1323 = 0b01111011;
        public const byte MMShuffle1330 = 0b01111100;
        public const byte MMShuffle1331 = 0b01111101;
        public const byte MMShuffle1332 = 0b01111110;
        public const byte MMShuffle1333 = 0b01111111;
        public const byte MMShuffle2000 = 0b10000000;
        public const byte MMShuffle2001 = 0b10000001;
        public const byte MMShuffle2002 = 0b10000010;
        public const byte MMShuffle2003 = 0b10000011;
        public const byte MMShuffle2010 = 0b10000100;
        public const byte MMShuffle2011 = 0b10000101;
        public const byte MMShuffle2012 = 0b10000110;
        public const byte MMShuffle2013 = 0b10000111;
        public const byte MMShuffle2020 = 0b10001000;
        public const byte MMShuffle2021 = 0b10001001;
        public const byte MMShuffle2022 = 0b10001010;
        public const byte MMShuffle2023 = 0b10001011;
        public const byte MMShuffle2030 = 0b10001100;
        public const byte MMShuffle2031 = 0b10001101;
        public const byte MMShuffle2032 = 0b10001110;
        public const byte MMShuffle2033 = 0b10001111;
        public const byte MMShuffle2100 = 0b10010000;
        public const byte MMShuffle2101 = 0b10010001;
        public const byte MMShuffle2102 = 0b10010010;
        public const byte MMShuffle2103 = 0b10010011;
        public const byte MMShuffle2110 = 0b10010100;
        public const byte MMShuffle2111 = 0b10010101;
        public const byte MMShuffle2112 = 0b10010110;
        public const byte MMShuffle2113 = 0b10010111;
        public const byte MMShuffle2120 = 0b10011000;
        public const byte MMShuffle2121 = 0b10011001;
        public const byte MMShuffle2122 = 0b10011010;
        public const byte MMShuffle2123 = 0b10011011;
        public const byte MMShuffle2130 = 0b10011100;
        public const byte MMShuffle2131 = 0b10011101;
        public const byte MMShuffle2132 = 0b10011110;
        public const byte MMShuffle2133 = 0b10011111;
        public const byte MMShuffle2200 = 0b10100000;
        public const byte MMShuffle2201 = 0b10100001;
        public const byte MMShuffle2202 = 0b10100010;
        public const byte MMShuffle2203 = 0b10100011;
        public const byte MMShuffle2210 = 0b10100100;
        public const byte MMShuffle2211 = 0b10100101;
        public const byte MMShuffle2212 = 0b10100110;
        public const byte MMShuffle2213 = 0b10100111;
        public const byte MMShuffle2220 = 0b10101000;
        public const byte MMShuffle2221 = 0b10101001;
        public const byte MMShuffle2222 = 0b10101010;
        public const byte MMShuffle2223 = 0b10101011;
        public const byte MMShuffle2230 = 0b10101100;
        public const byte MMShuffle2231 = 0b10101101;
        public const byte MMShuffle2232 = 0b10101110;
        public const byte MMShuffle2233 = 0b10101111;
        public const byte MMShuffle2300 = 0b10110000;
        public const byte MMShuffle2301 = 0b10110001;
        public const byte MMShuffle2302 = 0b10110010;
        public const byte MMShuffle2303 = 0b10110011;
        public const byte MMShuffle2310 = 0b10110100;
        public const byte MMShuffle2311 = 0b10110101;
        public const byte MMShuffle2312 = 0b10110110;
        public const byte MMShuffle2313 = 0b10110111;
        public const byte MMShuffle2320 = 0b10111000;
        public const byte MMShuffle2321 = 0b10111001;
        public const byte MMShuffle2322 = 0b10111010;
        public const byte MMShuffle2323 = 0b10111011;
        public const byte MMShuffle2330 = 0b10111100;
        public const byte MMShuffle2331 = 0b10111101;
        public const byte MMShuffle2332 = 0b10111110;
        public const byte MMShuffle2333 = 0b10111111;
        public const byte MMShuffle3000 = 0b11000000;
        public const byte MMShuffle3001 = 0b11000001;
        public const byte MMShuffle3002 = 0b11000010;
        public const byte MMShuffle3003 = 0b11000011;
        public const byte MMShuffle3010 = 0b11000100;
        public const byte MMShuffle3011 = 0b11000101;
        public const byte MMShuffle3012 = 0b11000110;
        public const byte MMShuffle3013 = 0b11000111;
        public const byte MMShuffle3020 = 0b11001000;
        public const byte MMShuffle3021 = 0b11001001;
        public const byte MMShuffle3022 = 0b11001010;
        public const byte MMShuffle3023 = 0b11001011;
        public const byte MMShuffle3030 = 0b11001100;
        public const byte MMShuffle3031 = 0b11001101;
        public const byte MMShuffle3032 = 0b11001110;
        public const byte MMShuffle3033 = 0b11001111;
        public const byte MMShuffle3100 = 0b11010000;
        public const byte MMShuffle3101 = 0b11010001;
        public const byte MMShuffle3102 = 0b11010010;
        public const byte MMShuffle3103 = 0b11010011;
        public const byte MMShuffle3110 = 0b11010100;
        public const byte MMShuffle3111 = 0b11010101;
        public const byte MMShuffle3112 = 0b11010110;
        public const byte MMShuffle3113 = 0b11010111;
        public const byte MMShuffle3120 = 0b11011000;
        public const byte MMShuffle3121 = 0b11011001;
        public const byte MMShuffle3122 = 0b11011010;
        public const byte MMShuffle3123 = 0b11011011;
        public const byte MMShuffle3130 = 0b11011100;
        public const byte MMShuffle3131 = 0b11011101;
        public const byte MMShuffle3132 = 0b11011110;
        public const byte MMShuffle3133 = 0b11011111;
        public const byte MMShuffle3200 = 0b11100000;
        public const byte MMShuffle3201 = 0b11100001;
        public const byte MMShuffle3202 = 0b11100010;
        public const byte MMShuffle3203 = 0b11100011;
        public const byte MMShuffle3210 = 0b11100100;
        public const byte MMShuffle3211 = 0b11100101;
        public const byte MMShuffle3212 = 0b11100110;
        public const byte MMShuffle3213 = 0b11100111;
        public const byte MMShuffle3220 = 0b11101000;
        public const byte MMShuffle3221 = 0b11101001;
        public const byte MMShuffle3222 = 0b11101010;
        public const byte MMShuffle3223 = 0b11101011;
        public const byte MMShuffle3230 = 0b11101100;
        public const byte MMShuffle3231 = 0b11101101;
        public const byte MMShuffle3232 = 0b11101110;
        public const byte MMShuffle3233 = 0b11101111;
        public const byte MMShuffle3300 = 0b11110000;
        public const byte MMShuffle3301 = 0b11110001;
        public const byte MMShuffle3302 = 0b11110010;
        public const byte MMShuffle3303 = 0b11110011;
        public const byte MMShuffle3310 = 0b11110100;
        public const byte MMShuffle3311 = 0b11110101;
        public const byte MMShuffle3312 = 0b11110110;
        public const byte MMShuffle3313 = 0b11110111;
        public const byte MMShuffle3320 = 0b11111000;
        public const byte MMShuffle3321 = 0b11111001;
        public const byte MMShuffle3322 = 0b11111010;
        public const byte MMShuffle3323 = 0b11111011;
        public const byte MMShuffle3330 = 0b11111100;
        public const byte MMShuffle3331 = 0b11111101;
        public const byte MMShuffle3332 = 0b11111110;
        public const byte MMShuffle3333 = 0b11111111;

        [MethodImpl(InliningOptions.ShortMethod)]
        public static byte MMShuffle(byte p3, byte p2, byte p1, byte p0)
            => (byte)((p3 << 6) | (p2 << 4) | (p1 << 2) | p0);

        [MethodImpl(InliningOptions.ShortMethod)]
        public static void MMShuffleSpan(ref Span<byte> span, byte control)
        {
            InverseMMShuffle(
                 control,
                 out int p3,
                 out int p2,
                 out int p1,
                 out int p0);

            ref byte spanBase = ref MemoryMarshal.GetReference(span);

            for (int i = 0; i < span.Length; i += 4)
            {
                Unsafe.Add(ref spanBase, i) = (byte)(p0 + i);
                Unsafe.Add(ref spanBase, i + 1) = (byte)(p1 + i);
                Unsafe.Add(ref spanBase, i + 2) = (byte)(p2 + i);
                Unsafe.Add(ref spanBase, i + 3) = (byte)(p3 + i);
            }
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        public static void InverseMMShuffle(
            byte control,
            out int p3,
            out int p2,
            out int p1,
            out int p0)
        {
            p3 = (control >> 6) & 0x3;
            p2 = (control >> 4) & 0x3;
            p1 = (control >> 2) & 0x3;
            p0 = (control >> 0) & 0x3;
        }
    }
}
