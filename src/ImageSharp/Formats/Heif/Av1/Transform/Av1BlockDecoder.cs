// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Quantification;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

internal class Av1BlockDecoder
{
    private readonly ObuSequenceHeader sequenceHeader;

    private readonly ObuFrameHeader frameHeader;

    private readonly Av1FrameInfo frameInfo;

    private readonly Av1FrameBuffer<byte> frameBuffer;

    private readonly bool isLoopFilterEnabled;

    private readonly int[] currentCoefficientIndex;

    public Av1BlockDecoder(ObuSequenceHeader sequenceHeader, ObuFrameHeader frameHeader, Av1FrameInfo frameInfo, Av1FrameBuffer<byte> frameBuffer)
    {
        this.sequenceHeader = sequenceHeader;
        this.frameHeader = frameHeader;
        this.frameInfo = frameInfo;
        this.frameBuffer = frameBuffer;
        int ySize = (1 << this.sequenceHeader.SuperblockSizeLog2) * (1 << this.sequenceHeader.SuperblockSizeLog2);
        int inverseQuantizationSize = ySize +
            (this.sequenceHeader.ColorConfig.SubSamplingX ? ySize >> 2 : ySize) +
            (this.sequenceHeader.ColorConfig.SubSamplingY ? ySize >> 2 : ySize);
        this.CurrentInverseQuantizationCoefficients = new int[inverseQuantizationSize];
        this.isLoopFilterEnabled = false;
        this.currentCoefficientIndex = new int[3];
    }

    public int[] CurrentInverseQuantizationCoefficients { get; private set; }

    public void UpdateSuperblock(Av1SuperblockInfo superblockInfo)
    {
        this.currentCoefficientIndex[0] = 0;
        this.currentCoefficientIndex[1] = 0;
        this.currentCoefficientIndex[2] = 0;
    }

    /// <summary>
    /// SVT: svt_aom_decode_block
    /// </summary>
    public void DecodeBlock(Av1BlockModeInfo modeInfo, Point modeInfoPosition, Av1BlockSize blockSize, Av1SuperblockInfo superblockInfo, Av1TileInfo tileInfo)
    {
        ObuColorConfig colorConfig = this.sequenceHeader.ColorConfig;
        Av1TransformType transformType;
        Av1TransformSize transformSize;
        int transformUnitCount;
        bool hasChroma = Av1TileReader.HasChroma(this.sequenceHeader, modeInfoPosition, blockSize);
        Av1PartitionInfo partitionInfo = new(modeInfo, superblockInfo, hasChroma, Av1PartitionType.None);

        int maxBlocksWide = partitionInfo.GetMaxBlockWide(blockSize, false);
        int maxBlocksHigh = partitionInfo.GetMaxBlockHigh(blockSize, false);

        bool isLossless = this.frameHeader.LosslessArray[modeInfo.SegmentId];
        bool isLosslessBlock = isLossless && ((blockSize >= Av1BlockSize.Block64x64) && (blockSize <= Av1BlockSize.Block128x128));
        int chromaTransformUnitCount = isLosslessBlock
                ? (maxBlocksWide * maxBlocksHigh) >> ((colorConfig.SubSamplingX ? 1 : 0) + (colorConfig.SubSamplingY ? 1 : 0))
                : modeInfo.TransformUnitsCount[(int)Av1Plane.U];
        bool highBitDepth = false;
        bool is16BitsPipeline = false;
        int loopFilterStride = this.frameHeader.ModeInfoStride;
        Av1PredictionDecoder predictionDecoder = new(this.sequenceHeader, this.frameHeader, false);
        Av1InverseQuantizer inverseQuantizer = new(this.sequenceHeader, this.frameHeader);

        for (int plane = 0; plane < colorConfig.PlaneCount; plane++)
        {
            int subX = (plane > 0) ? colorConfig.SubSamplingX ? 1 : 0 : 0;
            int subY = (plane > 0) ? colorConfig.SubSamplingY ? 1 : 0 : 0;

            if (plane != 0 && !partitionInfo.IsChroma)
            {
                continue;
            }

            int transformInfoIndex = plane switch
            {
                2 => superblockInfo.TransformInfoIndexUv + modeInfo.FirstTransformLocation[plane - 1] + chromaTransformUnitCount,
                1 => superblockInfo.TransformInfoIndexY + modeInfo.FirstTransformLocation[plane],
                0 => superblockInfo.TransformInfoIndexY + modeInfo.FirstTransformLocation[plane],
                _ => throw new InvalidImageContentException("Maximum of 3 color planes")
            };
            ref Av1TransformInfo transformInfo = ref Unsafe.Add(ref this.frameInfo.GetSuperblockTransform(plane, superblockInfo.Position), transformInfoIndex);

            if (isLosslessBlock)
            {
                Guard.IsTrue(transformInfo.Size == Av1TransformSize.Size4x4, nameof(transformInfo.Size), "Lossless may only have 4x4 blocks.");
                transformUnitCount = (maxBlocksWide * maxBlocksHigh) >> (subX + subY);
            }
            else
            {
                transformUnitCount = modeInfo.TransformUnitsCount[Math.Min(1, plane)];
            }

            Guard.IsFalse(transformUnitCount == 0, nameof(transformUnitCount), "Must have at least a single transform unit to decode.");

            // SVT: svt_aom_derive_blk_pointers
            DeriveBlockPointers(
                this.frameBuffer,
                plane,
                (modeInfoPosition.X >> subX) << Av1Constants.ModeInfoSizeLog2,
                (modeInfoPosition.Y >> subY) << Av1Constants.ModeInfoSizeLog2,
                out Span<byte> blockReconstructionBuffer,
                out int reconstructionStride,
                subX,
                subY);

            for (int tu = 0; tu < transformUnitCount; tu++)
            {
                Span<byte> transformBlockReconstructionBuffer;
                int transformBlockOffset;

                transformSize = transformInfo.Size;
                Span<int> coefficients = superblockInfo.GetCoefficients((Av1Plane)plane)[this.currentCoefficientIndex[plane]..];

                transformBlockOffset = ((transformInfo.OffsetY * reconstructionStride) + transformInfo.OffsetX) << Av1Constants.ModeInfoSizeLog2;
                transformBlockReconstructionBuffer = blockReconstructionBuffer.Slice(transformBlockOffset << (highBitDepth ? 1 : 0));

                if (this.isLoopFilterEnabled)
                {
                    /*
                    if (plane != 2)
                    {
                        // SVT: svt_aom_fill_4x4_lf_param
                        Fill4x4LoopFilterParameters(
                            this.loopFilterContext,
                            (modeInfoPosition.X & (~subX)) + (transformInfo.OffsetX << subX),
                            (modeInfoPosition.Y & (~subY)) + (transformInfo.OffsetY << subY),
                            loopFilterStride,
                            transformSize,
                            subX,
                            subY,
                            plane);
                    }*/
                }

                // if (!inter_block)
                if (true)
                {
                    // SVT: svt_av1_predict_intra
                    predictionDecoder.Decode(
                        partitionInfo,
                        (Av1Plane)plane,
                        transformSize,
                        tileInfo,
                        transformBlockReconstructionBuffer,
                        reconstructionStride,
                        this.frameBuffer.BitDepth,
                        transformInfo.OffsetX,
                        transformInfo.OffsetY);
                }

                int numberOfCoefficients = 0;

                if (!modeInfo.Skip && transformInfo.CodeBlockFlag)
                {
                    Span<int> quantizationCoefficients = this.CurrentInverseQuantizationCoefficients;
                    int inverseQuantizationSize = transformSize.GetWidth() * transformSize.GetHeight();
                    quantizationCoefficients[..inverseQuantizationSize].Clear();
                    transformType = transformInfo.Type;

                    // SVT: svt_aom_inverse_quantize
                    numberOfCoefficients = inverseQuantizer.InverseQuantize(
                        modeInfo, coefficients, quantizationCoefficients, transformType, transformSize, (Av1Plane)plane);
                    if (numberOfCoefficients != 0)
                    {
                        this.currentCoefficientIndex[plane] += numberOfCoefficients + 1;

                        if (this.frameBuffer.BitDepth == Av1BitDepth.EightBit && !is16BitsPipeline)
                        {
                            // SVT: svt_aom_inv_transform_recon8bit
                            Av1InverseTransformer.Reconstruct8Bit(
                                quantizationCoefficients,
                                transformBlockReconstructionBuffer,
                                reconstructionStride,
                                transformSize,
                                transformType,
                                plane,
                                numberOfCoefficients,
                                isLossless);
                        }
                        else
                        {
                            throw new NotImplementedException("No support for 16 bit pipeline yet.");
                        }
                    }
                }

                // Store Luma for CFL if required!
                if (plane == (int)Av1Plane.Y && StoreChromeFromLumeRequired(colorConfig, partitionInfo, hasChroma))
                {
                    /*
                    // SVT: svt_cfl_store_tx
                    ChromaFromLumaStoreTransform(
                        partitionInfo,
                        this.chromaFromLumaContext,
                        transformInfo.OffsetY,
                        transformInfo.OffsetX,
                        transformSize,
                        blockSize,
                        colorConfig,
                        transformBlockReconstructionBuffer,
                        reconstructionStride,
                        is16BitsPipeline);
                    */
                }

                // increment transform pointer
                transformInfo = ref Unsafe.Add(ref transformInfo, 1);
            }
        }
    }

    private static void DeriveBlockPointers(Av1FrameBuffer<byte> frameBuffer, int plane, int blockColumnInPixels, int blockRowInPixels, out Span<byte> blockReconstructionBuffer, out int reconstructionStride, int subX, int subY)
    {
        int blockOffset;

        if (plane == 0)
        {
            blockOffset = ((frameBuffer.OriginY + blockRowInPixels) * frameBuffer.BufferY!.Width) +
                (frameBuffer.OriginX + blockColumnInPixels);
            reconstructionStride = frameBuffer.BufferY!.Width;
        }
        else if (plane == 1)
        {
            blockOffset = (((frameBuffer.OriginY >> subY) + blockRowInPixels) * frameBuffer.BufferCb!.Width) +
                ((frameBuffer.OriginX >> subX) + blockColumnInPixels);
            reconstructionStride = frameBuffer.BufferCb!.Width;
        }
        else
        {
            blockOffset = (((frameBuffer.OriginY >> subY) + blockRowInPixels) * frameBuffer.BufferCr!.Width) +
                ((frameBuffer.OriginX >> subX) + blockColumnInPixels);
            reconstructionStride = frameBuffer.BufferCr!.Width;
        }

        if (frameBuffer.BitDepth != Av1BitDepth.EightBit || frameBuffer.Is16BitPipeline)
        {
            // 16bit pipeline
            blockOffset *= 2;
            if (plane == 0)
            {
                blockReconstructionBuffer = frameBuffer.BufferY!.DangerousGetSingleSpan()[blockOffset..];
            }
            else if (plane == 1)
            {
                blockReconstructionBuffer = frameBuffer.BufferCb!.DangerousGetSingleSpan()[blockOffset..];
            }
            else
            {
                blockReconstructionBuffer = frameBuffer.BufferCr!.DangerousGetSingleSpan()[blockOffset..];
            }
        }
        else
        {
            if (plane == 0)
            {
                blockReconstructionBuffer = frameBuffer.BufferY!.DangerousGetSingleSpan()[blockOffset..];
            }
            else if (plane == 1)
            {
                blockReconstructionBuffer = frameBuffer.BufferCb!.DangerousGetSingleSpan()[blockOffset..];
            }
            else
            {
                blockReconstructionBuffer = frameBuffer.BufferCr!.DangerousGetSingleSpan()[blockOffset..];
            }
        }
    }

    private static bool StoreChromeFromLumeRequired(ObuColorConfig colorConfig, Av1PartitionInfo partitionInfo, bool hasChroma) => false;
}
