// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Webp;

/// <summary>
/// Utility methods for lossy and lossless webp format.
/// </summary>
internal static class WebpCommonUtils
{
    /// <summary>
    /// Checks if the pixel row is not opaque.
    /// </summary>
    /// <param name="row">The row to check.</param>
    /// <returns>Returns true if alpha has non-0xff values.</returns>
    public static unsafe bool CheckNonOpaque(ReadOnlySpan<Bgra32> row)
    {
        if (Vector256.IsHardwareAccelerated)
        {
            ReadOnlySpan<byte> rowBytes = MemoryMarshal.AsBytes(row);
            int i = 0;
            int length = (row.Length * 4) - 3;
            fixed (byte* src = rowBytes)
            {
                Vector256<byte> alphaMaskVector256 = Vector256.Create(0, 0, 0, 255, 0, 0, 0, 255, 0, 0, 0, 255, 0, 0, 0, 255, 0, 0, 0, 255, 0, 0, 0, 255, 0, 0, 0, 255, 0, 0, 0, 255);
                Vector256<byte> all0x80Vector256 = Vector256.Create((byte)0x80).AsByte();

                for (; i + 128 <= length; i += 128)
                {
                    Vector256<byte> a0 = Vector256.Load(src + i).AsByte();
                    Vector256<byte> a1 = Vector256.Load(src + i + 32).AsByte();
                    Vector256<byte> a2 = Vector256.Load(src + i + 64).AsByte();
                    Vector256<byte> a3 = Vector256.Load(src + i + 96).AsByte();
                    Vector256<int> b0 = (a0 & alphaMaskVector256).AsInt32();
                    Vector256<int> b1 = (a1 & alphaMaskVector256).AsInt32();
                    Vector256<int> b2 = (a2 & alphaMaskVector256).AsInt32();
                    Vector256<int> b3 = (a3 & alphaMaskVector256).AsInt32();
                    Vector256<short> c0 = Vector256_.PackSignedSaturate(b0, b1).AsInt16();
                    Vector256<short> c1 = Vector256_.PackSignedSaturate(b2, b3).AsInt16();
                    Vector256<byte> d = Vector256_.PackSignedSaturate(c0, c1).AsByte();
                    Vector256<byte> bits = Vector256.Equals(d, all0x80Vector256);
                    uint mask = bits.ExtractMostSignificantBits();
                    if (mask != 0xFFFF_FFFF)
                    {
                        return true;
                    }
                }

                for (; i + 64 <= length; i += 64)
                {
                    if (IsNoneOpaque64BytesVector128(src, i))
                    {
                        return true;
                    }
                }

                for (; i + 32 <= length; i += 32)
                {
                    if (IsNonOpaque32BytesVector128(src, i))
                    {
                        return true;
                    }
                }

                for (; i <= length; i += 4)
                {
                    if (src[i + 3] != 0xFF)
                    {
                        return true;
                    }
                }
            }
        }
        else if (Vector128.IsHardwareAccelerated)
        {
            ReadOnlySpan<byte> rowBytes = MemoryMarshal.AsBytes(row);
            int i = 0;
            int length = (row.Length * 4) - 3;
            fixed (byte* src = rowBytes)
            {
                for (; i + 64 <= length; i += 64)
                {
                    if (IsNoneOpaque64BytesVector128(src, i))
                    {
                        return true;
                    }
                }

                for (; i + 32 <= length; i += 32)
                {
                    if (IsNonOpaque32BytesVector128(src, i))
                    {
                        return true;
                    }
                }

                for (; i <= length; i += 4)
                {
                    if (src[i + 3] != 0xFF)
                    {
                        return true;
                    }
                }
            }
        }
        else
        {
            for (int x = 0; x < row.Length; x++)
            {
                if (row[x].A != 0xFF)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static unsafe bool IsNoneOpaque64BytesVector128(byte* src, int i)
    {
        Vector128<byte> alphaMask = Vector128.Create(0, 0, 0, 255, 0, 0, 0, 255, 0, 0, 0, 255, 0, 0, 0, 255);

        Vector128<byte> a0 = Vector128.Load(src + i).AsByte();
        Vector128<byte> a1 = Vector128.Load(src + i + 16).AsByte();
        Vector128<byte> a2 = Vector128.Load(src + i + 32).AsByte();
        Vector128<byte> a3 = Vector128.Load(src + i + 48).AsByte();
        Vector128<int> b0 = (a0 & alphaMask).AsInt32();
        Vector128<int> b1 = (a1 & alphaMask).AsInt32();
        Vector128<int> b2 = (a2 & alphaMask).AsInt32();
        Vector128<int> b3 = (a3 & alphaMask).AsInt32();
        Vector128<short> c0 = Vector128_.PackSignedSaturate(b0, b1).AsInt16();
        Vector128<short> c1 = Vector128_.PackSignedSaturate(b2, b3).AsInt16();
        Vector128<byte> d = Vector128_.PackSignedSaturate(c0, c1).AsByte();
        Vector128<byte> bits = Vector128.Equals(d, Vector128.Create((byte)0x80).AsByte());
        uint mask = bits.ExtractMostSignificantBits();
        return mask != 0xFFFF;
    }

    private static unsafe bool IsNonOpaque32BytesVector128(byte* src, int i)
    {
        Vector128<byte> alphaMask = Vector128.Create(0, 0, 0, 255, 0, 0, 0, 255, 0, 0, 0, 255, 0, 0, 0, 255);

        Vector128<byte> a0 = Vector128.Load(src + i).AsByte();
        Vector128<byte> a1 = Vector128.Load(src + i + 16).AsByte();
        Vector128<int> b0 = (a0 & alphaMask).AsInt32();
        Vector128<int> b1 = (a1 & alphaMask).AsInt32();
        Vector128<short> c = Vector128_.PackSignedSaturate(b0, b1).AsInt16();
        Vector128<byte> d = Vector128_.PackSignedSaturate(c, c).AsByte();
        Vector128<byte> bits = Vector128.Equals(d, Vector128.Create((byte)0x80).AsByte());
        uint mask = bits.ExtractMostSignificantBits();
        return mask != 0xFFFF;
    }
}
