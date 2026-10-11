// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Dct;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing;

/// <summary>
/// Performs transpose on JPEG XL DCT blocks.
/// </summary>
internal static class JxlTranspose
{
    public static void Transpose(int r, int c, JxlDctSource from, JxlDctOutput to)
    {
        if (Vector256.IsHardwareAccelerated)
        {
            if (((r | c) & 7) == 0) // equivalent to (r % 8 == 0 && c % 8 == 0); micro-optimization, reduces one branch
            {
                // we can use SIMD
                TransposeSimd256(r, c, from, to, r, c);
                return;
            }
        }
        else if (Vector128.IsHardwareAccelerated)
        {
            if (((r | c) & 3) == 0) // equivalent to (r % 4 == 0 && c % 4 == 0); micro-optimization, reduces one branch
            {
                // we can use SIMD
                TransposeSimd128(r, c, from, to, r, c);
                return;
            }
        }

        // fallback: can't use SIMD (block size isn't aligned or
        // there's no v128/v256 support)
        for (int n = 0; n < r; n++)
        {
            for (int m = 0; m < c; m++)
            {
                to.Write(from.Read(n, m), m, n);
            }
        }
    }

    private static void TransposeSimd256(int rowsOr0, int colsOr0, JxlDctSource from, JxlDctOutput to, int rowsP, int colsP)
    {
        int rows = rowsOr0 == 0 ? rowsP : rowsOr0;
        int cols = colsOr0 == 0 ? colsP : colsOr0;

        for (int n = 0; n < rows; n += 8)
        {
            for (int m = 0; m < cols; m += 8)
            {
                Vector256<float> i0 = from.LoadPart256(n, m);
                Vector256<float> i1 = from.LoadPart256(n + 1, m);
                Vector256<float> i2 = from.LoadPart256(n + 2, m);
                Vector256<float> i3 = from.LoadPart256(n + 3, m);
                Vector256<float> i4 = from.LoadPart256(n + 4, m);
                Vector256<float> i5 = from.LoadPart256(n + 5, m);
                Vector256<float> i6 = from.LoadPart256(n + 6, m);
                Vector256<float> i7 = from.LoadPart256(n + 7, m);

                Vector256<float> q0 = Vector256_.InterleaveLower(i0, i2);
                Vector256<float> q1 = Vector256_.InterleaveLower(i1, i3);
                Vector256<float> q2 = Vector256_.InterleaveUpper(i0, i2);
                Vector256<float> q3 = Vector256_.InterleaveUpper(i1, i3);
                Vector256<float> q4 = Vector256_.InterleaveLower(i4, i6);
                Vector256<float> q5 = Vector256_.InterleaveLower(i5, i7);
                Vector256<float> q6 = Vector256_.InterleaveUpper(i4, i6);
                Vector256<float> q7 = Vector256_.InterleaveUpper(i5, i7);

                Vector256<float> r0 = Vector256_.InterleaveLower(q0, q1);
                Vector256<float> r1 = Vector256_.InterleaveUpper(q0, q1);
                Vector256<float> r2 = Vector256_.InterleaveLower(q2, q3);
                Vector256<float> r3 = Vector256_.InterleaveUpper(q2, q3);
                Vector256<float> r4 = Vector256_.InterleaveLower(q4, q5);
                Vector256<float> r5 = Vector256_.InterleaveUpper(q4, q5);
                Vector256<float> r6 = Vector256_.InterleaveLower(q6, q7);
                Vector256<float> r7 = Vector256_.InterleaveUpper(q6, q7);

                i0 = JxlSimdUtils.ConcatLowerLower(r4, r0);
                i1 = JxlSimdUtils.ConcatLowerLower(r5, r1);
                i2 = JxlSimdUtils.ConcatLowerLower(r6, r2);
                i3 = JxlSimdUtils.ConcatLowerLower(r7, r3);
                i4 = JxlSimdUtils.ConcatUpperUpper(r4, r0);
                i5 = JxlSimdUtils.ConcatUpperUpper(r5, r1);
                i6 = JxlSimdUtils.ConcatUpperUpper(r6, r2);
                i7 = JxlSimdUtils.ConcatUpperUpper(r7, r3);

                to.StorePart256(i0, m, n);
                to.StorePart256(i1, m + 1, n);
                to.StorePart256(i2, m + 2, n);
                to.StorePart256(i3, m + 3, n);
                to.StorePart256(i4, m + 4, n);
                to.StorePart256(i5, m + 5, n);
                to.StorePart256(i6, m + 6, n);
                to.StorePart256(i7, m + 7, n);
            }
        }
    }

    private static void TransposeSimd128(int rowsOr0, int colsOr0, JxlDctSource from, JxlDctOutput to, int rowsP, int colsP)
    {
        int rows = rowsOr0 == 0 ? rowsP : rowsOr0;
        int cols = colsOr0 == 0 ? colsP : colsOr0;

        for (int n = 0; n < rows; n += 4)
        {
            for (int m = 0; m < cols; m += 4)
            {
                Vector128<float> p0 = from.LoadPart128(n, m);
                Vector128<float> p1 = from.LoadPart128(n + 1, m);
                Vector128<float> p2 = from.LoadPart128(n + 2, m);
                Vector128<float> p3 = from.LoadPart128(n + 3, m);

                Vector128<float> q0 = Vector128_.InterleaveLower(p0, p2);
                Vector128<float> q1 = Vector128_.InterleaveLower(p1, p3);
                Vector128<float> q2 = Vector128_.InterleaveUpper(p0, p2);
                Vector128<float> q3 = Vector128_.InterleaveUpper(p1, p3);

                Vector128<float> r0 = Vector128_.InterleaveLower(q0, q1);
                Vector128<float> r1 = Vector128_.InterleaveUpper(q0, q1);
                Vector128<float> r2 = Vector128_.InterleaveLower(q2, q3);
                Vector128<float> r3 = Vector128_.InterleaveUpper(q2, q3);

                to.StorePart128(r0, m, n);
                to.StorePart128(r1, m + 1, n);
                to.StorePart128(r2, m + 2, n);
                to.StorePart128(r3, m + 3, n);
            }
        }
    }
}
