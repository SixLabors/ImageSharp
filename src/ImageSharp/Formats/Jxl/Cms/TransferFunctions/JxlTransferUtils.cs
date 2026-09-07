// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;

namespace SixLabors.ImageSharp.Formats.Jxl.Cms.TransferFunctions;

internal static class JxlTransferUtils
{
    public static Vector<float> FastLinearToSrgb(Vector<float> v)
    {
        Vector<float> v025_05 = ((v.As<float, int>() & new Vector<int>(0x3effffff)) | new Vector<int>(0x3e800000)).As<int, float>();

        Vector<float> d1 = (v025_05 * Vector.Create(0.059914046f)) - Vector.Create(0.108894556f);
        Vector<float> d2 = (d1 * v025_05) + Vector.Create(0.107963754f);
        Vector<float> pow = (d2 * v025_05) + Vector.Create(0.018092343f);

        const uint baseBits = 0x40000000;

        ReadOnlySpan<byte> powers25to18 =
        [
            0x00, 0x0a, 0x19, 0x26,
            0x32, 0x41, 0x4d, 0x5c,
            0x68, 0x75, 0x83, 0x8f,
            0xa0, 0xaa, 0xb9, 0xc6
        ];

        ReadOnlySpan<byte> powers17to10 =
        [
            0x00, 0xb7, 0x04, 0x0d,
            0xcb, 0xe7, 0x41, 0x68,
            0x51, 0xd1, 0xeb, 0xf2,
            0x00, 0xb7, 0x04, 0x0d
        ];

        Vector<int> bits = Vector.AsVectorInt32(v);
        Vector<int> exp = (bits >> 23) - Vector.Create(118);

        exp &= new Vector<int>(0xf);

        Vector<int> p25to18 = GatherByteTable(exp, powers25to18);
        Vector<int> p17to10 = GatherByteTable(exp, powers17to10);

        Vector<int> mulBits =
            (p25to18 << 18) |
            (p17to10 << 10) |
            new Vector<int>((int)baseBits);

        Vector<float> mul = Vector.AsVectorSingle(mulBits);

        Vector<float> cutoff = new(0.0031308f);

        return Vector.ConditionalSelect(
            Vector.LessThan(v, cutoff),
            v * new Vector<float>(12.92f),
            (pow * mul) - new Vector<float>(0.055f));
    }

    private static Vector<int> GatherByteTable(Vector<int> indices, ReadOnlySpan<byte> table)
    {
        int count = Vector<int>.Count;
        Span<int> result = stackalloc int[count];

        for (int i = 0; i < count; i++)
        {
            result[i] = table[indices[i]];
        }

        return new Vector<int>(result);
    }
}
