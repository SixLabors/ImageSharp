// <copyright file="IccLut16TagDataEntry.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System.Linq;
    using System.Numerics;

    /// <summary>
    /// This structure represents a color transform using tables
    /// with 16-bit precision.
    /// </summary>
    internal sealed class IccLut16TagDataEntry : IccTagDataEntry
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="IccLut16TagDataEntry"/> class.
        /// </summary>
        /// <param name="inputValues">Input LUT</param>
        /// <param name="clutValues">CLUT</param>
        /// <param name="outputValues">Output LUT</param>
        public IccLut16TagDataEntry(IccLut[] inputValues, IccClut clutValues, IccLut[] outputValues)
            : this(null, inputValues, clutValues, outputValues, IccProfileTag.Unknown)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IccLut16TagDataEntry"/> class.
        /// </summary>
        /// <param name="inputValues">Input LUT</param>
        /// <param name="clutValues">CLUT</param>
        /// <param name="outputValues">Output LUT</param>
        /// <param name="tagSignature">Tag Signature</param>
        public IccLut16TagDataEntry(IccLut[] inputValues, IccClut clutValues, IccLut[] outputValues, IccProfileTag tagSignature)
            : this(null, inputValues, clutValues, outputValues, tagSignature)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IccLut16TagDataEntry"/> class.
        /// </summary>
        /// <param name="matrix">Conversion matrix (must be 3x3)</param>
        /// <param name="inputValues">Input LUT</param>
        /// <param name="clutValues">CLUT</param>
        /// <param name="outputValues">Output LUT</param>
        public IccLut16TagDataEntry(float[,] matrix, IccLut[] inputValues, IccClut clutValues, IccLut[] outputValues)
            : this(matrix, inputValues, clutValues, outputValues, IccProfileTag.Unknown)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IccLut16TagDataEntry"/> class.
        /// </summary>
        /// <param name="matrix">Conversion matrix (must be 3x3)</param>
        /// <param name="inputValues">Input LUT</param>
        /// <param name="clutValues">CLUT</param>
        /// <param name="outputValues">Output LUT</param>
        /// <param name="tagSignature">Tag Signature</param>
        public IccLut16TagDataEntry(float[,] matrix, IccLut[] inputValues, IccClut clutValues, IccLut[] outputValues, IccProfileTag tagSignature)
            : base(IccTypeSignature.Lut16, tagSignature)
        {
            Guard.NotNull(inputValues, nameof(inputValues));
            Guard.NotNull(clutValues, nameof(clutValues));
            Guard.NotNull(outputValues, nameof(outputValues));

            if (matrix != null)
            {
                bool is3By3 = matrix.GetLength(0) == 3 && matrix.GetLength(1) == 3;
                Guard.IsTrue(is3By3, nameof(matrix), "Matrix must have a size of three by three");
            }

            Guard.IsTrue(this.InputChannelCount == clutValues.InputChannelCount, nameof(clutValues), "Input channel count does not match the CLUT size");
            Guard.IsTrue(this.OutputChannelCount == clutValues.OutputChannelCount, nameof(clutValues), "Output channel count does not match the CLUT size");

            this.Matrix = this.CreateMatrix(matrix);
            this.InputValues = inputValues;
            this.ClutValues = clutValues;
            this.OutputValues = outputValues;
        }

        /// <summary>
        /// Gets the number of input channels
        /// </summary>
        public int InputChannelCount
        {
            get { return this.InputValues.Length; }
        }

        /// <summary>
        /// Gets the number of output channels
        /// </summary>
        public int OutputChannelCount
        {
            get { return this.OutputValues.Length; }
        }

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

        /// <inheritdoc />
        public override bool Equals(IccTagDataEntry other)
        {
            if (base.Equals(other) && other is IccLut16TagDataEntry entry)
            {
                return this.ClutValues.Equals(entry.ClutValues)
                    && this.Matrix == entry.Matrix
                    && this.InputValues.SequenceEqual(entry.InputValues)
                    && this.OutputValues.SequenceEqual(entry.OutputValues);
            }

            return false;
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
