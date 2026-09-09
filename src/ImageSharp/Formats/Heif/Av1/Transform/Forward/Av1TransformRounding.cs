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
internal struct Av1TransformRounding
{
    /// <summary>
    /// The scalar rounding value.
    /// </summary>
    [FieldOffset(0)]
    public int Scalar;

    /// <summary>
    /// The four-lane rounding value used by 128-bit widened arithmetic.
    /// </summary>
    [FieldOffset(0)]
    public Vector128<int> Vector128;

    /// <summary>
    /// The eight-lane rounding value used by 256-bit widened arithmetic.
    /// </summary>
    [FieldOffset(0)]
    public Vector256<int> Vector256;

    /// <summary>
    /// The sixteen-lane rounding value used by 512-bit widened arithmetic.
    /// </summary>
    [FieldOffset(0)]
    public Vector512<int> Vector512;
}
