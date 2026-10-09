// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <summary>
/// Holds the values and buffers of one plane that every intra candidate of a block uses. A mode search builds it once
/// before its mode loop, so a candidate reads no plane, workspace buffer or frame setting again. Only the mode, the
/// angle and the transform type change between candidates.
/// </summary>
/// <typeparam name="TSample">The sample storage type.</typeparam>
internal readonly ref struct Av1IntraCandidatePlane<TSample>
    where TSample : unmanaged
{
    /// <summary>
    /// Gets the block workspace, which holds the quantizer matrices and the coding stage.
    /// </summary>
    public Av1EncoderBlockWorkspace Workspace { get; init; }

    /// <summary>
    /// Gets the tile symbol encoder that prices the coefficients.
    /// </summary>
    public Av1SymbolEncoder Writer { get; init; }

    /// <summary>
    /// Gets the rate tables and level storage of the writer, read once for the block.
    /// </summary>
    public Av1CoefficientTables Tables { get; init; }

    /// <summary>
    /// Gets the forward transform output buffer.
    /// </summary>
    public Span<int> TransformCoefficients { get; init; }

    /// <summary>
    /// Gets the dequantized coefficient buffer.
    /// </summary>
    public Span<int> DequantizedCoefficients { get; init; }

    /// <summary>
    /// Gets the intermediate buffer of the transforms.
    /// </summary>
    public Span<int> TransformWorkspace { get; init; }

    /// <summary>
    /// Gets the coefficient contexts of the transform block.
    /// </summary>
    public Av1TransformBlockContext Context { get; init; }

    /// <summary>
    /// Gets the rate-distortion multiplier.
    /// </summary>
    public int RateMultiplier { get; init; }

    /// <summary>
    /// Gets a value indicating whether chroma uses its own coefficient refinement weights.
    /// </summary>
    public bool UseChromaWeights { get; init; }

    /// <summary>
    /// Gets the source block, from its top-left sample.
    /// </summary>
    public ReadOnlySpan<TSample> Source { get; init; }

    /// <summary>
    /// Gets the number of samples between rows of <see cref="Source"/>.
    /// </summary>
    public int SourceStride { get; init; }

    /// <summary>
    /// Gets the block origin in plane samples.
    /// </summary>
    public Point BlockOrigin { get; init; }

    /// <summary>
    /// Gets the contiguous reconstruction of the candidate.
    /// </summary>
    public Span<TSample> Reconstruction { get; init; }

    /// <summary>
    /// Gets the frame plane that gets the prediction.
    /// </summary>
    public Av1PlaneRegion<TSample> Frame { get; init; }

    /// <summary>
    /// Gets all samples of <see cref="Frame"/>.
    /// </summary>
    public Span<TSample> FrameSamples { get; init; }

    /// <summary>
    /// Gets the top reference samples, after the shared corner.
    /// </summary>
    public ReadOnlySpan<TSample> Above { get; init; }

    /// <summary>
    /// Gets the left reference samples, after the shared corner.
    /// </summary>
    public ReadOnlySpan<TSample> Left { get; init; }

    /// <summary>
    /// Gets a value indicating whether the left reference is available.
    /// </summary>
    public bool HasLeft { get; init; }

    /// <summary>
    /// Gets a value indicating whether the top reference is available.
    /// </summary>
    public bool HasAbove { get; init; }

    /// <summary>
    /// Gets a value indicating whether the sequence enables the intra edge filter.
    /// </summary>
    public bool EnableIntraEdgeFilter { get; init; }

    /// <summary>
    /// Gets a value indicating whether a relevant neighbor uses a smooth mode, which selects the edge filter strength.
    /// </summary>
    public bool SmoothIntraEdges { get; init; }

    /// <summary>
    /// Gets the quantized coefficients of the candidate.
    /// </summary>
    public Span<int> QuantizedCoefficients { get; init; }

    /// <summary>
    /// Gets the transform size.
    /// </summary>
    public Av1TransformSize TransformSize { get; init; }

    /// <summary>
    /// Gets the plane of the block.
    /// </summary>
    public Av1Plane Plane { get; init; }

    /// <summary>
    /// Gets the quantizer index of the segment.
    /// </summary>
    public int QIndex { get; init; }

    /// <summary>
    /// Gets a value indicating whether the segment of the block codes losslessly.
    /// </summary>
    public bool Lossless { get; init; }

    /// <summary>
    /// Gets the DC quantizer adjustment of the plane.
    /// </summary>
    public int DcDeltaQ { get; init; }

    /// <summary>
    /// Gets the AC quantizer adjustment of the plane.
    /// </summary>
    public int AcDeltaQ { get; init; }

    /// <summary>
    /// Gets the coded sample bit depth.
    /// </summary>
    public Av1BitDepth BitDepth { get; init; }

    /// <summary>
    /// Gets the transform-domain distortion type and its mean-error threshold.
    /// </summary>
    public (int Type, uint Threshold) DistortionPolicy { get; init; }

    /// <summary>
    /// Gets the residual buffer of the workspace.
    /// </summary>
    public Span<short> Residual { get; init; }
}
