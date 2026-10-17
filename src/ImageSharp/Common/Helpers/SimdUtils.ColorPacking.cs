// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp;

internal static partial class SimdUtils
{
    /// <summary>
    /// Interleaves four values from each of three component registers into twelve contiguous float words.
    /// </summary>
    /// <param name="x">The four first components.</param>
    /// <param name="y">The four second components.</param>
    /// <param name="z">The four third components.</param>
    /// <param name="destination">The first of twelve destination float words.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Interleave3(Vector128<float> x, Vector128<float> y, Vector128<float> z, ref float destination)
    {
        // Input registers are [X0..X3], [Y0..Y3], and [Z0..Z3]. Transpose needs a
        // fourth row; shifted X supplies [X1 X2 X3 0] so its columns overlap as
        // [Xn Yn Zn Xn+1]. No fourth source component is read.
        Vector128<float> shiftedX = Vector128_.ShiftRightBytesInVector(x.AsByte(), sizeof(float)).AsSingle();
        Transpose4(x, y, z, shiftedX, out Vector128<float> pixel0, out Vector128<float> pixel1, out Vector128<float> pixel2, out Vector128<float> pixel3);

        // Dropping pixel2.X lets Y2 complete [Y1 Z1 X2] from pixel1. Dropping
        // pixel3.X leaves [Y3 Z3] to complete [Z2 X3] from pixel2.
        Vector128<byte> shiftedPixel2 = Vector128_.ShiftRightBytesInVector(pixel2.AsByte(), sizeof(float));
        Vector128<float> packed1 = Vector128_.AlignRight(shiftedPixel2, pixel1.AsByte(), sizeof(float)).AsSingle();
        Vector128<byte> shiftedPixel3 = Vector128_.ShiftRightBytesInVector(pixel3.AsByte(), sizeof(float));
        Vector128<float> packed2 = Vector128_.AlignRight(shiftedPixel3, pixel2.AsByte(), sizeof(float) * 2).AsSingle();

        // Three full-register stores write exactly twelve destination words;
        // this is safe when these four pixels end the packed row.
        ref Vector128<float> first = ref Unsafe.As<float, Vector128<float>>(ref destination);
        first = pixel0;
        Unsafe.Add(ref first, 1) = packed1;
        Unsafe.Add(ref first, 2) = packed2;
    }

    /// <summary>
    /// Interleaves eight values from three component registers into consecutive triples.
    /// </summary>
    /// <param name="x">The eight first components.</param>
    /// <param name="y">The eight second components.</param>
    /// <param name="z">The eight third components.</param>
    /// <param name="destination">The first of twenty-four destination float words.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Interleave3(Vector256<float> x, Vector256<float> y, Vector256<float> z, ref float destination)
    {
        // AVX2 has no two-source, full-width permute. Pack two four-color groups without emulating that operation.
        // Each half writes twelve words, so the second half starts after the first four complete triples.
        Interleave3(x.GetLower(), y.GetLower(), z.GetLower(), ref destination);
        Interleave3(x.GetUpper(), y.GetUpper(), z.GetUpper(), ref Unsafe.Add(ref destination, 12));
    }

    /// <summary>
    /// Interleaves sixteen values from three component registers into consecutive triples.
    /// </summary>
    /// <param name="x">The sixteen first components.</param>
    /// <param name="y">The sixteen second components.</param>
    /// <param name="z">The sixteen third components.</param>
    /// <param name="destination">The first of forty-eight destination float words.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Interleave3(Vector512<float> x, Vector512<float> y, Vector512<float> z, ref float destination)
    {
        // The output is [X0 Y0 Z0 X1 Y1 Z1 ... X15 Y15 Z15]. Each register holds sixteen words,
        // so the three output registers start at X0, Y5 and Z10. First place X and Y; zero selects X0
        // in positions replaced by Z below. Indices 0..15 select X and 16..31 select Y.
        Vector512<int> xy0 = Vector512_.PermuteVar16x32x2(x.AsInt32(), Vector512.Create(0, 16, 0, 1, 17, 0, 2, 18, 0, 3, 19, 0, 4, 20, 0, 5), y.AsInt32());
        Vector512<int> xy1 = Vector512_.PermuteVar16x32x2(x.AsInt32(), Vector512.Create(21, 0, 6, 22, 0, 7, 23, 0, 8, 24, 0, 9, 25, 0, 10, 26), y.AsInt32());
        Vector512<int> xy2 = Vector512_.PermuteVar16x32x2(x.AsInt32(), Vector512.Create(0, 11, 27, 0, 12, 28, 0, 13, 29, 0, 14, 30, 0, 15, 31, 0), y.AsInt32());

        // Keep the X/Y words by their output position and select Z from the second input at indices 16..31.
        // Three unaligned stores write exactly 48 words. No padding is required after the destination.
        Vector512<int> packed0 = Vector512_.PermuteVar16x32x2(xy0, Vector512.Create(0, 1, 16, 3, 4, 17, 6, 7, 18, 9, 10, 19, 12, 13, 20, 15), z.AsInt32());
        Vector512<int> packed1 = Vector512_.PermuteVar16x32x2(xy1, Vector512.Create(0, 21, 2, 3, 22, 5, 6, 23, 8, 9, 24, 11, 12, 25, 14, 15), z.AsInt32());
        Vector512<int> packed2 = Vector512_.PermuteVar16x32x2(xy2, Vector512.Create(26, 1, 2, 27, 4, 5, 28, 7, 8, 29, 10, 11, 30, 13, 14, 31), z.AsInt32());
        packed0.AsSingle().StoreUnsafe(ref destination);
        packed1.AsSingle().StoreUnsafe(ref destination, 16U);
        packed2.AsSingle().StoreUnsafe(ref destination, 32U);
    }

    /// <summary>
    /// Deinterleaves four packed three-component values into component registers.
    /// </summary>
    /// <param name="packed">The source triples.</param>
    /// <param name="index">The first of four source values.</param>
    /// <param name="x">The four first components.</param>
    /// <param name="y">The four second components.</param>
    /// <param name="z">The four third components.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Deinterleave3(ReadOnlySpan<Vector3> packed, int index, out Vector128<float> x, out Vector128<float> y, out Vector128<float> z)
    {
        ref float first = ref MemoryMarshal.GetReference(MemoryMarshal.Cast<Vector3, float>(packed));
        nuint offset = (uint)index * 3;

        // Each four-float load overlaps the following triple at its X component.
        // The last triple has only three valid floats, so it is padded in a register when it ends the span.
        Vector128<float> pixel0 = Unsafe.As<float, Vector128<float>>(ref Unsafe.Add(ref first, offset));
        Vector128<float> pixel1 = Unsafe.As<float, Vector128<float>>(ref Unsafe.Add(ref first, offset + 3));
        Vector128<float> pixel2 = Unsafe.As<float, Vector128<float>>(ref Unsafe.Add(ref first, offset + 6));
        ref float pixel3First = ref Unsafe.Add(ref first, offset + 9);

        // A fourth full load would include the next triple's first float. At the span end, read only the final three floats.
        Vector128<float> pixel3 = index + 4 < packed.Length
            ? Unsafe.As<float, Vector128<float>>(ref pixel3First)
            : Unsafe.As<float, Vector3>(ref pixel3First).AsVector128();

        Transpose4(pixel0, pixel1, pixel2, pixel3, out x, out y, out z, out _);
    }

    /// <summary>
    /// Deinterleaves eight packed three-component values into component registers.
    /// </summary>
    /// <param name="packed">The source triples.</param>
    /// <param name="index">The first of eight source values.</param>
    /// <param name="x">The eight first components.</param>
    /// <param name="y">The eight second components.</param>
    /// <param name="z">The eight third components.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Deinterleave3(ReadOnlySpan<Vector3> packed, int index, out Vector256<float> x, out Vector256<float> y, out Vector256<float> z)
    {
        // Both four-pixel groups are fully loaded before an aliasing caller writes output.
        Deinterleave3(packed, index, out Vector128<float> x0, out Vector128<float> y0, out Vector128<float> z0);
        Deinterleave3(packed, index + 4, out Vector128<float> x1, out Vector128<float> y1, out Vector128<float> z1);
        x = Vector256.Create(x0, x1);
        y = Vector256.Create(y0, y1);
        z = Vector256.Create(z0, z1);
    }

    /// <summary>
    /// Deinterleaves sixteen packed three-component values into component registers.
    /// </summary>
    /// <param name="packed">The source triples.</param>
    /// <param name="index">The first of sixteen source values.</param>
    /// <param name="x">The sixteen first components.</param>
    /// <param name="y">The sixteen second components.</param>
    /// <param name="z">The sixteen third components.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Deinterleave3(ReadOnlySpan<Vector3> packed, int index, out Vector512<float> x, out Vector512<float> y, out Vector512<float> z)
    {
        ref float first = ref MemoryMarshal.GetReference(MemoryMarshal.Cast<Vector3, float>(packed));
        nuint offset = (uint)index * 3;

        // Load all sixteen triples before any caller writes an aliased destination. The three registers
        // start at X0, Y5 and Z10 and cover exactly 48 words, including when this group ends the row.
        Vector512<int> packed0 = Vector512.LoadUnsafe(ref first, offset).AsInt32();
        Vector512<int> packed1 = Vector512.LoadUnsafe(ref first, offset + 16).AsInt32();
        Vector512<int> packed2 = Vector512.LoadUnsafe(ref first, offset + 32).AsInt32();

        // Select each channel from the first two registers. Indices 0..15 select packed0 and 16..31
        // select packed1. Zero selects packed0[0] in positions that the next permute fills from packed2.
        Vector512<int> x01 = Vector512_.PermuteVar16x32x2(packed0, Vector512.Create(0, 3, 6, 9, 12, 15, 18, 21, 24, 27, 30, 0, 0, 0, 0, 0), packed1);
        Vector512<int> y01 = Vector512_.PermuteVar16x32x2(packed0, Vector512.Create(1, 4, 7, 10, 13, 16, 19, 22, 25, 28, 31, 0, 0, 0, 0, 0), packed1);
        Vector512<int> z01 = Vector512_.PermuteVar16x32x2(packed0, Vector512.Create(2, 5, 8, 11, 14, 17, 20, 23, 26, 29, 0, 0, 0, 0, 0, 0), packed1);

        // Retain the first eleven X/Y values and ten Z values, then append their remaining values from packed2.
        // These are only bit permutations; negative values, HDR values and NaN payloads are not changed.
        x = Vector512_.PermuteVar16x32x2(x01, Vector512.Create(0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 17, 20, 23, 26, 29), packed2).AsSingle();
        y = Vector512_.PermuteVar16x32x2(y01, Vector512.Create(0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 18, 21, 24, 27, 30), packed2).AsSingle();
        z = Vector512_.PermuteVar16x32x2(z01, Vector512.Create(0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 16, 19, 22, 25, 28, 31), packed2).AsSingle();
    }

    /// <summary>
    /// Deinterleaves four packed quadruples into four component registers.
    /// </summary>
    /// <param name="packed">The source quadruples.</param>
    /// <param name="index">The first of four source values.</param>
    /// <param name="x">The four first components.</param>
    /// <param name="y">The four second components.</param>
    /// <param name="z">The four third components.</param>
    /// <param name="w">The four fourth components.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Deinterleave4(
        ReadOnlySpan<Vector4> packed,
        int index,
        out Vector128<float> x,
        out Vector128<float> y,
        out Vector128<float> z,
        out Vector128<float> w)
    {
        ref float first = ref MemoryMarshal.GetReference(MemoryMarshal.Cast<Vector4, float>(packed));
        nuint offset = (uint)index * 4;

        // The four rows are complete [X Y Z W] colors. Transposition puts the same component from each color in one register.
        Vector128<float> row0 = Vector128.LoadUnsafe(ref first, offset);
        Vector128<float> row1 = Vector128.LoadUnsafe(ref first, offset + 4);
        Vector128<float> row2 = Vector128.LoadUnsafe(ref first, offset + 8);
        Vector128<float> row3 = Vector128.LoadUnsafe(ref first, offset + 12);
        Transpose4(row0, row1, row2, row3, out x, out y, out z, out w);
    }

    /// <summary>
    /// Deinterleaves eight packed four-component values into component registers.
    /// </summary>
    /// <param name="packed">The source quadruples.</param>
    /// <param name="index">The first of eight source values.</param>
    /// <param name="x">The eight first components.</param>
    /// <param name="y">The eight second components.</param>
    /// <param name="z">The eight third components.</param>
    /// <param name="w">The eight fourth components.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Deinterleave4(
        ReadOnlySpan<Vector4> packed,
        int index,
        out Vector256<float> x,
        out Vector256<float> y,
        out Vector256<float> z,
        out Vector256<float> w)
    {
        ref float first = ref MemoryMarshal.GetReference(MemoryMarshal.Cast<Vector4, float>(packed));
        nuint offset = (uint)index * 4;

        // Four full-width loads cover exactly 8 colors. Read every word before an in-place caller can write output.
        Vector256<float> row0 = Vector256.LoadUnsafe(ref first, offset);
        Vector256<float> row1 = Vector256.LoadUnsafe(ref first, offset + 8);
        Vector256<float> row2 = Vector256.LoadUnsafe(ref first, offset + 16);
        Vector256<float> row3 = Vector256.LoadUnsafe(ref first, offset + 24);
        Transpose4(row0, row1, row2, row3, out x, out y, out z, out w);

        // Each 128-bit group now contains one component from four separated source colors.
        // This permutation restores increasing color indices within every component register.
        Vector256<int> order = Vector256.Create(0, 4, 1, 5, 2, 6, 3, 7);
        x = Vector256.ShuffleNative(x, order);
        y = Vector256.ShuffleNative(y, order);
        z = Vector256.ShuffleNative(z, order);
        w = Vector256.ShuffleNative(w, order);
    }

    /// <summary>
    /// Deinterleaves sixteen packed four-component values into component registers.
    /// </summary>
    /// <param name="packed">The source quadruples.</param>
    /// <param name="index">The first of sixteen source values.</param>
    /// <param name="x">The sixteen first components.</param>
    /// <param name="y">The sixteen second components.</param>
    /// <param name="z">The sixteen third components.</param>
    /// <param name="w">The sixteen fourth components.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Deinterleave4(
        ReadOnlySpan<Vector4> packed,
        int index,
        out Vector512<float> x,
        out Vector512<float> y,
        out Vector512<float> z,
        out Vector512<float> w)
    {
        ref float first = ref MemoryMarshal.GetReference(MemoryMarshal.Cast<Vector4, float>(packed));
        nuint offset = (uint)index * 4;

        // Four full-width loads cover exactly 16 colors. Read every word before an in-place caller can write output.
        Vector512<float> row0 = Vector512.LoadUnsafe(ref first, offset);
        Vector512<float> row1 = Vector512.LoadUnsafe(ref first, offset + 16);
        Vector512<float> row2 = Vector512.LoadUnsafe(ref first, offset + 32);
        Vector512<float> row3 = Vector512.LoadUnsafe(ref first, offset + 48);
        Transpose4(row0, row1, row2, row3, out x, out y, out z, out w);

        // Each 128-bit group now contains one component from four separated source colors.
        // This permutation restores increasing color indices within every component register.
        Vector512<int> order = Vector512.Create(0, 4, 8, 12, 1, 5, 9, 13, 2, 6, 10, 14, 3, 7, 11, 15);
        x = Vector512.ShuffleNative(x, order);
        y = Vector512.ShuffleNative(y, order);
        z = Vector512.ShuffleNative(z, order);
        w = Vector512.ShuffleNative(w, order);
    }

    /// <summary>
    /// Interleaves four values from each of four component registers into consecutive quadruples.
    /// </summary>
    /// <param name="x">The four first components.</param>
    /// <param name="y">The four second components.</param>
    /// <param name="z">The four third components.</param>
    /// <param name="w">The four fourth components.</param>
    /// <param name="destination">The first of sixteen destination float words.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Interleave4(Vector128<float> x, Vector128<float> y, Vector128<float> z, Vector128<float> w, ref float destination)
    {
        // Each transposed column is one complete color. Four unaligned stores write exactly the four-color block.
        Transpose4(x, y, z, w, out Vector128<float> pixel0, out Vector128<float> pixel1, out Vector128<float> pixel2, out Vector128<float> pixel3);
        pixel0.StoreUnsafe(ref destination);
        pixel1.StoreUnsafe(ref destination, 4);
        pixel2.StoreUnsafe(ref destination, 8);
        pixel3.StoreUnsafe(ref destination, 12);
    }

    /// <summary>
    /// Interleaves eight values from four component registers into consecutive quadruples.
    /// </summary>
    /// <param name="x">The eight first components.</param>
    /// <param name="y">The eight second components.</param>
    /// <param name="z">The eight third components.</param>
    /// <param name="w">The eight fourth components.</param>
    /// <param name="destination">The first of thirty-two destination float words.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Interleave4(Vector256<float> x, Vector256<float> y, Vector256<float> z, Vector256<float> w, ref float destination)
    {
        // Group the component lanes for independent four-by-four transposes in each 128-bit group.
        // This reverses Deinterleave4's final permutation; the transpose then produces consecutive complete colors.
        Vector256<int> order = Vector256.Create(0, 2, 4, 6, 1, 3, 5, 7);
        x = Vector256.ShuffleNative(x, order);
        y = Vector256.ShuffleNative(y, order);
        z = Vector256.ShuffleNative(z, order);
        w = Vector256.ShuffleNative(w, order);
        Transpose4(x, y, z, w, out Vector256<float> row0, out Vector256<float> row1, out Vector256<float> row2, out Vector256<float> row3);

        // Four stores cover exactly 32 float words. No padding is read or written at the end of a row.
        row0.StoreUnsafe(ref destination);
        row1.StoreUnsafe(ref destination, 8U);
        row2.StoreUnsafe(ref destination, 16U);
        row3.StoreUnsafe(ref destination, 24U);
    }

    /// <summary>
    /// Interleaves sixteen values from four component registers into consecutive quadruples.
    /// </summary>
    /// <param name="x">The sixteen first components.</param>
    /// <param name="y">The sixteen second components.</param>
    /// <param name="z">The sixteen third components.</param>
    /// <param name="w">The sixteen fourth components.</param>
    /// <param name="destination">The first of sixty-four destination float words.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Interleave4(Vector512<float> x, Vector512<float> y, Vector512<float> z, Vector512<float> w, ref float destination)
    {
        // Group the component lanes for independent four-by-four transposes in each 128-bit group.
        // This reverses Deinterleave4's final permutation; the transpose then produces consecutive complete colors.
        Vector512<int> order = Vector512.Create(0, 4, 8, 12, 1, 5, 9, 13, 2, 6, 10, 14, 3, 7, 11, 15);
        x = Vector512.ShuffleNative(x, order);
        y = Vector512.ShuffleNative(y, order);
        z = Vector512.ShuffleNative(z, order);
        w = Vector512.ShuffleNative(w, order);
        Transpose4(x, y, z, w, out Vector512<float> row0, out Vector512<float> row1, out Vector512<float> row2, out Vector512<float> row3);

        // Four stores cover exactly 64 float words. No padding is read or written at the end of a row.
        row0.StoreUnsafe(ref destination);
        row1.StoreUnsafe(ref destination, 16U);
        row2.StoreUnsafe(ref destination, 32U);
        row3.StoreUnsafe(ref destination, 48U);
    }

    /// <summary>
    /// Transforms four triples by a matrix using the same multiplication order as <see cref="Vector3.Transform(Vector3, Matrix4x4)"/>.
    /// </summary>
    /// <param name="matrix">The transformation matrix.</param>
    /// <param name="x">The four first components.</param>
    /// <param name="y">The four second components.</param>
    /// <param name="z">The four third components.</param>
    /// <param name="resultX">The transformed first components.</param>
    /// <param name="resultY">The transformed second components.</param>
    /// <param name="resultZ">The transformed third components.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Transform3(
        in Matrix4x4 matrix,
        Vector128<float> x,
        Vector128<float> y,
        Vector128<float> z,
        out Vector128<float> resultX,
        out Vector128<float> resultY,
        out Vector128<float> resultZ)
    {
        // Vector3.Transform starts with X, multiply-adds Y and Z in that order, then adds the translation row.
        resultX = Vector128.MultiplyAddEstimate(y, Vector128.Create(matrix.M21), x * Vector128.Create(matrix.M11));
        resultX = Vector128.MultiplyAddEstimate(z, Vector128.Create(matrix.M31), resultX) + Vector128.Create(matrix.M41);

        resultY = Vector128.MultiplyAddEstimate(y, Vector128.Create(matrix.M22), x * Vector128.Create(matrix.M12));
        resultY = Vector128.MultiplyAddEstimate(z, Vector128.Create(matrix.M32), resultY) + Vector128.Create(matrix.M42);

        resultZ = Vector128.MultiplyAddEstimate(y, Vector128.Create(matrix.M23), x * Vector128.Create(matrix.M13));
        resultZ = Vector128.MultiplyAddEstimate(z, Vector128.Create(matrix.M33), resultZ) + Vector128.Create(matrix.M43);
    }

    /// <summary>
    /// Transforms eight triples by a matrix using the multiplication order of <see cref="Vector3.Transform(Vector3, Matrix4x4)"/>.
    /// </summary>
    /// <param name="matrix">The transformation matrix.</param>
    /// <param name="x">The eight first components.</param>
    /// <param name="y">The eight second components.</param>
    /// <param name="z">The eight third components.</param>
    /// <param name="resultX">The transformed first components.</param>
    /// <param name="resultY">The transformed second components.</param>
    /// <param name="resultZ">The transformed third components.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Transform3(
        in Matrix4x4 matrix,
        Vector256<float> x,
        Vector256<float> y,
        Vector256<float> z,
        out Vector256<float> resultX,
        out Vector256<float> resultY,
        out Vector256<float> resultZ)
    {
        // Keep X, Y, Z, then translation as separate stages at native 256-bit width.
        resultX = Vector256.MultiplyAddEstimate(y, Vector256.Create(matrix.M21), x * Vector256.Create(matrix.M11));
        resultX = Vector256.MultiplyAddEstimate(z, Vector256.Create(matrix.M31), resultX) + Vector256.Create(matrix.M41);

        resultY = Vector256.MultiplyAddEstimate(y, Vector256.Create(matrix.M22), x * Vector256.Create(matrix.M12));
        resultY = Vector256.MultiplyAddEstimate(z, Vector256.Create(matrix.M32), resultY) + Vector256.Create(matrix.M42);

        resultZ = Vector256.MultiplyAddEstimate(y, Vector256.Create(matrix.M23), x * Vector256.Create(matrix.M13));
        resultZ = Vector256.MultiplyAddEstimate(z, Vector256.Create(matrix.M33), resultZ) + Vector256.Create(matrix.M43);
    }

    /// <summary>
    /// Transforms sixteen triples by a matrix using the multiplication order of <see cref="Vector3.Transform(Vector3, Matrix4x4)"/>.
    /// </summary>
    /// <param name="matrix">The transformation matrix.</param>
    /// <param name="x">The sixteen first components.</param>
    /// <param name="y">The sixteen second components.</param>
    /// <param name="z">The sixteen third components.</param>
    /// <param name="resultX">The transformed first components.</param>
    /// <param name="resultY">The transformed second components.</param>
    /// <param name="resultZ">The transformed third components.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Transform3(
        in Matrix4x4 matrix,
        Vector512<float> x,
        Vector512<float> y,
        Vector512<float> z,
        out Vector512<float> resultX,
        out Vector512<float> resultY,
        out Vector512<float> resultZ)
    {
        // Keep X, Y, Z, then translation as separate stages at native 512-bit width.
        resultX = Vector512.MultiplyAddEstimate(y, Vector512.Create(matrix.M21), x * Vector512.Create(matrix.M11));
        resultX = Vector512.MultiplyAddEstimate(z, Vector512.Create(matrix.M31), resultX) + Vector512.Create(matrix.M41);

        resultY = Vector512.MultiplyAddEstimate(y, Vector512.Create(matrix.M22), x * Vector512.Create(matrix.M12));
        resultY = Vector512.MultiplyAddEstimate(z, Vector512.Create(matrix.M32), resultY) + Vector512.Create(matrix.M42);

        resultZ = Vector512.MultiplyAddEstimate(y, Vector512.Create(matrix.M23), x * Vector512.Create(matrix.M13));
        resultZ = Vector512.MultiplyAddEstimate(z, Vector512.Create(matrix.M33), resultZ) + Vector512.Create(matrix.M43);
    }

    /// <summary>
    /// Normalizes three planar component lanes and interleaves them into packed XYZ values.
    /// </summary>
    /// <param name="xLane">The planar X components.</param>
    /// <param name="yLane">The planar Y components.</param>
    /// <param name="zLane">The planar Z components.</param>
    /// <param name="packed">The destination ordered as consecutive XYZ triples.</param>
    /// <param name="scale">The normalization factor applied to every component.</param>
    public static void PackedNormalizeInterleave3(
        ReadOnlySpan<float> xLane,
        ReadOnlySpan<float> yLane,
        ReadOnlySpan<float> zLane,
        Span<float> packed,
        float scale)
    {
        DebugGuard.IsTrue(packed.Length % 3 == 0, "Packed length must be divisible by 3.");
        DebugGuard.IsTrue(yLane.Length == xLane.Length, nameof(yLane), "Channels must be of same size!");
        DebugGuard.IsTrue(zLane.Length == xLane.Length, nameof(zLane), "Channels must be of same size!");
        DebugGuard.MustBeLessThanOrEqualTo(packed.Length / 3, xLane.Length, nameof(packed));

        ref float xLaneRef = ref MemoryMarshal.GetReference(xLane);
        ref float yLaneRef = ref MemoryMarshal.GetReference(yLane);
        ref float zLaneRef = ref MemoryMarshal.GetReference(zLane);
        ref float packedRef = ref MemoryMarshal.GetReference(packed);
        int i = 0;

        if (Vector512.IsHardwareAccelerated)
        {
            Vector512<float> scaleVector = Vector512.Create(scale);
            nuint count = xLane.Length.Vector512Count<float>();

            for (nuint block = 0; block < count; block++, i += Vector512<float>.Count)
            {
                // Each register holds one channel across sixteen colors. Normalize before the bit-only packing step.
                nuint offset = (uint)i;
                Vector512<float> x = Vector512.LoadUnsafe(ref xLaneRef, offset) * scaleVector;
                Vector512<float> y = Vector512.LoadUnsafe(ref yLaneRef, offset) * scaleVector;
                Vector512<float> z = Vector512.LoadUnsafe(ref zLaneRef, offset) * scaleVector;
                Interleave3(x, y, z, ref Unsafe.Add(ref packedRef, offset * 3));
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            Vector256<float> scaleVector = Vector256.Create(scale);
            nuint count = (xLane.Length - i).Vector256Count<float>();

            for (nuint block = 0; block < count; block++, i += Vector256<float>.Count)
            {
                // The same multiplication handles remaining groups of eight without changing rounding.
                nuint offset = (uint)i;
                Vector256<float> x = Vector256.LoadUnsafe(ref xLaneRef, offset) * scaleVector;
                Vector256<float> y = Vector256.LoadUnsafe(ref yLaneRef, offset) * scaleVector;
                Vector256<float> z = Vector256.LoadUnsafe(ref zLaneRef, offset) * scaleVector;
                Interleave3(x, y, z, ref Unsafe.Add(ref packedRef, offset * 3));
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            Vector128<float> scaleVector = Vector128.Create(scale);
            nuint count = (xLane.Length - i).Vector128Count<float>();

            for (nuint block = 0; block < count; block++, i += Vector128<float>.Count)
            {
                // Normalize each plane in register lanes before interleaving the twelve float words.
                nuint offset = (uint)i;
                Vector128<float> x = Vector128.LoadUnsafe(ref xLaneRef, offset) * scaleVector;
                Vector128<float> y = Vector128.LoadUnsafe(ref yLaneRef, offset) * scaleVector;
                Vector128<float> z = Vector128.LoadUnsafe(ref zLaneRef, offset) * scaleVector;
                Interleave3(x, y, z, ref Unsafe.Add(ref packedRef, offset * 3));
            }
        }

        // Fewer than four pixels remain after SIMD, or every pixel reaches this path
        // when the runtime cannot accelerate the cross-vector transpose.
        for (; i < xLane.Length; i++)
        {
            nuint sourceOffset = (uint)i;
            nuint packedOffset = sourceOffset * 3;
            Unsafe.Add(ref packedRef, packedOffset) = Unsafe.Add(ref xLaneRef, sourceOffset) * scale;
            Unsafe.Add(ref packedRef, packedOffset + 1) = Unsafe.Add(ref yLaneRef, sourceOffset) * scale;
            Unsafe.Add(ref packedRef, packedOffset + 2) = Unsafe.Add(ref zLaneRef, sourceOffset) * scale;
        }
    }

    /// <summary>
    /// Deinterleaves packed XYZ values into three planar component lanes.
    /// </summary>
    /// <param name="packed">The source ordered as consecutive XYZ triples.</param>
    /// <param name="xLane">The destination X components.</param>
    /// <param name="yLane">The destination Y components.</param>
    /// <param name="zLane">The destination Z components.</param>
    public static void UnpackDeinterleave3(ReadOnlySpan<Vector3> packed, Span<float> xLane, Span<float> yLane, Span<float> zLane)
    {
        DebugGuard.IsTrue(packed.Length == xLane.Length, nameof(packed), "Channels must be of same size!");
        DebugGuard.IsTrue(yLane.Length == xLane.Length, nameof(yLane), "Channels must be of same size!");
        DebugGuard.IsTrue(zLane.Length == xLane.Length, nameof(zLane), "Channels must be of same size!");

        ref float packedRef = ref MemoryMarshal.GetReference(MemoryMarshal.Cast<Vector3, float>(packed));
        ref float xLaneRef = ref MemoryMarshal.GetReference(xLane);
        ref float yLaneRef = ref MemoryMarshal.GetReference(yLane);
        ref float zLaneRef = ref MemoryMarshal.GetReference(zLane);
        int i = 0;

        if (Vector512.IsHardwareAccelerated)
        {
            nuint count = packed.Length.Vector512Count<float>();

            for (nuint block = 0; block < count; block++, i += Vector512<float>.Count)
            {
                // Load all sixteen triples before storing their separate component registers.
                Deinterleave3(packed, i, out Vector512<float> x, out Vector512<float> y, out Vector512<float> z);
                nuint offset = (uint)i;
                x.StoreUnsafe(ref xLaneRef, offset);
                y.StoreUnsafe(ref yLaneRef, offset);
                z.StoreUnsafe(ref zLaneRef, offset);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            nuint count = (packed.Length - i).Vector256Count<float>();

            for (nuint block = 0; block < count; block++, i += Vector256<float>.Count)
            {
                // Each remaining eight-color group uses the same component order as the widest path.
                Deinterleave3(packed, i, out Vector256<float> x, out Vector256<float> y, out Vector256<float> z);
                nuint offset = (uint)i;
                x.StoreUnsafe(ref xLaneRef, offset);
                y.StoreUnsafe(ref yLaneRef, offset);
                z.StoreUnsafe(ref zLaneRef, offset);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            nuint count = (packed.Length - i).Vector128Count<float>();

            for (nuint block = 0; block < count; block++, i += Vector128<float>.Count)
            {
                Deinterleave3(packed, i, out Vector128<float> x, out Vector128<float> y, out Vector128<float> z);
                nuint offset = (uint)i;
                x.StoreUnsafe(ref xLaneRef, offset);
                y.StoreUnsafe(ref yLaneRef, offset);
                z.StoreUnsafe(ref zLaneRef, offset);
            }
        }

        // The scalar remainder preserves the original scatter behavior for zero to
        // three pixels and provides the complete fallback on unsupported hardware.
        for (; i < packed.Length; i++)
        {
            nuint packedOffset = (uint)i * 3;
            Unsafe.Add(ref xLaneRef, i) = Unsafe.Add(ref packedRef, packedOffset);
            Unsafe.Add(ref yLaneRef, i) = Unsafe.Add(ref packedRef, packedOffset + 1);
            Unsafe.Add(ref zLaneRef, i) = Unsafe.Add(ref packedRef, packedOffset + 2);
        }
    }

    /// <summary>
    /// Normalizes four planar component lanes and interleaves them into packed XYZW values.
    /// </summary>
    /// <param name="xLane">The planar X components.</param>
    /// <param name="yLane">The planar Y components.</param>
    /// <param name="zLane">The planar Z components.</param>
    /// <param name="wLane">The planar W components.</param>
    /// <param name="packed">The destination ordered as consecutive XYZW groups.</param>
    /// <param name="maxValue">The maximum component value used to normalize each component.</param>
    public static void PackedNormalizeInterleave4(
        ReadOnlySpan<float> xLane,
        ReadOnlySpan<float> yLane,
        ReadOnlySpan<float> zLane,
        ReadOnlySpan<float> wLane,
        Span<float> packed,
        float maxValue)
    {
        DebugGuard.IsTrue(packed.Length % 4 == 0, "Packed length must be divisible by 4.");
        DebugGuard.IsTrue(yLane.Length == xLane.Length, nameof(yLane), "Channels must be of same size!");
        DebugGuard.IsTrue(zLane.Length == xLane.Length, nameof(zLane), "Channels must be of same size!");
        DebugGuard.IsTrue(wLane.Length == xLane.Length, nameof(wLane), "Channels must be of same size!");
        DebugGuard.MustBeLessThanOrEqualTo(packed.Length / 4, xLane.Length, nameof(packed));

        float scale = 1F / maxValue;
        ref float xLaneRef = ref MemoryMarshal.GetReference(xLane);
        ref float yLaneRef = ref MemoryMarshal.GetReference(yLane);
        ref float zLaneRef = ref MemoryMarshal.GetReference(zLane);
        ref float wLaneRef = ref MemoryMarshal.GetReference(wLane);
        ref float packedRef = ref MemoryMarshal.GetReference(packed);
        int i = 0;

        if (Vector128.IsHardwareAccelerated)
        {
            Vector128<float> scaleVector = Vector128.Create(scale);
            int oneVectorFromEnd = xLane.Length - Vector128<float>.Count;

            for (; i <= oneVectorFromEnd; i += Vector128<float>.Count)
            {
                // Four planar vectors form the rows of a 4x4 matrix. Transposition
                // converts them into four complete [Xn Yn Zn Wn] pixel vectors, so
                // normalization and interleaving require only four loads, four
                // multiplies, the register transpose, and four contiguous stores.
                Vector128<float> x = Unsafe.As<float, Vector128<float>>(ref Unsafe.Add(ref xLaneRef, i)) * scaleVector;
                Vector128<float> y = Unsafe.As<float, Vector128<float>>(ref Unsafe.Add(ref yLaneRef, i)) * scaleVector;
                Vector128<float> z = Unsafe.As<float, Vector128<float>>(ref Unsafe.Add(ref zLaneRef, i)) * scaleVector;
                Vector128<float> w = Unsafe.As<float, Vector128<float>>(ref Unsafe.Add(ref wLaneRef, i)) * scaleVector;

                Transpose4(x, y, z, w, out Vector128<float> pixel0, out Vector128<float> pixel1, out Vector128<float> pixel2, out Vector128<float> pixel3);

                ref Vector128<float> destination = ref Unsafe.As<float, Vector128<float>>(ref Unsafe.Add(ref packedRef, (uint)i * 4));

                destination = pixel0;
                Unsafe.Add(ref destination, 1) = pixel1;
                Unsafe.Add(ref destination, 2) = pixel2;
                Unsafe.Add(ref destination, 3) = pixel3;
            }
        }

        // Process the zero-to-three trailing pixels with the same normalization
        // and component order as the vector transpose.
        for (; i < xLane.Length; i++)
        {
            nuint sourceOffset = (uint)i;
            nuint packedOffset = sourceOffset * 4;
            Unsafe.Add(ref packedRef, packedOffset) = Unsafe.Add(ref xLaneRef, sourceOffset) * scale;
            Unsafe.Add(ref packedRef, packedOffset + 1) = Unsafe.Add(ref yLaneRef, sourceOffset) * scale;
            Unsafe.Add(ref packedRef, packedOffset + 2) = Unsafe.Add(ref zLaneRef, sourceOffset) * scale;
            Unsafe.Add(ref packedRef, packedOffset + 3) = Unsafe.Add(ref wLaneRef, sourceOffset) * scale;
        }
    }

    /// <summary>
    /// Inverts and normalizes four planar component lanes before interleaving them into packed XYZW values.
    /// </summary>
    /// <param name="xLane">The inverted planar X components.</param>
    /// <param name="yLane">The inverted planar Y components.</param>
    /// <param name="zLane">The inverted planar Z components.</param>
    /// <param name="wLane">The inverted planar W components.</param>
    /// <param name="packed">The destination ordered as consecutive conventional XYZW groups.</param>
    /// <param name="maxValue">The maximum component value used for inversion and normalization.</param>
    public static void PackedInvertNormalizeInterleave4(
        ReadOnlySpan<float> xLane,
        ReadOnlySpan<float> yLane,
        ReadOnlySpan<float> zLane,
        ReadOnlySpan<float> wLane,
        Span<float> packed,
        float maxValue)
    {
        DebugGuard.IsTrue(packed.Length % 4 == 0, "Packed length must be divisible by 4.");
        DebugGuard.IsTrue(yLane.Length == xLane.Length, nameof(yLane), "Channels must be of same size!");
        DebugGuard.IsTrue(zLane.Length == xLane.Length, nameof(zLane), "Channels must be of same size!");
        DebugGuard.IsTrue(wLane.Length == xLane.Length, nameof(wLane), "Channels must be of same size!");
        DebugGuard.MustBeLessThanOrEqualTo(packed.Length / 4, xLane.Length, nameof(packed));

        float scale = 1F / maxValue;
        ref float xLaneRef = ref MemoryMarshal.GetReference(xLane);
        ref float yLaneRef = ref MemoryMarshal.GetReference(yLane);
        ref float zLaneRef = ref MemoryMarshal.GetReference(zLane);
        ref float wLaneRef = ref MemoryMarshal.GetReference(wLane);
        ref float packedRef = ref MemoryMarshal.GetReference(packed);
        int i = 0;

        if (Vector128.IsHardwareAccelerated)
        {
            Vector128<float> maximumVector = Vector128.Create(maxValue);
            Vector128<float> scaleVector = Vector128.Create(scale);
            int oneVectorFromEnd = xLane.Length - Vector128<float>.Count;

            for (; i <= oneVectorFromEnd; i += Vector128<float>.Count)
            {
                // Adobe JPEG stores all four components inverted in the sample
                // domain. Reflecting and normalizing the planar vectors before the
                // transpose keeps both arithmetic operations lane-wise and leaves
                // the transpose responsible only for the planar-to-packed layout.
                Vector128<float> x = (maximumVector - Unsafe.As<float, Vector128<float>>(ref Unsafe.Add(ref xLaneRef, i))) * scaleVector;
                Vector128<float> y = (maximumVector - Unsafe.As<float, Vector128<float>>(ref Unsafe.Add(ref yLaneRef, i))) * scaleVector;
                Vector128<float> z = (maximumVector - Unsafe.As<float, Vector128<float>>(ref Unsafe.Add(ref zLaneRef, i))) * scaleVector;
                Vector128<float> w = (maximumVector - Unsafe.As<float, Vector128<float>>(ref Unsafe.Add(ref wLaneRef, i))) * scaleVector;

                Transpose4(x, y, z, w, out Vector128<float> pixel0, out Vector128<float> pixel1, out Vector128<float> pixel2, out Vector128<float> pixel3);

                ref Vector128<float> destination = ref Unsafe.As<float, Vector128<float>>(ref Unsafe.Add(ref packedRef, (uint)i * 4));

                destination = pixel0;
                Unsafe.Add(ref destination, 1) = pixel1;
                Unsafe.Add(ref destination, 2) = pixel2;
                Unsafe.Add(ref destination, 3) = pixel3;
            }
        }

        // Preserve the original operation order for the zero-to-three trailing pixels:
        // subtract in the sample domain, then multiply by the reciprocal maximum.
        for (; i < xLane.Length; i++)
        {
            nuint sourceOffset = (uint)i;
            nuint packedOffset = sourceOffset * 4;
            Unsafe.Add(ref packedRef, packedOffset) = (maxValue - Unsafe.Add(ref xLaneRef, sourceOffset)) * scale;
            Unsafe.Add(ref packedRef, packedOffset + 1) = (maxValue - Unsafe.Add(ref yLaneRef, sourceOffset)) * scale;
            Unsafe.Add(ref packedRef, packedOffset + 2) = (maxValue - Unsafe.Add(ref zLaneRef, sourceOffset)) * scale;
            Unsafe.Add(ref packedRef, packedOffset + 3) = (maxValue - Unsafe.Add(ref wLaneRef, sourceOffset)) * scale;
        }
    }

    /// <summary>
    /// Transposes four four-lane rows into four four-lane columns.
    /// </summary>
    /// <param name="row0">The first matrix row.</param>
    /// <param name="row1">The second matrix row.</param>
    /// <param name="row2">The third matrix row.</param>
    /// <param name="row3">The fourth matrix row.</param>
    /// <param name="column0">The first matrix column.</param>
    /// <param name="column1">The second matrix column.</param>
    /// <param name="column2">The third matrix column.</param>
    /// <param name="column3">The fourth matrix column.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Transpose4(
        Vector128<float> row0,
        Vector128<float> row1,
        Vector128<float> row2,
        Vector128<float> row3,
        out Vector128<float> column0,
        out Vector128<float> column1,
        out Vector128<float> column2,
        out Vector128<float> column3)
    {
        // The first unpack interleaves adjacent 32-bit lanes from rows 0/1 and 2/3:
        //   row01Low = [r0c0 r1c0 r0c1 r1c1]
        //   row23Low = [r2c0 r3c0 r2c1 r3c1]
        // A second unpack treats each adjacent pair as one 64-bit lane and combines
        // the row01 and row23 pairs into complete columns. The integer views only
        // expose the cross-platform unpack helpers; every floating-point bit is preserved.
        Vector128<int> row01Low = Vector128_.UnpackLow(row0.AsInt32(), row1.AsInt32());
        Vector128<int> row01High = Vector128_.UnpackHigh(row0.AsInt32(), row1.AsInt32());
        Vector128<int> row23Low = Vector128_.UnpackLow(row2.AsInt32(), row3.AsInt32());
        Vector128<int> row23High = Vector128_.UnpackHigh(row2.AsInt32(), row3.AsInt32());

        column0 = Vector128_.UnpackLow(row01Low.AsInt64(), row23Low.AsInt64()).AsSingle();
        column1 = Vector128_.UnpackHigh(row01Low.AsInt64(), row23Low.AsInt64()).AsSingle();
        column2 = Vector128_.UnpackLow(row01High.AsInt64(), row23High.AsInt64()).AsSingle();
        column3 = Vector128_.UnpackHigh(row01High.AsInt64(), row23High.AsInt64()).AsSingle();
    }

    /// <summary>
    /// Transposes four-by-four matrices independently within each 128-bit group.
    /// </summary>
    /// <param name="row0">The first rows of the matrices.</param>
    /// <param name="row1">The second rows of the matrices.</param>
    /// <param name="row2">The third rows of the matrices.</param>
    /// <param name="row3">The fourth rows of the matrices.</param>
    /// <param name="column0">The first columns of the matrices.</param>
    /// <param name="column1">The second columns of the matrices.</param>
    /// <param name="column2">The third columns of the matrices.</param>
    /// <param name="column3">The fourth columns of the matrices.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Transpose4(
        Vector256<float> row0,
        Vector256<float> row1,
        Vector256<float> row2,
        Vector256<float> row3,
        out Vector256<float> column0,
        out Vector256<float> column1,
        out Vector256<float> column2,
        out Vector256<float> column3)
    {
        // Match the 128-bit transpose: first pair adjacent 32-bit words, then pair the resulting 64-bit groups.
        // Unpack keeps 128-bit groups separate. Callers permute those groups when they need consecutive color indices.
        Vector256<int> row01Low = Vector256_.UnpackLow(row0.AsInt32(), row1.AsInt32());
        Vector256<int> row01High = Vector256_.UnpackHigh(row0.AsInt32(), row1.AsInt32());
        Vector256<int> row23Low = Vector256_.UnpackLow(row2.AsInt32(), row3.AsInt32());
        Vector256<int> row23High = Vector256_.UnpackHigh(row2.AsInt32(), row3.AsInt32());

        column0 = Vector256_.UnpackLow(row01Low.AsInt64(), row23Low.AsInt64()).AsSingle();
        column1 = Vector256_.UnpackHigh(row01Low.AsInt64(), row23Low.AsInt64()).AsSingle();
        column2 = Vector256_.UnpackLow(row01High.AsInt64(), row23High.AsInt64()).AsSingle();
        column3 = Vector256_.UnpackHigh(row01High.AsInt64(), row23High.AsInt64()).AsSingle();
    }

    /// <summary>
    /// Transposes four-by-four matrices independently within each 128-bit group.
    /// </summary>
    /// <param name="row0">The first rows of the matrices.</param>
    /// <param name="row1">The second rows of the matrices.</param>
    /// <param name="row2">The third rows of the matrices.</param>
    /// <param name="row3">The fourth rows of the matrices.</param>
    /// <param name="column0">The first columns of the matrices.</param>
    /// <param name="column1">The second columns of the matrices.</param>
    /// <param name="column2">The third columns of the matrices.</param>
    /// <param name="column3">The fourth columns of the matrices.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Transpose4(
        Vector512<float> row0,
        Vector512<float> row1,
        Vector512<float> row2,
        Vector512<float> row3,
        out Vector512<float> column0,
        out Vector512<float> column1,
        out Vector512<float> column2,
        out Vector512<float> column3)
    {
        // Match the 128-bit transpose: first pair adjacent 32-bit words, then pair the resulting 64-bit groups.
        // Unpack keeps 128-bit groups separate. Callers permute those groups when they need consecutive color indices.
        Vector512<int> row01Low = Vector512_.UnpackLow(row0.AsInt32(), row1.AsInt32());
        Vector512<int> row01High = Vector512_.UnpackHigh(row0.AsInt32(), row1.AsInt32());
        Vector512<int> row23Low = Vector512_.UnpackLow(row2.AsInt32(), row3.AsInt32());
        Vector512<int> row23High = Vector512_.UnpackHigh(row2.AsInt32(), row3.AsInt32());

        column0 = Vector512_.UnpackLow(row01Low.AsInt64(), row23Low.AsInt64()).AsSingle();
        column1 = Vector512_.UnpackHigh(row01Low.AsInt64(), row23Low.AsInt64()).AsSingle();
        column2 = Vector512_.UnpackLow(row01High.AsInt64(), row23High.AsInt64()).AsSingle();
        column3 = Vector512_.UnpackHigh(row01High.AsInt64(), row23High.AsInt64()).AsSingle();
    }
}
