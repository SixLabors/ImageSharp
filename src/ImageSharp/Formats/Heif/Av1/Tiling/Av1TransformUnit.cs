// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

/// <summary>
/// Stores the transform syntax and coefficient range for one AV1 transform unit.
/// </summary>
internal struct Av1TransformUnit
{
    /// <summary>
    /// Stores the luma, blue-difference, and red-difference end-of-block positions.
    /// </summary>
    private InlineArray3<ushort> nzCoefficientCount;

    /// <summary>
    /// Stores the luma and shared chroma transform types.
    /// </summary>
    private InlineArray2<Av1TransformType> transformType;

    /// <summary>
    /// Gets the nonzero-coefficient count for each color plane.
    /// </summary>
    [UnscopedRef]
    public Span<ushort> NzCoefficientCount => this.nzCoefficientCount;

    /// <summary>
    /// Gets the transform type selected for each color plane.
    /// </summary>
    [UnscopedRef]
    public Span<Av1TransformType> TransformType => this.transformType;

    /// <summary>
    /// Stores the three per-plane coefficient counts inline.
    /// </summary>
    [InlineArray(3)]
    private struct InlineArray3<T>
    {
        private T element;
    }

    /// <summary>
    /// Stores the luma and shared chroma transform types inline.
    /// </summary>
    [InlineArray(Av1Constants.PlaneTypeCount)]
    private struct InlineArray2<T>
    {
        private T element;
    }
}
