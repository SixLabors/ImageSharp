// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Hevc;

/// <summary>
/// Decodes the CABAC syntax values used to reconstruct one independently coded HEVC still picture.
/// </summary>
internal ref struct HevcCabacSyntaxReader
{
    /// <summary>
    /// The truncated-unary cutoff for a coding-unit luma quantization delta.
    /// </summary>
    private const int DeltaQuantizationCutoff = 5;

    /// <summary>
    /// The prefix length at which coefficient levels switch from Rice to exponential-Golomb coding.
    /// </summary>
    private const int CoefficientRemainingReduction = 3;

    /// <summary>
    /// The binary arithmetic decoder for the current entropy substream.
    /// </summary>
    private HevcCabacDecoder decoder;

    /// <summary>
    /// The adaptive intra-picture probability contexts for the current entropy substream.
    /// </summary>
    private readonly HevcCabacContexts contexts;

    /// <summary>
    /// Initializes a new instance of the <see cref="HevcCabacSyntaxReader"/> struct.
    /// </summary>
    /// <param name="data">The bytes of one bounded slice tile or wavefront entropy substream.</param>
    /// <param name="quantizationParameter">The slice luma quantization parameter.</param>
    /// <exception cref="InvalidImageContentException">The entropy substream is truncated.</exception>
    public HevcCabacSyntaxReader(ReadOnlySpan<byte> data, int quantizationParameter)
    {
        this.decoder = new HevcCabacDecoder(data);
        this.contexts = new HevcCabacContexts(quantizationParameter);
    }

    /// <summary>
    /// Gets the number of entropy-substream bytes loaded by the arithmetic decoder.
    /// </summary>
    public readonly int BytesConsumed => this.decoder.BytesConsumed;

    /// <summary>
    /// Copies the adaptive contexts required to initialize a later wavefront row.
    /// </summary>
    /// <param name="destination">The caller-owned context destination.</param>
    public readonly void CopyContextsTo(Span<HevcCabacContext> destination) => this.contexts.CopyTo(destination);

    /// <summary>
    /// Restores adaptive contexts captured after the second coding-tree block of the preceding wavefront row.
    /// </summary>
    /// <param name="source">The saved wavefront contexts.</param>
    public readonly void CopyContextsFrom(ReadOnlySpan<HevcCabacContext> source) => this.contexts.CopyFrom(source);

    /// <summary>
    /// Decodes the coding-unit transquant-bypass flag.
    /// </summary>
    /// <returns>The decoded flag value.</returns>
    public bool ReadTransquantBypass()
    {
        Span<HevcCabacContext> selectedContexts = this.contexts.TransquantBypass;
        return this.decoder.ReadDecision(ref selectedContexts[0]);
    }

    /// <summary>
    /// Decodes a coding-unit split flag.
    /// </summary>
    /// <param name="contextIndex">The context derived from the available neighboring coding-unit depths.</param>
    /// <returns>The decoded flag value.</returns>
    public bool ReadSplit(int contextIndex)
    {
        DebugGuard.MustBeBetweenOrEqualTo(contextIndex, 0, 2, nameof(contextIndex));
        Span<HevcCabacContext> selectedContexts = this.contexts.Split;
        return this.decoder.ReadDecision(ref selectedContexts[contextIndex]);
    }

    /// <summary>
    /// Decodes whether a minimum-size intra coding unit uses four square prediction partitions.
    /// </summary>
    /// <param name="isMinimumCodingBlockSize">
    /// A value indicating whether the coding unit is at the minimum coding-block size.
    /// </param>
    /// <returns>
    /// <see langword="true"/> for four square prediction partitions; <see langword="false"/> for one square partition.
    /// </returns>
    public bool ReadIntraNxNPartition(bool isMinimumCodingBlockSize)
    {
        if (!isMinimumCodingBlockSize)
        {
            return false;
        }

        Span<HevcCabacContext> selectedContexts = this.contexts.PartitionSize;
        return !this.decoder.ReadDecision(ref selectedContexts[0]);
    }

    /// <summary>
    /// Decodes whether a square intra coding unit carries raw pulse-code-modulated samples.
    /// </summary>
    /// <returns><see langword="true"/> when PCM sample syntax follows; otherwise, <see langword="false"/>.</returns>
    public bool ReadPcmFlag() => this.decoder.ReadPcmFlag();

    /// <summary>
    /// Reads one pulse-code-modulated component sample.
    /// </summary>
    /// <param name="bitDepth">The PCM sample precision.</param>
    /// <returns>The decoded unsigned sample.</returns>
    public ushort ReadPcmSample(int bitDepth) => this.decoder.ReadPcmSample(bitDepth);

    /// <summary>
    /// Restarts arithmetic decoding after the complete PCM coding-unit payload.
    /// </summary>
    public void RestartAfterPcm() => this.decoder.RestartAfterPcm();

    /// <summary>
    /// Decodes whether a luma intra mode is selected from the three most-probable modes.
    /// </summary>
    /// <returns>The decoded flag value.</returns>
    public bool ReadPreviousIntraLumaPredictionFlag()
    {
        Span<HevcCabacContext> selectedContexts = this.contexts.IntraPrediction;
        return this.decoder.ReadDecision(ref selectedContexts[0]);
    }

    /// <summary>
    /// Decodes the zero-based selector for one of the three most-probable luma intra modes.
    /// </summary>
    /// <returns>The selector in the inclusive range zero through two.</returns>
    public int ReadMostProbableIntraLumaPredictionIndex()
    {
        if (!this.decoder.ReadBypass())
        {
            return 0;
        }

        return this.decoder.ReadBypass() ? 2 : 1;
    }

    /// <summary>
    /// Decodes the five-bit selector for a luma intra mode outside the most-probable set.
    /// </summary>
    /// <returns>The decoded selector in the inclusive range zero through thirty-one.</returns>
    public int ReadRemainingIntraLumaPredictionMode() => (int)this.decoder.ReadBypassBits(5);

    /// <summary>
    /// Decodes the chroma intra prediction selector.
    /// </summary>
    /// <returns>
    /// Negative one when chroma derives its mode from luma; otherwise, the decoded selector in the inclusive range
    /// zero through three.
    /// </returns>
    public int ReadChromaPredictionModeIndex()
    {
        Span<HevcCabacContext> selectedContexts = this.contexts.ChromaPrediction;
        if (!this.decoder.ReadDecision(ref selectedContexts[0]))
        {
            return -1;
        }

        return (int)this.decoder.ReadBypassBits(2);
    }

    /// <summary>
    /// Decodes a transform-tree subdivision flag.
    /// </summary>
    /// <param name="log2TransformBlockSize">The base-two logarithm of the current transform-block size.</param>
    /// <returns>The decoded flag value.</returns>
    public bool ReadTransformSubdivision(int log2TransformBlockSize)
    {
        DebugGuard.MustBeBetweenOrEqualTo(log2TransformBlockSize, 3, 5, nameof(log2TransformBlockSize));
        Span<HevcCabacContext> selectedContexts = this.contexts.TransformSubdivision;
        return this.decoder.ReadDecision(ref selectedContexts[5 - log2TransformBlockSize]);
    }

    /// <summary>
    /// Decodes a transform-tree coded-block flag.
    /// </summary>
    /// <param name="isChroma">A value indicating whether the flag describes a chroma transform block.</param>
    /// <param name="contextIndex">The transform-depth-derived context index.</param>
    /// <returns>The decoded flag value.</returns>
    public bool ReadTransformCodedBlockFlag(bool isChroma, int contextIndex)
    {
        DebugGuard.MustBeBetweenOrEqualTo(contextIndex, 0, 4, nameof(contextIndex));
        Span<HevcCabacContext> selectedContexts = this.contexts.TransformCodedBlockFlag;
        int channelOffset = isChroma ? 5 : 0;
        return this.decoder.ReadDecision(ref selectedContexts[channelOffset + contextIndex]);
    }

    /// <summary>
    /// Decodes whether a transform block bypasses the inverse transform.
    /// </summary>
    /// <param name="isChroma">A value indicating whether the transform block belongs to a chroma channel.</param>
    /// <returns>The decoded flag value.</returns>
    public bool ReadTransformSkip(bool isChroma)
    {
        Span<HevcCabacContext> selectedContexts = this.contexts.TransformSkip;
        return this.decoder.ReadDecision(ref selectedContexts[isChroma ? 1 : 0]);
    }

    /// <summary>
    /// Decodes the signed coding-unit luma quantization-parameter delta.
    /// </summary>
    /// <returns>The signed delta value.</returns>
    /// <exception cref="InvalidImageContentException">The coded magnitude exceeds a 32-bit signed value.</exception>
    public int ReadDeltaQuantizationParameter()
    {
        Span<HevcCabacContext> selectedContexts = this.contexts.DeltaQuantization;
        ulong magnitude = this.ReadTruncatedUnary(selectedContexts, 0, 1, DeltaQuantizationCutoff);
        if (magnitude == DeltaQuantizationCutoff)
        {
            magnitude += this.ReadBypassExponentialGolomb(0);
        }

        if (magnitude > int.MaxValue)
        {
            throw new InvalidImageContentException("The HEVC coding-unit quantization delta is too large.");
        }

        if (magnitude == 0)
        {
            return 0;
        }

        int signedMagnitude = (int)magnitude;
        return this.decoder.ReadBypass() ? -signedMagnitude : signedMagnitude;
    }

    /// <summary>
    /// Decodes the coding-unit chroma quantization-adjustment selector.
    /// </summary>
    /// <param name="listLength">The number of chroma offset pairs declared by the picture parameters.</param>
    /// <returns>Zero when no adjustment applies; otherwise, the one-based offset-list selector.</returns>
    public int ReadChromaQuantizationAdjustment(int listLength)
    {
        Span<HevcCabacContext> flagContexts = this.contexts.ChromaQuantizationAdjustmentFlag;
        if (!this.decoder.ReadDecision(ref flagContexts[0]))
        {
            return 0;
        }

        if (listLength == 1)
        {
            return 1;
        }

        Span<HevcCabacContext> indexContexts = this.contexts.ChromaQuantizationAdjustmentIndex;
        return (int)this.ReadTruncatedUnary(indexContexts, 0, 0, listLength - 1) + 1;
    }

    /// <summary>
    /// Decodes the cross-component residual-prediction scale for one chroma plane.
    /// </summary>
    /// <param name="chromaPlaneIndex">Zero for Cb or one for Cr.</param>
    /// <returns>Zero when prediction is disabled; otherwise, a signed power of two from one through eight.</returns>
    public int ReadCrossComponentPredictionScale(int chromaPlaneIndex)
    {
        DebugGuard.MustBeBetweenOrEqualTo(chromaPlaneIndex, 0, 1, nameof(chromaPlaneIndex));
        Span<HevcCabacContext> selectedContexts = this.contexts.CrossComponentPrediction;
        int contextOffset = chromaPlaneIndex * 5;
        if (!this.decoder.ReadDecision(ref selectedContexts[contextOffset]))
        {
            return 0;
        }

        int magnitudeLog2 = 0;
        if (this.decoder.ReadDecision(ref selectedContexts[contextOffset + 1]))
        {
            Span<HevcCabacContext> magnitudeContexts = selectedContexts.Slice(contextOffset + 2, 2);
            magnitudeLog2 = (int)this.ReadTruncatedUnary(magnitudeContexts, 0, 1, 2) + 1;
        }

        int magnitude = 1 << magnitudeLog2;
        return this.decoder.ReadDecision(ref selectedContexts[contextOffset + 4]) ? -magnitude : magnitude;
    }

    /// <summary>
    /// Decodes a sample-adaptive-offset merge flag.
    /// </summary>
    /// <returns>The decoded flag value.</returns>
    public bool ReadSampleAdaptiveOffsetMerge()
    {
        Span<HevcCabacContext> selectedContexts = this.contexts.SampleAdaptiveOffsetMerge;
        return this.decoder.ReadDecision(ref selectedContexts[0]);
    }

    /// <summary>
    /// Decodes the sample-adaptive-offset mode selector.
    /// </summary>
    /// <returns>Zero for off, one for band offset, or two for edge offset.</returns>
    public int ReadSampleAdaptiveOffsetType()
    {
        Span<HevcCabacContext> selectedContexts = this.contexts.SampleAdaptiveOffsetType;
        if (!this.decoder.ReadDecision(ref selectedContexts[0]))
        {
            return 0;
        }

        return this.decoder.ReadBypass() ? 2 : 1;
    }

    /// <summary>
    /// Decodes a truncated-unary absolute sample-adaptive-offset value.
    /// </summary>
    /// <param name="maximumValue">The inclusive maximum offset magnitude.</param>
    /// <returns>The decoded offset magnitude.</returns>
    public int ReadSampleAdaptiveOffsetAbsolute(int maximumValue)
    {
        if (maximumValue == 0 || !this.decoder.ReadBypass())
        {
            return 0;
        }

        int value = 1;
        while (value < maximumValue && this.decoder.ReadBypass())
        {
            value++;
        }

        return value;
    }

    /// <summary>
    /// Decodes the five-bit sample-adaptive band-offset starting position.
    /// </summary>
    /// <returns>The decoded band position.</returns>
    public int ReadSampleAdaptiveOffsetBandPosition() => (int)this.decoder.ReadBypassBits(5);

    /// <summary>
    /// Decodes the two-bit sample-adaptive edge-offset class.
    /// </summary>
    /// <returns>The decoded edge class.</returns>
    public int ReadSampleAdaptiveOffsetEdgeClass() => (int)this.decoder.ReadBypassBits(2);

    /// <summary>
    /// Decodes a sample-adaptive band-offset sign.
    /// </summary>
    /// <returns><see langword="true"/> for a negative offset; otherwise, <see langword="false"/>.</returns>
    public bool ReadSampleAdaptiveOffsetSign() => this.decoder.ReadBypass();

    /// <summary>
    /// Decodes a horizontal last-significant-coefficient prefix flag.
    /// </summary>
    /// <param name="isChroma">A value indicating whether the coefficient belongs to a chroma channel.</param>
    /// <param name="contextIndex">The block-size and prefix-derived context index within the channel.</param>
    /// <returns>The decoded flag value.</returns>
    public bool ReadLastSignificantX(bool isChroma, int contextIndex)
    {
        Span<HevcCabacContext> selectedContexts = this.contexts.LastSignificantX;
        return this.decoder.ReadDecision(ref selectedContexts[(isChroma ? 15 : 0) + contextIndex]);
    }

    /// <summary>
    /// Decodes a vertical last-significant-coefficient prefix flag.
    /// </summary>
    /// <param name="isChroma">A value indicating whether the coefficient belongs to a chroma channel.</param>
    /// <param name="contextIndex">The block-size and prefix-derived context index within the channel.</param>
    /// <returns>The decoded flag value.</returns>
    public bool ReadLastSignificantY(bool isChroma, int contextIndex)
    {
        Span<HevcCabacContext> selectedContexts = this.contexts.LastSignificantY;
        return this.decoder.ReadDecision(ref selectedContexts[(isChroma ? 15 : 0) + contextIndex]);
    }

    /// <summary>
    /// Decodes a significant-coefficient-group flag.
    /// </summary>
    /// <param name="isChroma">A value indicating whether the coefficient group belongs to a chroma channel.</param>
    /// <param name="contextIndex">The neighboring-group-derived context index.</param>
    /// <returns>The decoded flag value.</returns>
    public bool ReadSignificantCoefficientGroup(bool isChroma, int contextIndex)
    {
        Span<HevcCabacContext> selectedContexts = this.contexts.SignificantCoefficientGroup;
        return this.decoder.ReadDecision(ref selectedContexts[(isChroma ? 2 : 0) + contextIndex]);
    }

    /// <summary>
    /// Decodes a significant-coefficient flag.
    /// </summary>
    /// <param name="isChroma">A value indicating whether the coefficient belongs to a chroma channel.</param>
    /// <param name="contextIndex">The scan-position-derived context index within the channel.</param>
    /// <returns>The decoded flag value.</returns>
    public bool ReadSignificantCoefficient(bool isChroma, int contextIndex)
    {
        Span<HevcCabacContext> selectedContexts = this.contexts.SignificantCoefficient;
        return this.decoder.ReadDecision(ref selectedContexts[(isChroma ? 28 : 0) + contextIndex]);
    }

    /// <summary>
    /// Decodes whether a significant coefficient has an absolute level greater than one.
    /// </summary>
    /// <param name="isChroma">A value indicating whether the coefficient belongs to a chroma channel.</param>
    /// <param name="contextIndex">The coefficient-group and preceding-level-derived context index.</param>
    /// <returns>The decoded flag value.</returns>
    public bool ReadCoefficientGreaterThanOne(bool isChroma, int contextIndex)
    {
        Span<HevcCabacContext> selectedContexts = this.contexts.GreaterThanOne;
        return this.decoder.ReadDecision(ref selectedContexts[(isChroma ? 16 : 0) + contextIndex]);
    }

    /// <summary>
    /// Decodes whether the first eligible coefficient has an absolute level greater than two.
    /// </summary>
    /// <param name="isChroma">A value indicating whether the coefficient belongs to a chroma channel.</param>
    /// <param name="contextIndex">The coefficient-group-derived context index within the channel.</param>
    /// <returns>The decoded flag value.</returns>
    public bool ReadCoefficientGreaterThanTwo(bool isChroma, int contextIndex)
    {
        Span<HevcCabacContext> selectedContexts = this.contexts.GreaterThanTwo;
        return this.decoder.ReadDecision(ref selectedContexts[(isChroma ? 4 : 0) + contextIndex]);
    }

    /// <summary>
    /// Decodes an absolute coefficient-level remainder.
    /// </summary>
    /// <param name="riceParameter">The current Golomb-Rice parameter.</param>
    /// <param name="useLimitedPrefixLength">
    /// A value indicating whether extended-precision processing limits the prefix length.
    /// </param>
    /// <param name="maximumLog2TransformDynamicRange">The channel's maximum transform dynamic range.</param>
    /// <returns>The decoded nonnegative coefficient-level remainder.</returns>
    /// <exception cref="InvalidImageContentException">The coded remainder exceeds a 32-bit unsigned value.</exception>
    public uint ReadCoefficientRemaining(
        int riceParameter,
        bool useLimitedPrefixLength,
        int maximumLog2TransformDynamicRange)
    {
        int longestPrefix = useLimitedPrefixLength
            ? 32 - maximumLog2TransformDynamicRange
            : int.MaxValue;

        // Extended-precision streams cap the unary prefix at the transform dynamic range. Reaching that cap
        // implies the end of the prefix even when the final bypass bin is one, so no terminating zero is required.
        int prefix = 0;
        while (prefix < longestPrefix && this.decoder.ReadBypass())
        {
            prefix++;
        }

        if (prefix < CoefficientRemainingReduction)
        {
            uint suffix = this.decoder.ReadBypassBits(riceParameter);
            ulong value = ((ulong)prefix << riceParameter) + suffix;
            if (value > uint.MaxValue)
            {
                throw new InvalidImageContentException("The HEVC coefficient level is too large.");
            }

            return (uint)value;
        }

        int prefixLength = prefix - CoefficientRemainingReduction;
        int suffixLength;
        if (useLimitedPrefixLength)
        {
            int maximumPrefixLength = 32
                - (CoefficientRemainingReduction + maximumLog2TransformDynamicRange);

            suffixLength = prefixLength == maximumPrefixLength
                ? maximumLog2TransformDynamicRange - riceParameter
                : prefixLength;
        }
        else
        {
            suffixLength = prefixLength;
        }

        int codedSuffixLength = suffixLength + riceParameter;
        if (prefixLength >= 32 || codedSuffixLength > 32)
        {
            throw new InvalidImageContentException("The HEVC coefficient level is too large.");
        }

        // Prefixes beyond the first three represent an exponential-Golomb basis; the Rice parameter scales both
        // that basis and the suffix while the bounded arithmetic reader supplies the remaining low bits.
        uint codeWord = this.decoder.ReadBypassBits(codedSuffixLength);
        ulong baseValue = (((1UL << prefixLength) - 1) + CoefficientRemainingReduction) << riceParameter;
        ulong result = baseValue + codeWord;
        if (result > uint.MaxValue)
        {
            throw new InvalidImageContentException("The HEVC coefficient level is too large.");
        }

        return (uint)result;
    }

    /// <summary>
    /// Decodes a most-significant-bit-first sequence of equal-probability flags.
    /// </summary>
    /// <param name="bitCount">The number of flags to decode.</param>
    /// <returns>The decoded unsigned value.</returns>
    public uint ReadBypassBits(int bitCount) => this.decoder.ReadBypassBits(bitCount);

    /// <summary>
    /// Selects the byte-aligned range used by aligned bypass syntax.
    /// </summary>
    public void AlignBypass() => this.decoder.AlignBypass();

    /// <summary>
    /// Decodes the flag that terminates a coding-tree block or entropy substream.
    /// </summary>
    /// <returns>The decoded termination flag.</returns>
    public bool ReadTerminate() => this.decoder.ReadTerminate();

    /// <summary>
    /// Validates the stop bit and zero padding after a terminating entropy-coded value.
    /// </summary>
    /// <exception cref="InvalidImageContentException">The entropy substream has invalid termination alignment.</exception>
    public readonly void ValidateTerminationAlignment() => this.decoder.ValidateTerminationAlignment();

    /// <summary>
    /// Decodes a context-adaptive truncated-unary value.
    /// </summary>
    /// <param name="selectedContexts">The context set selected for the syntax element.</param>
    /// <param name="firstContextIndex">The context used by the first binary decision.</param>
    /// <param name="continuationContextIndex">The context used by each subsequent decision.</param>
    /// <param name="maximumValue">The inclusive maximum decoded value.</param>
    /// <returns>The decoded truncated-unary value.</returns>
    private uint ReadTruncatedUnary(
        Span<HevcCabacContext> selectedContexts,
        int firstContextIndex,
        int continuationContextIndex,
        int maximumValue)
    {
        if (maximumValue == 0
            || !this.decoder.ReadDecision(ref selectedContexts[firstContextIndex]))
        {
            return 0;
        }

        uint value = 1;
        while (value < maximumValue
            && this.decoder.ReadDecision(ref selectedContexts[continuationContextIndex]))
        {
            value++;
        }

        return value;
    }

    /// <summary>
    /// Decodes an equal-probability exponential-Golomb value.
    /// </summary>
    /// <param name="order">The initial suffix width.</param>
    /// <returns>The decoded unsigned value.</returns>
    /// <exception cref="InvalidImageContentException">The coded value exceeds a 32-bit unsigned value.</exception>
    private uint ReadBypassExponentialGolomb(int order)
    {
        ulong value = 0;
        int suffixWidth = order;
        while (this.decoder.ReadBypass())
        {
            if (suffixWidth >= 32)
            {
                throw new InvalidImageContentException("The HEVC exponential-Golomb value is too large.");
            }

            value += 1UL << suffixWidth;
            suffixWidth++;
        }

        // Each leading one adds the basis for the current order and widens the final suffix by one bit.
        value += this.decoder.ReadBypassBits(suffixWidth);
        if (value > uint.MaxValue)
        {
            throw new InvalidImageContentException("The HEVC exponential-Golomb value is too large.");
        }

        return (uint)value;
    }
}
