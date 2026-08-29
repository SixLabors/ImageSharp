// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Hevc;

/// <summary>
/// Owns the bounded state used to reconstruct one independently decodable HEVC still picture.
/// </summary>
internal sealed partial class HevcPictureDecoder : IDisposable
{
    /// <summary>
    /// The maximum square transform-block sample count.
    /// </summary>
    private const int MaximumTransformSampleCount = 32 * 32;

    /// <summary>
    /// The largest reference array used by a thirty-two-sample prediction block.
    /// </summary>
    private const int MaximumReferenceLength = (2 * 32) + 1;

    /// <summary>
    /// The configuration providing picture-lifetime allocations.
    /// </summary>
    private readonly Configuration configuration;

    /// <summary>
    /// The active picture parameters.
    /// </summary>
    private readonly HevcPictureParameterSet pictureParameterSet;

    /// <summary>
    /// The active sequence parameters.
    /// </summary>
    private readonly HevcSequenceParameterSet sequenceParameterSet;

    /// <summary>
    /// The decoded coding-unit state.
    /// </summary>
    private readonly HevcCodingTreeState[] codingTreeStates;

    /// <summary>
    /// The decoded intra-prediction modes.
    /// </summary>
    private readonly HevcIntraPredictionState[] intraPredictionStates;

    /// <summary>
    /// The completed prediction-block state used for reference availability.
    /// </summary>
    private readonly HevcReconstructionState reconstructionState;

    /// <summary>
    /// The reusable coefficient entropy decoder.
    /// </summary>
    private readonly HevcCoefficientDecoder coefficientDecoder;

    /// <summary>
    /// The resolved sample-adaptive-offset parameters for every coding-tree block.
    /// </summary>
    private readonly HevcSampleAdaptiveOffsetState sampleAdaptiveOffsetState;

    /// <summary>
    /// The transform and prediction boundaries required by the deblocking stage.
    /// </summary>
    private readonly HevcDeblockingState deblockingState;

    /// <summary>
    /// The integer coefficient, residual, and transform workspace.
    /// </summary>
    private readonly IMemoryOwner<int> integerScratch;

    /// <summary>
    /// The prediction, reference, and reference-substitution workspace.
    /// </summary>
    private readonly IMemoryOwner<ushort> predictionScratch;

    /// <summary>
    /// The ordered intra-reference availability workspace.
    /// </summary>
    private readonly IMemoryOwner<bool> availabilityScratch;

    /// <summary>
    /// The per-color-plane adaptive contexts captured after the second coding-tree block of a wavefront row.
    /// </summary>
    private readonly HevcCabacContext[] wavefrontContexts = new HevcCabacContext[HevcCabacContexts.ContextCount * 3];

    /// <summary>
    /// The per-color-plane persistent Rice statistics captured with the wavefront probability contexts.
    /// </summary>
    private readonly int[] wavefrontRiceAdaptation = new int[12];

    /// <summary>
    /// Whether retained wavefront contexts are available for each color plane.
    /// </summary>
    private InlineArray4<bool> hasWavefrontContexts;

    /// <summary>
    /// The tile that owns each color plane's retained wavefront contexts.
    /// </summary>
    private InlineArray4<int> wavefrontContextTileIndices;

    /// <summary>
    /// The adaptive contexts retained at the end of a dependent-slice prediction region.
    /// </summary>
    private readonly HevcCabacContext[] sliceSegmentContexts = new HevcCabacContext[HevcCabacContexts.ContextCount * 3];

    /// <summary>
    /// The persistent Rice statistics retained with dependent-slice probability contexts.
    /// </summary>
    private readonly int[] sliceSegmentRiceAdaptation = new int[12];

    /// <summary>
    /// Whether retained dependent-slice contexts are available.
    /// </summary>
    private InlineArray4<bool> hasSliceSegmentContexts;

    /// <summary>
    /// The luma quantization parameter most recently coded in the current prediction region.
    /// </summary>
    private int lastCodedQuantizationParameter;

    /// <summary>
    /// The effective luma quantization parameter of the current quantization group.
    /// </summary>
    private int currentQuantizationParameter;

    /// <summary>
    /// The one-based chroma quantization-offset-list selector of the current quantization group.
    /// </summary>
    private int currentChromaQuantizationAdjustment;

    /// <summary>
    /// The Cb quantization-parameter offset signaled by the governing independent slice.
    /// </summary>
    private int currentSliceChromaBlueQuantizationOffset;

    /// <summary>
    /// The Cr quantization-parameter offset signaled by the governing independent slice.
    /// </summary>
    private int currentSliceChromaRedQuantizationOffset;

    /// <summary>
    /// Whether the current quantization group can still signal its luma delta.
    /// </summary>
    private bool quantizationParameterDeltaPending;

    /// <summary>
    /// Whether the current quantization group can still signal its chroma adjustment.
    /// </summary>
    private bool chromaQuantizationAdjustmentPending;

    /// <summary>
    /// Initializes a new instance of the <see cref="HevcPictureDecoder"/> class.
    /// </summary>
    /// <param name="configuration">The configuration providing all decoder-owned memory.</param>
    /// <param name="pictureParameterSet">The picture parameters governing the coded still image.</param>
    public HevcPictureDecoder(Configuration configuration, HevcPictureParameterSet pictureParameterSet)
    {
        this.configuration = configuration;
        this.pictureParameterSet = pictureParameterSet;
        this.sequenceParameterSet = pictureParameterSet.SequenceParameterSet;
        HevcPictureBuffer? picture = null;
        HevcCodingTreeState[]? codingTreeStates = null;
        HevcIntraPredictionState[]? intraPredictionStates = null;
        HevcReconstructionState? reconstructionState = null;
        HevcCoefficientDecoder? coefficientDecoder = null;
        HevcSampleAdaptiveOffsetState? sampleAdaptiveOffsetState = null;
        HevcDeblockingState? deblockingState = null;
        IMemoryOwner<int>? integerScratch = null;
        IMemoryOwner<ushort>? predictionScratch = null;
        IMemoryOwner<bool>? availabilityScratch = null;
        try
        {
            picture = new HevcPictureBuffer(configuration, this.sequenceParameterSet);
            int codingTreeStateCount = this.sequenceParameterSet.SeparateColorPlaneFlag ? 3 : 1;
            codingTreeStates = new HevcCodingTreeState[codingTreeStateCount];
            for (int index = 0; index < codingTreeStates.Length; index++)
            {
                codingTreeStates[index] = new HevcCodingTreeState(configuration, this.sequenceParameterSet);
            }

            int intraPredictionStateCount = this.sequenceParameterSet.SeparateColorPlaneFlag ? 3 : 1;
            intraPredictionStates = new HevcIntraPredictionState[intraPredictionStateCount];
            for (int index = 0; index < intraPredictionStates.Length; index++)
            {
                intraPredictionStates[index] = new HevcIntraPredictionState(configuration, this.sequenceParameterSet);
            }

            reconstructionState = new HevcReconstructionState(configuration, this.sequenceParameterSet);
            coefficientDecoder = new HevcCoefficientDecoder(configuration);
            int codingTreeBlockCount = HevcParameterSetSyntax.GetCodingTreeBlockCount(
                this.sequenceParameterSet.Width,
                this.sequenceParameterSet.CodingTreeBlockLog2)
                * HevcParameterSetSyntax.GetCodingTreeBlockCount(
                    this.sequenceParameterSet.Height,
                    this.sequenceParameterSet.CodingTreeBlockLog2);

            sampleAdaptiveOffsetState = new HevcSampleAdaptiveOffsetState(configuration, codingTreeBlockCount);
            deblockingState = new HevcDeblockingState(configuration, this.sequenceParameterSet);

            // Six transform-sized integer regions retain quantized, dequantized, reconstructed, cross-component, and
            // two-pass inverse-transform data without allocating in coding-unit or transform-unit loops.
            integerScratch = configuration.MemoryAllocator.Allocate<int>(MaximumTransformSampleCount * 6);
            int maximumPredictionScratch = HevcIntraPredictor.GetScratchLength(5);
            int maximumReferenceScratch = HevcIntraPredictor.GetReferenceScratchLength(5, 4);
            predictionScratch = configuration.MemoryAllocator.Allocate<ushort>(
                MaximumTransformSampleCount + maximumPredictionScratch + maximumReferenceScratch + (MaximumReferenceLength * 4));

            availabilityScratch = configuration.MemoryAllocator.Allocate<bool>((4 * 32 / 2) + 1);

            this.Picture = picture;
            this.codingTreeStates = codingTreeStates;
            this.intraPredictionStates = intraPredictionStates;
            this.reconstructionState = reconstructionState;
            this.coefficientDecoder = coefficientDecoder;
            this.sampleAdaptiveOffsetState = sampleAdaptiveOffsetState;
            this.deblockingState = deblockingState;
            this.integerScratch = integerScratch;
            this.predictionScratch = predictionScratch;
            this.availabilityScratch = availabilityScratch;
        }
        catch
        {
            // No decoder ownership is published when construction fails. Unwind every completed child owner in reverse
            // order because the caller cannot dispose an object whose constructor did not return.
            availabilityScratch?.Dispose();
            predictionScratch?.Dispose();
            integerScratch?.Dispose();
            deblockingState?.Dispose();
            sampleAdaptiveOffsetState?.Dispose();
            coefficientDecoder?.Dispose();
            reconstructionState?.Dispose();
            if (intraPredictionStates is not null)
            {
                for (int index = intraPredictionStates.Length - 1; index >= 0; index--)
                {
                    intraPredictionStates[index]?.Dispose();
                }
            }

            if (codingTreeStates is not null)
            {
                for (int index = codingTreeStates.Length - 1; index >= 0; index--)
                {
                    codingTreeStates[index]?.Dispose();
                }
            }

            picture?.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Gets the native-precision reconstructed component planes.
    /// </summary>
    public HevcPictureBuffer Picture { get; }

    /// <summary>
    /// Reconstructs every ordered slice segment in one independently decodable image item.
    /// </summary>
    /// <param name="bitstream">The validated image-item NAL units and slice segments.</param>
    /// <exception cref="InvalidImageContentException">
    /// A slice changes the coded picture parameters, overlaps an earlier segment, or does not terminate at a valid
    /// coding-tree boundary.
    /// </exception>
    public void Decode(HevcImageItemBitstream bitstream)
    {
        HevcTileLayout tileLayout = new(this.pictureParameterSet);
        int planeCount = this.sequenceParameterSet.SeparateColorPlaneFlag ? 3 : 1;
        int[] nextCodingTreeBlockAddressesInTileScan = new int[planeCount];
        int[] independentSliceIndices = new int[planeCount];
        HevcSliceSegmentHeader?[] independentSlices = new HevcSliceSegmentHeader?[planeCount];
        for (int sliceIndex = 0; sliceIndex < bitstream.SliceSegments.Count; sliceIndex++)
        {
            HevcSliceSegmentHeader slice = bitstream.SliceSegments[sliceIndex];
            int colorPlane = this.sequenceParameterSet.SeparateColorPlaneFlag ? slice.ColorPlaneId : 0;
            if (slice.PictureParameterSet.Id != this.pictureParameterSet.Id
                || slice.PictureParameterSet.SequenceParameterSetId != this.pictureParameterSet.SequenceParameterSetId)
            {
                throw new InvalidImageContentException("The HEVC still picture changes parameter sets between slice segments.");
            }

            if (!slice.DependentSliceSegment)
            {
                independentSlices[colorPlane] = slice;
                independentSliceIndices[colorPlane]++;
            }

            HevcSliceSegmentHeader? independentSlice = independentSlices[colorPlane];
            if (independentSlice is null)
            {
                throw new InvalidImageContentException("The HEVC still picture begins with a dependent slice segment.");
            }

            int sliceStartAddressInTileScan = tileLayout.GetTileScanAddress(slice.SliceSegmentAddress);
            if (sliceStartAddressInTileScan != nextCodingTreeBlockAddressesInTileScan[colorPlane])
            {
                throw new InvalidImageContentException("The HEVC slice segments do not cover the coded picture in order.");
            }

            nextCodingTreeBlockAddressesInTileScan[colorPlane] = this.DecodeSliceSegment(
                slice,
                independentSlice,
                independentSliceIndices[colorPlane],
                in tileLayout,
                sliceStartAddressInTileScan,
                tileLayout.GetTileScanAddress(independentSlice.SliceSegmentAddress));
        }

        int codingTreeBlockCount = HevcParameterSetSyntax.GetCodingTreeBlockCount(
            this.sequenceParameterSet.Width,
            this.sequenceParameterSet.CodingTreeBlockLog2)
            * HevcParameterSetSyntax.GetCodingTreeBlockCount(
                this.sequenceParameterSet.Height,
                this.sequenceParameterSet.CodingTreeBlockLog2);

        foreach (int nextAddress in nextCodingTreeBlockAddressesInTileScan)
        {
            if (nextAddress != codingTreeBlockCount)
            {
                throw new InvalidImageContentException("The HEVC slice segments do not reconstruct the complete coded picture.");
            }
        }

        this.ApplyDeblockingFilter(in tileLayout);
        if (this.sampleAdaptiveOffsetState.HasEnabledParameters)
        {
            // SAO classification always observes the complete post-deblocking picture, never samples already offset by an
            // earlier CTB. One picture-lifetime snapshot provides that invariant without row allocations or filter-order coupling.
            using HevcPictureBuffer sampleAdaptiveOffsetSource = new(this.configuration, this.sequenceParameterSet);
            this.Picture.CopyTo(sampleAdaptiveOffsetSource);
            this.ApplySampleAdaptiveOffset(sampleAdaptiveOffsetSource, in tileLayout);
        }
    }

    /// <summary>
    /// Releases all current-picture state and reconstructed planes.
    /// </summary>
    public void Dispose()
    {
        this.availabilityScratch.Dispose();
        this.predictionScratch.Dispose();
        this.integerScratch.Dispose();
        this.deblockingState.Dispose();
        this.sampleAdaptiveOffsetState.Dispose();
        this.coefficientDecoder.Dispose();
        this.reconstructionState.Dispose();
        foreach (HevcIntraPredictionState state in this.intraPredictionStates)
        {
            state.Dispose();
        }

        foreach (HevcCodingTreeState state in this.codingTreeStates)
        {
            state.Dispose();
        }

        this.Picture.Dispose();
    }
}
