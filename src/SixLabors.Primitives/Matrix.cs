// <copyright file="Matrix.cs" company="Six Labors">
// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace SixLabors.Primitives
{
    using System;
    using System.Numerics;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// A Matrix object for applying matrix transforms to primitives.
    /// </summary>
    public struct Matrix : IEquatable<Matrix>
    {
        private Matrix3x2 backingMatrix;

        private static readonly Matrix _identity = new Matrix
        {
            backingMatrix = Matrix3x2.Identity
        };

        /// <summary>
        /// Returns the multiplicative identity matrix.
        /// </summary>
        public static Matrix Identity => _identity;

        /// <summary>
        /// Returns whether the matrix is the identity matrix.
        /// </summary>
        public bool IsIdentity => this.backingMatrix.IsIdentity;

        /// <summary>
        /// Gets or Sets the translation component of this matrix.
        /// </summary>
        public PointF Translation
        {
            get => this.backingMatrix.Translation;
            set => this.backingMatrix.Translation = value;
        }

        /// <summary>
        /// The first element of the first row
        /// </summary>
        public float M11 { get => this.backingMatrix.M11; set => this.backingMatrix.M11 = value; }

        /// <summary>
        /// The second element of the first row
        /// </summary>
        public float M12 { get => this.backingMatrix.M12; set => this.backingMatrix.M12 = value; }

        /// <summary>
        /// The first element of the second row
        /// </summary>
        public float M21 { get => this.backingMatrix.M21; set => this.backingMatrix.M21 = value; }

        /// <summary>
        /// The second element of the second row
        /// </summary>
        public float M22 { get => this.backingMatrix.M22; set => this.backingMatrix.M22 = value; }

        /// <summary>
        /// The first element of the third row
        /// </summary>
        public float M31 { get => this.backingMatrix.M31; set => this.backingMatrix.M31 = value; }

        /// <summary>
        /// The second element of the third row
        /// </summary>
        public float M32 { get => this.backingMatrix.M32; set => this.backingMatrix.M32 = value; }

        /// <summary>
        /// Constructs a Matrix3x2 from the given components.
        /// </summary>
        public Matrix(float m11, float m12,
                         float m21, float m22,
                         float m31, float m32)
        {
            this.backingMatrix = new Matrix3x2(m11, m12, m21, m22, m31, m32);
        }

        /// <summary>
        /// Creates a translation matrix from the given vector.
        /// </summary>
        /// <param name="position">The translation position.</param>
        /// <returns>A translation matrix.</returns>
        public static Matrix CreateTranslation(PointF position) => Matrix3x2.CreateTranslation(position);

        /// <summary>
        /// Creates a translation matrix from the given X and Y components.
        /// </summary>
        /// <param name="xPosition">The X position.</param>
        /// <param name="yPosition">The Y position.</param>
        /// <returns>A translation matrix.</returns>
        public static Matrix CreateTranslation(float xPosition, float yPosition) => Matrix3x2.CreateTranslation(xPosition, yPosition);

        /// <summary>
        /// Creates a scale matrix from the given X and Y components.
        /// </summary>
        /// <param name="xScale">Value to scale by on the X-axis.</param>
        /// <param name="yScale">Value to scale by on the Y-axis.</param>
        /// <returns>A scaling matrix.</returns>
        public static Matrix CreateScale(float xScale, float yScale) => Matrix3x2.CreateScale(xScale, yScale);

        /// <summary>
        /// Creates a scale matrix that is offset by a given center point.
        /// </summary>
        /// <param name="xScale">Value to scale by on the X-axis.</param>
        /// <param name="yScale">Value to scale by on the Y-axis.</param>
        /// <param name="centerPoint">The center point.</param>
        /// <returns>A scaling matrix.</returns>
        public static Matrix CreateScale(float xScale, float yScale, PointF centerPoint) => Matrix3x2.CreateScale(xScale, yScale, centerPoint);

        /// <summary>
        /// Creates a scale matrix from the given vector scale.
        /// </summary>
        /// <param name="scales">The scale to use.</param>
        /// <returns>A scaling matrix.</returns>
        public static Matrix CreateScale(SizeF scales) => Matrix3x2.CreateScale(scales);

        /// <summary>
        /// Creates a scale matrix from the given vector scale with an offset from the given center point.
        /// </summary>
        /// <param name="scales">The scale to use.</param>
        /// <param name="centerPoint">The center offset.</param>
        /// <returns>A scaling matrix.</returns>
        public static Matrix CreateScale(SizeF scales, PointF centerPoint) => Matrix3x2.CreateScale(scales, centerPoint);

        /// <summary>
        /// Creates a scale matrix that scales uniformly with the given scale.
        /// </summary>
        /// <param name="scale">The uniform scale to use.</param>
        /// <returns>A scaling matrix.</returns>
        public static Matrix CreateScale(float scale) => Matrix3x2.CreateScale(scale);

        /// <summary>
        /// Creates a scale matrix that scales uniformly with the given scale with an offset from the given center.
        /// </summary>
        /// <param name="scale">The uniform scale to use.</param>
        /// <param name="centerPoint">The center offset.</param>
        /// <returns>A scaling matrix.</returns>
        public static Matrix CreateScale(float scale, PointF centerPoint) => Matrix3x2.CreateScale(scale, centerPoint);

        /// <summary>
        /// Creates a skew matrix from the given angles in radians.
        /// </summary>
        /// <param name="radiansX">The X angle, in radians.</param>
        /// <param name="radiansY">The Y angle, in radians.</param>
        /// <returns>A skew matrix.</returns>
        public static Matrix CreateSkew(float radiansX, float radiansY) => Matrix3x2.CreateSkew(radiansX, radiansY);

        /// <summary>
        /// Creates a skew matrix from the given angles in radians.
        /// </summary>
        /// <param name="degreesX">The X angle, in degrees.</param>
        /// <param name="degreesY">The Y angle, in degrees.</param>
        /// <returns>A skew matrix.</returns>
        public static Matrix CreateSkewDegrees(float degreesX, float degreesY) => Matrix3x2.CreateSkew(MathF.ToRadians(degreesX), MathF.ToRadians(degreesY));

        /// <summary>
        /// Creates a skew matrix from the given angles in radians and a center point.
        /// </summary>
        /// <param name="radiansX">The X angle, in radians.</param>
        /// <param name="radiansY">The Y angle, in radians.</param>
        /// <param name="centerPoint">The center point.</param>
        /// <returns>A skew matrix.</returns>
        public static Matrix CreateSkew(float radiansX, float radiansY, PointF centerPoint) => Matrix3x2.CreateSkew(radiansX, radiansY, centerPoint);

        /// <summary>
        /// Creates a skew matrix from the given angles in radians and a center point.
        /// </summary>
        /// <param name="degreesX">The X angle, in degrees.</param>
        /// <param name="degreesY">The Y angle, in degrees.</param>
        /// <param name="centerPoint">The center point.</param>
        /// <returns>A skew matrix.</returns>
        public static Matrix CreateSkewDegrees(float degreesX, float degreesY, PointF centerPoint) => Matrix3x2.CreateSkew(MathF.ToRadians(degreesX), MathF.ToRadians(degreesY), centerPoint);

        /// <summary>
        /// Creates a rotation matrix using the given rotation in radians.
        /// </summary>
        /// <param name="radians">The amount of rotation, in radians.</param>
        /// <returns>A rotation matrix.</returns>
        public static Matrix CreateRotation(float radians) => System.Numerics.Matrix3x2.CreateRotation(radians);

        /// <summary>
        /// Creates a rotation matrix using the given rotation in radians.
        /// </summary>
        /// <param name="degrees">The amount of rotation, in degrees.</param>
        /// <returns>A rotation matrix.</returns>
        public static Matrix CreateRotationDegrees(float degrees) => System.Numerics.Matrix3x2.CreateRotation(MathF.ToRadians(degrees));

        /// <summary>
        /// Creates a rotation matrix using the given rotation in radians and a center point.
        /// </summary>
        /// <param name="radians">The amount of rotation, in radians.</param>
        /// <param name="centerPoint">The center point.</param>
        /// <returns>A rotation matrix.</returns>
        public static Matrix CreateRotation(float radians, PointF centerPoint) => System.Numerics.Matrix3x2.CreateRotation(radians, centerPoint);

        /// <summary>
        /// Creates a rotation matrix using the given rotation in radians and a center point.
        /// </summary>
        /// <param name="degrees">The amount of rotation, in degrees.</param>
        /// <param name="centerPoint">The center point.</param>
        /// <returns>A rotation matrix.</returns>
        public static Matrix CreateRotationDegrees(float degrees, PointF centerPoint) => Matrix3x2.CreateRotation(MathF.ToRadians(degrees), centerPoint);

        /// <summary>
        /// Calculates the determinant for this matrix. 
        /// The determinant is calculated by expanding the matrix with a third column whose values are (0,0,1).
        /// </summary>
        /// <returns>The determinant.</returns>
        public float GetDeterminant() => this.backingMatrix.GetDeterminant();

        /// <summary>
        /// Attempts to invert the given matrix. If the operation succeeds, the inverted matrix is stored in the result parameter.
        /// </summary>
        /// <param name="matrix">The source matrix.</param>
        /// <param name="result">The output matrix.</param>
        /// <returns>True if the operation succeeded, False otherwise.</returns>
        public static bool Invert(Matrix matrix, out Matrix result)
        {
            Matrix3x2 m;
            var b = System.Numerics.Matrix3x2.Invert(matrix.backingMatrix, out m);
            result = m;
            return b;
        }

        /// <summary>
        /// Linearly interpolates from matrix1 to matrix2, based on the third parameter.
        /// </summary>
        /// <param name="matrix1">The first source matrix.</param>
        /// <param name="matrix2">The second source matrix.</param>
        /// <param name="amount">The relative weighting of matrix2.</param>
        /// <returns>The interpolated matrix.</returns>
        public static Matrix Lerp(Matrix matrix1, Matrix matrix2, float amount) => Matrix3x2.Lerp(matrix1.backingMatrix, matrix2.backingMatrix, amount);

        /// <summary>
        /// Negates the given matrix by multiplying all values by -1.
        /// </summary>
        /// <param name="value">The source matrix.</param>
        /// <returns>The negated matrix.</returns>
        public static Matrix Negate(Matrix value) => -value.backingMatrix;

        /// <summary>
        /// Adds each matrix element in value1 with its corresponding element in value2.
        /// </summary>
        /// <param name="value1">The first source matrix.</param>
        /// <param name="value2">The second source matrix.</param>
        /// <returns>The matrix containing the summed values.</returns>
        public static Matrix Add(Matrix value1, Matrix value2) => value1.backingMatrix + value2.backingMatrix;

        /// <summary>
        /// Subtracts each matrix element in value2 from its corresponding element in value1.
        /// </summary>
        /// <param name="value1">The first source matrix.</param>
        /// <param name="value2">The second source matrix.</param>
        /// <returns>The matrix containing the resulting values.</returns>
        public static Matrix Subtract(Matrix value1, Matrix value2) => value1.backingMatrix - value2.backingMatrix;

        /// <summary>
        /// Multiplies two matrices together and returns the resulting matrix.
        /// </summary>
        /// <param name="value1">The first source matrix.</param>
        /// <param name="value2">The second source matrix.</param>
        /// <returns>The product matrix.</returns>
        public static Matrix Multiply(Matrix value1, Matrix value2) => value1.backingMatrix * value2.backingMatrix;

        /// <summary>
        /// Scales all elements in a matrix by the given scalar factor.
        /// </summary>
        /// <param name="value1">The source matrix.</param>
        /// <param name="value2">The scaling value to use.</param>
        /// <returns>The resulting matrix.</returns>
        public static Matrix Multiply(Matrix value1, float value2) => value1.backingMatrix * value2;

        /// <summary>
        /// Negates the given matrix by multiplying all values by -1.
        /// </summary>
        /// <param name="value">The source matrix.</param>
        /// <returns>The negated matrix.</returns>
        public static Matrix operator -(Matrix value) => -value.backingMatrix;

        /// <summary>
        /// Adds each matrix element in value1 with its corresponding element in value2.
        /// </summary>
        /// <param name="value1">The first source matrix.</param>
        /// <param name="value2">The second source matrix.</param>
        /// <returns>The matrix containing the summed values.</returns>
        public static Matrix operator +(Matrix value1, Matrix value2) => value1.backingMatrix + value2.backingMatrix;

        /// <summary>
        /// Subtracts each matrix element in value2 from its corresponding element in value1.
        /// </summary>
        /// <param name="value1">The first source matrix.</param>
        /// <param name="value2">The second source matrix.</param>
        /// <returns>The matrix containing the resulting values.</returns>
        public static Matrix operator -(Matrix value1, Matrix value2) => value1.backingMatrix - value2.backingMatrix;

        /// <summary>
        /// Multiplies two matrices together and returns the resulting matrix.
        /// </summary>
        /// <param name="value1">The first source matrix.</param>
        /// <param name="value2">The second source matrix.</param>
        /// <returns>The product matrix.</returns>
        public static Matrix operator *(Matrix value1, Matrix value2) => value1.backingMatrix * value2.backingMatrix;

        /// <summary>
        /// Scales all elements in a matrix by the given scalar factor.
        /// </summary>
        /// <param name="value1">The source matrix.</param>
        /// <param name="value2">The scaling value to use.</param>
        /// <returns>The resulting matrix.</returns>
        public static Matrix operator *(Matrix value1, float value2) => value1.backingMatrix * value2;

        /// <summary>
        /// Returns a boolean indicating whether the given matrices are equal.
        /// </summary>
        /// <param name="value1">The first source matrix.</param>
        /// <param name="value2">The second source matrix.</param>
        /// <returns>True if the matrices are equal; False otherwise.</returns>
        public static bool operator ==(Matrix value1, Matrix value2) => value1.backingMatrix == value2.backingMatrix;

        /// <summary>
        /// Returns a boolean indicating whether the given matrices are not equal.
        /// </summary>
        /// <param name="value1">The first source matrix.</param>
        /// <param name="value2">The second source matrix.</param>
        /// <returns>True if the matrices are not equal; False if they are equal.</returns>
        public static bool operator !=(Matrix value1, Matrix value2)
        {
            return value1.backingMatrix != value2.backingMatrix;

        }

        /// <summary>
        /// Returns a boolean indicating whether the matrix is equal to the other given matrix.
        /// </summary>
        /// <param name="other">The other matrix to test equality against.</param>
        /// <returns>True if this matrix is equal to other; False otherwise.</returns>
        public bool Equals(Matrix other)
        {
            return this.backingMatrix == other.backingMatrix;
        }

        /// <summary>
        /// Returns a boolean indicating whether the given Object is equal to this matrix instance.
        /// </summary>
        /// <param name="obj">The Object to compare against.</param>
        /// <returns>True if the Object is equal to this matrix; False otherwise.</returns>
        public override bool Equals(object obj)
        {
            if (obj is Matrix)
            {
                return Equals((Matrix)obj);
            }

            return false;
        }

        /// <summary>
        /// Returns a String representing this matrix instance.
        /// </summary>
        /// <returns>The string representation.</returns>
        public override string ToString() => this.backingMatrix.ToString();

        /// <summary>
        /// Returns the hash code for this instance.
        /// </summary>
        /// <returns>The hash code.</returns>
        public override int GetHashCode() => this.backingMatrix.GetHashCode();

        /// <summary>
        /// Creates a <see cref="Matrix3x2"/> with the values of the specified <see cref="Matrix"/>.
        /// </summary>
        /// <param name="matrix">The matrix.</param>
        /// <returns>
        /// The <see cref="Matrix3x2"/>.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator Matrix3x2(Matrix matrix) => matrix.backingMatrix;

        /// <summary>
        /// Creates a <see cref="Matrix3x2"/> with the values of the specified <see cref="Matrix"/>.
        /// </summary>
        /// <param name="matrix">The matrix.</param>
        /// <returns>
        /// The <see cref="Matrix"/>.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator Matrix(Matrix3x2 matrix) => new Matrix { backingMatrix = matrix };

        internal static Matrix CreateScale(Vector2 scale, object zero)
        {
            throw new NotImplementedException();
        }
    }
}
