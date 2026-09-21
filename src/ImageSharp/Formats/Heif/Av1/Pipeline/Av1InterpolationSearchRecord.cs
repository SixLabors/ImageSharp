// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <summary>
/// Retains an interpolation decision for reuse within one block's mode search.
/// </summary>
internal struct Av1InterpolationSearchRecord
{
    /// <summary>The maximum cached decisions for one block.</summary>
    public const int Capacity = 128;

    /// <summary>The primary predictor vector.</summary>
    public Av1MotionVector Primary;

    /// <summary>The secondary predictor vector.</summary>
    public Av1MotionVector Secondary;

    /// <summary>The primary reference slot name.</summary>
    public Av1ReferenceFrameType PrimaryReference;

    /// <summary>The secondary reference slot name.</summary>
    public Av1ReferenceFrameType SecondaryReference;

    /// <summary>The compound blend family.</summary>
    public Av1CompoundType CompoundType;

    /// <summary>Whether the compound uses the average index.</summary>
    public bool CompoundIndex;

    /// <summary>The horizontal interpolation filter.</summary>
    public Av1InterpolationFilter HorizontalFilter;

    /// <summary>The vertical interpolation filter.</summary>
    public Av1InterpolationFilter VerticalFilter;

    /// <summary>The selected filter's modeled rate-distortion cost.</summary>
    public long Cost;
}
