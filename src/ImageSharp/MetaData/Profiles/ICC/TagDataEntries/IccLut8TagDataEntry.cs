// <copyright file="IccLut8TagDataEntry.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;
    using System.Linq;
    using System.Numerics;

    /// <summary>
    /// This structure represents a color transform using tables
    /// with 8-bit precision.
    /// </summary>
    internal sealed class IccLut8TagDataEntry : IccTagDataEntry, IEquatable<IccLut8TagDataEntry>
    {
        private static readonly float[,] IdentityMatrix = { { 1, 0, 0 }, { 0, 1, 0 }, { 0, 0, 1 } };

        /// <summary>
        /// Initializes a new instance of the <see cref="IccLut8TagDataEntry"/> class.
        /// </summary>
        /// <param name="inputValues">Input LUT</param>
        /// <param name="clutValues">CLUT</param>
        /// <param name="outputValues">Output LUT</param>
        public IccLut8TagDataEntry(IccLut[] inputValues, IccClut clutValues, IccLut[] outputValues)
            : this(IdentityMatrix, inputValues, clutValues, outputValues, IccProfileTag.Unknown)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IccLut8TagDataEntry"/> class.
        /// </summary>
        /// <param name="inputValues">Input LUT</param>
        /// <param name="clutValues">CLUT</param>
        /// <param name="outputValues">Output LUT</param>
        /// <param name="tagSignature">Tag Signature</param>
        public IccLut8TagDataEntry(IccLut[] inputValues, IccClut clutValues, IccLut[] outputValues, IccProfileTag tagSignature)
            : this(IdentityMatrix, inputValues, clutValues, outputValues, tagSignature)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IccLut8TagDataEntry"/> class.
        /// </summary>
        /// <param name="matrix">Conversion matrix (must be 3x3)</param>
        /// <param name="inputValues">Input LUT</param>
        /// <param name="clutValues">CLUT</param>
        /// <param name="outputValues">Output LUT</param>
        public IccLut8TagDataEntry(float[,] matrix, IccLut[] inputValues, IccClut clutValues, IccLut[] outputValues)
            : this(matrix, inputValues, clutValues, outputValues, IccProfileTag.Unknown)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IccLut8TagDataEntry"/> class.
        /// </summary>
        /// <param name="matrix">Conversion matrix (must be 3x3)</param>
        /// <param name="inputValues">Input LUT</param>
        /// <param name="clutValues">CLUT</param>
        /// <param name="outputValues">Output LUT</param>
        /// <param name="tagSignature">Tag Signature</param>
        public IccLut8TagDataEntry(float[,] matrix, IccLut[] inputValues, IccClut clutValues, IccLut[] outputValues, IccProfileTag tagSignature)
            : base(IccTypeSignature.Lut8, tagSignature)
        {
            Guard.NotNull(matrix, nameof(matrix));
            Guard.NotNull(inputValues, nameof(inputValues));
            Guard.NotNull(clutValues, nameof(clutValues));
            Guard.NotNull(outputValues, nameof(outputValues));

            bool is3By3 = matrix.GetLength(0) == 3 && matrix.GetLength(1) == 3;
            Guard.IsTrue(is3By3, nameof(matrix), "Matrix must have a size of three by three");

            this.Matrix = this.CreateMatrix(matrix);
            this.InputValues = inputValues;
            this.ClutValues = clutValues;
            this.OutputValues = outputValues;

            Guard.IsTrue(this.InputChannelCount == clutValues.InputChannelCount, nameof(clutValues), "Input channel count does not match the CLUT size");
            Guard.IsTrue(this.OutputChannelCount == clutValues.OutputChannelCount, nameof(clutValues), "Output channel count does not match the CLUT size");

            Guard.IsFalse(inputValues.Any(t => t.Values.Length != 256), nameof(inputValues), "Input lookup table has to have a length of 256");
            Guard.IsFalse(outputValues.Any(t => t.Values.Length != 256), nameof(outputValues), "Output lookup table has to have a length of 256");
        }

        /// <summary>
        /// Gets the number of input channels
        /// </summary>
        public int InputChannelCount => this.InputValues.Length;

        /// <summary>
        /// Gets the number of output channels
        /// </summary>
        public int OutputChannelCount => this.OutputValues.Length;

        /// <summary>
        ///  Gets the conversion matrix
        /// </summary>
        public Matrix4x4 Matrix { get; }

        /// <summary>
        /// Gets the input lookup table
        /// </summary>
        public IccLut[] InputValues { get; }

        /// <summary>
        /// Gets the color lookup table
        /// </summary>
        public IccClut ClutValues { get; }

        /// <summary>
        /// Gets the output lookup table
        /// </summary>
        public IccLut[] OutputValues { get; }

        /// <inheritdoc/>
        public override bool Equals(IccTagDataEntry other)
        {
            var entry = other as IccLut8TagDataEntry;
            return entry != null && this.Equals(entry);
        }

        /// <inheritdoc/>
        public bool Equals(IccLut8TagDataEntry other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return base.Equals(other)
                && this.Matrix.Equals(other.Matrix)
                && this.InputValues.SequenceEqual(other.InputValues)
                && this.ClutValues.Equals(other.ClutValues)
                && this.OutputValues.SequenceEqual(other.OutputValues);
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            return obj is IccLut8TagDataEntry && this.Equals((IccLut8TagDataEntry)obj);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = base.GetHashCode();
                hashCode = (hashCode * 397) ^ this.Matrix.GetHashCode();
                hashCode = (hashCode * 397) ^ (this.InputValues != null ? this.InputValues.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (this.ClutValues != null ? this.ClutValues.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (this.OutputValues != null ? this.OutputValues.GetHashCode() : 0);
                return hashCode;
            }
        }

        private Matrix4x4 CreateMatrix(float[,] matrix)
        {
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
