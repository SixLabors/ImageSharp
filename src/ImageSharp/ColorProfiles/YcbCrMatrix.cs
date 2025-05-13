// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.ColorProfiles.WorkingSpaces;

namespace SixLabors.ImageSharp.ColorProfiles;

/// <summary>
/// <para>
/// Represents a YCbCr color matrix containing forward and inverse transformation matrices,
/// and the chrominance offsets to apply for full-range encoding
/// </para>
/// <para>
/// These matrices must be selected to match the characteristics of the associated <see cref="RgbWorkingSpace"/>,
/// including its transfer function (gamma or companding) and chromaticity coordinates. Using mismatched matrices and
/// working spaces will produce incorrect conversions.
/// </para>
/// </summary>
public readonly struct YCbCrMatrix
{
    /// <summary>
    /// Initializes a new instance of the <see cref="YCbCrMatrix"/> struct.
    /// </summary>
    /// <param name="forward">
    /// The forward transformation matrix from RGB to YCbCr. The matrix must include the
    /// standard chrominance offsets in the fourth column, such as <c>(0, 0.5, 0.5)</c>.
    /// </param>
    /// <param name="inverse">
    /// The inverse transformation matrix from YCbCr to RGB. This matrix expects that
    /// chrominance offsets have already been subtracted prior to application.
    /// </param>
    /// <param name="offset">
    /// The chrominance offsets to be added after the forward conversion,
    /// and subtracted before the inverse conversion. Usually <c>(0, 0.5, 0.5)</c>.
    /// </param>
    public YCbCrMatrix(Matrix4x4 forward, Matrix4x4 inverse, Vector3 offset)
    {
        this.Forward = forward;
        this.Inverse = inverse;
        this.Offset = offset;
    }

    /// <summary>
    /// Gets the matrix used to convert gamma-encoded RGB to YCbCr.
    /// </summary>
    public Matrix4x4 Forward { get; }

    /// <summary>
    /// Gets the matrix used to convert YCbCr back to gamma-encoded RGB.
    /// </summary>
    public Matrix4x4 Inverse { get; }

    /// <summary>
    /// Gets the chrominance offset vector to apply during encoding (add) or decoding (subtract).
    /// </summary>
    public Vector3 Offset { get; }
}
