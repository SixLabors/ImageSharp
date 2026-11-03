// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Tests;

/// <summary>
/// Compares floating-point components with an absolute error bound or decimal rounding.
/// </summary>
internal readonly struct ApproximateFloatComparer :
    IEqualityComparer<float>,
    IEqualityComparer<Vector2>,
    IEqualityComparer<Vector3>,
    IEqualityComparer<Vector4>,
    IEqualityComparer<ColorMatrix>,
    IEqualityComparer<Vector256<float>>,
    IEqualityComparer<Vector512<float>>
{
    private readonly float epsilon;

    // Zero selects absolute differences, including the default struct's exact comparison.
    // Decimal comparisons store one more than the number of decimal places, so zero places remain distinct.
    private readonly int decimalPrecision;

    /// <summary>
    /// Initializes a comparer with an absolute error bound.
    /// </summary>
    /// <param name="epsilon">The maximum allowed absolute difference between components.</param>
    public ApproximateFloatComparer(float epsilon = 1F)
        : this(epsilon, 0)
    {
    }

    /// <summary>
    /// Initializes a comparer with an absolute error bound or an encoded decimal precision.
    /// </summary>
    /// <param name="epsilon">The absolute error bound when decimal precision is zero.</param>
    /// <param name="decimalPrecision">Zero for absolute differences; otherwise one more than the number of decimal places.</param>
    private ApproximateFloatComparer(float epsilon, int decimalPrecision)
    {
        this.epsilon = epsilon;
        this.decimalPrecision = decimalPrecision;
    }

    /// <summary>
    /// Creates a comparer that rounds each component to the specified decimal places before checking equality.
    /// </summary>
    /// <param name="precision">The number of decimal places, from zero through fifteen.</param>
    /// <returns>A comparer that uses <see cref="Math.Round(double, int)"/> with midpoint rounding to even.</returns>
    public static ApproximateFloatComparer FromPrecision(int precision) => new(0F, precision + 1);

    /// <inheritdoc/>
    public bool Equals(float x, float y)
    {
        if (this.decimalPrecision != 0)
        {
            // Keep decimal equality separate from an absolute error bound. Two values can cross a decimal
            // rounding midpoint even when their difference is smaller than one unit at that precision.
            return Math.Round(x, this.decimalPrecision - 1) == Math.Round(y, this.decimalPrecision - 1);
        }

        // Equal infinities and matching NaNs have no finite difference to compare with epsilon.
        if (x == y || (float.IsNaN(x) && float.IsNaN(y)))
        {
            return true;
        }

        float d = x - y;

        return d >= -this.epsilon && d <= this.epsilon;
    }

    /// <inheritdoc/>
    public int GetHashCode(float obj)
        => obj.GetHashCode();

    /// <inheritdoc/>
    public bool Equals(Vector2 x, Vector2 y)
        => this.Equals(x.X, y.X) && this.Equals(x.Y, y.Y);

    /// <inheritdoc/>
    public int GetHashCode(Vector2 obj)
        => obj.GetHashCode();

    /// <inheritdoc/>
    public bool Equals(Vector3 x, Vector3 y)
        => this.Equals(x.X, y.X)
        && this.Equals(x.Y, y.Y)
        && this.Equals(x.Z, y.Z);

    /// <inheritdoc/>
    public int GetHashCode(Vector3 obj)
        => obj.GetHashCode();

    /// <inheritdoc/>
    public bool Equals(Vector4 x, Vector4 y)
        => this.Equals(x.X, y.X)
        && this.Equals(x.Y, y.Y)
        && this.Equals(x.Z, y.Z)
        && this.Equals(x.W, y.W);

    /// <inheritdoc/>
    public int GetHashCode(Vector4 obj)
        => obj.GetHashCode();

    /// <inheritdoc/>
    public bool Equals(ColorMatrix x, ColorMatrix y)
        => this.Equals(x.M11, y.M11) && this.Equals(x.M12, y.M12) && this.Equals(x.M13, y.M13) && this.Equals(x.M14, y.M14)
        && this.Equals(x.M21, y.M21) && this.Equals(x.M22, y.M22) && this.Equals(x.M23, y.M23) && this.Equals(x.M24, y.M24)
        && this.Equals(x.M31, y.M31) && this.Equals(x.M32, y.M32) && this.Equals(x.M33, y.M33) && this.Equals(x.M34, y.M34)
        && this.Equals(x.M41, y.M41) && this.Equals(x.M42, y.M42) && this.Equals(x.M43, y.M43) && this.Equals(x.M44, y.M44)
        && this.Equals(x.M51, y.M51) && this.Equals(x.M52, y.M52) && this.Equals(x.M53, y.M53) && this.Equals(x.M54, y.M54);

    /// <inheritdoc/>
    public int GetHashCode(ColorMatrix obj) => obj.GetHashCode();

    public bool Equals(Vector256<float> x, Vector256<float> y)
        => this.Equals(x.GetElement(0), y.GetElement(0))
        && this.Equals(x.GetElement(1), y.GetElement(1))
        && this.Equals(x.GetElement(2), y.GetElement(2))
        && this.Equals(x.GetElement(3), y.GetElement(3))
        && this.Equals(x.GetElement(4), y.GetElement(4))
        && this.Equals(x.GetElement(5), y.GetElement(5))
        && this.Equals(x.GetElement(6), y.GetElement(6))
        && this.Equals(x.GetElement(7), y.GetElement(7));

    public int GetHashCode([DisallowNull] Vector256<float> obj) => obj.GetHashCode();

    public bool Equals(Vector512<float> x, Vector512<float> y)
    {
        for (int i = 0; i < Vector512<float>.Count; i++)
        {
            if (!this.Equals(x.GetElement(i), y.GetElement(i)))
            {
                return false;
            }
        }

        return true;
    }

    public int GetHashCode([DisallowNull] Vector512<float> obj) => obj.GetHashCode();
}
