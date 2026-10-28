// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.ColorProfiles;

/// <summary>
/// Implementation of the Von Kries chromatic adaptation model.
/// </summary>
/// <remarks>
/// Transformation described here:
/// http://www.brucelindbloom.com/index.html?Eqn_ChromAdapt.html
/// </remarks>
public static class VonKriesChromaticAdaptation
{
    /// <summary>
    /// Performs a linear transformation of a source color in to the destination color.
    /// </summary>
    /// <remarks>Doesn't crop the resulting color space coordinates (e.g. allows negative values for XYZ coordinates).</remarks>
    /// <param name="source">The source color.</param>
    /// <param name="whitePoints">The conversion white points.</param>
    /// <param name="matrix">The chromatic adaptation matrix.</param>
    /// <returns>The <see cref="CieXyz"/></returns>
    public static CieXyz Transform(in CieXyz source, (CieXyz From, CieXyz To) whitePoints, Matrix4x4 matrix)
    {
        Matrix4x4.Invert(matrix, out Matrix4x4 inverseMatrix);
        return Transform(in source, whitePoints, matrix, inverseMatrix);
    }

    /// <summary>
    /// Performs a bulk linear transformation of a source color in to the destination color.
    /// </summary>
    /// <remarks>Doesn't crop the resulting color space coordinates (e. g. allows negative values for XYZ coordinates).</remarks>
    /// <param name="source">The span to the source colors.</param>
    /// <param name="destination">The span to the destination colors.</param>
    /// <param name="whitePoints">The conversion white points.</param>
    /// <param name="matrix">The chromatic adaptation matrix.</param>
    public static void Transform(
        ReadOnlySpan<CieXyz> source,
        Span<CieXyz> destination,
        (CieXyz From, CieXyz To) whitePoints,
        Matrix4x4 matrix)
    {
        Matrix4x4.Invert(matrix, out Matrix4x4 inverseMatrix);
        Transform(source, destination, whitePoints, matrix, inverseMatrix);
    }

    /// <summary>
    /// Performs a linear transformation of a source color in to the destination color using a
    /// precomputed inverse of <paramref name="matrix"/>.
    /// </summary>
    /// <remarks>
    /// For callers that already hold the inverse, e.g. <see cref="ColorConversionOptions.InverseAdaptationMatrix"/>,
    /// to avoid repeating <see cref="Matrix4x4.Invert"/> on every call.
    /// </remarks>
    /// <param name="source">The source color.</param>
    /// <param name="whitePoints">The conversion white points.</param>
    /// <param name="matrix">The chromatic adaptation matrix.</param>
    /// <param name="inverseMatrix">The inverse of <paramref name="matrix"/>.</param>
    /// <returns>The <see cref="CieXyz"/></returns>
    public static CieXyz Transform(
        in CieXyz source,
        (CieXyz From, CieXyz To) whitePoints,
        Matrix4x4 matrix,
        Matrix4x4 inverseMatrix)
    {
        CieXyz from = whitePoints.From;
        CieXyz to = whitePoints.To;

        if (from.Equals(to))
        {
            return new CieXyz(source.X, source.Y, source.Z);
        }

        Vector3 sourceColorLms = Vector3.Transform(source.AsVector3Unsafe(), matrix);
        Vector3 sourceWhitePointLms = Vector3.Transform(from.AsVector3Unsafe(), matrix);
        Vector3 targetWhitePointLms = Vector3.Transform(to.AsVector3Unsafe(), matrix);

        Vector3 vector = targetWhitePointLms / sourceWhitePointLms;
        Vector3 targetColorLms = Vector3.Multiply(vector, sourceColorLms);

        return new CieXyz(Vector3.Transform(targetColorLms, inverseMatrix));
    }

    /// <summary>
    /// Performs a bulk linear transformation of a source color in to the destination color using a
    /// precomputed inverse of <paramref name="matrix"/>.
    /// </summary>
    /// <remarks>
    /// For callers that already hold the inverse, e.g. <see cref="ColorConversionOptions.InverseAdaptationMatrix"/>,
    /// to avoid repeating <see cref="Matrix4x4.Invert"/> on every call.
    /// </remarks>
    /// <param name="source">The span to the source colors.</param>
    /// <param name="destination">The span to the destination colors.</param>
    /// <param name="whitePoints">The conversion white points.</param>
    /// <param name="matrix">The chromatic adaptation matrix.</param>
    /// <param name="inverseMatrix">The inverse of <paramref name="matrix"/>.</param>
    public static void Transform(
        ReadOnlySpan<CieXyz> source,
        Span<CieXyz> destination,
        (CieXyz From, CieXyz To) whitePoints,
        Matrix4x4 matrix,
        Matrix4x4 inverseMatrix)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
        int count = source.Length;

        CieXyz from = whitePoints.From;
        CieXyz to = whitePoints.To;

        if (from.Equals(to))
        {
            source.CopyTo(destination[..count]);
            return;
        }

        ref CieXyz sourceBase = ref MemoryMarshal.GetReference(source);
        ref CieXyz destinationBase = ref MemoryMarshal.GetReference(destination);

        Vector3 sourceWhitePointLms = Vector3.Transform(from.AsVector3Unsafe(), matrix);
        Vector3 targetWhitePointLms = Vector3.Transform(to.AsVector3Unsafe(), matrix);

        Vector3 vector = targetWhitePointLms / sourceWhitePointLms;

        int i = 0;
        ReadOnlySpan<Vector3> packed = MemoryMarshal.Cast<CieXyz, Vector3>(source);
        ReadOnlySpan<float> components = MemoryMarshal.Cast<CieXyz, float>(source);

        if (Vector512.IsHardwareAccelerated)
        {
            Vector512<float> ratioX = Vector512.Create(vector.X);
            Vector512<float> ratioY = Vector512.Create(vector.Y);
            Vector512<float> ratioZ = Vector512.Create(vector.Z);
            nuint vectorCount = components.Vector512Count<float>() / 3;

            for (nuint vectorIndex = 0; vectorIndex < vectorCount; vectorIndex++, i += Vector512<float>.Count)
            {
                // Each complete group applies the configured adaptation matrix, LMS white-point scaling, then its inverse without clipping.
                SimdUtils.Deinterleave3(packed, i, out Vector512<float> x, out Vector512<float> y, out Vector512<float> z);
                SimdUtils.Transform3(in matrix, x, y, z, out Vector512<float> l, out Vector512<float> m, out Vector512<float> s);
                l *= ratioX;
                m *= ratioY;
                s *= ratioZ;
                SimdUtils.Transform3(in inverseMatrix, l, m, s, out Vector512<float> adaptedX, out Vector512<float> adaptedY, out Vector512<float> adaptedZ);
                SimdUtils.Interleave3(adaptedX, adaptedY, adaptedZ, ref Unsafe.As<CieXyz, float>(ref Unsafe.Add(ref destinationBase, i)));
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            Vector256<float> ratioX = Vector256.Create(vector.X);
            Vector256<float> ratioY = Vector256.Create(vector.Y);
            Vector256<float> ratioZ = Vector256.Create(vector.Z);
            nuint vectorCount = components[(i * 3)..].Vector256Count<float>() / 3;

            for (nuint vectorIndex = 0; vectorIndex < vectorCount; vectorIndex++, i += Vector256<float>.Count)
            {
                // Remaining eight-pixel groups preserve the same separate PCS stages and source order.
                SimdUtils.Deinterleave3(packed, i, out Vector256<float> x, out Vector256<float> y, out Vector256<float> z);
                SimdUtils.Transform3(in matrix, x, y, z, out Vector256<float> l, out Vector256<float> m, out Vector256<float> s);
                l *= ratioX;
                m *= ratioY;
                s *= ratioZ;
                SimdUtils.Transform3(in inverseMatrix, l, m, s, out Vector256<float> adaptedX, out Vector256<float> adaptedY, out Vector256<float> adaptedZ);
                SimdUtils.Interleave3(adaptedX, adaptedY, adaptedZ, ref Unsafe.As<CieXyz, float>(ref Unsafe.Add(ref destinationBase, i)));
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            Vector128<float> ratioX = Vector128.Create(vector.X);
            Vector128<float> ratioY = Vector128.Create(vector.Y);
            Vector128<float> ratioZ = Vector128.Create(vector.Z);
            nuint vectorCount = components[(i * 3)..].Vector128Count<float>() / 3;

            for (nuint vectorIndex = 0; vectorIndex < vectorCount; vectorIndex++, i += Vector128<float>.Count)
            {
                // Each transform remains a separate PCS stage, preserving the scalar adaptation order and unclipped range.
                SimdUtils.Deinterleave3(packed, i, out Vector128<float> x, out Vector128<float> y, out Vector128<float> z);
                SimdUtils.Transform3(in matrix, x, y, z, out Vector128<float> l, out Vector128<float> m, out Vector128<float> s);
                l *= ratioX;
                m *= ratioY;
                s *= ratioZ;
                SimdUtils.Transform3(in inverseMatrix, l, m, s, out Vector128<float> adaptedX, out Vector128<float> adaptedY, out Vector128<float> adaptedZ);
                SimdUtils.Interleave3(adaptedX, adaptedY, adaptedZ, ref Unsafe.As<CieXyz, float>(ref Unsafe.Add(ref destinationBase, i)));
            }
        }

        for (; i < count; i++)
        {
            ref CieXyz sp = ref Unsafe.Add(ref sourceBase, i);
            ref CieXyz dp = ref Unsafe.Add(ref destinationBase, i);

            Vector3 sourceColorLms = Vector3.Transform(sp.AsVector3Unsafe(), matrix);

            Vector3 targetColorLms = Vector3.Multiply(vector, sourceColorLms);
            dp = new CieXyz(Vector3.Transform(targetColorLms, inverseMatrix));
        }
    }
}
