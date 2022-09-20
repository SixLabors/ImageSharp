// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Metadata.Profiles.Icc;

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
    public DenseMatrix<float> MatrixIxO { get; }

    /// <summary>
    /// Gets the one dimensional matrix with size of Output-Channels x 1
    /// </summary>
    public float[] MatrixOx1 { get; }

    /// <inheritdoc />
    public override bool Equals(IccMultiProcessElement? other)
    {
        if (base.Equals(other) && other is IccMatrixProcessElement element)
        {
            return this.EqualsMatrix(element)
                && this.MatrixOx1.AsSpan().SequenceEqual(element.MatrixOx1);
        }

        return false;
    }

    /// <inheritdoc />
    public bool Equals(IccMatrixProcessElement? other)
        => this.Equals((IccMultiProcessElement?)other);

    /// <inheritdoc />
    public override bool Equals(object? obj)
        => this.Equals(obj as IccMatrixProcessElement);

    /// <inheritdoc />
    public override int GetHashCode()
        => HashCode.Combine(base.GetHashCode(), this.MatrixIxO, this.MatrixOx1);

    private bool EqualsMatrix(IccMatrixProcessElement element)
        => this.MatrixIxO.Equals(element.MatrixIxO);
}
