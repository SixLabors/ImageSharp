// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.
#nullable disable

using System.Numerics;

// TODO: Review the use of base IccTagDataEntry comparison.
namespace SixLabors.ImageSharp.Metadata.Profiles.Icc;

/// <summary>
/// This structure represents a color transform.
/// </summary>
internal sealed class IccLutBToATagDataEntry : IccTagDataEntry, IEquatable<IccLutBToATagDataEntry>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="IccLutBToATagDataEntry"/> class.
    /// </summary>
    /// <param name="curveB">B Curve</param>
    /// <param name="matrix3x3">Two dimensional conversion matrix (3x3)</param>
    /// <param name="matrix3x1">One dimensional conversion matrix (3x1)</param>
    /// <param name="curveM">M Curve</param>
    /// <param name="clutValues">CLUT</param>
    /// <param name="curveA">A Curve</param>
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
    /// <param name="curveB">B Curve</param>
    /// <param name="matrix3x3">Two dimensional conversion matrix (3x3)</param>
    /// <param name="matrix3x1">One dimensional conversion matrix (3x1)</param>
    /// <param name="curveM">M Curve</param>
    /// <param name="clutValues">CLUT</param>
    /// <param name="curveA">A Curve</param>
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
        VerifyMatrix(matrix3x3, matrix3x1);
        this.VerifyCurve(curveA, nameof(curveA));
        this.VerifyCurve(curveB, nameof(curveB));
        this.VerifyCurve(curveM, nameof(curveM));

        this.Matrix3x3 = CreateMatrix3x3(matrix3x3);
        this.Matrix3x1 = CreateMatrix3x1(matrix3x1);
        this.CurveA = curveA;
        this.CurveB = curveB;
        this.CurveM = curveM;
        this.ClutValues = clutValues;

        (this.InputChannelCount, this.OutputChannelCount) = this.GetChannelCounts();
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
    public override bool Equals(IccTagDataEntry other) => other is IccLutBToATagDataEntry entry && this.Equals(entry);

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
            && EqualsCurve(this.CurveB, other.CurveB)
            && EqualsCurve(this.CurveM, other.CurveM)
            && EqualsCurve(this.CurveA, other.CurveA);
    }

    /// <inheritdoc/>
    public override bool Equals(object obj) => obj is IccLutBToATagDataEntry other && this.Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        HashCode hashCode = default;
        hashCode.Add(this.Signature);
        hashCode.Add(this.InputChannelCount);
        hashCode.Add(this.OutputChannelCount);
        hashCode.Add(this.Matrix3x3);
        hashCode.Add(this.Matrix3x1);
        hashCode.Add(this.ClutValues);
        hashCode.Add(this.CurveB);
        hashCode.Add(this.CurveM);
        hashCode.Add(this.CurveA);

        return hashCode.ToHashCode();
    }

    /// <summary>
    /// Compares two curve arrays, treating <see langword="null"/> consistently.
    /// </summary>
    private static bool EqualsCurve(IccTagDataEntry[] thisCurves, IccTagDataEntry[] entryCurves)
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

    /// <summary>
    /// Validates the configured processing stages and derives the external channel counts.
    /// </summary>
    /// <remarks>
    /// Stages are evaluated in ICC <c>mBA</c> order: B, Matrix, M, CLUT, A.
    /// Sparse pipelines are valid as long as adjacent stages agree on channel counts.
    /// </remarks>
    private (int InputChannelCount, int OutputChannelCount) GetChannelCounts()
    {
        // There are at most five possible mBA stages: B, Matrix, M, CLUT, and A.
        List<(int Input, int Output, string Name)> stages = new(5);

        if (this.CurveB != null)
        {
            Guard.MustBeBetweenOrEqualTo(this.CurveB.Length, 1, 15, nameof(this.CurveB));
            stages.Add((this.CurveB.Length, this.CurveB.Length, nameof(this.CurveB)));
        }

        if (this.Matrix3x3 != null || this.Matrix3x1 != null)
        {
            Guard.IsTrue(this.Matrix3x3 != null && this.Matrix3x1 != null, nameof(this.Matrix3x3), "Matrix must include both the 3x3 and 3x1 components");
            stages.Add((3, 3, nameof(this.Matrix3x3)));
        }

        if (this.CurveM != null)
        {
            Guard.MustBeBetweenOrEqualTo(this.CurveM.Length, 1, 15, nameof(this.CurveM));
            stages.Add((this.CurveM.Length, this.CurveM.Length, nameof(this.CurveM)));
        }

        if (this.ClutValues != null)
        {
            stages.Add((this.ClutValues.InputChannelCount, this.ClutValues.OutputChannelCount, nameof(this.ClutValues)));
        }

        if (this.CurveA != null)
        {
            Guard.MustBeBetweenOrEqualTo(this.CurveA.Length, 1, 15, nameof(this.CurveA));
            stages.Add((this.CurveA.Length, this.CurveA.Length, nameof(this.CurveA)));
        }

        Guard.IsTrue(stages.Count > 0, nameof(this.CurveB), "BToA tag must contain at least one processing element");

        for (int i = 1; i < stages.Count; i++)
        {
            Guard.IsTrue(
                stages[i - 1].Output == stages[i].Input,
                stages[i].Name,
                $"Output channel count of {stages[i - 1].Name} does not match input channel count of {stages[i].Name}");
        }

        return (stages[0].Input, stages[^1].Output);
    }

    /// <summary>
    /// Verifies that every supplied curve entry is a supported one-dimensional curve type.
    /// </summary>
    private void VerifyCurve(IccTagDataEntry[] curves, string name)
    {
        if (curves != null)
        {
            bool isNotCurve = curves.Any(t => t is not IccParametricCurveTagDataEntry and not IccCurveTagDataEntry);
            Guard.IsFalse(isNotCurve, nameof(name), $"{nameof(name)} must be of type {nameof(IccParametricCurveTagDataEntry)} or {nameof(IccCurveTagDataEntry)}");
        }
    }

    /// <summary>
    /// Verifies the dimensions of the optional matrix components.
    /// </summary>
    private static void VerifyMatrix(float[,] matrix3x3, float[] matrix3x1)
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

    /// <summary>
    /// Creates the one-dimensional matrix vector when present.
    /// </summary>
    private static Vector3? CreateMatrix3x1(float[] matrix)
    {
        if (matrix is null)
        {
            return null;
        }

        return new Vector3(matrix[0], matrix[1], matrix[2]);
    }

    /// <summary>
    /// Creates the three-by-three matrix when present.
    /// </summary>
    private static Matrix4x4? CreateMatrix3x3(float[,] matrix)
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
