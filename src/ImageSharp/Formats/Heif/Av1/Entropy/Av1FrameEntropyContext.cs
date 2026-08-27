// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;

/// <summary>
/// Owns the adaptive AV1 distributions currently implemented by the frame and tile syntax decoders.
/// </summary>
/// <remarks>
/// One frame context supplies the initial state copied into every tile context. Each tile adapts an independent working
/// copy, and only the tile selected by <c>context_update_tile_id</c> supplies the completed frame snapshot.
/// </remarks>
internal sealed class Av1FrameEntropyContext
{
    /// <summary>
    /// The inclusive upper bound of the first AV1 coefficient-probability quantizer band.
    /// </summary>
    private const int FirstQuantizerBandMaximum = 20;

    /// <summary>
    /// The inclusive upper bound of the second AV1 coefficient-probability quantizer band.
    /// </summary>
    private const int SecondQuantizerBandMaximum = 60;

    /// <summary>
    /// The inclusive upper bound of the third AV1 coefficient-probability quantizer band.
    /// </summary>
    private const int ThirdQuantizerBandMaximum = 120;

    /// <summary>
    /// The immutable normative contexts used to restore reusable frame state without rebuilding distribution graphs.
    /// </summary>
    private static readonly Av1FrameEntropyContext[] DefaultPrototypes =
    [
        new((byte)0),
        new((byte)1),
        new((byte)2),
        new((byte)3)
    ];

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1FrameEntropyContext"/> class from the normative default
    /// distributions selected by a frame quantizer index.
    /// </summary>
    /// <param name="qIndex">The frame base quantizer index selecting coefficient distribution defaults.</param>
    public Av1FrameEntropyContext(int qIndex)
        : this(DefaultPrototypes[GetQContext(qIndex)])
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1FrameEntropyContext"/> class as an immutable normative prototype.
    /// </summary>
    /// <param name="qContext">The zero-based coefficient-probability quantizer band.</param>
    private Av1FrameEntropyContext(byte qContext)
    {
        int qIndex = qContext switch
        {
            0 => 0,
            1 => FirstQuantizerBandMaximum + 1,
            2 => SecondQuantizerBandMaximum + 1,
            _ => ThirdQuantizerBandMaximum + 1
        };

        // Every default-distribution accessor constructs independently mutable state. Retaining those returned
        // graphs directly confines generated-table construction to the four process-wide quantizer-band prototypes.
        this.IntraBlockCopy = Av1DefaultDistributions.IntraBlockCopy;
        this.DisplacementVector = new();
        this.SwitchableRestoration = Av1DefaultDistributions.SwitchableRestoration;
        this.WienerRestoration = Av1DefaultDistributions.WienerRestoration;
        this.SgrProjectionRestoration = Av1DefaultDistributions.SgrProjectionRestoration;
        this.PaletteYMode = Av1DefaultDistributions.PaletteYMode;
        this.PaletteUvMode = Av1DefaultDistributions.PaletteUvMode;
        this.PaletteYSize = Av1DefaultDistributions.PaletteYSize;
        this.PaletteUvSize = Av1DefaultDistributions.PaletteUvSize;
        this.PaletteYColorIndex = Av1DefaultDistributions.PaletteYColorIndex;
        this.PaletteUvColorIndex = Av1DefaultDistributions.PaletteUvColorIndex;
        this.PartitionTypes = Av1DefaultDistributions.PartitionTypes;
        this.FrameYMode = Av1DefaultDistributions.FrameYMode;
        this.KeyFrameYMode = Av1DefaultDistributions.KeyFrameYMode;
        this.IntraInter = Av1DefaultDistributions.IntraInter;
        this.UvMode = Av1DefaultDistributions.UvMode;
        this.Skip = Av1DefaultDistributions.Skip;
        this.SkipMode = Av1DefaultDistributions.SkipMode;
        this.DeltaLoopFilterAbsolute = Av1DefaultDistributions.DeltaLoopFilterAbsolute;
        this.DeltaQuantizerAbsolute = Av1DefaultDistributions.DeltaQuantizerAbsolute;
        this.SegmentId = Av1DefaultDistributions.SegmentId;
        this.SegmentIdPredicted = Av1DefaultDistributions.SegmentIdPredicted;
        this.AngleDelta = Av1DefaultDistributions.AngleDelta;
        this.FilterIntraMode = Av1DefaultDistributions.FilterIntraMode;
        this.FilterIntra = Av1DefaultDistributions.FilterIntra;
        this.TransformSize = Av1DefaultDistributions.TransformSize;
        this.ChromaFromLumaSign = Av1DefaultDistributions.ChromaFromLumaSign;
        this.ChromaFromLumaAlpha = Av1DefaultDistributions.ChromaFromLumaAlpha;
        this.IntraExtendedTransform = Av1DefaultDistributions.IntraExtendedTransform;
        this.InterExtendedTransform = Av1DefaultDistributions.InterExtendedTransform;

        // Coefficient defaults use one of four quantizer bands. Their array shapes remain fixed, so later tile resets
        // copy only thresholds and update counts into this context's already allocated distribution graph.
        this.EndOfBlockFlag = Av1DefaultDistributions.GetEndOfBlockFlag(qIndex);
        this.CoefficientsBase = Av1DefaultDistributions.GetCoefficientsBase(qIndex);
        this.BaseEndOfBlock = Av1DefaultDistributions.GetBaseEndOfBlock(qIndex);
        this.DcSign = Av1DefaultDistributions.GetDcSign(qIndex);
        this.CoefficientsBaseRange = Av1DefaultDistributions.GetCoefficientsBaseRange(qIndex);
        this.TransformBlockSkip = Av1DefaultDistributions.GetTransformBlockSkip(qIndex);
        this.EndOfBlockExtra = Av1DefaultDistributions.GetEndOfBlockExtra(qIndex);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1FrameEntropyContext"/> class with an independently adaptable copy of a prototype.
    /// </summary>
    /// <param name="source">The prototype or retained context whose state is copied.</param>
    private Av1FrameEntropyContext(Av1FrameEntropyContext source)
    {
        // Session and retained-frame contexts need one mutable graph, not four generated quantizer-band graphs whose
        // unused bands are immediately discarded. Deep-copy the already selected prototype shape exactly once.
        this.IntraBlockCopy = source.IntraBlockCopy.CreateCopy();
        this.DisplacementVector = new();
        this.DisplacementVector.CopyFrom(source.DisplacementVector);
        this.SwitchableRestoration = source.SwitchableRestoration.CreateCopy();
        this.WienerRestoration = source.WienerRestoration.CreateCopy();
        this.SgrProjectionRestoration = source.SgrProjectionRestoration.CreateCopy();
        this.PaletteYMode = Av1Distribution.CreateCopy(source.PaletteYMode);
        this.PaletteUvMode = Av1Distribution.CreateCopy(source.PaletteUvMode);
        this.PaletteYSize = Av1Distribution.CreateCopy(source.PaletteYSize);
        this.PaletteUvSize = Av1Distribution.CreateCopy(source.PaletteUvSize);
        this.PaletteYColorIndex = Av1Distribution.CreateCopy(source.PaletteYColorIndex);
        this.PaletteUvColorIndex = Av1Distribution.CreateCopy(source.PaletteUvColorIndex);
        this.PartitionTypes = Av1Distribution.CreateCopy(source.PartitionTypes);
        this.FrameYMode = Av1Distribution.CreateCopy(source.FrameYMode);
        this.KeyFrameYMode = Av1Distribution.CreateCopy(source.KeyFrameYMode);
        this.IntraInter = Av1Distribution.CreateCopy(source.IntraInter);
        this.UvMode = Av1Distribution.CreateCopy(source.UvMode);
        this.Skip = Av1Distribution.CreateCopy(source.Skip);
        this.SkipMode = Av1Distribution.CreateCopy(source.SkipMode);
        this.DeltaLoopFilterAbsolute = source.DeltaLoopFilterAbsolute.CreateCopy();
        this.DeltaQuantizerAbsolute = source.DeltaQuantizerAbsolute.CreateCopy();
        this.SegmentId = Av1Distribution.CreateCopy(source.SegmentId);
        this.SegmentIdPredicted = Av1Distribution.CreateCopy(source.SegmentIdPredicted);
        this.AngleDelta = Av1Distribution.CreateCopy(source.AngleDelta);
        this.FilterIntraMode = source.FilterIntraMode.CreateCopy();
        this.FilterIntra = Av1Distribution.CreateCopy(source.FilterIntra);
        this.TransformSize = Av1Distribution.CreateCopy(source.TransformSize);
        this.EndOfBlockFlag = Av1Distribution.CreateCopy(source.EndOfBlockFlag);
        this.CoefficientsBase = Av1Distribution.CreateCopy(source.CoefficientsBase);
        this.BaseEndOfBlock = Av1Distribution.CreateCopy(source.BaseEndOfBlock);
        this.DcSign = Av1Distribution.CreateCopy(source.DcSign);
        this.CoefficientsBaseRange = Av1Distribution.CreateCopy(source.CoefficientsBaseRange);
        this.TransformBlockSkip = Av1Distribution.CreateCopy(source.TransformBlockSkip);
        this.EndOfBlockExtra = Av1Distribution.CreateCopy(source.EndOfBlockExtra);
        this.ChromaFromLumaSign = source.ChromaFromLumaSign.CreateCopy();
        this.ChromaFromLumaAlpha = Av1Distribution.CreateCopy(source.ChromaFromLumaAlpha);
        this.IntraExtendedTransform = Av1Distribution.CreateCopy(source.IntraExtendedTransform);
        this.InterExtendedTransform = Av1Distribution.CreateCopy(source.InterExtendedTransform);
    }

    /// <summary>
    /// Gets the intra-block-copy distribution.
    /// </summary>
    public Av1Distribution IntraBlockCopy { get; }

    /// <summary>
    /// Gets the integer displacement-vector context used by intra-block copy.
    /// </summary>
    public Av1DisplacementVectorContext DisplacementVector { get; }

    /// <summary>
    /// Gets the switchable loop-restoration distribution.
    /// </summary>
    public Av1Distribution SwitchableRestoration { get; }

    /// <summary>
    /// Gets the Wiener loop-restoration distribution.
    /// </summary>
    public Av1Distribution WienerRestoration { get; }

    /// <summary>
    /// Gets the self-guided loop-restoration distribution.
    /// </summary>
    public Av1Distribution SgrProjectionRestoration { get; }

    /// <summary>
    /// Gets the luma palette-mode distributions.
    /// </summary>
    public Av1Distribution[][] PaletteYMode { get; }

    /// <summary>
    /// Gets the chroma palette-mode distributions.
    /// </summary>
    public Av1Distribution[] PaletteUvMode { get; }

    /// <summary>
    /// Gets the luma palette-size distributions.
    /// </summary>
    public Av1Distribution[] PaletteYSize { get; }

    /// <summary>
    /// Gets the chroma palette-size distributions.
    /// </summary>
    public Av1Distribution[] PaletteUvSize { get; }

    /// <summary>
    /// Gets the luma palette color-index distributions.
    /// </summary>
    public Av1Distribution[][] PaletteYColorIndex { get; }

    /// <summary>
    /// Gets the chroma palette color-index distributions.
    /// </summary>
    public Av1Distribution[][] PaletteUvColorIndex { get; }

    /// <summary>
    /// Gets the partition-type distributions.
    /// </summary>
    public Av1Distribution[] PartitionTypes { get; }

    /// <summary>
    /// Gets the inter-frame intra luma-mode distributions indexed by the normative block-size group.
    /// </summary>
    public Av1Distribution[] FrameYMode { get; }

    /// <summary>
    /// Gets the key-frame luma-mode distributions.
    /// </summary>
    public Av1Distribution[][] KeyFrameYMode { get; }

    /// <summary>
    /// Gets the distributions that select intra or inter prediction from the available spatial neighbors.
    /// </summary>
    public Av1Distribution[] IntraInter { get; }

    /// <summary>
    /// Gets the chroma intra-mode distributions.
    /// </summary>
    public Av1Distribution[][] UvMode { get; }

    /// <summary>
    /// Gets the transform-skip distributions.
    /// </summary>
    public Av1Distribution[] Skip { get; }

    /// <summary>
    /// Gets the skip-mode distributions.
    /// </summary>
    public Av1Distribution[] SkipMode { get; }

    /// <summary>
    /// Gets the absolute loop-filter delta distribution.
    /// </summary>
    public Av1Distribution DeltaLoopFilterAbsolute { get; }

    /// <summary>
    /// Gets the absolute quantizer delta distribution.
    /// </summary>
    public Av1Distribution DeltaQuantizerAbsolute { get; }

    /// <summary>
    /// Gets the spatial segment-identifier distributions.
    /// </summary>
    public Av1Distribution[] SegmentId { get; }

    /// <summary>
    /// Gets the temporal segment-map prediction distributions.
    /// </summary>
    public Av1Distribution[] SegmentIdPredicted { get; }

    /// <summary>
    /// Gets the directional angle-delta distributions.
    /// </summary>
    public Av1Distribution[] AngleDelta { get; }

    /// <summary>
    /// Gets the filter-intra mode distribution.
    /// </summary>
    public Av1Distribution FilterIntraMode { get; }

    /// <summary>
    /// Gets the filter-intra enable distributions.
    /// </summary>
    public Av1Distribution[] FilterIntra { get; }

    /// <summary>
    /// Gets the transform-size distributions.
    /// </summary>
    public Av1Distribution[][] TransformSize { get; }

    /// <summary>
    /// Gets the end-of-block token distributions selected for the frame base quantizer.
    /// </summary>
    public Av1Distribution[][][] EndOfBlockFlag { get; }

    /// <summary>
    /// Gets the coefficient base-level distributions selected for the frame base quantizer.
    /// </summary>
    public Av1Distribution[][][] CoefficientsBase { get; }

    /// <summary>
    /// Gets the final-nonzero coefficient distributions selected for the frame base quantizer.
    /// </summary>
    public Av1Distribution[][][] BaseEndOfBlock { get; }

    /// <summary>
    /// Gets the DC sign distributions selected for the frame base quantizer.
    /// </summary>
    public Av1Distribution[][] DcSign { get; }

    /// <summary>
    /// Gets the coefficient base-range distributions selected for the frame base quantizer.
    /// </summary>
    public Av1Distribution[][][] CoefficientsBaseRange { get; }

    /// <summary>
    /// Gets the transform-block skip distributions selected for the frame base quantizer.
    /// </summary>
    public Av1Distribution[][] TransformBlockSkip { get; }

    /// <summary>
    /// Gets the end-of-block extra-bit distributions selected for the frame base quantizer.
    /// </summary>
    public Av1Distribution[][][] EndOfBlockExtra { get; }

    /// <summary>
    /// Gets the joint chroma-from-luma sign distribution.
    /// </summary>
    public Av1Distribution ChromaFromLumaSign { get; }

    /// <summary>
    /// Gets the chroma-from-luma alpha-magnitude distributions.
    /// </summary>
    public Av1Distribution[] ChromaFromLumaAlpha { get; }

    /// <summary>
    /// Gets the intra transform-type distributions.
    /// </summary>
    public Av1Distribution[][][] IntraExtendedTransform { get; }

    /// <summary>
    /// Gets the inter transform-type distributions.
    /// </summary>
    public Av1Distribution[][] InterExtendedTransform { get; }

    /// <summary>
    /// Restores the normative frame defaults selected by a base quantizer index.
    /// </summary>
    /// <param name="qIndex">The frame base quantizer index selecting coefficient distribution defaults.</param>
    public void ResetToDefaults(int qIndex)
    {
        int qContext = GetQContext(qIndex);

        // The prototypes are never exposed to a range reader. Copying their state lets a decoder session reuse the
        // same three mutable object graphs even when successive frames select different coefficient-model bands.
        this.CopyFrom(DefaultPrototypes[qContext]);
    }

    /// <summary>
    /// Maps a frame base quantizer to its normative coefficient-probability initialization band.
    /// </summary>
    /// <param name="qIndex">The frame base quantizer index.</param>
    /// <returns>The zero-based quantizer-band index.</returns>
    private static int GetQContext(int qIndex)
        => qIndex switch
        {
            <= FirstQuantizerBandMaximum => 0,
            <= SecondQuantizerBandMaximum => 1,
            <= ThirdQuantizerBandMaximum => 2,
            _ => 3
        };

    /// <summary>
    /// Replaces every probability threshold and adaptation count with state copied from another frame context.
    /// </summary>
    /// <param name="source">The frame context state to copy.</param>
    public void CopyFrom(Av1FrameEntropyContext source)
    {
        this.IntraBlockCopy.CopyFrom(source.IntraBlockCopy);
        this.DisplacementVector.CopyFrom(source.DisplacementVector);
        this.SwitchableRestoration.CopyFrom(source.SwitchableRestoration);
        this.WienerRestoration.CopyFrom(source.WienerRestoration);
        this.SgrProjectionRestoration.CopyFrom(source.SgrProjectionRestoration);
        CopyState(source.PaletteYMode, this.PaletteYMode);
        CopyState(source.PaletteUvMode, this.PaletteUvMode);
        CopyState(source.PaletteYSize, this.PaletteYSize);
        CopyState(source.PaletteUvSize, this.PaletteUvSize);
        CopyState(source.PaletteYColorIndex, this.PaletteYColorIndex);
        CopyState(source.PaletteUvColorIndex, this.PaletteUvColorIndex);
        CopyState(source.PartitionTypes, this.PartitionTypes);
        CopyState(source.FrameYMode, this.FrameYMode);
        CopyState(source.KeyFrameYMode, this.KeyFrameYMode);
        CopyState(source.IntraInter, this.IntraInter);
        CopyState(source.UvMode, this.UvMode);
        CopyState(source.Skip, this.Skip);
        CopyState(source.SkipMode, this.SkipMode);
        this.DeltaLoopFilterAbsolute.CopyFrom(source.DeltaLoopFilterAbsolute);
        this.DeltaQuantizerAbsolute.CopyFrom(source.DeltaQuantizerAbsolute);
        CopyState(source.SegmentId, this.SegmentId);
        CopyState(source.SegmentIdPredicted, this.SegmentIdPredicted);
        CopyState(source.AngleDelta, this.AngleDelta);
        this.FilterIntraMode.CopyFrom(source.FilterIntraMode);
        CopyState(source.FilterIntra, this.FilterIntra);
        CopyState(source.TransformSize, this.TransformSize);
        CopyState(source.EndOfBlockFlag, this.EndOfBlockFlag);
        CopyState(source.CoefficientsBase, this.CoefficientsBase);
        CopyState(source.BaseEndOfBlock, this.BaseEndOfBlock);
        CopyState(source.DcSign, this.DcSign);
        CopyState(source.CoefficientsBaseRange, this.CoefficientsBaseRange);
        CopyState(source.TransformBlockSkip, this.TransformBlockSkip);
        CopyState(source.EndOfBlockExtra, this.EndOfBlockExtra);
        this.ChromaFromLumaSign.CopyFrom(source.ChromaFromLumaSign);
        CopyState(source.ChromaFromLumaAlpha, this.ChromaFromLumaAlpha);
        CopyState(source.IntraExtendedTransform, this.IntraExtendedTransform);
        CopyState(source.InterExtendedTransform, this.InterExtendedTransform);
    }

    /// <summary>
    /// Copies this tile-adapted context into a destination used as completed frame state.
    /// </summary>
    /// <param name="destination">The independently owned frame context that receives the snapshot.</param>
    /// <remarks>
    /// AV1 resets CDF observation counters after publishing the context-update tile. The copied thresholds remain
    /// adapted, while the next frame starts its update-rate history from zero.
    /// </remarks>
    public void SnapshotTo(Av1FrameEntropyContext destination)
    {
        destination.CopyFrom(this);
        destination.ResetUpdateCounts();
    }

    /// <summary>
    /// Resets the observation count of every distribution without changing its probability thresholds.
    /// </summary>
    private void ResetUpdateCounts()
    {
        this.IntraBlockCopy.ResetUpdateCount();
        this.DisplacementVector.ResetUpdateCounts();
        this.SwitchableRestoration.ResetUpdateCount();
        this.WienerRestoration.ResetUpdateCount();
        this.SgrProjectionRestoration.ResetUpdateCount();
        ResetUpdateCounts(this.PaletteYMode);
        ResetUpdateCounts(this.PaletteUvMode);
        ResetUpdateCounts(this.PaletteYSize);
        ResetUpdateCounts(this.PaletteUvSize);
        ResetUpdateCounts(this.PaletteYColorIndex);
        ResetUpdateCounts(this.PaletteUvColorIndex);
        ResetUpdateCounts(this.PartitionTypes);
        ResetUpdateCounts(this.FrameYMode);
        ResetUpdateCounts(this.KeyFrameYMode);
        ResetUpdateCounts(this.IntraInter);
        ResetUpdateCounts(this.UvMode);
        ResetUpdateCounts(this.Skip);
        ResetUpdateCounts(this.SkipMode);
        this.DeltaLoopFilterAbsolute.ResetUpdateCount();
        this.DeltaQuantizerAbsolute.ResetUpdateCount();
        ResetUpdateCounts(this.SegmentId);
        ResetUpdateCounts(this.SegmentIdPredicted);
        ResetUpdateCounts(this.AngleDelta);
        this.FilterIntraMode.ResetUpdateCount();
        ResetUpdateCounts(this.FilterIntra);
        ResetUpdateCounts(this.TransformSize);
        ResetUpdateCounts(this.EndOfBlockFlag);
        ResetUpdateCounts(this.CoefficientsBase);
        ResetUpdateCounts(this.BaseEndOfBlock);
        ResetUpdateCounts(this.DcSign);
        ResetUpdateCounts(this.CoefficientsBaseRange);
        ResetUpdateCounts(this.TransformBlockSkip);
        ResetUpdateCounts(this.EndOfBlockExtra);
        this.ChromaFromLumaSign.ResetUpdateCount();
        ResetUpdateCounts(this.ChromaFromLumaAlpha);
        ResetUpdateCounts(this.IntraExtendedTransform);
        ResetUpdateCounts(this.InterExtendedTransform);
    }

    /// <summary>
    /// Copies one distribution row into an existing row with the same default-table shape.
    /// </summary>
    /// <param name="source">The source distribution row.</param>
    /// <param name="destination">The destination distribution row.</param>
    private static void CopyState(Av1Distribution[] source, Av1Distribution[] destination)
    {
        for (int index = 0; index < source.Length; index++)
        {
            destination[index].CopyFrom(source[index]);
        }
    }

    /// <summary>
    /// Copies a two-dimensional distribution table into an existing table with the same default-table shape.
    /// </summary>
    /// <param name="source">The source distribution table.</param>
    /// <param name="destination">The destination distribution table.</param>
    private static void CopyState(Av1Distribution[][] source, Av1Distribution[][] destination)
    {
        for (int index = 0; index < source.Length; index++)
        {
            CopyState(source[index], destination[index]);
        }
    }

    /// <summary>
    /// Copies a three-dimensional distribution table into an existing table with the same default-table shape.
    /// </summary>
    /// <param name="source">The source distribution table.</param>
    /// <param name="destination">The destination distribution table.</param>
    private static void CopyState(Av1Distribution[][][] source, Av1Distribution[][][] destination)
    {
        for (int index = 0; index < source.Length; index++)
        {
            CopyState(source[index], destination[index]);
        }
    }

    /// <summary>
    /// Resets observation counts in one distribution row.
    /// </summary>
    /// <param name="distributions">The distribution row to reset.</param>
    private static void ResetUpdateCounts(Av1Distribution[] distributions)
    {
        for (int index = 0; index < distributions.Length; index++)
        {
            distributions[index].ResetUpdateCount();
        }
    }

    /// <summary>
    /// Resets observation counts in a two-dimensional distribution table.
    /// </summary>
    /// <param name="distributions">The distribution table to reset.</param>
    private static void ResetUpdateCounts(Av1Distribution[][] distributions)
    {
        for (int index = 0; index < distributions.Length; index++)
        {
            ResetUpdateCounts(distributions[index]);
        }
    }

    /// <summary>
    /// Resets observation counts in a three-dimensional distribution table.
    /// </summary>
    /// <param name="distributions">The distribution table to reset.</param>
    private static void ResetUpdateCounts(Av1Distribution[][][] distributions)
    {
        for (int index = 0; index < distributions.Length; index++)
        {
            ResetUpdateCounts(distributions[index]);
        }
    }
}
