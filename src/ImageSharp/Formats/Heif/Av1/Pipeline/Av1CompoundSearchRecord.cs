// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <summary>
/// Retains compound estimates for matching predictor pairs within one block search.
/// </summary>
internal struct Av1CompoundSearchRecord
{
    /// <summary>
    /// The maximum number of retained predictor pairs.
    /// </summary>
    public const int Capacity = 64;

    /// <summary>
    /// Stores the original primary motion vector.
    /// </summary>
    public Av1MotionVector Primary;

    /// <summary>
    /// Stores the original secondary motion vector.
    /// </summary>
    public Av1MotionVector Secondary;

    /// <summary>
    /// Stores the first reference slot.
    /// </summary>
    public Av1ReferenceFrameType PrimaryReference;

    /// <summary>
    /// Stores the second reference slot.
    /// </summary>
    public Av1ReferenceFrameType SecondaryReference;

    /// <summary>
    /// Stores the prediction mode that populated the record.
    /// </summary>
    public Av1PredictionMode Mode;

    /// <summary>
    /// Stores the interpolation filter shared by both axes.
    /// </summary>
    public Av1InterpolationFilter Filter;

    /// <summary>
    /// Stores the dynamic reference-list index.
    /// </summary>
    public int ReferenceIndex;

    /// <summary>
    /// Stores whether the first predictor uses a non-translational global model.
    /// </summary>
    public bool PrimaryGlobal;

    /// <summary>
    /// Stores whether the second predictor uses a non-translational global model.
    /// </summary>
    public bool SecondaryGlobal;

    /// <summary>
    /// Stores the selected compound type.
    /// </summary>
    public Av1CompoundType SelectedType;

    /// <summary>
    /// Stores the selected wedge index.
    /// </summary>
    public byte WedgeIndex;

    /// <summary>
    /// Stores the selected wedge orientation.
    /// </summary>
    public bool WedgeSign;

    /// <summary>
    /// Stores the selected difference-weighted orientation.
    /// </summary>
    public Av1DifferenceWeightedMaskType MaskType;

    /// <summary>
    /// Stores the residual and skip syntax rate for each compound type.
    /// </summary>
    public InlineArray4<int> Rates;

    /// <summary>
    /// Stores the transform-domain distortion for each compound type.
    /// </summary>
    public InlineArray4<long> Distortions;

    /// <summary>
    /// Stores the model residual rate for each compound type.
    /// </summary>
    public InlineArray4<int> ModelRates;

    /// <summary>
    /// Stores the model distortion for each compound type.
    /// </summary>
    public InlineArray4<long> ModelDistortions;

    /// <summary>
    /// Stores the blend syntax rate for each compound type.
    /// </summary>
    public InlineArray4<int> BlendRates;
}
