// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

#pragma warning disable SA1117 // Parameters should be on same line or separate lines

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp;

/// <summary>
/// A structure encapsulating a 5x4 matrix used for transforming the color and alpha components of an image.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public partial struct ColorMatrix : IEquatable<ColorMatrix>
{
    /// <summary>
    /// Value at row 1, column 1 of the matrix.
    /// </summary>
    public float M11;

    /// <summary>
    /// Value at row 1, column 2 of the matrix.
    /// </summary>
    public float M12;

    /// <summary>
    /// Value at row 1, column 3 of the matrix.
    /// </summary>
    public float M13;

    /// <summary>
    /// Value at row 1, column 4 of the matrix.
    /// </summary>
    public float M14;

    /// <summary>
    /// Value at row 2, column 1 of the matrix.
    /// </summary>
    public float M21;

    /// <summary>
    /// Value at row 2, column 2 of the matrix.
    /// </summary>
    public float M22;

    /// <summary>
    /// Value at row 2, column 3 of the matrix.
    /// </summary>
    public float M23;

    /// <summary>
    /// Value at row 2, column 4 of the matrix.
    /// </summary>
    public float M24;

    /// <summary>
    /// Value at row 3, column 1 of the matrix.
    /// </summary>
    public float M31;

    /// <summary>
    /// Value at row 3, column 2 of the matrix.
    /// </summary>
    public float M32;

    /// <summary>
    /// Value at row 3, column 3 of the matrix.
    /// </summary>
    public float M33;

    /// <summary>
    /// Value at row 3, column 4 of the matrix.
    /// </summary>
    public float M34;

    /// <summary>
    /// Value at row 4, column 1 of the matrix.
    /// </summary>
    public float M41;

    /// <summary>
    /// Value at row 4, column 2 of the matrix.
    /// </summary>
    public float M42;

    /// <summary>
    /// Value at row 4, column 3 of the matrix.
    /// </summary>
    public float M43;

    /// <summary>
    /// Value at row 4, column 4 of the matrix.
    /// </summary>
    public float M44;

    /// <summary>
    /// Value at row 5, column 1 of the matrix.
    /// </summary>
    public float M51;

    /// <summary>
    /// Value at row 5, column 2 of the matrix.
    /// </summary>
    public float M52;

    /// <summary>
    /// Value at row 5, column 3 of the matrix.
    /// </summary>
    public float M53;

    /// <summary>
    /// Value at row 5, column 4 of the matrix.
    /// </summary>
    public float M54;

    /// <summary>
    /// Initializes a new instance of the <see cref="ColorMatrix"/> struct.
    /// </summary>
    /// <param name="m11">The value at row 1, column 1 of the matrix.</param>
    /// <param name="m12">The value at row 1, column 2 of the matrix.</param>
    /// <param name="m13">The value at row 1, column 3 of the matrix.</param>
    /// <param name="m14">The value at row 1, column 4 of the matrix.</param>
    /// <param name="m21">The value at row 2, column 1 of the matrix.</param>
    /// <param name="m22">The value at row 2, column 2 of the matrix.</param>
    /// <param name="m23">The value at row 2, column 3 of the matrix.</param>
    /// <param name="m24">The value at row 2, column 4 of the matrix.</param>
    /// <param name="m31">The value at row 3, column 1 of the matrix.</param>
    /// <param name="m32">The value at row 3, column 2 of the matrix.</param>
    /// <param name="m33">The value at row 3, column 3 of the matrix.</param>
    /// <param name="m34">The value at row 3, column 4 of the matrix.</param>
    /// <param name="m41">The value at row 4, column 1 of the matrix.</param>
    /// <param name="m42">The value at row 4, column 2 of the matrix.</param>
    /// <param name="m43">The value at row 4, column 3 of the matrix.</param>
    /// <param name="m44">The value at row 4, column 4 of the matrix.</param>
    /// <param name="m51">The value at row 5, column 1 of the matrix.</param>
    /// <param name="m52">The value at row 5, column 2 of the matrix.</param>
    /// <param name="m53">The value at row 5, column 3 of the matrix.</param>
    /// <param name="m54">The value at row 5, column 4 of the matrix.</param>
    public ColorMatrix(float m11, float m12, float m13, float m14,
        float m21, float m22, float m23, float m24,
        float m31, float m32, float m33, float m34,
        float m41, float m42, float m43, float m44,
        float m51, float m52, float m53, float m54)
    {
        Unsafe.SkipInit(out this);

        this.AsImpl().Init(m11, m12, m13, m14, m21, m22, m23, m24, m31, m32, m33, m34, m41, m42, m43, m44, m51, m52, m53,
            m54);
    }

    /// <summary>
    /// Gets the multiplicative identity matrix.
    /// </summary>
    public static ColorMatrix Identity
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Impl.Identity.AsColorMatrix();
    }

    /// <summary>
    /// Gets a value indicating whether the matrix is the identity matrix.
    /// </summary>
    public bool IsIdentity
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this.AsROImpl().IsIdentity;
    }

    /// <summary>
    /// Adds two matrices together.
    /// </summary>
    /// <param name="value1">The first source matrix.</param>
    /// <param name="value2">The second source matrix.</param>
    /// <returns>The resulting matrix.</returns>
    public static ColorMatrix operator +(ColorMatrix value1, ColorMatrix value2)
        => (value1.AsImpl() + value2.AsImpl()).AsColorMatrix();

    /// <summary>
    /// Subtracts the second matrix from the first.
    /// </summary>
    /// <param name="value1">The first source matrix.</param>
    /// <param name="value2">The second source matrix.</param>
    /// <returns>The result of the subtraction.</returns>
    public static ColorMatrix operator -(ColorMatrix value1, ColorMatrix value2)
        => (value1.AsImpl() - value2.AsImpl()).AsColorMatrix();

    /// <summary>
    /// Returns a new matrix with the negated elements of the given matrix.
    /// </summary>
    /// <param name="value">The source matrix.</param>
    /// <returns>The negated matrix.</returns>
    public static ColorMatrix operator -(ColorMatrix value)
        => (-value.AsImpl()).AsColorMatrix();

    /// <summary>
    /// Multiplies a matrix by another matrix.
    /// </summary>
    /// <param name="value1">The first source matrix.</param>
    /// <param name="value2">The second source matrix.</param>
    /// <returns>The result of the multiplication.</returns>
    public static ColorMatrix operator *(ColorMatrix value1, ColorMatrix value2)
        => (value1.AsImpl() * value2.AsImpl()).AsColorMatrix();

    /// <summary>
    /// Multiplies a matrix by a scalar value.
    /// </summary>
    /// <param name="value1">The source matrix.</param>
    /// <param name="value2">The scaling factor.</param>
    /// <returns>The scaled matrix.</returns>
    public static ColorMatrix operator *(ColorMatrix value1, float value2)
        => (value1.AsImpl() * value2).AsColorMatrix();

    /// <summary>
    /// Returns a boolean indicating whether the given two matrices are equal.
    /// </summary>
    /// <param name="value1">The first matrix to compare.</param>
    /// <param name="value2">The second matrix to compare.</param>
    /// <returns>True if the given matrices are equal; False otherwise.</returns>
    public static bool operator ==(ColorMatrix value1, ColorMatrix value2)
        => value1.AsImpl() == value2.AsImpl();

    /// <summary>
    /// Returns a boolean indicating whether the given two matrices are not equal.
    /// </summary>
    /// <param name="value1">The first matrix to compare.</param>
    /// <param name="value2">The second matrix to compare.</param>
    /// <returns>True if the given matrices are equal; False otherwise.</returns>
    public static bool operator !=(ColorMatrix value1, ColorMatrix value2)
        => value1.AsImpl() != value2.AsImpl();

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override readonly bool Equals([NotNullWhen(true)] object? obj)
        => this.AsROImpl().Equals(obj);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool Equals(ColorMatrix other)
        => this.AsROImpl().Equals(in other.AsImpl());

    /// <inheritdoc/>
    public override int GetHashCode()
        => this.AsROImpl().GetHashCode();

    /// <inheritdoc/>
    public override string ToString()
    {
        CultureInfo ci = CultureInfo.CurrentCulture;

        return string.Format(
            ci,
            "{{ {{M11:{0} M12:{1} M13:{2} M14:{3}}} {{M21:{4} M22:{5} M23:{6} M24:{7}}} {{M31:{8} M32:{9} M33:{10} M34:{11}}} {{M41:{12} M42:{13} M43:{14} M44:{15}}} {{M51:{16} M52:{17} M53:{18} M54:{19}}} }}",
            this.M11.ToString(ci), this.M12.ToString(ci), this.M13.ToString(ci), this.M14.ToString(ci),
            this.M21.ToString(ci), this.M22.ToString(ci), this.M23.ToString(ci), this.M24.ToString(ci),
            this.M31.ToString(ci), this.M32.ToString(ci), this.M33.ToString(ci), this.M34.ToString(ci),
            this.M41.ToString(ci), this.M42.ToString(ci), this.M43.ToString(ci), this.M44.ToString(ci),
            this.M51.ToString(ci), this.M52.ToString(ci), this.M53.ToString(ci), this.M54.ToString(ci));
    }
}
