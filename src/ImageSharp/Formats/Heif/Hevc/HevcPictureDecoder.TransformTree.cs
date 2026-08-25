// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;

namespace SixLabors.ImageSharp.Formats.Heif.Hevc;

/// <content>
/// Implements transform-tree syntax, coefficient reconstruction, and intra sample reconstruction.
/// </content>
internal sealed partial class HevcPictureDecoder
{
    /// <summary>
    /// Decodes and reconstructs one transform-tree node.
    /// </summary>
    /// <param name="reader">The active entropy-substream reader.</param>
    /// <param name="geometry">The luma and component rectangles at this transform depth.</param>
    /// <param name="transformDepth">The transform depth relative to the coding-unit root.</param>
    /// <param name="minimumTransformLog2">The smallest luma transform permitted in the coding unit.</param>
    /// <param name="usesNxNPartitions">Whether the coding unit has four luma prediction partitions.</param>
    /// <param name="transquantBypass">Whether the coding unit bypasses inverse quantization and transform.</param>
    /// <param name="regionId">The current independent-slice and tile prediction region.</param>
    /// <param name="colorPlaneIndex">The selected separate-color plane, or zero for combined coding.</param>
    /// <param name="parentChromaBlueFlags">The blue-difference coded-block flags inherited from the parent.</param>
    /// <param name="parentChromaRedFlags">The red-difference coded-block flags inherited from the parent.</param>
    private void DecodeTransformTree(
        ref HevcCabacSyntaxReader reader,
        in HevcTransformUnitGeometry geometry,
        int transformDepth,
        int minimumTransformLog2,
        bool usesNxNPartitions,
        bool transquantBypass,
        int regionId,
        int colorPlaneIndex,
        HevcCodedBlockFlags parentChromaBlueFlags,
        HevcCodedBlockFlags parentChromaRedFlags)
    {
        int log2Size = geometry.Log2LumaSize;
        HevcTransformComponentGeometry primaryGeometry = geometry.Primary;
        HevcTransformComponentGeometry chromaBlueGeometry = geometry.ChromaBlue;
        HevcTransformComponentGeometry chromaRedGeometry = geometry.ChromaRed;
        bool split;
        if (usesNxNPartitions && transformDepth == 0)
        {
            split = true;
        }
        else if (log2Size > this.sequenceParameterSet.MaxTransformBlockLog2)
        {
            split = true;
        }
        else if (log2Size == this.sequenceParameterSet.MinTransformBlockLog2 || log2Size == minimumTransformLog2)
        {
            split = false;
        }
        else
        {
            split = reader.ReadTransformSubdivision(log2Size);
        }

        HevcCodedBlockFlags chromaBlueFlags = parentChromaBlueFlags;
        HevcCodedBlockFlags chromaRedFlags = parentChromaRedFlags;
        if (geometry.HasCombinedChroma)
        {
            chromaBlueFlags = DecodeChromaCodedBlockFlags(
                ref reader,
                in chromaBlueGeometry,
                transformDepth,
                split,
                parentChromaBlueFlags);

            chromaRedFlags = DecodeChromaCodedBlockFlags(
                ref reader,
                in chromaRedGeometry,
                transformDepth,
                split,
                parentChromaRedFlags);
        }

        if (split)
        {
            for (int child = 0; child < 4; child++)
            {
                HevcTransformUnitGeometry childGeometry = geometry.CreateChild(child);
                this.DecodeTransformTree(
                    ref reader,
                    in childGeometry,
                    transformDepth + 1,
                    minimumTransformLog2,
                    usesNxNPartitions,
                    transquantBypass,
                    regionId,
                    colorPlaneIndex,
                    chromaBlueFlags,
                    chromaRedFlags);
            }

            return;
        }

        this.deblockingState.MarkBlock(
            geometry.PrimaryPlane,
            primaryGeometry.X,
            primaryGeometry.Y,
            primaryGeometry.Width,
            primaryGeometry.Height);

        HevcCodedBlockFlags primaryFlags = new(reader.ReadTransformCodedBlockFlag(false, transformDepth == 0 ? 1 : 0));
        bool hasCodedResidual = primaryFlags.Any || chromaBlueFlags.Any || chromaRedFlags.Any;
        if (hasCodedResidual && this.quantizationParameterDeltaPending)
        {
            this.ApplyQuantizationParameterDelta(reader.ReadDeltaQuantizationParameter());
            this.quantizationParameterDeltaPending = false;
        }

        if ((chromaBlueFlags.Any || chromaRedFlags.Any)
            && this.chromaQuantizationAdjustmentPending
            && !transquantBypass)
        {
            this.currentChromaQuantizationAdjustment = reader.ReadChromaQuantizationAdjustment(
                this.pictureParameterSet.ChromaQuantizationParameterOffsetsCb.Count);

            this.chromaQuantizationAdjustmentPending = false;
        }

        HevcQuantizationParameters quantizationParameters = this.CreateQuantizationParameters();
        Span<int> lumaResidual = this.integerScratch.Memory.Span.Slice(MaximumTransformSampleCount * 3, MaximumTransformSampleCount);
        lumaResidual.Clear();
        this.DecodeComponentSections(
            ref reader,
            geometry.PrimaryPlane,
            in primaryGeometry,
            primaryFlags,
            transquantBypass,
            regionId,
            colorPlaneIndex,
            in quantizationParameters,
            lumaResidual,
            true,
            0,
            in primaryGeometry);

        if (!geometry.HasCombinedChroma)
        {
            return;
        }

        int chromaMode = this.intraPredictionStates[colorPlaneIndex].GetChromaMode(geometry.Primary.X, geometry.Primary.Y);
        int chromaBlueAlpha = 0;
        bool canPredictAcrossComponents = this.pictureParameterSet.CrossComponentPredictionEnabled
            && primaryFlags.Any
            && chromaMode == 36
            && chromaBlueGeometry.Process
            && chromaBlueGeometry.Width == chromaBlueGeometry.Height;

        if (canPredictAcrossComponents)
        {
            chromaBlueAlpha = reader.ReadCrossComponentPredictionScale(0);
        }

        this.DecodeComponentSections(
            ref reader,
            HevcPlane.Cb,
            in chromaBlueGeometry,
            chromaBlueFlags,
            transquantBypass,
            regionId,
            colorPlaneIndex,
            in quantizationParameters,
            lumaResidual,
            false,
            chromaBlueAlpha,
            in primaryGeometry);

        int chromaRedAlpha = 0;
        if (canPredictAcrossComponents)
        {
            // The Cr scale follows the complete Cb residual syntax. Reading both scales together changes every
            // subsequent CABAC decision whenever Cb carries coefficients.
            chromaRedAlpha = reader.ReadCrossComponentPredictionScale(1);
        }

        this.DecodeComponentSections(
            ref reader,
            HevcPlane.Cr,
            in chromaRedGeometry,
            chromaRedFlags,
            transquantBypass,
            regionId,
            colorPlaneIndex,
            in quantizationParameters,
            lumaResidual,
            false,
            chromaRedAlpha,
            in primaryGeometry);
    }

    /// <summary>
    /// Decodes chroma coded-block flags at the highest transform level that owns the component rectangle.
    /// </summary>
    /// <param name="reader">The active entropy-substream reader.</param>
    /// <param name="geometry">The current chroma component rectangle.</param>
    /// <param name="transformDepth">The luma transform depth.</param>
    /// <param name="lumaSplit">Whether the current luma transform node subdivides.</param>
    /// <param name="parentFlags">The coded-block flags inherited from the parent transform node.</param>
    /// <returns>The flags governing the current component rectangle.</returns>
    private static HevcCodedBlockFlags DecodeChromaCodedBlockFlags(
        ref HevcCabacSyntaxReader reader,
        in HevcTransformComponentGeometry geometry,
        int transformDepth,
        bool lumaSplit,
        HevcCodedBlockFlags parentFlags)
    {
        if (!geometry.Process)
        {
            return parentFlags;
        }

        bool shouldDecode = transformDepth == 0 || (geometry.ProcessesAllQuadrants && parentFlags.Any);
        if (!shouldDecode)
        {
            return parentFlags;
        }

        int context = transformDepth;
        bool canQuadSplit = geometry.Width >= 8 && geometry.Height >= 8;
        if (geometry.Width != geometry.Height && (!lumaSplit || !canQuadSplit))
        {
            bool first = reader.ReadTransformCodedBlockFlag(true, context);
            bool second = reader.ReadTransformCodedBlockFlag(true, context);
            return new HevcCodedBlockFlags(first, second);
        }

        return new HevcCodedBlockFlags(reader.ReadTransformCodedBlockFlag(true, context));
    }

    /// <summary>
    /// Decodes one square component block or the two square sub-blocks of a rectangular 4:2:2 transform section.
    /// </summary>
    /// <param name="reader">The active entropy-substream reader.</param>
    /// <param name="plane">The reconstructed component plane.</param>
    /// <param name="geometry">The component rectangle.</param>
    /// <param name="codedBlockFlags">The component coded-block flags.</param>
    /// <param name="transquantBypass">Whether the coding unit bypasses inverse quantization and transform.</param>
    /// <param name="regionId">The current independent-slice and tile prediction region.</param>
    /// <param name="colorPlaneIndex">The selected separate-color plane, or zero for combined coding.</param>
    /// <param name="quantizationParameters">The effective component quantization parameters.</param>
    /// <param name="lumaResidual">The current luma residual retained for cross-component prediction.</param>
    /// <param name="retainResidual">Whether reconstructed residuals are copied to <paramref name="lumaResidual"/>.</param>
    /// <param name="crossComponentAlpha">The signed inverse cross-component prediction scale.</param>
    /// <param name="lumaGeometry">The luma transform rectangle governing cross-component residual addressing.</param>
    private void DecodeComponentSections(
        ref HevcCabacSyntaxReader reader,
        HevcPlane plane,
        in HevcTransformComponentGeometry geometry,
        HevcCodedBlockFlags codedBlockFlags,
        bool transquantBypass,
        int regionId,
        int colorPlaneIndex,
        in HevcQuantizationParameters quantizationParameters,
        Span<int> lumaResidual,
        bool retainResidual,
        int crossComponentAlpha,
        in HevcTransformComponentGeometry lumaGeometry)
    {
        if (!geometry.Process)
        {
            return;
        }

        if (geometry.Width == geometry.Height)
        {
            this.DecodeComponentBlock(
                ref reader,
                plane,
                geometry.X,
                geometry.Y,
                geometry.Width,
                codedBlockFlags.First,
                transquantBypass,
                regionId,
                colorPlaneIndex,
                in quantizationParameters,
                lumaResidual,
                retainResidual,
                crossComponentAlpha,
                this.GetLumaResidualOffset(plane, geometry.X, geometry.Y, in lumaGeometry),
                lumaGeometry.Width);

            return;
        }

        int size = Math.Min(geometry.Width, geometry.Height);
        int secondX = geometry.Width > geometry.Height ? geometry.X + size : geometry.X;
        int secondY = geometry.Height > geometry.Width ? geometry.Y + size : geometry.Y;
        this.DecodeComponentBlock(
            ref reader,
            plane,
            geometry.X,
            geometry.Y,
            size,
            codedBlockFlags.First,
            transquantBypass,
            regionId,
            colorPlaneIndex,
            in quantizationParameters,
            lumaResidual,
            retainResidual,
            crossComponentAlpha,
            this.GetLumaResidualOffset(plane, geometry.X, geometry.Y, in lumaGeometry),
            lumaGeometry.Width);

        this.DecodeComponentBlock(
            ref reader,
            plane,
            secondX,
            secondY,
            size,
            codedBlockFlags.Second,
            transquantBypass,
            regionId,
            colorPlaneIndex,
            in quantizationParameters,
            lumaResidual,
            retainResidual,
            crossComponentAlpha,
            this.GetLumaResidualOffset(plane, secondX, secondY, in lumaGeometry),
            lumaGeometry.Width);
    }

    /// <summary>
    /// Decodes, predicts, and reconstructs one square transform block.
    /// </summary>
    /// <param name="reader">The active entropy-substream reader.</param>
    /// <param name="plane">The reconstructed component plane.</param>
    /// <param name="x">The block left coordinate in component samples.</param>
    /// <param name="y">The block top coordinate in component samples.</param>
    /// <param name="size">The square transform-block side.</param>
    /// <param name="codedBlockFlag">Whether coefficient syntax is present.</param>
    /// <param name="transquantBypass">Whether the coding unit bypasses inverse quantization and transform.</param>
    /// <param name="regionId">The current independent-slice and tile prediction region.</param>
    /// <param name="colorPlaneIndex">The selected separate-color plane, or zero for combined coding.</param>
    /// <param name="quantizationParameters">The effective component quantization parameters.</param>
    /// <param name="lumaResidual">The current luma residual retained for cross-component prediction.</param>
    /// <param name="retainResidual">Whether reconstructed residuals are copied to <paramref name="lumaResidual"/>.</param>
    /// <param name="crossComponentAlpha">The signed inverse cross-component prediction scale.</param>
    /// <param name="lumaResidualOffset">The first colocated sample in the retained luma residual.</param>
    /// <param name="lumaResidualStride">The retained luma residual row stride.</param>
    private void DecodeComponentBlock(
        ref HevcCabacSyntaxReader reader,
        HevcPlane plane,
        int x,
        int y,
        int size,
        bool codedBlockFlag,
        bool transquantBypass,
        int regionId,
        int colorPlaneIndex,
        in HevcQuantizationParameters quantizationParameters,
        Span<int> lumaResidual,
        bool retainResidual,
        int crossComponentAlpha,
        int lumaResidualOffset,
        int lumaResidualStride)
    {
        int log2Size = BitOperations.Log2((uint)size);
        int sampleCount = size * size;
        Span<int> integerScratch = this.integerScratch.Memory.Span;
        Span<int> quantized = integerScratch[..MaximumTransformSampleCount];
        Span<int> dequantized = integerScratch.Slice(MaximumTransformSampleCount, MaximumTransformSampleCount);
        Span<int> residual = integerScratch.Slice(MaximumTransformSampleCount * 2, MaximumTransformSampleCount);
        Span<int> transformScratch = integerScratch.Slice(MaximumTransformSampleCount * 4, MaximumTransformSampleCount * 2);
        Span<ushort> prediction = this.PredictComponentBlock(plane, x, y, log2Size, regionId, colorPlaneIndex);
        residual[..sampleCount].Clear();
        bool useLumaSyntax = this.sequenceParameterSet.SeparateColorPlaneFlag;
        HevcPlane codingPlane = useLumaSyntax ? HevcPlane.Y : plane;
        int lumaX = x << this.Picture.GetSubsamplingX(plane);
        int lumaY = y << this.Picture.GetSubsamplingY(plane);
        int codingPredictionMode = plane == HevcPlane.Y || useLumaSyntax
            ? this.intraPredictionStates[colorPlaneIndex].GetLumaMode(lumaX, lumaY)
            : this.intraPredictionStates[colorPlaneIndex].GetEffectiveChromaMode(lumaX, lumaY);

        int predictionMode = codingPredictionMode;
        if (plane != HevcPlane.Y && !useLumaSyntax && this.sequenceParameterSet.ChromaFormat == 2)
        {
            predictionMode = HevcIntraPredictionMode.RemapChroma422(predictionMode);
        }

        bool transformSkip = codedBlockFlag
            && !transquantBypass
            && this.pictureParameterSet.TransformSkipEnabled
            && log2Size <= this.pictureParameterSet.MaxTransformSkipBlockLog2
            && reader.ReadTransformSkip(codingPlane != HevcPlane.Y);

        HevcResidualDpcmMode residualDpcmMode = this.sequenceParameterSet.ImplicitResidualDpcmEnabled && (transformSkip || transquantBypass)
            ? HevcResidualReconstructor.GetImplicitResidualDpcmMode(predictionMode, false)
            : HevcResidualDpcmMode.None;

        if (codedBlockFlag)
        {
            HevcCoefficientCodingParameters codingParameters = HevcCoefficientCodingParameters.Create(
                this.pictureParameterSet,
                size,
                size,
                plane,
                true,
                codingPredictionMode,
                transformSkip,
                transquantBypass,
                residualDpcmMode,
                useLumaSyntax);

            this.coefficientDecoder.Decode(ref reader, quantized, in codingParameters);

            bool rotate = HevcResidualReconstructor.IsNonTransformedResidualRotated(
                this.sequenceParameterSet.TransformSkipRotationEnabled,
                true,
                size);

            if (transquantBypass)
            {
                HevcResidualReconstructor.CopyBypassed(quantized[..sampleCount], residual, rotate);
            }
            else
            {
                int bitDepth = this.Picture.GetBitDepth(plane);
                int maxTransformDynamicRange = this.sequenceParameterSet.GetMaxTransformDynamicRange(codingPlane);
                int quantizationParameter = useLumaSyntax
                    ? quantizationParameters.Luma
                    : quantizationParameters.Get(plane);

                HevcInverseQuantizer.Dequantize(
                    quantized,
                    dequantized,
                    log2Size,
                    bitDepth,
                    maxTransformDynamicRange,
                    quantizationParameter,
                    this.sequenceParameterSet.ScalingListEnabled,
                    this.pictureParameterSet.ScalingList,
                    codingPlane,
                    true,
                    transformSkip,
                    this.sequenceParameterSet.ExtendedPrecisionProcessingEnabled);

                if (transformSkip)
                {
                    HevcResidualReconstructor.ApplyTransformSkip(
                        dequantized,
                        residual,
                        size,
                        size,
                        bitDepth,
                        maxTransformDynamicRange,
                        log2Size,
                        this.sequenceParameterSet.ExtendedPrecisionProcessingEnabled,
                        rotate);
                }
                else
                {
                    HevcInverseTransformer.Transform(
                        dequantized,
                        residual,
                        log2Size,
                        log2Size,
                        bitDepth,
                        maxTransformDynamicRange,
                        codingPlane == HevcPlane.Y && log2Size == 2,
                        transformScratch);
                }
            }

            HevcResidualReconstructor.ApplyResidualDpcm(residual, size, size, residualDpcmMode);
        }

        if (crossComponentAlpha != 0)
        {
            for (int row = 0; row < size; row++)
            {
                HevcResidualReconstructor.ApplyCrossComponentPrediction(
                    lumaResidual.Slice(lumaResidualOffset + (row * lumaResidualStride), size),
                    residual.Slice(row * size, size),
                    size,
                    crossComponentAlpha,
                    this.sequenceParameterSet.BitDepthLuma - this.sequenceParameterSet.BitDepthChroma);
            }
        }

        if (retainResidual)
        {
            for (int row = 0; row < size; row++)
            {
                residual.Slice(row * size, size).CopyTo(lumaResidual.Slice(lumaResidualOffset + (row * lumaResidualStride), size));
            }
        }

        HevcInverseTransformer.AddResidual(
            residual,
            prediction,
            size,
            size,
            size,
            this.Picture.GetBitDepth(plane));

        this.CopyPredictionToPicture(prediction, plane, x, y, size);
        this.reconstructionState.MarkReconstructed(plane, x, y, size, size, regionId);
    }

    /// <summary>
    /// Gets the packed luma-residual offset colocated with one component block.
    /// </summary>
    /// <param name="plane">The component plane.</param>
    /// <param name="x">The component block left coordinate.</param>
    /// <param name="y">The component block top coordinate.</param>
    /// <param name="lumaGeometry">The governing luma transform rectangle.</param>
    /// <returns>The zero-based packed luma-residual offset.</returns>
    private int GetLumaResidualOffset(HevcPlane plane, int x, int y, in HevcTransformComponentGeometry lumaGeometry)
    {
        int lumaX = x << this.Picture.GetSubsamplingX(plane);
        int lumaY = y << this.Picture.GetSubsamplingY(plane);
        return ((lumaY - lumaGeometry.Y) * lumaGeometry.Width) + lumaX - lumaGeometry.X;
    }

    /// <summary>
    /// Applies the signed coding-unit luma quantization delta with bit-depth-dependent modular wrapping.
    /// </summary>
    /// <param name="delta">The decoded signed delta.</param>
    private void ApplyQuantizationParameterDelta(int delta)
    {
        int bitDepthOffset = 6 * (this.sequenceParameterSet.BitDepthLuma - 8);
        int modulus = 52 + bitDepthOffset;
        int value = this.currentQuantizationParameter + delta + bitDepthOffset;
        value %= modulus;
        if (value < 0)
        {
            value += modulus;
        }

        this.currentQuantizationParameter = value - bitDepthOffset;
    }

    /// <summary>
    /// Creates the component quantization parameters selected by picture, slice, and coding-unit offsets.
    /// </summary>
    /// <returns>The effective luma, Cb, and Cr quantization parameters.</returns>
    private HevcQuantizationParameters CreateQuantizationParameters()
    {
        int cbOffset = this.pictureParameterSet.ChromaCbQuantizationParameterOffset + this.currentSliceChromaBlueQuantizationOffset;
        int crOffset = this.pictureParameterSet.ChromaCrQuantizationParameterOffset + this.currentSliceChromaRedQuantizationOffset;
        if (this.currentChromaQuantizationAdjustment > 0)
        {
            int adjustmentIndex = this.currentChromaQuantizationAdjustment - 1;
            cbOffset += this.pictureParameterSet.ChromaQuantizationParameterOffsetsCb[adjustmentIndex];
            crOffset += this.pictureParameterSet.ChromaQuantizationParameterOffsetsCr[adjustmentIndex];
        }

        return new HevcQuantizationParameters(
            this.currentQuantizationParameter,
            this.sequenceParameterSet.BitDepthLuma,
            this.sequenceParameterSet.BitDepthChroma,
            this.sequenceParameterSet.ChromaFormat,
            cbOffset,
            crOffset);
    }
}
