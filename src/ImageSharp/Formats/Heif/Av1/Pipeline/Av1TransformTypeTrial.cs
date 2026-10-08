// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <summary>
/// Holds the values and buffers that every transform type of one transform block uses. The type search builds it
/// once before its type loop, so a trial reads no workspace buffer, rate table or setting again. Only the transform
/// type changes between trials.
/// </summary>
internal readonly ref struct Av1TransformTypeTrial
{
    /// <summary>
    /// Gets the block workspace, which holds the quantizer matrices of each type.
    /// </summary>
    public Av1EncoderBlockWorkspace Workspace { get; init; }

    /// <summary>
    /// Gets the tile symbol encoder that prices the coefficients.
    /// </summary>
    public Av1SymbolEncoder Writer { get; init; }

    /// <summary>
    /// Gets the rate tables and scratch storage of the writer, read once for the type loop.
    /// </summary>
    public Av1CoefficientTables Tables { get; init; }

    /// <summary>
    /// Gets the coefficient contexts of the transform block.
    /// </summary>
    public Av1TransformBlockContext Context { get; init; }

    /// <summary>
    /// Gets the source-minus-prediction block.
    /// </summary>
    public ReadOnlySpan<short> Residual { get; init; }

    /// <summary>
    /// Gets the number of residual samples between rows.
    /// </summary>
    public int ResidualStride { get; init; }

    /// <summary>
    /// Gets the forward transform output buffer.
    /// </summary>
    public Span<int> TransformCoefficients { get; init; }

    /// <summary>
    /// Gets the intermediate buffer of the forward transform.
    /// </summary>
    public Span<int> TransformWorkspace { get; init; }

    /// <summary>
    /// Gets the transform size.
    /// </summary>
    public Av1TransformSize TransformSize { get; init; }

    /// <summary>
    /// Gets the spatial prediction mode, which selects the transform-type context.
    /// </summary>
    public Av1PredictionMode IntraDirection { get; init; }

    /// <summary>
    /// Gets the filter-intra mode, or <see cref="Av1FilterIntraMode.AllFilterIntraModes"/> for none.
    /// </summary>
    public Av1FilterIntraMode FilterIntraMode { get; init; }

    /// <summary>
    /// Gets a value indicating whether the frame restricts the transform types.
    /// </summary>
    public bool UseReducedTransformSet { get; init; }

    /// <summary>
    /// Gets a value indicating whether the inter transform syntax applies.
    /// </summary>
    public bool UsesInterTransformSet { get; init; }

    /// <summary>
    /// Gets the quantizer index of the segment.
    /// </summary>
    public int QIndex { get; init; }

    /// <summary>
    /// Gets the DC quantizer adjustment of the plane.
    /// </summary>
    public int DcDeltaQ { get; init; }

    /// <summary>
    /// Gets the AC quantizer adjustment of the plane.
    /// </summary>
    public int AcDeltaQ { get; init; }

    /// <summary>
    /// Gets the quantizer sharpness of the encoder.
    /// </summary>
    public int Sharpness { get; init; }

    /// <summary>
    /// Gets the coded sample precision.
    /// </summary>
    public Av1BitDepth BitDepth { get; init; }

    /// <summary>
    /// Gets the luminance or chroma component.
    /// </summary>
    public Av1ComponentType ComponentType { get; init; }

    /// <summary>
    /// Gets the rate-distortion multiplier of the block.
    /// </summary>
    public int RateMultiplier { get; init; }

    /// <summary>
    /// Gets a value indicating whether the prediction uses an inter transform set.
    /// </summary>
    public bool IsInter { get; init; }

    /// <summary>
    /// Gets a value indicating whether chroma uses its own coefficient refinement weights.
    /// </summary>
    public bool UseChromaWeights { get; init; }

    /// <summary>
    /// Gets a value indicating whether the block energy gate turned coefficient refinement off.
    /// </summary>
    public bool SkipTrellis { get; init; }

    /// <summary>
    /// Gets the transform-scaled SATD gate, or <see cref="uint.MaxValue"/> for none.
    /// </summary>
    public uint SatdThreshold { get; init; }

    /// <summary>
    /// Gets a value indicating whether the block codes its residual mean as the DC coefficient alone.
    /// </summary>
    public bool DcOnly { get; init; }

    /// <summary>
    /// Gets the signed transform-domain residual mean of a DC-only block.
    /// </summary>
    public long PerPixelMean { get; init; }
}
