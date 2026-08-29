// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Hevc;

/// <content>
/// Implements intra prediction, reconstructed-plane writes, and PCM sample reconstruction.
/// </content>
internal sealed partial class HevcPictureDecoder
{
    /// <summary>
    /// Reconstructs one packed intra-prediction block in caller-owned scratch.
    /// </summary>
    /// <param name="plane">The reconstructed component plane.</param>
    /// <param name="x">The prediction-block left coordinate in component samples.</param>
    /// <param name="y">The prediction-block top coordinate in component samples.</param>
    /// <param name="log2Size">The base-two logarithm of the square prediction-block side.</param>
    /// <param name="regionId">The current independent-slice and tile prediction region.</param>
    /// <param name="colorPlaneIndex">The selected separate-color plane, or zero for combined coding.</param>
    /// <param name="transquantBypass">Whether the governing coding unit bypasses inverse quantization and transform.</param>
    /// <returns>The packed predicted samples.</returns>
    private Span<ushort> PredictComponentBlock(HevcPlane plane, int x, int y, int log2Size, int regionId, int colorPlaneIndex, bool transquantBypass)
    {
        int size = 1 << log2Size;
        int sampleCount = size * size;
        int referenceLength = (size * 2) + 1;
        Span<ushort> scratch = this.predictionScratch.Memory.Span;
        Span<ushort> prediction = scratch[..sampleCount];
        Span<ushort> top = scratch.Slice(MaximumTransformSampleCount, MaximumReferenceLength);
        Span<ushort> left = scratch.Slice(MaximumTransformSampleCount + MaximumReferenceLength, MaximumReferenceLength);
        Span<ushort> filteredTop = scratch.Slice(MaximumTransformSampleCount + (MaximumReferenceLength * 2), MaximumReferenceLength);
        Span<ushort> filteredLeft = scratch.Slice(MaximumTransformSampleCount + (MaximumReferenceLength * 3), MaximumReferenceLength);
        int referenceScratchOffset = MaximumTransformSampleCount + (MaximumReferenceLength * 4);
        int unitWidth = this.reconstructionState.GetUnitWidth(plane);
        int unitHeight = this.reconstructionState.GetUnitHeight(plane);
        int referenceScratchLength = HevcIntraPredictor.GetReferenceScratchLength(log2Size, unitWidth);
        Span<ushort> referenceScratch = scratch.Slice(referenceScratchOffset, referenceScratchLength);
        Span<ushort> operationScratch = scratch[(referenceScratchOffset + referenceScratchLength)..];
        Span<bool> availability = this.availabilityScratch.Memory.Span;
        int availabilityCount = this.reconstructionState.BuildReferenceAvailability(
            plane,
            x,
            y,
            log2Size,
            regionId,
            availability);

        HevcIntraPredictor.PrepareReferenceSamples(
            this.Picture,
            plane,
            x,
            y,
            log2Size,
            unitWidth,
            unitHeight,
            availability[..availabilityCount],
            top,
            left,
            referenceScratch);

        int lumaX = x << this.Picture.GetSubsamplingX(plane);
        int lumaY = y << this.Picture.GetSubsamplingY(plane);
        bool useLumaSyntax = plane == HevcPlane.Y || this.sequenceParameterSet.SeparateColorPlaneFlag;
        int mode = useLumaSyntax
            ? this.intraPredictionStates[colorPlaneIndex].GetLumaMode(lumaX, lumaY)
            : this.intraPredictionStates[colorPlaneIndex].GetEffectiveChromaMode(lumaX, lumaY);

        if (!useLumaSyntax && this.sequenceParameterSet.ChromaFormat == 2)
        {
            mode = HevcIntraPredictionMode.RemapChroma422(mode);
        }

        // H.265 8.4.4.2.3 and 8.4.4.2.6 restrict prediction-edge filtering to luma blocks no larger than 16 samples.
        // Implicit RDPCM bypasses that filtering for the lossless horizontal and vertical prediction modes.
        bool filterPredictionEdges = useLumaSyntax
            && size <= 16
            && !(transquantBypass
                && this.sequenceParameterSet.ImplicitResidualDpcmEnabled
                && (mode == HevcIntraPredictionMode.Horizontal || mode == HevcIntraPredictionMode.Vertical));

        bool filterReferences = HevcIntraPredictor.ShouldFilterReferenceSamples(
            useLumaSyntax ? HevcPlane.Y : plane,
            mode,
            log2Size,
            this.sequenceParameterSet.ChromaFormat,
            this.sequenceParameterSet.IntraSmoothingDisabled);

        ReadOnlySpan<ushort> selectedTop = top[..referenceLength];
        ReadOnlySpan<ushort> selectedLeft = left[..referenceLength];
        if (filterReferences)
        {
            // Normal three-tap smoothing extends to combined 4:4:4 chroma, but strong bilinear smoothing is a luma
            // operation. Separate color planes use luma syntax and therefore retain the luma behavior.
            bool useStrongSmoothing = useLumaSyntax && this.sequenceParameterSet.StrongIntraSmoothingEnabled;

            HevcIntraPredictor.FilterReferenceSamples(
                selectedTop,
                selectedLeft,
                filteredTop,
                filteredLeft,
                log2Size,
                this.Picture.GetBitDepth(plane),
                useStrongSmoothing);

            selectedTop = filteredTop[..referenceLength];
            selectedLeft = filteredLeft[..referenceLength];
        }

        HevcIntraPredictor.Predict(
            selectedTop,
            selectedLeft,
            prediction,
            size,
            log2Size,
            mode,
            this.Picture.GetBitDepth(plane),
            filterPredictionEdges,
            operationScratch);

        return prediction;
    }

    /// <summary>
    /// Copies one packed reconstructed block into the allocator-owned picture plane.
    /// </summary>
    /// <param name="source">The packed reconstructed samples.</param>
    /// <param name="plane">The destination component plane.</param>
    /// <param name="x">The destination left coordinate.</param>
    /// <param name="y">The destination top coordinate.</param>
    /// <param name="size">The square block side.</param>
    private void CopyPredictionToPicture(ReadOnlySpan<ushort> source, HevcPlane plane, int x, int y, int size)
    {
        for (int row = 0; row < size; row++)
        {
            source.Slice(row * size, size).CopyTo(this.Picture.GetRowSpan(plane, y + row)[x..]);
        }
    }

    /// <summary>
    /// Reads and writes every raw sample in one PCM coding unit before arithmetic decoding restarts.
    /// </summary>
    /// <param name="reader">The suspended entropy-substream reader.</param>
    /// <param name="x">The coding-unit left luma coordinate.</param>
    /// <param name="y">The coding-unit top luma coordinate.</param>
    /// <param name="log2Size">The base-two logarithm of the coding-unit side.</param>
    /// <param name="regionId">The current independent-slice and tile prediction region.</param>
    /// <param name="colorPlaneIndex">The selected separate-color plane, or zero for combined coding.</param>
    private void DecodePcmCodingUnit(
        ref HevcCabacSyntaxReader reader,
        int x,
        int y,
        int log2Size,
        int regionId,
        int colorPlaneIndex)
    {
        int size = 1 << log2Size;
        if (this.sequenceParameterSet.SeparateColorPlaneFlag)
        {
            HevcPlane plane = (HevcPlane)colorPlaneIndex;
            this.DecodePcmPlane(ref reader, plane, x, y, size, size, this.sequenceParameterSet.PcmBitDepthLuma, regionId);
            return;
        }

        this.DecodePcmPlane(ref reader, HevcPlane.Y, x, y, size, size, this.sequenceParameterSet.PcmBitDepthLuma, regionId);
        if (this.sequenceParameterSet.ChromaFormat == 0)
        {
            return;
        }

        int subsamplingX = this.Picture.GetSubsamplingX(HevcPlane.Cb);
        int subsamplingY = this.Picture.GetSubsamplingY(HevcPlane.Cb);
        int chromaWidth = size >> subsamplingX;
        int chromaHeight = size >> subsamplingY;
        int chromaX = x >> subsamplingX;
        int chromaY = y >> subsamplingY;
        this.DecodePcmPlane(
            ref reader,
            HevcPlane.Cb,
            chromaX,
            chromaY,
            chromaWidth,
            chromaHeight,
            this.sequenceParameterSet.PcmBitDepthChroma,
            regionId);

        this.DecodePcmPlane(
            ref reader,
            HevcPlane.Cr,
            chromaX,
            chromaY,
            chromaWidth,
            chromaHeight,
            this.sequenceParameterSet.PcmBitDepthChroma,
            regionId);
    }

    /// <summary>
    /// Reads one rectangular PCM component plane directly into the reconstructed picture.
    /// </summary>
    /// <param name="reader">The suspended entropy-substream reader.</param>
    /// <param name="plane">The destination component plane.</param>
    /// <param name="x">The destination left coordinate.</param>
    /// <param name="y">The destination top coordinate.</param>
    /// <param name="width">The component rectangle width.</param>
    /// <param name="height">The component rectangle height.</param>
    /// <param name="bitDepth">The PCM sample precision.</param>
    /// <param name="regionId">The current independent-slice and tile prediction region.</param>
    private void DecodePcmPlane(
        ref HevcCabacSyntaxReader reader,
        HevcPlane plane,
        int x,
        int y,
        int width,
        int height,
        int bitDepth,
        int regionId)
    {
        // PCM samples can use fewer bits than the reconstructed component. H.265 places those bits at the
        // most-significant end of the component range, so the raw code value must be restored before filtering.
        int bitDepthShift = this.Picture.GetBitDepth(plane) - bitDepth;
        for (int row = 0; row < height; row++)
        {
            Span<ushort> destination = this.Picture.GetRowSpan(plane, y + row).Slice(x, width);
            for (int column = 0; column < width; column++)
            {
                destination[column] = (ushort)(reader.ReadPcmSample(bitDepth) << bitDepthShift);
            }
        }

        this.reconstructionState.MarkReconstructed(plane, x, y, width, height, regionId);
    }
}
