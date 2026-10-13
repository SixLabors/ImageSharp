// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <summary>
/// Retains motion-search features for one square node during a superblock's partition search.
/// </summary>
internal struct Av1SimpleMotionData
{
    // Starts use the common eighth-sample representation but always contain full-sample multiples.
    // Child nodes inherit the parent's truncated full-sample winner before their first search.
    public InlineArray8<Av1MotionVector> Starts;
    public int SquaredError;
    public int Variance;
    public InlineArray4<int> RectangleSquaredErrors;
    public InlineArray4<int> RectangleVariances;
    public bool WholeBlockValid;
    public bool RectanglesValid;
    public Av1PartitionType Partition;
}
