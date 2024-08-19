// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

internal class Av1BlockDecoder
{
    private readonly ObuSequenceHeader sequenceHeader;

    private readonly ObuFrameHeader frameHeader;

    private readonly Av1FrameInfo frameInfo;

    private readonly Av1FrameBuffer frameBuffer;

    public Av1BlockDecoder(ObuSequenceHeader sequenceHeader, ObuFrameHeader frameHeader, Av1FrameInfo frameInfo, Av1FrameBuffer frameBuffer)
    {
        this.sequenceHeader = sequenceHeader;
        this.frameHeader = frameHeader;
        this.frameInfo = frameInfo;
        this.frameBuffer = frameBuffer;
    }

    public static void DecodeBlock(Av1BlockModeInfo modeInfo, Point modeInfoPosition, Av1BlockSize blockSize, Av1SuperblockInfo superblockInfo)
    {
        /*
        ObuColorConfig colorConfig = this.sequenceHeader.ColorConfig;
        Av1TransformType transformType;
        Span<int> coefficients;
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

            this.DeriveBlockPointers(
                this.reconstructionFrameBuffer,
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
                coefficients = this.currentCoefficients[plane];

                transformBlockOffset = ((transformInfo.OffsetY * reconstructionStride) + transformInfo.OffsetX) << Av1Constants.ModeInfoSizeLog2;
                transformBlockReconstructionBuffer = blockReconstructionBuffer.Slice(transformBlockOffset << (highBitDepth ? 1 : 0));

                if (this.isLoopFilterEnabled)
                {
                    if (plane != 2)
                    {
                        Fill4x4LoopFilterParameters(
                            this.loopFilterContext,
                            (modeInfoPosition.X & (~subX)) + (transformInfo.OffsetX << subX),
                            (modeInfoPosition.Y & (~subY)) + (transformInfo.OffsetY << subY),
                            loopFilterStride,
                            transformSize,
                            subX,
                            subY,
                            plane);
                    }
                }

                // if (!inter_block)
                if (true)
                {
                    PredictIntra(
                        partitionInfo,
                        plane,
                        transformSize,
                        tile,
                        transformBlockReconstructionBuffer,
                        reconstructionStride,
                        this.reconstructionFrameBuffer.BitDepth,
                        transformInfo.OffsetX,
                        transformInfo.OffsetY);
                }

                int numberOfCoefficients = 0;

                if (!modeInfo.Skip && transformInfo.CodeBlockFlag)
                {
                    Span<int> quantizationCoefficients = this.CurrentInverseQuantizationCoefficients;
                    int inverseQuantizationSize = transformSize.GetWidth() * transformSize.GetHeight();
                    quantizationCoefficients[..inverseQuantizationSize].Clear();
                    this.CurrentInverseQuantizationCoefficients = quantizationCoefficients[inverseQuantizationSize..];
                    transformType = transformInfo.Type;

                    numberOfCoefficients = InverseQuantize(
                        partitionInfo, modeInfo, coefficients, quantizationCoefficients, transformType, transformSize, plane);
                    if (numberOfCoefficients != 0)
                    {
                        this.CurrentCoefficients[plane] += numberOfCoefficients + 1;

                        if (this.reconstructionFrameBuffer.BitDepth == Av1BitDepth.EightBit && !is16BitsPipeline)
                        {
                            InverseTransformReconstruction8Bit(
                                quantizationCoefficients,
                                (Span<byte>)transformBlockReconstructionBuffer,
                                reconstructionStride,
                                (Span<byte>)transformBlockReconstructionBuffer,
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
                if (plane == (int)Av1Plane.Y && StoreChromeFromLumeRequired(colorConfig, partitionInfo, this.frameHeader.IsChroma))
                {
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
                }

                // increment transform pointer
                transformInfo = ref Unsafe.Add(ref transformInfo, 1);
            }
        }*/
    }
}
