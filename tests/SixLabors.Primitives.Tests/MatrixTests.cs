// <copyright file="SizeTests.cs" company="Six Labors">
// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace SixLabors.Primitives.Tests
{
    using System.Globalization;
    using System.Numerics;
    using Xunit;

    /// <summary>
    /// Tests the <see cref="Matrix"/> struct.
    /// </summary>
    public class MatrixTests
    {

        [Fact]
        public void ImplicitCastMatrixToMatrix3x2()
        {
            Matrix matrix = new Matrix(1, 2, 3, 4, 5, 6);

            Matrix3x2 convertedMatrix = matrix;
            Assert.Equal(1, convertedMatrix.M11);
            Assert.Equal(2, convertedMatrix.M12);
            Assert.Equal(3, convertedMatrix.M21);
            Assert.Equal(4, convertedMatrix.M22);
            Assert.Equal(5, convertedMatrix.M31);
            Assert.Equal(6, convertedMatrix.M32);
        }

        [Fact]
        public void ImplicitCastMatrix3x2ToMatrix()
        {
            Matrix3x2 matrix = new Matrix3x2(1, 2, 3, 4, 5, 6);

            Matrix convertedMatrix = matrix;
            Assert.Equal(1, convertedMatrix.M11);
            Assert.Equal(2, convertedMatrix.M12);
            Assert.Equal(3, convertedMatrix.M21);
            Assert.Equal(4, convertedMatrix.M22);
            Assert.Equal(5, convertedMatrix.M31);
            Assert.Equal(6, convertedMatrix.M32);
        }

        /// matrix test mostly tken directly from CoreFX
        /// 
        static Matrix GenerateMatrixNumberFrom1To6()
        {
            Matrix a = new Matrix();
            a.M11 = 1.0f;
            a.M12 = 2.0f;
            a.M21 = 3.0f;
            a.M22 = 4.0f;
            a.M31 = 5.0f;
            a.M32 = 6.0f;
            return a;
        }

        static Matrix GenerateTestMatrix()
        {
            Matrix m = Matrix.CreateRotation(MathF.ToRadians(30.0f));
            m.Translation = new Vector2(111.0f, 222.0f);
            return m;
        }

        // A test for Identity
        [Fact]
        public void MatrixIdentityTest()
        {
            Matrix val = new Matrix();
            val.M11 = val.M22 = 1.0f;

            Assert.True(ApproximateFloatComparer.Equal(val, Matrix.Identity), "Matrix.Indentity was not set correctly.");
        }

        // A test for Determinant
        [Fact]
        public void MatrixDeterminantTest()
        {
            Matrix target = Matrix.CreateRotation(MathF.ToRadians(30.0f));

            float val = 1.0f;
            float det = target.GetDeterminant();

            Assert.True(ApproximateFloatComparer.Equal(val, det), "Matrix.Determinant was not set correctly.");
        }

        // A test for Determinant
        // Determinant test |A| = 1 / |A'|
        [Fact]
        public void MatrixDeterminantTest1()
        {
            Matrix a = new Matrix();
            a.M11 = 5.0f;
            a.M12 = 2.0f;
            a.M21 = 12.0f;
            a.M22 = 6.8f;
            a.M31 = 6.5f;
            a.M32 = 1.0f;
            Matrix i;
            Assert.True(Matrix.Invert(a, out i));

            float detA = a.GetDeterminant();
            float detI = i.GetDeterminant();
            float t = 1.0f / detI;

            // only accurate to 3 precision
            Assert.True(System.Math.Abs(detA - t) < 1e-3, "Matrix.Determinant was not set correctly.");

            // sanity check against 4x4 version
            Assert.Equal(new Matrix4x4(a).GetDeterminant(), detA);
            Assert.Equal(new Matrix4x4(i).GetDeterminant(), detI);
        }

        // A test for Invert (Matrix)
        [Fact]
        public void MatrixInvertTest()
        {
            Matrix mtx = Matrix.CreateRotation(MathF.ToRadians(30.0f));

            Matrix expected = new Matrix();
            expected.M11 = 0.8660254f;
            expected.M12 = -0.5f;

            expected.M21 = 0.5f;
            expected.M22 = 0.8660254f;

            expected.M31 = 0;
            expected.M32 = 0;

            Matrix actual;

            Assert.True(Matrix.Invert(mtx, out actual));
            Assert.True(ApproximateFloatComparer.Equal(expected, actual), "Matrix.Invert did not return the expected value.");

            Matrix i = mtx * actual;
            Assert.True(ApproximateFloatComparer.Equal(i, Matrix.Identity), "Matrix.Invert did not return the expected value.");
        }

        // A test for Invert (Matrix)
        [Fact]
        public void MatrixInvertIdentityTest()
        {
            Matrix mtx = Matrix.Identity;

            Matrix actual;
            Assert.True(Matrix.Invert(mtx, out actual));

            Assert.True(ApproximateFloatComparer.Equal(actual, Matrix.Identity));
        }

        // A test for Invert (Matrix)
        [Fact]
        public void MatrixInvertTranslationTest()
        {
            Matrix mtx = Matrix.CreateTranslation(23, 42);

            Matrix actual;
            Assert.True(Matrix.Invert(mtx, out actual));

            Matrix i = mtx * actual;
            Assert.True(ApproximateFloatComparer.Equal(i, Matrix.Identity));
        }

        // A test for Invert (Matrix)
        [Fact]
        public void MatrixInvertRotationTest()
        {
            Matrix mtx = Matrix.CreateRotation(2);

            Matrix actual;
            Assert.True(Matrix.Invert(mtx, out actual));

            Matrix i = mtx * actual;
            Assert.True(ApproximateFloatComparer.Equal(i, Matrix.Identity));
        }

        // A test for Invert (Matrix)
        [Fact]
        public void MatrixInvertScaleTest()
        {
            Matrix mtx = Matrix.CreateScale(23, -42);

            Matrix actual;
            Assert.True(Matrix.Invert(mtx, out actual));

            Matrix i = mtx * actual;
            Assert.True(ApproximateFloatComparer.Equal(i, Matrix.Identity));
        }

        // A test for Invert (Matrix)
        [Fact]
        public void MatrixInvertAffineTest()
        {
            Matrix mtx = Matrix.CreateRotation(2) *
                            Matrix.CreateScale(23, -42) *
                            Matrix.CreateTranslation(17, 53);

            Matrix actual;
            Assert.True(Matrix.Invert(mtx, out actual));

            Matrix i = mtx * actual;
            Assert.True(ApproximateFloatComparer.Equal(i, Matrix.Identity));
        }

        // A test for CreateRotation (float)
        [Fact]
        public void MatrixCreateRotationTest()
        {
            float radians = MathF.ToRadians(50.0f);

            Matrix expected = new Matrix();
            expected.M11 = 0.642787635f;
            expected.M12 = 0.766044438f;
            expected.M21 = -0.766044438f;
            expected.M22 = 0.642787635f;

            Matrix actual;
            actual = Matrix.CreateRotation(radians);
            Assert.True(ApproximateFloatComparer.Equal(expected, actual), "Matrix.CreateRotation did not return the expected value.");
        }

        // A test for CreateRotation (float, Vector2f)
        [Fact]
        public void MatrixCreateRotationCenterTest()
        {
            float radians = MathF.ToRadians(30.0f);
            Vector2 center = new Vector2(23, 42);

            Matrix rotateAroundZero = Matrix.CreateRotation(radians, Vector2.Zero);
            Matrix rotateAroundZeroExpected = Matrix.CreateRotation(radians);
            Assert.True(ApproximateFloatComparer.Equal(rotateAroundZero, rotateAroundZeroExpected));

            Matrix rotateAroundCenter = Matrix.CreateRotation(radians, center);
            Matrix rotateAroundCenterExpected = Matrix.CreateTranslation(-center) * Matrix.CreateRotation(radians) * Matrix.CreateTranslation(center);
            Assert.True(ApproximateFloatComparer.Equal(rotateAroundCenter, rotateAroundCenterExpected));
        }

        // A test for CreateRotation (float)
        [Fact]
        public void MatrixCreateRotationRightAngleTest()
        {
            // 90 degree rotations must be exact!
            Matrix actual = Matrix.CreateRotation(0);
            Assert.Equal(new Matrix(1, 0, 0, 1, 0, 0), actual);

            actual = Matrix.CreateRotation(MathF.PI / 2);
            Assert.Equal(new Matrix(0, 1, -1, 0, 0, 0), actual);

            actual = Matrix.CreateRotation(MathF.PI);
            Assert.Equal(new Matrix(-1, 0, 0, -1, 0, 0), actual);

            actual = Matrix.CreateRotation(MathF.PI * 3 / 2);
            Assert.Equal(new Matrix(0, -1, 1, 0, 0, 0), actual);

            actual = Matrix.CreateRotation(MathF.PI * 2);
            Assert.Equal(new Matrix(1, 0, 0, 1, 0, 0), actual);

            actual = Matrix.CreateRotation(MathF.PI * 5 / 2);
            Assert.Equal(new Matrix(0, 1, -1, 0, 0, 0), actual);

            actual = Matrix.CreateRotation(-MathF.PI / 2);
            Assert.Equal(new Matrix(0, -1, 1, 0, 0, 0), actual);

            // But merely close-to-90 rotations should not be excessively clamped.
            float delta = MathF.ToRadians(0.01f);

            actual = Matrix.CreateRotation(MathF.PI + delta);
            Assert.False(ApproximateFloatComparer.Equal(new Matrix(-1, 0, 0, -1, 0, 0), actual));

            actual = Matrix.CreateRotation(MathF.PI - delta);
            Assert.False(ApproximateFloatComparer.Equal(new Matrix(-1, 0, 0, -1, 0, 0), actual));
        }

        // A test for CreateRotation (float, Vector2f)
        [Fact]
        public void MatrixCreateRotationRightAngleCenterTest()
        {
            Vector2 center = new Vector2(3, 7);

            // 90 degree rotations must be exact!
            Matrix actual = Matrix.CreateRotation(0, center);
            Assert.Equal(new Matrix(1, 0, 0, 1, 0, 0), actual);

            actual = Matrix.CreateRotation(MathF.PI / 2, center);
            Assert.Equal(new Matrix(0, 1, -1, 0, 10, 4), actual);

            actual = Matrix.CreateRotation(MathF.PI, center);
            Assert.Equal(new Matrix(-1, 0, 0, -1, 6, 14), actual);

            actual = Matrix.CreateRotation(MathF.PI * 3 / 2, center);
            Assert.Equal(new Matrix(0, -1, 1, 0, -4, 10), actual);

            actual = Matrix.CreateRotation(MathF.PI * 2, center);
            Assert.Equal(new Matrix(1, 0, 0, 1, 0, 0), actual);

            actual = Matrix.CreateRotation(MathF.PI * 5 / 2, center);
            Assert.Equal(new Matrix(0, 1, -1, 0, 10, 4), actual);

            actual = Matrix.CreateRotation(-MathF.PI / 2, center);
            Assert.Equal(new Matrix(0, -1, 1, 0, -4, 10), actual);

            // But merely close-to-90 rotations should not be excessively clamped.
            float delta = MathF.ToRadians(0.01f);

            actual = Matrix.CreateRotation(MathF.PI + delta, center);
            Assert.False(ApproximateFloatComparer.Equal(new Matrix(-1, 0, 0, -1, 6, 14), actual));

            actual = Matrix.CreateRotation(MathF.PI - delta, center);
            Assert.False(ApproximateFloatComparer.Equal(new Matrix(-1, 0, 0, -1, 6, 14), actual));
        }

        // A test for Invert (Matrix)
        // Non invertible matrix - determinant is zero - singular matrix
        [Fact]
        public void MatrixInvertTest1()
        {
            Matrix a = new Matrix();
            a.M11 = 0.0f;
            a.M12 = 2.0f;
            a.M21 = 0.0f;
            a.M22 = 4.0f;
            a.M31 = 5.0f;
            a.M32 = 6.0f;

            float detA = a.GetDeterminant();
            Assert.True(ApproximateFloatComparer.Equal(detA, 0.0f), "Matrix.Invert did not return the expected value.");

            Matrix actual;
            Assert.False(Matrix.Invert(a, out actual));

            // all the elements in Actual is NaN
            Assert.True(
                float.IsNaN(actual.M11) && float.IsNaN(actual.M12) &&
                float.IsNaN(actual.M21) && float.IsNaN(actual.M22) &&
                float.IsNaN(actual.M31) && float.IsNaN(actual.M32)
                , "Matrix.Invert did not return the expected value.");
        }

        // A test for Lerp (Matrix, Matrix, float)
        [Fact]
        public void MatrixLerpTest()
        {
            Matrix a = new Matrix();
            a.M11 = 11.0f;
            a.M12 = 12.0f;
            a.M21 = 21.0f;
            a.M22 = 22.0f;
            a.M31 = 31.0f;
            a.M32 = 32.0f;

            Matrix b = GenerateMatrixNumberFrom1To6();

            float t = 0.5f;

            Matrix expected = new Matrix();
            expected.M11 = a.M11 + (b.M11 - a.M11) * t;
            expected.M12 = a.M12 + (b.M12 - a.M12) * t;

            expected.M21 = a.M21 + (b.M21 - a.M21) * t;
            expected.M22 = a.M22 + (b.M22 - a.M22) * t;

            expected.M31 = a.M31 + (b.M31 - a.M31) * t;
            expected.M32 = a.M32 + (b.M32 - a.M32) * t;

            Matrix actual;
            actual = Matrix.Lerp(a, b, t);
            Assert.True(ApproximateFloatComparer.Equal(expected, actual), "Matrix.Lerp did not return the expected value.");
        }

        // A test for operator - (Matrix)
        [Fact]
        public void MatrixUnaryNegationTest()
        {
            Matrix a = GenerateMatrixNumberFrom1To6();

            Matrix expected = new Matrix();
            expected.M11 = -1.0f;
            expected.M12 = -2.0f;
            expected.M21 = -3.0f;
            expected.M22 = -4.0f;
            expected.M31 = -5.0f;
            expected.M32 = -6.0f;

            Matrix actual = -a;
            Assert.True(ApproximateFloatComparer.Equal(expected, actual), "Matrix.operator - did not return the expected value.");
        }

        // A test for operator - (Matrix, Matrix)
        [Fact]
        public void MatrixSubtractionTest()
        {
            Matrix a = GenerateMatrixNumberFrom1To6();
            Matrix b = GenerateMatrixNumberFrom1To6();
            Matrix expected = new Matrix();

            Matrix actual = a - b;
            Assert.True(ApproximateFloatComparer.Equal(expected, actual), "Matrix.operator - did not return the expected value.");
        }

        // A test for operator * (Matrix, Matrix)
        [Fact]
        public void MatrixMultiplyTest1()
        {
            Matrix a = GenerateMatrixNumberFrom1To6();
            Matrix b = GenerateMatrixNumberFrom1To6();

            Matrix expected = new Matrix();
            expected.M11 = a.M11 * b.M11 + a.M12 * b.M21;
            expected.M12 = a.M11 * b.M12 + a.M12 * b.M22;

            expected.M21 = a.M21 * b.M11 + a.M22 * b.M21;
            expected.M22 = a.M21 * b.M12 + a.M22 * b.M22;

            expected.M31 = a.M31 * b.M11 + a.M32 * b.M21 + b.M31;
            expected.M32 = a.M31 * b.M12 + a.M32 * b.M22 + b.M32;

            Matrix actual = a * b;
            Assert.True(ApproximateFloatComparer.Equal(expected, actual), "Matrix.operator * did not return the expected value.");

            // Sanity check by comparison with 4x4 multiply.
            a = Matrix.CreateRotation(MathF.ToRadians(30)) * Matrix.CreateTranslation(23, 42);
            b = Matrix.CreateScale(3, 7) * Matrix.CreateTranslation(666, -1);

            actual = a * b;

            Matrix4x4 a44 = new Matrix4x4(a);
            Matrix4x4 b44 = new Matrix4x4(b);
            Matrix4x4 expected44 = a44 * b44;
            Matrix4x4 actual44 = new Matrix4x4(actual);

            Assert.True(ApproximateFloatComparer.Equal(expected44, actual44), "Matrix.operator * did not return the expected value.");
        }

        // A test for operator * (Matrix, Matrix)
        // Multiply with identity matrix
        [Fact]
        public void MatrixMultiplyTest4()
        {
            Matrix a = new Matrix();
            a.M11 = 1.0f;
            a.M12 = 2.0f;
            a.M21 = 5.0f;
            a.M22 = -6.0f;
            a.M31 = 9.0f;
            a.M32 = 10.0f;

            Matrix b = new Matrix();
            b = Matrix.Identity;

            Matrix expected = a;
            Matrix actual = a * b;

            Assert.True(ApproximateFloatComparer.Equal(expected, actual), "Matrix.operator * did not return the expected value.");
        }

        // A test for operator + (Matrix, Matrix)
        [Fact]
        public void MatrixAdditionTest()
        {
            Matrix a = GenerateMatrixNumberFrom1To6();
            Matrix b = GenerateMatrixNumberFrom1To6();

            Matrix expected = new Matrix();
            expected.M11 = a.M11 + b.M11;
            expected.M12 = a.M12 + b.M12;
            expected.M21 = a.M21 + b.M21;
            expected.M22 = a.M22 + b.M22;
            expected.M31 = a.M31 + b.M31;
            expected.M32 = a.M32 + b.M32;

            Matrix actual;

            actual = a + b;

            Assert.True(ApproximateFloatComparer.Equal(expected, actual), "Matrix.operator + did not return the expected value.");
        }

        // A test for ToString ()
        [Fact]
        public void MatrixToStringTest()
        {
            Matrix a = new Matrix();
            a.M11 = 11.0f;
            a.M12 = -12.0f;
            a.M21 = 21.0f;
            a.M22 = 22.0f;
            a.M31 = 31.0f;
            a.M32 = 32.0f;

            string expected = "{ {M11:11 M12:-12} " +
                                "{M21:21 M22:22} " +
                                "{M31:31 M32:32} }";
            string actual;

            actual = a.ToString();
            Assert.Equal(expected, actual);
        }

        // A test for Add (Matrix, Matrix)
        [Fact]
        public void MatrixAddTest()
        {
            Matrix a = GenerateMatrixNumberFrom1To6();
            Matrix b = GenerateMatrixNumberFrom1To6();

            Matrix expected = new Matrix();
            expected.M11 = a.M11 + b.M11;
            expected.M12 = a.M12 + b.M12;
            expected.M21 = a.M21 + b.M21;
            expected.M22 = a.M22 + b.M22;
            expected.M31 = a.M31 + b.M31;
            expected.M32 = a.M32 + b.M32;

            Matrix actual;

            actual = Matrix.Add(a, b);
            Assert.Equal(expected, actual);
        }

        // A test for Equals (object)
        [Fact]
        public void MatrixEqualsTest()
        {
            Matrix a = GenerateMatrixNumberFrom1To6();
            Matrix b = GenerateMatrixNumberFrom1To6();

            // case 1: compare between same values
            object obj = b;

            bool expected = true;
            bool actual = a.Equals(obj);
            Assert.Equal(expected, actual);

            // case 2: compare between different values
            b.M11 = 11.0f;
            obj = b;
            expected = false;
            actual = a.Equals(obj);
            Assert.Equal(expected, actual);

            // case 3: compare between different types.
            obj = new Vector4();
            expected = false;
            actual = a.Equals(obj);
            Assert.Equal(expected, actual);

            // case 3: compare against null.
            obj = null;
            expected = false;
            actual = a.Equals(obj);
            Assert.Equal(expected, actual);
        }

        // A test for GetHashCode ()
        [Fact]
        public void MatrixGetHashCodeTest()
        {
            Matrix target = GenerateMatrixNumberFrom1To6();
            int expected = unchecked(target.M11.GetHashCode() + target.M12.GetHashCode() +
                                     target.M21.GetHashCode() + target.M22.GetHashCode() +
                                     target.M31.GetHashCode() + target.M32.GetHashCode());
            int actual;

            actual = target.GetHashCode();
            Assert.Equal(expected, actual);
        }

        // A test for Multiply (Matrix, Matrix)
        [Fact]
        public void MatrixMultiplyTest3()
        {
            Matrix a = GenerateMatrixNumberFrom1To6();
            Matrix b = GenerateMatrixNumberFrom1To6();

            Matrix expected = new Matrix();
            expected.M11 = a.M11 * b.M11 + a.M12 * b.M21;
            expected.M12 = a.M11 * b.M12 + a.M12 * b.M22;

            expected.M21 = a.M21 * b.M11 + a.M22 * b.M21;
            expected.M22 = a.M21 * b.M12 + a.M22 * b.M22;

            expected.M31 = a.M31 * b.M11 + a.M32 * b.M21 + b.M31;
            expected.M32 = a.M31 * b.M12 + a.M32 * b.M22 + b.M32;
            Matrix actual;
            actual = Matrix.Multiply(a, b);

            Assert.Equal(expected, actual);

            // Sanity check by comparison with 4x4 multiply.
            a = Matrix.CreateRotation(MathF.ToRadians(30)) * Matrix.CreateTranslation(23, 42);
            b = Matrix.CreateScale(3, 7) * Matrix.CreateTranslation(666, -1);

            actual = Matrix.Multiply(a, b);

            Matrix4x4 a44 = new Matrix4x4(a);
            Matrix4x4 b44 = new Matrix4x4(b);
            Matrix4x4 expected44 = Matrix4x4.Multiply(a44, b44);
            Matrix4x4 actual44 = new Matrix4x4(actual);

            Assert.True(ApproximateFloatComparer.Equal(expected44, actual44), "Matrix.Multiply did not return the expected value.");
        }

        // A test for Multiply (Matrix, float)
        [Fact]
        public void MatrixMultiplyTest5()
        {
            Matrix a = GenerateMatrixNumberFrom1To6();
            Matrix expected = new Matrix(3, 6, 9, 12, 15, 18);
            Matrix actual = Matrix.Multiply(a, 3);

            Assert.Equal(expected, actual);
        }

        // A test for Multiply (Matrix, float)
        [Fact]
        public void MatrixMultiplyTest6()
        {
            Matrix a = GenerateMatrixNumberFrom1To6();
            Matrix expected = new Matrix(3, 6, 9, 12, 15, 18);
            Matrix actual = a * 3;

            Assert.Equal(expected, actual);
        }

        // A test for Negate (Matrix)
        [Fact]
        public void MatrixNegateTest()
        {
            Matrix m = GenerateMatrixNumberFrom1To6();

            Matrix expected = new Matrix();
            expected.M11 = -1.0f;
            expected.M12 = -2.0f;
            expected.M21 = -3.0f;
            expected.M22 = -4.0f;
            expected.M31 = -5.0f;
            expected.M32 = -6.0f;
            Matrix actual;

            actual = Matrix.Negate(m);
            Assert.Equal(expected, actual);
        }

        // A test for operator != (Matrix, Matrix)
        [Fact]
        public void MatrixInequalityTest()
        {
            Matrix a = GenerateMatrixNumberFrom1To6();
            Matrix b = GenerateMatrixNumberFrom1To6();

            // case 1: compare between same values
            bool expected = false;
            bool actual = a != b;
            Assert.Equal(expected, actual);

            // case 2: compare between different values
            b.M11 = 11.0f;
            expected = true;
            actual = a != b;
            Assert.Equal(expected, actual);
        }

        // A test for operator == (Matrix, Matrix)
        [Fact]
        public void MatrixEqualityTest()
        {
            Matrix a = GenerateMatrixNumberFrom1To6();
            Matrix b = GenerateMatrixNumberFrom1To6();

            // case 1: compare between same values
            bool expected = true;
            bool actual = a == b;
            Assert.Equal(expected, actual);

            // case 2: compare between different values
            b.M11 = 11.0f;
            expected = false;
            actual = a == b;
            Assert.Equal(expected, actual);
        }

        // A test for Subtract (Matrix, Matrix)
        [Fact]
        public void MatrixSubtractTest()
        {
            Matrix a = GenerateMatrixNumberFrom1To6();
            Matrix b = GenerateMatrixNumberFrom1To6();
            Matrix expected = new Matrix();
            Matrix actual;

            actual = Matrix.Subtract(a, b);
            Assert.Equal(expected, actual);
        }

        // A test for CreateScale (Vector2f)
        [Fact]
        public void MatrixCreateScaleTest1()
        {
            SizeF scales = new SizeF(2.0f, 3.0f);
            Matrix expected = new Matrix(
                2.0f, 0.0f,
                0.0f, 3.0f,
                0.0f, 0.0f);
            Matrix actual = Matrix.CreateScale(scales);
            Assert.Equal(expected, actual);
        }

        // A test for CreateScale (Vector2f, Vector2f)
        [Fact]
        public void MatrixCreateScaleCenterTest1()
        {
            SizeF scale = new SizeF(3, 4);
            PointF center = new PointF(23, 42);

            Matrix scaleAroundZero = Matrix.CreateScale(scale, PointF.Zero);
            Matrix scaleAroundZeroExpected = Matrix.CreateScale(scale);
            Assert.True(ApproximateFloatComparer.Equal(scaleAroundZero, scaleAroundZeroExpected));

            Matrix scaleAroundCenter = Matrix.CreateScale(scale, center);
            Matrix scaleAroundCenterExpected = Matrix.CreateTranslation(-center) * Matrix.CreateScale(scale) * Matrix.CreateTranslation(center);
            Assert.True(ApproximateFloatComparer.Equal(scaleAroundCenter, scaleAroundCenterExpected));
        }

        // A test for CreateScale (float)
        [Fact]
        public void MatrixCreateScaleTest2()
        {
            float scale = 2.0f;
            Matrix expected = new Matrix(
                2.0f, 0.0f,
                0.0f, 2.0f,
                0.0f, 0.0f);
            Matrix actual = Matrix.CreateScale(scale);
            Assert.Equal(expected, actual);
        }

        // A test for CreateScale (float, Vector2f)
        [Fact]
        public void MatrixCreateScaleCenterTest2()
        {
            float scale = 5;
            PointF center = new PointF(23, 42);

            Matrix scaleAroundZero = Matrix.CreateScale(scale, PointF.Zero);
            Matrix scaleAroundZeroExpected = Matrix.CreateScale(scale);
            Assert.True(ApproximateFloatComparer.Equal(scaleAroundZero, scaleAroundZeroExpected));

            Matrix scaleAroundCenter = Matrix.CreateScale(scale, center);
            Matrix scaleAroundCenterExpected = Matrix.CreateTranslation(-center) * Matrix.CreateScale(scale) * Matrix.CreateTranslation(center);
            Assert.True(ApproximateFloatComparer.Equal(scaleAroundCenter, scaleAroundCenterExpected));
        }

        // A test for CreateScale (float, float)
        [Fact]
        public void MatrixCreateScaleTest3()
        {
            float xScale = 2.0f;
            float yScale = 3.0f;
            Matrix expected = new Matrix(
                2.0f, 0.0f,
                0.0f, 3.0f,
                0.0f, 0.0f);
            Matrix actual = Matrix.CreateScale(xScale, yScale);
            Assert.Equal(expected, actual);
        }

        // A test for CreateScale (float, float, Vector2f)
        [Fact]
        public void MatrixCreateScaleCenterTest3()
        {
            SizeF scale = new SizeF(3, 4);
            PointF center = new PointF(23, 42);

            Matrix scaleAroundZero = Matrix.CreateScale(scale.Width, scale.Height, Vector2.Zero);
            Matrix scaleAroundZeroExpected = Matrix.CreateScale(scale.Width, scale.Height);
            Assert.True(ApproximateFloatComparer.Equal(scaleAroundZero, scaleAroundZeroExpected));

            Matrix scaleAroundCenter = Matrix.CreateScale(scale.Width, scale.Height, center);
            Matrix scaleAroundCenterExpected = Matrix.CreateTranslation(-center) * Matrix.CreateScale(scale.Width, scale.Height) * Matrix.CreateTranslation(center);
            Assert.True(ApproximateFloatComparer.Equal(scaleAroundCenter, scaleAroundCenterExpected));
        }

        // A test for CreateTranslation (Vector2f)
        [Fact]
        public void MatrixCreateTranslationTest1()
        {
            PointF position = new PointF(2.0f, 3.0f);
            Matrix expected = new Matrix(
                1.0f, 0.0f,
                0.0f, 1.0f,
                2.0f, 3.0f);

            Matrix actual = Matrix.CreateTranslation(position);
            Assert.Equal(expected, actual);
        }

        // A test for CreateTranslation (float, float)
        [Fact]
        public void MatrixCreateTranslationTest2()
        {
            float xPosition = 2.0f;
            float yPosition = 3.0f;

            Matrix expected = new Matrix(
                1.0f, 0.0f,
                0.0f, 1.0f,
                2.0f, 3.0f);

            Matrix actual = Matrix.CreateTranslation(xPosition, yPosition);
            Assert.Equal(expected, actual);
        }

        // A test for Translation
        [Fact]
        public void MatrixTranslationTest()
        {
            Matrix a = GenerateTestMatrix();
            Matrix b = a;

            // Transformed vector that has same semantics of property must be same.
            PointF val = new PointF(a.M31, a.M32);
            Assert.Equal(val, a.Translation);

            // Set value and get value must be same.
            val = new PointF(1.0f, 2.0f);
            a.Translation = val;
            Assert.Equal(val, a.Translation);

            // Make sure it only modifies expected value of matrix.
            Assert.True(
                a.M11 == b.M11 && a.M12 == b.M12 &&
                a.M21 == b.M21 && a.M22 == b.M22 &&
                a.M31 != b.M31 && a.M32 != b.M32,
                "Matrix.Translation modified unexpected value of matrix.");
        }

        // A test for Equals (Matrix)
        [Fact]
        public void MatrixEqualsTest1()
        {
            Matrix a = GenerateMatrixNumberFrom1To6();
            Matrix b = GenerateMatrixNumberFrom1To6();

            // case 1: compare between same values
            bool expected = true;
            bool actual = a.Equals(b);
            Assert.Equal(expected, actual);

            // case 2: compare between different values
            b.M11 = 11.0f;
            expected = false;
            actual = a.Equals(b);
            Assert.Equal(expected, actual);
        }

        // A test for CreateSkew (float, float)
        [Fact]
        public void MatrixCreateSkewIdentityTest()
        {
            Matrix expected = Matrix.Identity;
            Matrix actual = Matrix.CreateSkew(0, 0);
            Assert.Equal(expected, actual);
        }

        // A test for CreateSkew (float, float)
        [Fact]
        public void MatrixCreateSkewXTest()
        {
            Matrix expected = new Matrix(1, 0, -0.414213562373095f, 1, 0, 0);
            Matrix actual = Matrix.CreateSkew(-MathF.PI / 8, 0);
            Assert.True(ApproximateFloatComparer.Equal(expected, actual));

            expected = new Matrix(1, 0, 0.414213562373095f, 1, 0, 0);
            actual = Matrix.CreateSkew(MathF.PI / 8, 0);
            Assert.True(ApproximateFloatComparer.Equal(expected, actual));

            PointF result = PointF.Transform(new PointF(0, 0), actual);
            Assert.True(ApproximateFloatComparer.Equal(new PointF(0, 0), result));

            result = PointF.Transform(new Vector2(0, 1), actual);
            Assert.True(ApproximateFloatComparer.Equal(new PointF(0.414213568f, 1), result));
            result = PointF.Transform(new PointF(0, -1), actual);
            Assert.True(ApproximateFloatComparer.Equal(new PointF(-0.414213568f, -1), result));

            result = PointF.Transform(new PointF(3, 10), actual);
            Assert.True(ApproximateFloatComparer.Equal(new PointF(7.14213568f, 10), result));
        }

        // A test for CreateSkew (float, float)
        [Fact]
        public void MatrixCreateSkewYTest()
        {
            Matrix expected = new Matrix(1, -0.414213562373095f, 0, 1, 0, 0);
            Matrix actual = Matrix.CreateSkew(0, -MathF.PI / 8);
            Assert.True(ApproximateFloatComparer.Equal(expected, actual));

            expected = new Matrix(1, 0.414213562373095f, 0, 1, 0, 0);
            actual = Matrix.CreateSkew(0, MathF.PI / 8);
            Assert.True(ApproximateFloatComparer.Equal(expected, actual));

            Vector2 result = Vector2.Transform(new Vector2(0, 0), actual);
            Assert.True(ApproximateFloatComparer.Equal(new Vector2(0, 0), result));

            result = Vector2.Transform(new Vector2(1, 0), actual);
            Assert.True(ApproximateFloatComparer.Equal(new Vector2(1, 0.414213568f), result));

            result = Vector2.Transform(new Vector2(-1, 0), actual);
            Assert.True(ApproximateFloatComparer.Equal(new Vector2(-1, -0.414213568f), result));

            result = Vector2.Transform(new Vector2(10, 3), actual);
            Assert.True(ApproximateFloatComparer.Equal(new Vector2(10, 7.14213568f), result));
        }

        // A test for CreateSkew (float, float)
        [Fact]
        public void MatrixCreateSkewXYTest()
        {
            Matrix expected = new Matrix(1, -0.414213562373095f, 1, 1, 0, 0);
            Matrix actual = Matrix.CreateSkew(MathF.PI / 4, -MathF.PI / 8);
            Assert.True(ApproximateFloatComparer.Equal(expected, actual));

            Vector2 result = Vector2.Transform(new Vector2(0, 0), actual);
            Assert.True(ApproximateFloatComparer.Equal(new Vector2(0, 0), result));

            result = Vector2.Transform(new Vector2(1, 0), actual);
            Assert.True(ApproximateFloatComparer.Equal(new Vector2(1, -0.414213562373095f), result));

            result = Vector2.Transform(new Vector2(0, 1), actual);
            Assert.True(ApproximateFloatComparer.Equal(new Vector2(1, 1), result));

            result = Vector2.Transform(new Vector2(1, 1), actual);
            Assert.True(ApproximateFloatComparer.Equal(new Vector2(2, 0.585786437626905f), result));
        }

        // A test for CreateSkew (float, float, Vector2f)
        [Fact]
        public void MatrixCreateSkewCenterTest()
        {
            float skewX = 1, skewY = 2;
            Vector2 center = new Vector2(23, 42);

            Matrix skewAroundZero = Matrix.CreateSkew(skewX, skewY, Vector2.Zero);
            Matrix skewAroundZeroExpected = Matrix.CreateSkew(skewX, skewY);
            Assert.True(ApproximateFloatComparer.Equal(skewAroundZero, skewAroundZeroExpected));

            Matrix skewAroundCenter = Matrix.CreateSkew(skewX, skewY, center);
            Matrix skewAroundCenterExpected = Matrix.CreateTranslation(-center) * Matrix.CreateSkew(skewX, skewY) * Matrix.CreateTranslation(center);
            Assert.True(ApproximateFloatComparer.Equal(skewAroundCenter, skewAroundCenterExpected));
        }

        // A test for IsIdentity
        [Fact]
        public void MatrixIsIdentityTest()
        {
            Assert.True(Matrix.Identity.IsIdentity);
            Assert.True(new Matrix(1, 0, 0, 1, 0, 0).IsIdentity);
            Assert.False(new Matrix(0, 0, 0, 1, 0, 0).IsIdentity);
            Assert.False(new Matrix(1, 1, 0, 1, 0, 0).IsIdentity);
            Assert.False(new Matrix(1, 0, 1, 1, 0, 0).IsIdentity);
            Assert.False(new Matrix(1, 0, 0, 0, 0, 0).IsIdentity);
            Assert.False(new Matrix(1, 0, 0, 1, 1, 0).IsIdentity);
            Assert.False(new Matrix(1, 0, 0, 1, 0, 1).IsIdentity);
        }

        // A test for Matrix comparison involving NaN values
        [Fact]
        public void MatrixEqualsNanTest()
        {
            Matrix a = new Matrix(float.NaN, 0, 0, 0, 0, 0);
            Matrix b = new Matrix(0, float.NaN, 0, 0, 0, 0);
            Matrix c = new Matrix(0, 0, float.NaN, 0, 0, 0);
            Matrix d = new Matrix(0, 0, 0, float.NaN, 0, 0);
            Matrix e = new Matrix(0, 0, 0, 0, float.NaN, 0);
            Matrix f = new Matrix(0, 0, 0, 0, 0, float.NaN);

            Assert.False(a == new Matrix());
            Assert.False(b == new Matrix());
            Assert.False(c == new Matrix());
            Assert.False(d == new Matrix());
            Assert.False(e == new Matrix());
            Assert.False(f == new Matrix());

            Assert.True(a != new Matrix());
            Assert.True(b != new Matrix());
            Assert.True(c != new Matrix());
            Assert.True(d != new Matrix());
            Assert.True(e != new Matrix());
            Assert.True(f != new Matrix());

            Assert.False(a.Equals(new Matrix()));
            Assert.False(b.Equals(new Matrix()));
            Assert.False(c.Equals(new Matrix()));
            Assert.False(d.Equals(new Matrix()));
            Assert.False(e.Equals(new Matrix()));
            Assert.False(f.Equals(new Matrix()));

            Assert.False(a.IsIdentity);
            Assert.False(b.IsIdentity);
            Assert.False(c.IsIdentity);
            Assert.False(d.IsIdentity);
            Assert.False(e.IsIdentity);
            Assert.False(f.IsIdentity);

            // Counterintuitive result - IEEE rules for NaN comparison are weird!
            Assert.False(a.Equals(a));
            Assert.False(b.Equals(b));
            Assert.False(c.Equals(c));
            Assert.False(d.Equals(d));
            Assert.False(e.Equals(e));
            Assert.False(f.Equals(f));
        }
    }
}