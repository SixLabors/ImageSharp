// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Hevc;

/// <summary>
/// Contains the decoded header and entropy-coded payload of one HEVC still-picture slice segment.
/// </summary>
internal sealed class HevcSliceSegmentHeader
{
    /// <summary>
    /// The decoded-byte lengths preceding each tile or wavefront entropy entry point.
    /// </summary>
    private int[] entryPointOffsets = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="HevcSliceSegmentHeader"/> class.
    /// </summary>
    /// <param name="nalUnit">The item-local instantaneous-decoder-refresh NAL unit.</param>
    /// <param name="pictureParameterSets">The picture parameter sets available to the coded image item.</param>
    /// <exception cref="InvalidImageContentException">
    /// The NAL unit is not a base-layer IDR slice, references unavailable parameters, or contains malformed
    /// still-picture slice-header syntax.
    /// </exception>
    public HevcSliceSegmentHeader(
        HevcNalUnit nalUnit,
        IReadOnlyList<HevcPictureParameterSet> pictureParameterSets)
    {
        if (!nalUnit.Header.IsInstantaneousDecoderRefresh
            || nalUnit.Header.LayerId != 0
            || nalUnit.Header.TemporalId != 0)
        {
            throw new InvalidImageContentException("The HEVC image item contains a non-IDR or layered coded slice.");
        }

        this.NalUnit = nalUnit;
        HevcBitReader reader = new(nalUnit.Rbsp.Span);
        this.FirstSliceSegmentInPicture = reader.ReadFlag();

        // An IDR item has no earlier picture whose output can affect the returned still image. Consume the required
        // random-access flag without retaining sequence-output state in the image decoder.
        reader.ReadFlag();

        uint pictureParameterSetId = reader.ReadUnsignedExpGolomb();
        if (pictureParameterSetId > 63)
        {
            throw new InvalidImageContentException("The HEVC slice segment has an invalid picture-parameter-set identifier.");
        }

        HevcPictureParameterSet? pictureParameterSet = null;
        foreach (HevcPictureParameterSet candidate in pictureParameterSets)
        {
            if (candidate.Id == pictureParameterSetId)
            {
                pictureParameterSet = candidate;
                break;
            }
        }

        if (pictureParameterSet is null)
        {
            throw new InvalidImageContentException("The HEVC slice segment references an unavailable picture parameter set.");
        }

        this.PictureParameterSet = pictureParameterSet;
        HevcSequenceParameterSet sequenceParameterSet = pictureParameterSet.SequenceParameterSet;
        if (pictureParameterSet.DependentSliceSegmentsEnabled && !this.FirstSliceSegmentInPicture)
        {
            this.DependentSliceSegment = reader.ReadFlag();
        }

        int codingTreeBlockColumns = HevcParameterSetSyntax.GetCodingTreeBlockCount(
            sequenceParameterSet.Width,
            sequenceParameterSet.CodingTreeBlockLog2);

        int codingTreeBlockRows = HevcParameterSetSyntax.GetCodingTreeBlockCount(
            sequenceParameterSet.Height,
            sequenceParameterSet.CodingTreeBlockLog2);

        int codingTreeBlockCount = codingTreeBlockColumns * codingTreeBlockRows;
        if (!this.FirstSliceSegmentInPicture)
        {
            int addressBitCount = HevcParameterSetSyntax.GetCeilingLog2(codingTreeBlockCount);
            uint address = reader.ReadBits(addressBitCount);
            if (address >= codingTreeBlockCount)
            {
                throw new InvalidImageContentException("The HEVC slice segment address is outside the coded picture.");
            }

            this.SliceSegmentAddress = (int)address;
        }

        if (!this.DependentSliceSegment)
        {
            this.ReadIndependentHeader(ref reader);
        }

        this.ReadEntryPoints(ref reader, codingTreeBlockCount);
        if (pictureParameterSet.SliceSegmentHeaderExtensionPresent)
        {
            uint extensionLength = reader.ReadUnsignedExpGolomb();
            if (extensionLength > int.MaxValue || extensionLength > reader.BitsRemaining / 8)
            {
                throw new InvalidImageContentException("The HEVC slice-segment header extension is truncated.");
            }

            for (int byteIndex = 0; byteIndex < extensionLength; byteIndex++)
            {
                reader.ReadBits(8);
            }
        }

        reader.ReadByteAlignment();
        this.HeaderLength = reader.BitPosition / 8;
        this.SliceData = nalUnit.Rbsp[this.HeaderLength..];
        if (this.SliceData.IsEmpty)
        {
            throw new InvalidImageContentException("The HEVC slice segment contains no entropy-coded data.");
        }

        int encodedHeaderLength = GetEncodedPayloadOffset(
            this.HeaderLength,
            nalUnit.EmulationPreventionBytePositions.Span);

        int availableEncodedData = nalUnit.EncodedPayloadLength - encodedHeaderLength;
        int cumulativeEntryPointOffset = 0;
        int previousDecodedBoundary = this.HeaderLength;
        int[] entryPointOffsets = this.entryPointOffsets;
        for (int index = 0; index < entryPointOffsets.Length; index++)
        {
            int entryPointOffset = entryPointOffsets[index];
            if (cumulativeEntryPointOffset > availableEncodedData - entryPointOffset)
            {
                throw new InvalidImageContentException("The HEVC slice entry point extends beyond its NAL unit.");
            }

            cumulativeEntryPointOffset += entryPointOffset;
            int encodedBoundary = encodedHeaderLength + cumulativeEntryPointOffset;
            int decodedBoundary = GetDecodedPayloadOffset(
                encodedBoundary,
                nalUnit.EmulationPreventionBytePositions.Span);

            // entry_point_offset_minus1 counts encoded NAL bytes. The entropy decoder consumes the de-escaped RBSP,
            // so each retained substream length must exclude prevention bytes from its own encoded interval.
            entryPointOffsets[index] = decodedBoundary - previousDecodedBoundary;
            previousDecodedBoundary = decodedBoundary;
        }
    }

    /// <summary>
    /// Gets the complete decoded NAL unit containing this slice segment.
    /// </summary>
    public HevcNalUnit NalUnit { get; }

    /// <summary>
    /// Gets a value indicating whether this is the first slice segment of the coded picture.
    /// </summary>
    public bool FirstSliceSegmentInPicture { get; }

    /// <summary>
    /// Gets a value indicating whether this segment inherits syntax from an earlier independent slice.
    /// </summary>
    public bool DependentSliceSegment { get; }

    /// <summary>
    /// Gets the picture parameters selected by this slice segment.
    /// </summary>
    public HevcPictureParameterSet PictureParameterSet { get; }

    /// <summary>
    /// Gets the raster-scan address of the first coding-tree block in this slice segment.
    /// </summary>
    public int SliceSegmentAddress { get; }

    /// <summary>
    /// Gets the independent slice prediction type, or <see langword="null"/> for a dependent segment.
    /// </summary>
    public HevcSliceType? SliceType { get; private set; }

    /// <summary>
    /// Gets the selected color-plane identifier for separate-plane 4:4:4 coding.
    /// </summary>
    public byte ColorPlaneId { get; private set; }

    /// <summary>
    /// Gets a value indicating whether luma sample-adaptive offset filtering is enabled.
    /// </summary>
    public bool? SampleAdaptiveOffsetLumaEnabled { get; private set; }

    /// <summary>
    /// Gets a value indicating whether chroma sample-adaptive offset filtering is enabled.
    /// </summary>
    public bool? SampleAdaptiveOffsetChromaEnabled { get; private set; }

    /// <summary>
    /// Gets the effective luma quantization parameter, or <see langword="null"/> for a dependent segment.
    /// </summary>
    public int? QuantizationParameter { get; private set; }

    /// <summary>
    /// Gets the slice-level Cb quantization-parameter offset.
    /// </summary>
    public int ChromaCbQuantizationParameterOffset { get; private set; }

    /// <summary>
    /// Gets the slice-level Cr quantization-parameter offset.
    /// </summary>
    public int ChromaCrQuantizationParameterOffset { get; private set; }

    /// <summary>
    /// Gets a value indicating whether coding units can select the PPS chroma-offset list.
    /// </summary>
    public bool? ChromaQuantizationParameterOffsetListEnabled { get; private set; }

    /// <summary>
    /// Gets a value indicating whether deblocking is disabled for this independent slice.
    /// </summary>
    public bool? DeblockingFilterDisabled { get; private set; }

    /// <summary>
    /// Gets half the effective deblocking beta-threshold offset.
    /// </summary>
    public int DeblockingFilterBetaOffsetDiv2 { get; private set; }

    /// <summary>
    /// Gets half the effective deblocking clipping-threshold offset.
    /// </summary>
    public int DeblockingFilterTcOffsetDiv2 { get; private set; }

    /// <summary>
    /// Gets a value indicating whether in-loop filtering crosses slice boundaries.
    /// </summary>
    public bool? LoopFilterAcrossSlicesEnabled { get; private set; }

    /// <summary>
    /// Gets the decoded-byte lengths that separate tile or wavefront entropy substreams after the first substream.
    /// </summary>
    public IReadOnlyList<int> EntryPointOffsets => this.entryPointOffsets;

    /// <summary>
    /// Gets the number of independently initialized tile or wavefront entropy substreams in this slice segment.
    /// </summary>
    public int EntropySubstreamCount => this.entryPointOffsets.Length + 1;

    /// <summary>
    /// Gets the slice-header length in decoded raw-byte-sequence payload bytes.
    /// </summary>
    public int HeaderLength { get; }

    /// <summary>
    /// Gets the entropy-coded slice data following byte alignment.
    /// </summary>
    public ReadOnlyMemory<byte> SliceData { get; }

    /// <summary>
    /// Gets one bounded entropy substream in slice coding order.
    /// </summary>
    /// <param name="index">The zero-based entropy-substream index.</param>
    /// <returns>The decoded raw-byte-sequence payload bytes belonging to the selected substream.</returns>
    public ReadOnlyMemory<byte> GetEntropySubstream(int index)
    {
        DebugGuard.MustBeBetweenOrEqualTo(index, 0, this.entryPointOffsets.Length, nameof(index));
        int offset = 0;
        for (int precedingIndex = 0; precedingIndex < index; precedingIndex++)
        {
            offset += this.entryPointOffsets[precedingIndex];
        }

        int length = index < this.entryPointOffsets.Length
            ? this.entryPointOffsets[index]
            : this.SliceData.Length - offset;

        return this.SliceData.Slice(offset, length);
    }

    /// <summary>
    /// Reads fields carried only by an independent slice-segment header.
    /// </summary>
    /// <param name="reader">The slice-segment raw byte sequence payload reader.</param>
    /// <exception cref="InvalidImageContentException">
    /// The slice is not intra-coded or its quantization and filter fields are outside the governing parameter bounds.
    /// </exception>
    private void ReadIndependentHeader(ref HevcBitReader reader)
    {
        HevcPictureParameterSet pictureParameterSet = this.PictureParameterSet;
        HevcSequenceParameterSet sequenceParameterSet = pictureParameterSet.SequenceParameterSet;
        for (int extraBit = 0; extraBit < pictureParameterSet.ExtraSliceHeaderBitCount; extraBit++)
        {
            reader.ReadFlag();
        }

        uint sliceType = reader.ReadUnsignedExpGolomb();
        if (sliceType != (uint)HevcSliceType.Intra)
        {
            throw new InvalidImageContentException("An independently coded HEVC image item must contain intra IDR slices.");
        }

        this.SliceType = HevcSliceType.Intra;
        if (pictureParameterSet.OutputFlagPresent && !reader.ReadFlag())
        {
            throw new InvalidImageContentException("The HEVC image-item slice is marked as unavailable for output.");
        }

        if (sequenceParameterSet.SeparateColorPlaneFlag)
        {
            this.ColorPlaneId = (byte)reader.ReadBits(2);
            if (this.ColorPlaneId > 2)
            {
                throw new InvalidImageContentException("The HEVC slice segment has an invalid separate color-plane identifier.");
            }
        }

        bool hasCombinedChromaPlanes = sequenceParameterSet.ChromaFormat != 0
            && !sequenceParameterSet.SeparateColorPlaneFlag;

        if (sequenceParameterSet.SampleAdaptiveOffsetEnabled)
        {
            this.SampleAdaptiveOffsetLumaEnabled = reader.ReadFlag();
            this.SampleAdaptiveOffsetChromaEnabled = hasCombinedChromaPlanes && reader.ReadFlag();
        }

        int sliceQuantizationParameterDelta = reader.ReadSignedExpGolomb();
        long quantizationParameter = 26L
            + pictureParameterSet.InitialQuantizationParameterMinus26
            + sliceQuantizationParameterDelta;

        int minimumQuantizationParameter = -6 * (sequenceParameterSet.BitDepthLuma - 8);
        if (quantizationParameter < minimumQuantizationParameter || quantizationParameter > 51)
        {
            throw new InvalidImageContentException("The HEVC slice segment has an invalid luma quantization parameter.");
        }

        this.QuantizationParameter = (int)quantizationParameter;
        if (pictureParameterSet.SliceChromaQuantizationParameterOffsetsPresent && hasCombinedChromaPlanes)
        {
            this.ChromaCbQuantizationParameterOffset = HevcParameterSetSyntax.ReadQuantizationParameterOffset(ref reader);
            this.ChromaCrQuantizationParameterOffset = HevcParameterSetSyntax.ReadQuantizationParameterOffset(ref reader);
            if (pictureParameterSet.ChromaCbQuantizationParameterOffset + this.ChromaCbQuantizationParameterOffset is < -12 or > 12
                || pictureParameterSet.ChromaCrQuantizationParameterOffset + this.ChromaCrQuantizationParameterOffset is < -12 or > 12)
            {
                throw new InvalidImageContentException("The HEVC slice and picture chroma quantization offsets have an invalid sum.");
            }
        }

        if (pictureParameterSet.ChromaQuantizationParameterOffsetsCb.Count != 0)
        {
            this.ChromaQuantizationParameterOffsetListEnabled = reader.ReadFlag();
        }

        this.ReadDeblockingFilterFields(ref reader);
        bool sampleAdaptiveOffsetEnabled = this.SampleAdaptiveOffsetLumaEnabled == true
            || this.SampleAdaptiveOffsetChromaEnabled == true;

        if (pictureParameterSet.LoopFilterAcrossSlicesEnabled
            && (sampleAdaptiveOffsetEnabled || this.DeblockingFilterDisabled == false))
        {
            this.LoopFilterAcrossSlicesEnabled = reader.ReadFlag();
        }
        else
        {
            this.LoopFilterAcrossSlicesEnabled = pictureParameterSet.LoopFilterAcrossSlicesEnabled;
        }
    }

    /// <summary>
    /// Resolves the independent slice's effective deblocking mode and threshold offsets.
    /// </summary>
    /// <param name="reader">The slice-segment raw byte sequence payload reader.</param>
    private void ReadDeblockingFilterFields(ref HevcBitReader reader)
    {
        HevcPictureParameterSet pictureParameterSet = this.PictureParameterSet;
        bool overrideFilter = false;
        if (pictureParameterSet.DeblockingFilterControlPresent
            && pictureParameterSet.DeblockingFilterOverrideEnabled)
        {
            overrideFilter = reader.ReadFlag();
        }

        if (overrideFilter)
        {
            this.DeblockingFilterDisabled = reader.ReadFlag();
            if (this.DeblockingFilterDisabled == false)
            {
                this.DeblockingFilterBetaOffsetDiv2 = HevcParameterSetSyntax.ReadDeblockingFilterOffset(ref reader);
                this.DeblockingFilterTcOffsetDiv2 = HevcParameterSetSyntax.ReadDeblockingFilterOffset(ref reader);
            }

            return;
        }

        this.DeblockingFilterDisabled = pictureParameterSet.DeblockingFilterControlPresent
            && pictureParameterSet.DeblockingFilterDisabled;

        this.DeblockingFilterBetaOffsetDiv2 = pictureParameterSet.DeblockingFilterBetaOffsetDiv2;
        this.DeblockingFilterTcOffsetDiv2 = pictureParameterSet.DeblockingFilterTcOffsetDiv2;
    }

    /// <summary>
    /// Reads tile or wavefront substream entry-point byte lengths.
    /// </summary>
    /// <param name="reader">The slice-segment raw byte sequence payload reader.</param>
    /// <param name="codingTreeBlockCount">The number of coding-tree blocks in the coded picture.</param>
    /// <exception cref="InvalidImageContentException">
    /// The entry-point count, field width, or byte length exceeds the bounded picture or integer range.
    /// </exception>
    private void ReadEntryPoints(ref HevcBitReader reader, int codingTreeBlockCount)
    {
        HevcPictureParameterSet pictureParameterSet = this.PictureParameterSet;
        if (!pictureParameterSet.TilesEnabled && !pictureParameterSet.EntropyCodingSynchronizationEnabled)
        {
            return;
        }

        uint entryPointCount = reader.ReadUnsignedExpGolomb();
        if (entryPointCount >= codingTreeBlockCount)
        {
            throw new InvalidImageContentException("The HEVC slice segment declares too many entropy entry points.");
        }

        if (entryPointCount == 0)
        {
            return;
        }

        uint offsetLengthMinusOne = reader.ReadUnsignedExpGolomb();
        if (offsetLengthMinusOne > 31)
        {
            throw new InvalidImageContentException("The HEVC slice entry-point offset width is invalid.");
        }

        int offsetBitCount = (int)offsetLengthMinusOne + 1;
        int[] entryPointOffsets = new int[entryPointCount];
        for (int entryPoint = 0; entryPoint < entryPointOffsets.Length; entryPoint++)
        {
            uint entryPointOffsetMinusOne = reader.ReadBits(offsetBitCount);
            if (entryPointOffsetMinusOne >= int.MaxValue)
            {
                throw new InvalidImageContentException("The HEVC slice entry-point byte length is too large.");
            }

            entryPointOffsets[entryPoint] = (int)entryPointOffsetMinusOne + 1;
        }

        this.entryPointOffsets = entryPointOffsets;
    }

    /// <summary>
    /// Converts an RBSP byte boundary to its corresponding encoded-payload boundary.
    /// </summary>
    /// <param name="rbspOffset">The decoded raw-byte-sequence payload offset.</param>
    /// <param name="emulationPreventionBytePositions">The removed encoded-payload byte positions.</param>
    /// <returns>The encoded byte-sequence payload offset at the same syntax boundary.</returns>
    internal static int GetEncodedPayloadOffset(
        int rbspOffset,
        ReadOnlySpan<int> emulationPreventionBytePositions)
    {
        int encodedOffset = rbspOffset;
        foreach (int preventionBytePosition in emulationPreventionBytePositions)
        {
            if (preventionBytePosition >= encodedOffset)
            {
                break;
            }

            encodedOffset++;
        }

        return encodedOffset;
    }

    /// <summary>
    /// Converts an encoded-payload byte boundary to its corresponding RBSP boundary.
    /// </summary>
    /// <param name="encodedOffset">The encoded byte-sequence payload offset.</param>
    /// <param name="emulationPreventionBytePositions">The removed encoded-payload byte positions.</param>
    /// <returns>The decoded raw-byte-sequence payload offset at the same syntax boundary.</returns>
    internal static int GetDecodedPayloadOffset(
        int encodedOffset,
        ReadOnlySpan<int> emulationPreventionBytePositions)
    {
        int decodedOffset = encodedOffset;
        foreach (int preventionBytePosition in emulationPreventionBytePositions)
        {
            if (preventionBytePosition >= encodedOffset)
            {
                break;
            }

            decodedOffset--;
        }

        return decodedOffset;
    }
}
