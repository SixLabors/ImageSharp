// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

#pragma warning disable SA1117 // Parameters should be on same line or separate lines
using System;
using System.Globalization;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// A structure encapsulating a 5x4 matrix used for transforming the color and alpha components of an image.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct ColorMatrix : IEquatable<ColorMatrix>
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
            this.M11 = m11;
            this.M12 = m12;
            this.M13 = m13;
            this.M14 = m14;

            this.M21 = m21;
            this.M22 = m22;
            this.M23 = m23;
            this.M24 = m24;

            this.M31 = m31;
            this.M32 = m32;
            this.M33 = m33;
            this.M34 = m34;

            this.M41 = m41;
            this.M42 = m42;
            this.M43 = m43;
            this.M44 = m44;

            this.M51 = m51;
            this.M52 = m52;
            this.M53 = m53;
            this.M54 = m54;
        }

        /// <summary>
        /// Gets the multiplicative identity matrix.
        /// </summary>
        public static ColorMatrix Identity { get; } =
            new ColorMatrix(1F, 0F, 0F, 0F,
                          0F, 1F, 0F, 0F,
                          0F, 0F, 1F, 0F,
                          0F, 0F, 0F, 1F,
                          0F, 0F, 0F, 0F);

        /// <summary>
        /// Gets a value indicating whether the matrix is the identity matrix.
        /// </summary>
        public bool IsIdentity
        {
            get
            {
                // Check diagonal element first for early out.
                return this.M11 == 1F && this.M22 == 1F && this.M33 == 1F && this.M44 == 1F
                       && this.M12 == 0F && this.M13 == 0F && this.M14 == 0F
                       && this.M21 == 0F && this.M23 == 0F && this.M24 == 0F
                       && this.M31 == 0F && this.M32 == 0F && this.M34 == 0F
                       && this.M41 == 0F && this.M42 == 0F && this.M43 == 0F
                       && this.M51 == 0F && this.M52 == 0F && this.M53 == 0F && this.M54 == 0F;
            }
        }

        /// <summary>
        /// Adds two matrices together.
        /// </summary>
        /// <param name="value1">The first source matrix.</param>
        /// <param name="value2">The second source matrix.</param>
        /// <returns>The resulting matrix.</returns>
        public static ColorMatrix operator +(ColorMatrix value1, ColorMatrix value2)
        {
            var m = default(ColorMatrix);

            m.M11 = value1.M11 + value2.M11;
            m.M12 = value1.M12 + value2.M12;
            m.M13 = value1.M13 + value2.M13;
            m.M14 = value1.M14 + value2.M14;
            m.M21 = value1.M21 + value2.M21;
            m.M22 = value1.M22 + value2.M22;
            m.M23 = value1.M23 + value2.M23;
            m.M24 = value1.M24 + value2.M24;
            m.M31 = value1.M31 + value2.M31;
            m.M32 = value1.M32 + value2.M32;
            m.M33 = value1.M33 + value2.M33;
            m.M34 = value1.M34 + value2.M34;
            m.M41 = value1.M41 + value2.M41;
            m.M42 = value1.M42 + value2.M42;
            m.M43 = value1.M43 + value2.M43;
            m.M44 = value1.M44 + value2.M44;
            m.M51 = value1.M51 + value2.M51;
            m.M52 = value1.M52 + value2.M52;
            m.M53 = value1.M53 + value2.M53;
            m.M54 = value1.M54 + value2.M54;

            return m;
        }

        /// <summary>
        /// Subtracts the second matrix from the first.
        /// </summary>
        /// <param name="value1">The first source matrix.</param>
        /// <param name="value2">The second source matrix.</param>
        /// <returns>The result of the subtraction.</returns>
        public static ColorMatrix operator -(ColorMatrix value1, ColorMatrix value2)
        {
            var m = default(ColorMatrix);

            m.M11 = value1.M11 - value2.M11;
            m.M12 = value1.M12 - value2.M12;
            m.M13 = value1.M13 - value2.M13;
            m.M14 = value1.M14 - value2.M14;
            m.M21 = value1.M21 - value2.M21;
            m.M22 = value1.M22 - value2.M22;
            m.M23 = value1.M23 - value2.M23;
            m.M24 = value1.M24 - value2.M24;
            m.M31 = value1.M31 - value2.M31;
            m.M32 = value1.M32 - value2.M32;
            m.M33 = value1.M33 - value2.M33;
            m.M34 = value1.M34 - value2.M34;
            m.M41 = value1.M41 - value2.M41;
            m.M42 = value1.M42 - value2.M42;
            m.M43 = value1.M43 - value2.M43;
            m.M44 = value1.M44 - value2.M44;
            m.M51 = value1.M51 - value2.M51;
            m.M52 = value1.M52 - value2.M52;
            m.M53 = value1.M53 - value2.M53;
            m.M54 = value1.M54 - value2.M54;

            return m;
        }

        /// <summary>
        /// Returns a new matrix with the negated elements of the given matrix.
        /// </summary>
        /// <param name="value">The source matrix.</param>
        /// <returns>The negated matrix.</returns>
        public static ColorMatrix operator -(ColorMatrix value)
        {
            var m = default(ColorMatrix);

            m.M11 = -value.M11;
            m.M12 = -value.M12;
            m.M13 = -value.M13;
            m.M14 = -value.M14;
            m.M21 = -value.M21;
            m.M22 = -value.M22;
            m.M23 = -value.M23;
            m.M24 = -value.M24;
            m.M31 = -value.M31;
            m.M32 = -value.M32;
            m.M33 = -value.M33;
            m.M34 = -value.M34;
            m.M41 = -value.M41;
            m.M42 = -value.M42;
            m.M43 = -value.M43;
            m.M44 = -value.M44;
            m.M51 = -value.M51;
            m.M52 = -value.M52;
            m.M53 = -value.M53;
            m.M54 = -value.M54;

            return m;
        }

        /// <summary>
        /// Multiplies a matrix by another matrix.
        /// </summary>
        /// <param name="value1">The first source matrix.</param>
        /// <param name="value2">The second source matrix.</param>
        /// <returns>The result of the multiplication.</returns>
        public static ColorMatrix operator *(ColorMatrix value1, ColorMatrix value2)
        {
            var m = default(ColorMatrix);

            // First row
            m.M11 = (value1.M11 * value2.M11) + (value1.M12 * value2.M21) + (value1.M13 * value2.M31) + (value1.M14 * value2.M41);
            m.M12 = (value1.M11 * value2.M12) + (value1.M12 * value2.M22) + (value1.M13 * value2.M32) + (value1.M14 * value2.M42);
            m.M13 = (value1.M11 * value2.M13) + (value1.M12 * value2.M23) + (value1.M13 * value2.M33) + (value1.M14 * value2.M43);
            m.M14 = (value1.M11 * value2.M14) + (value1.M12 * value2.M24) + (value1.M13 * value2.M34) + (value1.M14 * value2.M44);

            // Second row
            m.M21 = (value1.M21 * value2.M11) + (value1.M22 * value2.M21) + (value1.M23 * value2.M31) + (value1.M24 * value2.M41);
            m.M22 = (value1.M21 * value2.M12) + (value1.M22 * value2.M22) + (value1.M23 * value2.M32) + (value1.M24 * value2.M42);
            m.M23 = (value1.M21 * value2.M13) + (value1.M22 * value2.M23) + (value1.M23 * value2.M33) + (value1.M24 * value2.M43);
            m.M24 = (value1.M21 * value2.M14) + (value1.M22 * value2.M24) + (value1.M23 * value2.M34) + (value1.M24 * value2.M44);

            // Third row
            m.M31 = (value1.M31 * value2.M11) + (value1.M32 * value2.M21) + (value1.M33 * value2.M31) + (value1.M34 * value2.M41);
            m.M32 = (value1.M31 * value2.M12) + (value1.M32 * value2.M22) + (value1.M33 * value2.M32) + (value1.M34 * value2.M42);
            m.M33 = (value1.M31 * value2.M13) + (value1.M32 * value2.M23) + (value1.M33 * value2.M33) + (value1.M34 * value2.M43);
            m.M34 = (value1.M31 * value2.M14) + (value1.M32 * value2.M24) + (value1.M33 * value2.M34) + (value1.M34 * value2.M44);

            // Fourth row
            m.M41 = (value1.M41 * value2.M11) + (value1.M42 * value2.M21) + (value1.M43 * value2.M31) + (value1.M44 * value2.M41);
            m.M42 = (value1.M41 * value2.M12) + (value1.M42 * value2.M22) + (value1.M43 * value2.M32) + (value1.M44 * value2.M42);
            m.M43 = (value1.M41 * value2.M13) + (value1.M42 * value2.M23) + (value1.M43 * value2.M33) + (value1.M44 * value2.M43);
            m.M44 = (value1.M41 * value2.M14) + (value1.M42 * value2.M24) + (value1.M43 * value2.M34) + (value1.M44 * value2.M44);

            // Fifth row
            m.M51 = (value1.M51 * value2.M11) + (value1.M52 * value2.M21) + (value1.M53 * value2.M31) + (value1.M54 * value2.M41) + value2.M51;
            m.M52 = (value1.M51 * value2.M12) + (value1.M52 * value2.M22) + (value1.M53 * value2.M32) + (value1.M54 * value2.M52) + value2.M52;
            m.M53 = (value1.M51 * value2.M13) + (value1.M52 * value2.M23) + (value1.M53 * value2.M33) + (value1.M54 * value2.M53) + value2.M53;
            m.M54 = (value1.M51 * value2.M14) + (value1.M52 * value2.M24) + (value1.M53 * value2.M34) + (value1.M54 * value2.M54) + value2.M54;

            return m;
        }

        /// <summary>
        /// Multiplies a matrix by a scalar value.
        /// </summary>
        /// <param name="value1">The source matrix.</param>
        /// <param name="value2">The scaling factor.</param>
        /// <returns>The scaled matrix.</returns>
        public static ColorMatrix operator *(ColorMatrix value1, float value2)
        {
            var m = default(ColorMatrix);

            m.M11 = value1.M11 * value2;
            m.M12 = value1.M12 * value2;
            m.M13 = value1.M13 * value2;
            m.M14 = value1.M14 * value2;
            m.M21 = value1.M21 * value2;
            m.M22 = value1.M22 * value2;
            m.M23 = value1.M23 * value2;
            m.M24 = value1.M24 * value2;
            m.M31 = value1.M31 * value2;
            m.M32 = value1.M32 * value2;
            m.M33 = value1.M33 * value2;
            m.M34 = value1.M34 * value2;
            m.M41 = value1.M41 * value2;
            m.M42 = value1.M42 * value2;
            m.M43 = value1.M43 * value2;
            m.M44 = value1.M44 * value2;
            m.M51 = value1.M51 * value2;
            m.M52 = value1.M52 * value2;
            m.M53 = value1.M53 * value2;
            m.M54 = value1.M54 * value2;

            return m;
        }

        /// <summary>
        /// Returns a boolean indicating whether the given two matrices are equal.
        /// </summary>
        /// <param name="value1">The first matrix to compare.</param>
        /// <param name="value2">The second matrix to compare.</param>
        /// <returns>True if the given matrices are equal; False otherwise.</returns>
        public static bool operator ==(ColorMatrix value1, ColorMatrix value2) => value1.Equals(value2);

        /// <summary>
        /// Returns a boolean indicating whether the given two matrices are not equal.
        /// </summary>
        /// <param name="value1">The first matrix to compare.</param>
        /// <param name="value2">The second matrix to compare.</param>
        /// <returns>True if the given matrices are equal; False otherwise.</returns>
        public static bool operator !=(ColorMatrix value1, ColorMatrix value2) => !value1.Equals(value2);

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is ColorMatrix matrix && this.Equals(matrix);

        /// <inheritdoc/>
        public bool Equals(ColorMatrix other) =>
            this.M11 == other.M11
            && this.M12 == other.M12
            && this.M13 == other.M13
            && this.M14 == other.M14
            && this.M21 == other.M21
            && this.M22 == other.M22
            && this.M23 == other.M23
            && this.M24 == other.M24
            && this.M31 == other.M31
            && this.M32 == other.M32
            && this.M33 == other.M33
            && this.M34 == other.M34
            && this.M41 == other.M41
            && this.M42 == other.M42
            && this.M43 == other.M43
            && this.M44 == other.M44
            && this.M51 == other.M51
            && this.M52 == other.M52
            && this.M53 == other.M53
            && this.M54 == other.M54;

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            HashCode hash = default;
            hash.Add(this.M11);
            hash.Add(this.M12);
            hash.Add(this.M13);
            hash.Add(this.M14);
            hash.Add(this.M21);
            hash.Add(this.M22);
            hash.Add(this.M23);
            hash.Add(this.M24);
            hash.Add(this.M31);
            hash.Add(this.M32);
            hash.Add(this.M33);
            hash.Add(this.M34);
            hash.Add(this.M41);
            hash.Add(this.M42);
            hash.Add(this.M43);
            hash.Add(this.M44);
            hash.Add(this.M51);
            hash.Add(this.M52);
            hash.Add(this.M53);
            hash.Add(this.M54);
            return hash.ToHashCode();
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            CultureInfo ci = CultureInfo.CurrentCulture;

            return string.Format(ci, "{{ {{M11:{0} M12:{1} M13:{2} M14:{3}}} {{M21:{4} M22:{5} M23:{6} M24:{7}}} {{M31:{8} M32:{9} M33:{10} M34:{11}}} {{M41:{12} M42:{13} M43:{14} M44:{15}}} {{M51:{16} M52:{17} M53:{18} M54:{19}}} }}",
                                 this.M11.ToString(ci), this.M12.ToString(ci), this.M13.ToString(ci), this.M14.ToString(ci),
                                 this.M21.ToString(ci), this.M22.ToString(ci), this.M23.ToString(ci), this.M24.ToString(ci),
                                 this.M31.ToString(ci), this.M32.ToString(ci), this.M33.ToString(ci), this.M34.ToString(ci),
                                 this.M41.ToString(ci), this.M42.ToString(ci), this.M43.ToString(ci), this.M44.ToString(ci),
                                 this.M51.ToString(ci), this.M52.ToString(ci), this.M53.ToString(ci), this.M54.ToString(ci));
        }
    }
}
