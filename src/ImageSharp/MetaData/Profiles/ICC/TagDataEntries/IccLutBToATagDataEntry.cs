// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Linq;
using System.Numerics;

namespace SixLabors.ImageSharp.MetaData.Profiles.Icc
{
    /// <summary>
    /// This structure represents a color transform.
    /// </summary>
    internal sealed class IccLutBToATagDataEntry : IccTagDataEntry, IEquatable<IccLutBToATagDataEntry>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="IccLutBToATagDataEntry"/> class.
        /// </summary>
        /// <param name="curveA">A Curve</param>
        /// <param name="clutValues">CLUT</param>
        /// <param name="curveM">M Curve</param>
        /// <param name="matrix3x3">Two dimensional conversion matrix (3x3)</param>
        /// <param name="matrix3x1">One dimensional conversion matrix (3x1)</param>
        /// <param name="curveB">B Curve</param>
        public IccLutBToATagDataEntry(
            IccTagDataEntry[] curveB,
            float[,] matrix3x3,
            float[] matrix3x1,
            IccTagDataEntry[] curveM,
            IccClut clutValues,
            IccTagDataEntry[] curveA)
            : this(curveB, matrix3x3, matrix3x1, curveM, clutValues, curveA, IccProfileTag.Unknown)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IccLutBToATagDataEntry"/> class.
        /// </summary>
        /// <param name="curveA">A Curve</param>
        /// <param name="clutValues">CLUT</param>
        /// <param name="curveM">M Curve</param>
        /// <param name="matrix3x3">Two dimensional conversion matrix (3x3)</param>
        /// <param name="matrix3x1">One dimensional conversion matrix (3x1)</param>
        /// <param name="curveB">B Curve</param>
        /// <param name="tagSignature">Tag Signature</param>
        public IccLutBToATagDataEntry(
            IccTagDataEntry[] curveB,
            float[,] matrix3x3,
            float[] matrix3x1,
            IccTagDataEntry[] curveM,
            IccClut clutValues,
            IccTagDataEntry[] curveA,
            IccProfileTag tagSignature)
            : base(IccTypeSignature.LutBToA, tagSignature)
        {
            this.VerifyMatrix(matrix3x3, matrix3x1);
            this.VerifyCurve(curveA, nameof(curveA));
            this.VerifyCurve(curveB, nameof(curveB));
            this.VerifyCurve(curveM, nameof(curveM));

            this.Matrix3x3 = this.CreateMatrix3x3(matrix3x3);
            this.Matrix3x1 = this.CreateMatrix3x1(matrix3x1);
            this.CurveA = curveA;
            this.CurveB = curveB;
            this.CurveM = curveM;
            this.ClutValues = clutValues;

            if (this.IsBMatrixMClutA())
            {
                Guard.IsTrue(this.CurveB.Length == 3, nameof(this.CurveB), $"{nameof(this.CurveB)} must have a length of three");
                Guard.IsTrue(this.CurveM.Length == 3, nameof(this.CurveM), $"{nameof(this.CurveM)} must have a length of three");
                Guard.MustBeBetweenOrEqualTo(this.CurveA.Length, 1, 15, nameof(this.CurveA));

                this.InputChannelCount = 3;
                this.OutputChannelCount = curveA.Length;

                Guard.IsTrue(this.InputChannelCount == clutValues.InputChannelCount, nameof(clutValues), "Input channel count does not match the CLUT size");
                Guard.IsTrue(this.OutputChannelCount == clutValues.OutputChannelCount, nameof(clutValues), "Output channel count does not match the CLUT size");
            }
            else if (this.IsBMatrixM())
            {
                Guard.IsTrue(this.CurveB.Length == 3, nameof(this.CurveB), $"{nameof(this.CurveB)} must have a length of three");
                Guard.IsTrue(this.CurveM.Length == 3, nameof(this.CurveM), $"{nameof(this.CurveM)} must have a length of three");

                this.InputChannelCount = this.OutputChannelCount = 3;
            }
            else if (this.IsBClutA())
            {
                Guard.MustBeBetweenOrEqualTo(this.CurveA.Length, 1, 15, nameof(this.CurveA));
                Guard.MustBeBetweenOrEqualTo(this.CurveB.Length, 1, 15, nameof(this.CurveB));

                this.InputChannelCount = curveB.Length;
                this.OutputChannelCount = curveA.Length;

                Guard.IsTrue(this.InputChannelCount == clutValues.InputChannelCount, nameof(clutValues), "Input channel count does not match the CLUT size");
                Guard.IsTrue(this.OutputChannelCount == clutValues.OutputChannelCount, nameof(clutValues), "Output channel count does not match the CLUT size");
            }
            else if (this.IsB())
            {
                this.InputChannelCount = this.OutputChannelCount = this.CurveB.Length;
            }
            else
            {
                throw new ArgumentException("Invalid combination of values given");
            }
        }

        /// <summary>
        /// Gets the number of input channels
        /// </summary>
        public int InputChannelCount { get; }

        /// <summary>
        /// Gets the number of output channels
        /// </summary>
        public int OutputChannelCount { get; }

        /// <summary>
        /// Gets the two dimensional conversion matrix (3x3)
        /// </summary>
        public Matrix4x4? Matrix3x3 { get; }

        /// <summary>
        /// Gets the one dimensional conversion matrix (3x1)
        /// </summary>
        public Vector3? Matrix3x1 { get; }

        /// <summary>
        /// Gets the color lookup table
        /// </summary>
        public IccClut ClutValues { get; }

        /// <summary>
        /// Gets the B Curve
        /// </summary>
        public IccTagDataEntry[] CurveB { get; }

        /// <summary>
        /// Gets the M Curve
        /// </summary>
        public IccTagDataEntry[] CurveM { get; }

        /// <summary>
        /// Gets the A Curve
        /// </summary>
        public IccTagDataEntry[] CurveA { get; }

        /// <inheritdoc/>
        public override bool Equals(IccTagDataEntry other)
        {
            return other is IccLutBToATagDataEntry entry && this.Equals(entry);
        }

        /// <inheritdoc/>
        public bool Equals(IccLutBToATagDataEntry other)
        {
            if (other is null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return base.Equals(other)
                && this.InputChannelCount == other.InputChannelCount
                && this.OutputChannelCount == other.OutputChannelCount
                && this.Matrix3x3.Equals(other.Matrix3x3)
                && this.Matrix3x1.Equals(other.Matrix3x1)
                && this.ClutValues.Equals(other.ClutValues)
                && this.EqualsCurve(this.CurveB, other.CurveB)
                && this.EqualsCurve(this.CurveM, other.CurveM)
                && this.EqualsCurve(this.CurveA, other.CurveA);
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return obj is IccLutBToATagDataEntry other && this.Equals(other);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = base.GetHashCode();
                hashCode = (hashCode * 397) ^ this.InputChannelCount;
                hashCode = (hashCode * 397) ^ this.OutputChannelCount;
                hashCode = (hashCode * 397) ^ this.Matrix3x3.GetHashCode();
                hashCode = (hashCode * 397) ^ this.Matrix3x1.GetHashCode();
                hashCode = (hashCode * 397) ^ (this.ClutValues?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ (this.CurveB?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ (this.CurveM?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ (this.CurveA?.GetHashCode() ?? 0);
                return hashCode;
            }
        }

        private bool EqualsCurve(IccTagDataEntry[] thisCurves, IccTagDataEntry[] entryCurves)
        {
            bool thisNull = thisCurves is null;
            bool entryNull = entryCurves is null;

            if (thisNull && entryNull)
            {
                return true;
            }

            if (entryNull)
            {
                return false;
            }

            return thisCurves.SequenceEqual(entryCurves);
        }

        private bool IsBMatrixMClutA()
        {
            return this.CurveB != null
                && this.Matrix3x3 != null
                && this.Matrix3x1 != null
                && this.CurveM != null
                && this.ClutValues != null
                && this.CurveA != null;
        }

        private bool IsBMatrixM()
        {
            return this.CurveB != null
                && this.Matrix3x3 != null
                && this.Matrix3x1 != null
                && this.CurveM != null;
        }

        private bool IsBClutA()
        {
            return this.CurveB != null
                && this.ClutValues != null
                && this.CurveA != null;
        }

        private bool IsB()
        {
            return this.CurveB != null;
        }

        private void VerifyCurve(IccTagDataEntry[] curves, string name)
        {
            if (curves != null)
            {
                bool isNotCurve = curves.Any(t => !(t is IccParametricCurveTagDataEntry) && !(t is IccCurveTagDataEntry));
                Guard.IsFalse(isNotCurve, nameof(name), $"{nameof(name)} must be of type {nameof(IccParametricCurveTagDataEntry)} or {nameof(IccCurveTagDataEntry)}");
            }
        }

        private void VerifyMatrix(float[,] matrix3x3, float[] matrix3x1)
        {
            if (matrix3x1 != null)
            {
                Guard.IsTrue(matrix3x1.Length == 3, nameof(matrix3x1), "Matrix must have a size of three");
            }

            if (matrix3x3 != null)
            {
                bool is3By3 = matrix3x3.GetLength(0) == 3 && matrix3x3.GetLength(1) == 3;
                Guard.IsTrue(is3By3, nameof(matrix3x3), "Matrix must have a size of three by three");
            }
        }

        private Vector3? CreateMatrix3x1(float[] matrix)
        {
            if (matrix is null)
            {
                return null;
            }

            return new Vector3(matrix[0], matrix[1], matrix[2]);
        }

        private Matrix4x4? CreateMatrix3x3(float[,] matrix)
        {
            if (matrix is null)
            {
                return null;
            }

            return new Matrix4x4(
                matrix[0, 0],
                matrix[0, 1],
                matrix[0, 2],
                0,
                matrix[1, 0],
                matrix[1, 1],
                matrix[1, 2],
                0,
                matrix[2, 0],
                matrix[2, 1],
                matrix[2, 2],
                0,
                0,
                0,
                0,
                1);
        }
    }
}
