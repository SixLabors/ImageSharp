// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Hevc;

/// <summary>
/// Owns the adaptive CABAC probability contexts used to decode one intra-coded HEVC entropy substream.
/// </summary>
internal sealed class HevcCabacContexts
{
    /// <summary>
    /// The first transquant-bypass context.
    /// </summary>
    private const int TransquantBypassOffset = 0;

    /// <summary>
    /// The first coding-unit split context.
    /// </summary>
    private const int SplitOffset = 1;

    /// <summary>
    /// The intra partition-size context.
    /// </summary>
    private const int PartitionSizeOffset = 4;

    /// <summary>
    /// The luma intra-prediction context.
    /// </summary>
    private const int IntraPredictionOffset = 5;

    /// <summary>
    /// The first chroma intra-prediction context.
    /// </summary>
    private const int ChromaPredictionOffset = 6;

    /// <summary>
    /// The first luma quantization-delta context.
    /// </summary>
    private const int DeltaQuantizationOffset = 8;

    /// <summary>
    /// The chroma quantization-adjustment flag context.
    /// </summary>
    private const int ChromaQuantizationAdjustmentFlagOffset = 11;

    /// <summary>
    /// The chroma quantization-adjustment index context.
    /// </summary>
    private const int ChromaQuantizationAdjustmentIndexOffset = 12;

    /// <summary>
    /// The first transform-tree coded-block-flag context.
    /// </summary>
    private const int TransformCodedBlockFlagOffset = 13;

    /// <summary>
    /// The first horizontal last-significant-coefficient context.
    /// </summary>
    private const int LastSignificantXOffset = 23;

    /// <summary>
    /// The first vertical last-significant-coefficient context.
    /// </summary>
    private const int LastSignificantYOffset = 53;

    /// <summary>
    /// The first significant-coefficient-group context.
    /// </summary>
    private const int SignificantCoefficientGroupOffset = 83;

    /// <summary>
    /// The first significant-coefficient context.
    /// </summary>
    private const int SignificantCoefficientOffset = 87;

    /// <summary>
    /// The first greater-than-one coefficient-level context.
    /// </summary>
    private const int GreaterThanOneOffset = 131;

    /// <summary>
    /// The first greater-than-two coefficient-level context.
    /// </summary>
    private const int GreaterThanTwoOffset = 155;

    /// <summary>
    /// The sample-adaptive-offset merge context.
    /// </summary>
    private const int SampleAdaptiveOffsetMergeOffset = 161;

    /// <summary>
    /// The sample-adaptive-offset type context.
    /// </summary>
    private const int SampleAdaptiveOffsetTypeOffset = 162;

    /// <summary>
    /// The first transform-tree subdivision context.
    /// </summary>
    private const int TransformSubdivisionOffset = 163;

    /// <summary>
    /// The first transform-skip context.
    /// </summary>
    private const int TransformSkipOffset = 166;

    /// <summary>
    /// The first cross-component prediction context.
    /// </summary>
    private const int CrossComponentPredictionOffset = 168;

    /// <summary>
    /// The number of contexts used by the independently coded intra-picture syntax.
    /// </summary>
    private const int ContextCount = 178;

    /// <summary>
    /// The HEVC intra-slice initialization values in the same order as the owned context ranges.
    /// </summary>
    private static readonly byte[] IntraInitializationValues =
    [

        // cu_transquant_bypass_flag
        154,

        // split_cu_flag
        139, 141, 157,

        // part_mode and prev_intra_luma_pred_flag
        184,
        184,

        // intra_chroma_pred_mode
        63, 139,

        // cu_qp_delta_abs, cu_chroma_qp_offset_flag, and cu_chroma_qp_offset_idx
        154, 154, 154,
        154,
        154,

        // cbf_luma followed by the chroma coded-block flags
        111, 141, 154, 154, 154,
        94, 138, 182, 154, 154,

        // last_sig_coeff_x_prefix: luma followed by chroma
        110, 110, 124, 125, 140, 153, 125, 127, 140, 109, 111, 143, 127, 111, 79,
        108, 123, 63, 154, 154, 154, 154, 154, 154, 154, 154, 154, 154, 154, 154,

        // last_sig_coeff_y_prefix: luma followed by chroma
        110, 110, 124, 125, 140, 153, 125, 127, 140, 109, 111, 143, 127, 111, 79,
        108, 123, 63, 154, 154, 154, 154, 154, 154, 154, 154, 154, 154, 154, 154,

        // coded_sub_block_flag: luma followed by chroma
        91, 171, 134, 141,

        // sig_coeff_flag: luma followed by chroma
        111, 111, 125, 110, 110, 94, 124, 108, 124, 107, 125, 141, 179, 153,
        125, 107, 125, 141, 179, 153, 125, 107, 125, 141, 179, 153, 125, 141,
        140, 139, 182, 182, 152, 136, 152, 136, 153, 136, 139, 111, 136, 139, 111, 111,

        // coeff_abs_level_greater1_flag: luma followed by chroma
        140, 92, 137, 138, 140, 152, 138, 139, 153, 74, 149, 92, 139, 107, 122, 152,
        140, 179, 166, 182, 140, 227, 122, 197,

        // coeff_abs_level_greater2_flag: luma followed by chroma
        138, 153, 136, 167, 152, 152,

        // sao_merge_flag and sao_type_idx
        153,
        200,

        // split_transform_flag
        153, 138, 138,

        // transform_skip_flag: luma followed by chroma
        139, 139,

        // cross_comp_pred: five sign/magnitude contexts for Cb followed by five for Cr
        154, 154, 154, 154, 154, 154, 154, 154, 154, 154
    ];

    /// <summary>
    /// The contiguous adaptive context storage owned by the entropy substream.
    /// </summary>
    private readonly HevcCabacContext[] contexts;

    /// <summary>
    /// Initializes a new instance of the <see cref="HevcCabacContexts"/> class for an intra-coded slice.
    /// </summary>
    /// <param name="quantizationParameter">The slice luma quantization parameter.</param>
    public HevcCabacContexts(int quantizationParameter)
    {
        this.contexts = new HevcCabacContext[ContextCount];
        for (int index = 0; index < this.contexts.Length; index++)
        {
            this.contexts[index] = new HevcCabacContext(quantizationParameter, IntraInitializationValues[index]);
        }
    }

    /// <summary>
    /// Gets the coding-unit transquant-bypass context.
    /// </summary>
    public Span<HevcCabacContext> TransquantBypass => this.contexts.AsSpan(TransquantBypassOffset, 1);

    /// <summary>
    /// Gets the coding-unit split contexts, ordered by neighboring split depth.
    /// </summary>
    public Span<HevcCabacContext> Split => this.contexts.AsSpan(SplitOffset, 3);

    /// <summary>
    /// Gets the intra partition-size context.
    /// </summary>
    public Span<HevcCabacContext> PartitionSize => this.contexts.AsSpan(PartitionSizeOffset, 1);

    /// <summary>
    /// Gets the luma intra-prediction context.
    /// </summary>
    public Span<HevcCabacContext> IntraPrediction => this.contexts.AsSpan(IntraPredictionOffset, 1);

    /// <summary>
    /// Gets the chroma intra-prediction contexts.
    /// </summary>
    public Span<HevcCabacContext> ChromaPrediction => this.contexts.AsSpan(ChromaPredictionOffset, 2);

    /// <summary>
    /// Gets the luma quantization-delta contexts.
    /// </summary>
    public Span<HevcCabacContext> DeltaQuantization => this.contexts.AsSpan(DeltaQuantizationOffset, 3);

    /// <summary>
    /// Gets the chroma quantization-adjustment flag context.
    /// </summary>
    public Span<HevcCabacContext> ChromaQuantizationAdjustmentFlag =>
        this.contexts.AsSpan(ChromaQuantizationAdjustmentFlagOffset, 1);

    /// <summary>
    /// Gets the chroma quantization-adjustment index context.
    /// </summary>
    public Span<HevcCabacContext> ChromaQuantizationAdjustmentIndex =>
        this.contexts.AsSpan(ChromaQuantizationAdjustmentIndexOffset, 1);

    /// <summary>
    /// Gets the transform-tree coded-block-flag contexts, with luma preceding chroma.
    /// </summary>
    public Span<HevcCabacContext> TransformCodedBlockFlag =>
        this.contexts.AsSpan(TransformCodedBlockFlagOffset, 10);

    /// <summary>
    /// Gets the horizontal last-significant-coefficient contexts, with luma preceding chroma.
    /// </summary>
    public Span<HevcCabacContext> LastSignificantX => this.contexts.AsSpan(LastSignificantXOffset, 30);

    /// <summary>
    /// Gets the vertical last-significant-coefficient contexts, with luma preceding chroma.
    /// </summary>
    public Span<HevcCabacContext> LastSignificantY => this.contexts.AsSpan(LastSignificantYOffset, 30);

    /// <summary>
    /// Gets the significant-coefficient-group contexts, with luma preceding chroma.
    /// </summary>
    public Span<HevcCabacContext> SignificantCoefficientGroup =>
        this.contexts.AsSpan(SignificantCoefficientGroupOffset, 4);

    /// <summary>
    /// Gets the significant-coefficient contexts, with luma preceding chroma.
    /// </summary>
    public Span<HevcCabacContext> SignificantCoefficient =>
        this.contexts.AsSpan(SignificantCoefficientOffset, 44);

    /// <summary>
    /// Gets the greater-than-one coefficient-level contexts, with luma preceding chroma.
    /// </summary>
    public Span<HevcCabacContext> GreaterThanOne => this.contexts.AsSpan(GreaterThanOneOffset, 24);

    /// <summary>
    /// Gets the greater-than-two coefficient-level contexts, with luma preceding chroma.
    /// </summary>
    public Span<HevcCabacContext> GreaterThanTwo => this.contexts.AsSpan(GreaterThanTwoOffset, 6);

    /// <summary>
    /// Gets the sample-adaptive-offset merge context.
    /// </summary>
    public Span<HevcCabacContext> SampleAdaptiveOffsetMerge =>
        this.contexts.AsSpan(SampleAdaptiveOffsetMergeOffset, 1);

    /// <summary>
    /// Gets the sample-adaptive-offset type context.
    /// </summary>
    public Span<HevcCabacContext> SampleAdaptiveOffsetType =>
        this.contexts.AsSpan(SampleAdaptiveOffsetTypeOffset, 1);

    /// <summary>
    /// Gets the transform-tree subdivision contexts.
    /// </summary>
    public Span<HevcCabacContext> TransformSubdivision =>
        this.contexts.AsSpan(TransformSubdivisionOffset, 3);

    /// <summary>
    /// Gets the transform-skip contexts, with luma preceding chroma.
    /// </summary>
    public Span<HevcCabacContext> TransformSkip => this.contexts.AsSpan(TransformSkipOffset, 2);

    /// <summary>
    /// Gets the cross-component prediction contexts, with Cb preceding Cr.
    /// </summary>
    public Span<HevcCabacContext> CrossComponentPrediction =>
        this.contexts.AsSpan(CrossComponentPredictionOffset, 10);
}
