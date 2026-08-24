// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

/// <summary>
/// Identifies a concrete one-dimensional AV1 transform function and length.
/// </summary>
internal enum Av1TransformFunctionType
{
    /// <summary>
    /// A four-sample discrete cosine transform.
    /// </summary>
    Dct4,

    /// <summary>
    /// An eight-sample discrete cosine transform.
    /// </summary>
    Dct8,

    /// <summary>
    /// A sixteen-sample discrete cosine transform.
    /// </summary>
    Dct16,

    /// <summary>
    /// A thirty-two-sample discrete cosine transform.
    /// </summary>
    Dct32,

    /// <summary>
    /// A sixty-four-sample discrete cosine transform.
    /// </summary>
    Dct64,

    /// <summary>
    /// A four-sample asymmetric discrete sine transform.
    /// </summary>
    Adst4,

    /// <summary>
    /// An eight-sample asymmetric discrete sine transform.
    /// </summary>
    Adst8,

    /// <summary>
    /// A sixteen-sample asymmetric discrete sine transform.
    /// </summary>
    Adst16,

    /// <summary>
    /// A thirty-two-sample asymmetric discrete sine transform.
    /// </summary>
    Adst32,

    /// <summary>
    /// A four-sample identity transform.
    /// </summary>
    Identity4,

    /// <summary>
    /// An eight-sample identity transform.
    /// </summary>
    Identity8,

    /// <summary>
    /// A sixteen-sample identity transform.
    /// </summary>
    Identity16,

    /// <summary>
    /// A thirty-two-sample identity transform.
    /// </summary>
    Identity32,

    /// <summary>
    /// A sixty-four-sample identity transform.
    /// </summary>
    Identity64,

    /// <summary>
    /// No valid transform function.
    /// </summary>
    Invalid,
}
