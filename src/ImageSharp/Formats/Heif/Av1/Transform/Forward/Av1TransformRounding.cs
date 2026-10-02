// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform.Forward;

/// <summary>
/// Stores the fixed-point rounding value in the lane shape selected for one forward transform.
/// </summary>
/// <remarks>
/// The fields overlap because a closed transform instantiation reads exactly one representation. This keeps the
/// rounding broadcast outside the butterfly sequence without increasing the caller-owned transform workspace.
/// </remarks>
[StructLayout(LayoutKind.Explicit)]
internal readonly struct Av1TransformRounding
{
    /// <summary>
    /// The scalar rounding value.
    /// </summary>
    [FieldOffset(0)]
    public readonly int Scalar;

    /// <summary>
    /// The four-lane rounding value used by 128-bit widened arithmetic.
    /// </summary>
    [FieldOffset(0)]
    public readonly Vector128<int> Vector128;

    /// <summary>
    /// The eight-lane rounding value used by 256-bit widened arithmetic.
    /// </summary>
    [FieldOffset(0)]
    public readonly Vector256<int> Vector256;

    /// <summary>
    /// The sixteen-lane rounding value used by 512-bit widened arithmetic.
    /// </summary>
    [FieldOffset(0)]
    public readonly Vector512<int> Vector512;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1TransformRounding"/> struct for scalar arithmetic.
    /// </summary>
    /// <param name="scalar">The scalar rounding value.</param>
    public Av1TransformRounding(int scalar) => this.Scalar = scalar;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1TransformRounding"/> struct for 128-bit widened arithmetic.
    /// </summary>
    /// <param name="vector">The four-lane rounding value.</param>
    public Av1TransformRounding(Vector128<int> vector) => this.Vector128 = vector;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1TransformRounding"/> struct for 256-bit widened arithmetic.
    /// </summary>
    /// <param name="vector">The eight-lane rounding value.</param>
    public Av1TransformRounding(Vector256<int> vector) => this.Vector256 = vector;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1TransformRounding"/> struct for 512-bit widened arithmetic.
    /// </summary>
    /// <param name="vector">The sixteen-lane rounding value.</param>
    public Av1TransformRounding(Vector512<int> vector) => this.Vector512 = vector;
}
