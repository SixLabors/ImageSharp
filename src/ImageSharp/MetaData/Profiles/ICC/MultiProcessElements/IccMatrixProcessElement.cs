// <copyright file="IccMatrixProcessElement.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;
    using System.Linq;

    using ImageSharp.Memory;

    /// <summary>
    /// A matrix element to process data
    /// </summary>
    internal sealed class IccMatrixProcessElement : IccMultiProcessElement, IEquatable<IccMatrixProcessElement>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="IccMatrixProcessElement"/> class.
        /// </summary>
        /// <param name="matrixIxO">Two dimensional matrix with size of Input-Channels x Output-Channels</param>
        /// <param name="matrixOx1">One dimensional matrix with size of Output-Channels x 1</param>
        public IccMatrixProcessElement(float[,] matrixIxO, float[] matrixOx1)
            : base(IccMultiProcessElementSignature.Matrix, matrixIxO?.GetLength(0) ?? 1, matrixIxO?.GetLength(1) ?? 1)
        {
            Guard.NotNull(matrixIxO, nameof(matrixIxO));
            Guard.NotNull(matrixOx1, nameof(matrixOx1));

            bool matrixSizeCorrect = matrixIxO.GetLength(1) == matrixOx1.Length;
            Guard.IsTrue(matrixSizeCorrect, $"{nameof(matrixIxO)},{nameof(matrixIxO)}", "Output channel length must match");

            this.MatrixIxO = matrixIxO;
            this.MatrixOx1 = matrixOx1;
        }

        /// <summary>
        /// Gets the two dimensional matrix with size of Input-Channels x Output-Channels
        /// </summary>
        public Fast2DArray<float> MatrixIxO { get; }

        /// <summary>
        /// Gets the one dimensional matrix with size of Output-Channels x 1
        /// </summary>
        public float[] MatrixOx1 { get; }

        /// <inheritdoc />
        public override bool Equals(IccMultiProcessElement other)
        {
            if (base.Equals(other) && other is IccMatrixProcessElement element)
            {
                return this.EqualsMatrix(element)
                    && this.MatrixOx1.SequenceEqual(element.MatrixOx1);
            }

            return false;
        }

        /// <inheritdoc />
        public bool Equals(IccMatrixProcessElement other)
        {
            return this.Equals((IccMultiProcessElement)other);
        }

        private bool EqualsMatrix(IccMatrixProcessElement element)
        {
            for (int x = 0; x < this.MatrixIxO.Width; x++)
            {
                for (int y = 0; y < this.MatrixIxO.Height; y++)
                {
                    if (this.MatrixIxO[x, y] != element.MatrixIxO[x, y])
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }
}
