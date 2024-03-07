// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

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
    /// <param name="options">The color profile conversion options.</param>
    /// <param name="source">The source color.</param>
    /// <returns>The <see cref="CieXyz"/></returns>
    public static CieXyz Transform(ColorConversionOptions options, in CieXyz source)
    {
        CieXyz sourceWhitePoint = options.WhitePoint;
        CieXyz destinationWhitePoint = options.TargetWhitePoint;

        if (sourceWhitePoint.Equals(destinationWhitePoint))
        {
            return new(source.X, source.Y, source.Z);
        }

        Matrix4x4 matrix = options.AdaptationMatrix;

        Vector3 sourceColorLms = Vector3.Transform(source.ToVector3(), matrix);
        Vector3 sourceWhitePointLms = Vector3.Transform(sourceWhitePoint.ToVector3(), matrix);
        Vector3 targetWhitePointLms = Vector3.Transform(destinationWhitePoint.ToVector3(), matrix);

        Vector3 vector = targetWhitePointLms / sourceWhitePointLms;
        Vector3 targetColorLms = Vector3.Multiply(vector, sourceColorLms);

        Matrix4x4.Invert(matrix, out Matrix4x4 inverseMatrix);
        return new CieXyz(Vector3.Transform(targetColorLms, inverseMatrix));
    }

    /// <summary>
    /// Performs a bulk linear transformation of a source color in to the destination color.
    /// </summary>
    /// <remarks>Doesn't crop the resulting color space coordinates (e. g. allows negative values for XYZ coordinates).</remarks>
    /// <param name="options">The color profile conversion options.</param>
    /// <param name="source">The span to the source colors.</param>
    /// <param name="destination">The span to the destination colors.</param>
    public static void Transform(ColorConversionOptions options, ReadOnlySpan<CieXyz> source, Span<CieXyz> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
        int count = source.Length;

        CieXyz sourceWhitePoint = options.WhitePoint;
        CieXyz destinationWhitePoint = options.TargetWhitePoint;

        if (sourceWhitePoint.Equals(destinationWhitePoint))
        {
            source.CopyTo(destination[..count]);
            return;
        }

        Matrix4x4 matrix = options.AdaptationMatrix;
        Matrix4x4.Invert(matrix, out Matrix4x4 inverseMatrix);

        ref CieXyz sourceBase = ref MemoryMarshal.GetReference(source);
        ref CieXyz destinationBase = ref MemoryMarshal.GetReference(destination);

        for (nuint i = 0; i < (uint)count; i++)
        {
            ref CieXyz sp = ref Unsafe.Add(ref sourceBase, i);
            ref CieXyz dp = ref Unsafe.Add(ref destinationBase, i);

            Vector3 sourceColorLms = Vector3.Transform(sp.ToVector3(), matrix);
            Vector3 sourceWhitePointLms = Vector3.Transform(sourceWhitePoint.ToVector3(), matrix);
            Vector3 targetWhitePointLms = Vector3.Transform(destinationWhitePoint.ToVector3(), matrix);

            Vector3 vector = targetWhitePointLms / sourceWhitePointLms;
            Vector3 targetColorLms = Vector3.Multiply(vector, sourceColorLms);
            dp = new CieXyz(Vector3.Transform(targetColorLms, inverseMatrix));
        }
    }
}
