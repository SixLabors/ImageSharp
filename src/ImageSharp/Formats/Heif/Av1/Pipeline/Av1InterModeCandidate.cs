// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <summary>
/// Retains prediction syntax while transform search is deferred.
/// </summary>
internal struct Av1InterModeCandidate : IComparable<Av1InterModeCandidate>
{
    /// <summary>The maximum retained predictions for one coding block.</summary>
    public const int Capacity = 1024;

    /// <summary>The prediction parameters, without residual decisions.</summary>
    public Av1EncoderBlockModeInfo ModeInfo;

    /// <summary>The primary prediction vector.</summary>
    public Av1MotionVector Vector;

    /// <summary>The secondary prediction vector.</summary>
    public Av1MotionVector SecondaryVector;

    /// <summary>The complete prediction syntax rate.</summary>
    public int PredictionRate;

    /// <summary>The selected dynamic reference-list entry.</summary>
    public int ReferenceIndex;

    /// <summary>The estimated combined rate and distortion.</summary>
    public long EstimatedCost;

    /// <summary>The complete prediction error in squared-error units scaled by sixteen.</summary>
    public long PredictionError;

    /// <summary>The luma prediction error in squared-error units scaled by sixteen.</summary>
    public long LumaPredictionError;

    /// <summary>The original search position used to preserve equal-cost ordering.</summary>
    public int SearchIndex;

    /// <summary>
    /// Orders predictions by estimated cost. Predictions of equal cost keep their search order.
    /// </summary>
    /// <param name="other">The prediction to compare with.</param>
    /// <returns>A negative value if this prediction sorts first, zero if both are equal, and a positive value otherwise.</returns>
    public readonly int CompareTo(Av1InterModeCandidate other)
    {
        int comparison = this.EstimatedCost.CompareTo(other.EstimatedCost);
        return comparison != 0 ? comparison : this.SearchIndex.CompareTo(other.SearchIndex);
    }
}
