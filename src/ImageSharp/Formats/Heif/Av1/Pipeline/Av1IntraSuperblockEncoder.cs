// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <summary>
/// Builds fixed-partition intra decisions and reconstructed samples for one AV1 superblock.
/// </summary>
internal static partial class Av1IntraSuperblockEncoder
{
    /// <summary>
    /// Encodes one superblock stored as eight-bit samples.
    /// </summary>
    /// <param name="source">The coded source frame.</param>
    /// <param name="reconstruction">The reconstructed frame updated by the block transforms.</param>
    /// <param name="picture">The frame coding and mode-information state.</param>
    /// <param name="superblock">The reusable partition and final-block decisions.</param>
    /// <param name="coefficientBuffer">The frame-owned quantized coefficient and transform state.</param>
    /// <param name="blockWorkspace">The reusable block arithmetic workspace.</param>
    public static void Encode(
        Av1EncoderFrame<byte> source,
        Av1EncoderFrame<byte> reconstruction,
        Av1PictureControlSet picture,
        Av1Superblock superblock,
        Av1EncoderCoefficientBuffer coefficientBuffer,
        Av1EncoderBlockWorkspace blockWorkspace)
        => Encode<byte, ByteOperator>(source, reconstruction, picture, superblock, coefficientBuffer, blockWorkspace);

    /// <summary>
    /// Encodes one superblock stored as high-bit-depth samples.
    /// </summary>
    /// <param name="source">The coded source frame.</param>
    /// <param name="reconstruction">The reconstructed frame updated by the block transforms.</param>
    /// <param name="picture">The frame coding and mode-information state.</param>
    /// <param name="superblock">The reusable partition and final-block decisions.</param>
    /// <param name="coefficientBuffer">The frame-owned quantized coefficient and transform state.</param>
    /// <param name="blockWorkspace">The reusable block arithmetic workspace.</param>
    public static void Encode(
        Av1EncoderFrame<ushort> source,
        Av1EncoderFrame<ushort> reconstruction,
        Av1PictureControlSet picture,
        Av1Superblock superblock,
        Av1EncoderCoefficientBuffer coefficientBuffer,
        Av1EncoderBlockWorkspace blockWorkspace)
        => Encode<ushort, UInt16Operator>(source, reconstruction, picture, superblock, coefficientBuffer, blockWorkspace);

    private static void Encode<TSample, TOperator>(
        Av1EncoderFrame<TSample> source,
        Av1EncoderFrame<TSample> reconstruction,
        Av1PictureControlSet picture,
        Av1Superblock superblock,
        Av1EncoderCoefficientBuffer coefficientBuffer,
        Av1EncoderBlockWorkspace blockWorkspace)
        where TSample : unmanaged
        where TOperator : struct, IBlockEncodingOperator<TSample>
    {
        int superblockSize = picture.Sequence.SequenceHeader.SuperblockSize.GetWidth();
        Point superblockOrigin = new(
            (superblock.Index % coefficientBuffer.SuperblockColumnCount) * superblockSize,
            (superblock.Index / coefficientBuffer.SuperblockColumnCount) * superblockSize);

        superblock.Workspace.Reset();
        Traversal<TSample, TOperator> traversal = new(
            source.CodedView,
            reconstruction.CodedView,
            picture,
            superblock,
            coefficientBuffer,
            blockWorkspace);

        traversal.EncodePartitionTree(superblockOrigin, picture.Sequence.SequenceHeader.SuperblockSize);
    }

    private static void EncodePlaneBlock<TSample, TOperator>(
        Av1EncoderFrame<TSample>.PlanarView source,
        Av1EncoderFrame<TSample>.PlanarView reconstruction,
        Av1EncoderBlockWorkspace blockWorkspace,
        ObuQuantizationParameters quantization,
        Av1BitDepth bitDepth,
        Av1Plane plane,
        Point blockOrigin,
        Av1TransformSize transformSize,
        Span<int> coefficients,
        ref Av1EncoderTransformBlockState state)
        where TSample : unmanaged
        where TOperator : struct, IBlockEncodingOperator<TSample>
    {
        Buffer2DRegion<TSample> sourcePlane = source.GetPlane(plane);
        Buffer2DRegion<TSample> reconstructionPlane = reconstruction.GetPlane(plane);
        int width = transformSize.GetWidth();
        int height = transformSize.GetHeight();
        bool hasLeft = blockOrigin.X > 0;
        bool hasAbove = blockOrigin.Y > 0;
        ReadOnlySpan<TSample> above = hasAbove
            ? reconstructionPlane.DangerousGetRowSpan(blockOrigin.Y - 1).Slice(blockOrigin.X, width)
            : [];

        Span<TSample> left = TOperator.GetLeftReference(blockWorkspace.Residual, height);
        if (hasLeft)
        {
            for (int row = 0; row < height; row++)
            {
                left[row] = reconstructionPlane.DangerousGetRowSpan(blockOrigin.Y + row)[blockOrigin.X - 1];
            }
        }

        // Prediction consumes every gathered reference before residual construction reuses the same workspace bytes.
        TOperator.Encode(
            blockWorkspace,
            sourcePlane,
            reconstructionPlane,
            blockOrigin,
            above,
            left,
            hasLeft,
            hasAbove,
            coefficients,
            transformSize,
            quantization.QIndex[0],
            quantization.DeltaQDc[(int)plane],
            quantization.DeltaQAc[(int)plane],
            plane,
            bitDepth,
            ref state);
    }

    /// <summary>
    /// Retains the stack-only state shared by recursive partition and final-block traversal.
    /// </summary>
    /// <typeparam name="TSample">The native unsigned sample storage type.</typeparam>
    /// <typeparam name="TOperator">The type-specific block encoding operations.</typeparam>
    private ref struct Traversal<TSample, TOperator>
        where TSample : unmanaged
        where TOperator : struct, IBlockEncodingOperator<TSample>
    {
        private readonly Av1EncoderFrame<TSample>.PlanarView source;
        private readonly Av1EncoderFrame<TSample>.PlanarView reconstruction;
        private readonly Av1PictureControlSet picture;
        private readonly Av1Superblock superblock;
        private readonly Av1EncoderBlockWorkspace blockWorkspace;
        private readonly ObuQuantizationParameters quantization;
        private readonly Av1BitDepth bitDepth;
        private readonly Span<int> lumaCoefficients;
        private readonly Span<int> blueCoefficients;
        private readonly Span<int> redCoefficients;
        private readonly Span<Av1EncoderTransformBlockState> lumaTransformBlocks;
        private readonly Span<Av1EncoderTransformBlockState> blueTransformBlocks;
        private readonly Span<Av1EncoderTransformBlockState> redTransformBlocks;
        private int partitionIndex;
        private int finalBlockIndex;
        private int codedAreaLuma;
        private int codedAreaChroma;

        public Traversal(
            Av1EncoderFrame<TSample>.PlanarView source,
            Av1EncoderFrame<TSample>.PlanarView reconstruction,
            Av1PictureControlSet picture,
            Av1Superblock superblock,
            Av1EncoderCoefficientBuffer coefficientBuffer,
            Av1EncoderBlockWorkspace blockWorkspace)
        {
            this.source = source;
            this.reconstruction = reconstruction;
            this.picture = picture;
            this.superblock = superblock;
            this.blockWorkspace = blockWorkspace;
            this.quantization = picture.Parent.FrameHeader.QuantizationParameters;
            this.bitDepth = picture.Sequence.SequenceHeader.ColorConfig.BitDepth;
            this.lumaCoefficients = coefficientBuffer.GetPlaneSpan(superblock.Index, Av1Plane.Y);
            this.blueCoefficients = coefficientBuffer.GetPlaneSpan(superblock.Index, Av1Plane.U);
            this.redCoefficients = coefficientBuffer.GetPlaneSpan(superblock.Index, Av1Plane.V);
            this.lumaTransformBlocks = coefficientBuffer.GetTransformBlockSpan(superblock.Index, Av1Plane.Y);
            this.blueTransformBlocks = coefficientBuffer.GetTransformBlockSpan(superblock.Index, Av1Plane.U);
            this.redTransformBlocks = coefficientBuffer.GetTransformBlockSpan(superblock.Index, Av1Plane.V);
            this.partitionIndex = 0;
            this.finalBlockIndex = 0;
            this.codedAreaLuma = 0;
            this.codedAreaChroma = 0;
        }

        public void EncodePartitionTree(Point blockOrigin, Av1BlockSize blockSize)
        {
            Av1EncoderCommon common = this.picture.Parent.Common;
            Point modeInfoPosition = new(blockOrigin.X >> Av1Constants.ModeInfoSizeLog2, blockOrigin.Y >> Av1Constants.ModeInfoSizeLog2);
            if (modeInfoPosition.Y >= common.ModeInfoRowCount || modeInfoPosition.X >= common.ModeInfoColumnCount)
            {
                return;
            }

            if (blockSize == Av1BlockSize.Block8x8)
            {
                this.superblock.CodingUnitPartitionTypes[this.partitionIndex++] = (byte)Av1PartitionType.None;
                this.EncodeFinalBlock(blockOrigin, modeInfoPosition);
                return;
            }

            this.superblock.CodingUnitPartitionTypes[this.partitionIndex++] = (byte)Av1PartitionType.Split;
            Av1BlockSize subSize = Av1PartitionType.Split.GetBlockSubSize(blockSize);
            int halfBlockSize = blockSize.GetWidth() >> 1;

            // The preorder and out-of-frame pruning match tile writing, so one reusable decision workspace is sufficient.
            this.EncodePartitionTree(blockOrigin, subSize);
            this.EncodePartitionTree(blockOrigin + new Size(halfBlockSize, 0), subSize);
            this.EncodePartitionTree(blockOrigin + new Size(0, halfBlockSize), subSize);
            this.EncodePartitionTree(blockOrigin + new Size(halfBlockSize, halfBlockSize), subSize);
        }

        private void EncodeFinalBlock(Point blockOrigin, Point modeInfoPosition)
        {
            const Av1BlockSize BlockSize = Av1BlockSize.Block8x8;
            const Av1TransformSize LumaTransformSize = Av1TransformSize.Size8x8;
            int qIndex = this.quantization.QIndex[0];
            ref Av1MacroBlockModeInfo modeInfo = ref this.picture.GetMacroBlockModeInfo(modeInfoPosition);
            modeInfo.Block = new Av1EncoderBlockModeInfo
            {
                BlockSize = BlockSize,
                PartitionType = Av1PartitionType.None,
                SegmentId = 0,
                TransformSize = LumaTransformSize,
                Mode = Av1PredictionMode.DC,
                UvMode = Av1ChromaPredictionMode.DC
            };

            modeInfo.CdefStrength = 0;
            ref Av1EncoderBlockStruct block = ref this.superblock.FinalBlocks[this.finalBlockIndex++];
            block.HasChroma = !this.source.IsMonochrome;
            block.QuantizationIndex = qIndex;
            block.SegmentId = 0;

            int lumaTransformIndex = this.codedAreaLuma /
                Av1EncoderCoefficientBuffer.TransformBlockUnitCoefficientCount;

            ref Av1EncoderTransformBlockState lumaState = ref this.lumaTransformBlocks[lumaTransformIndex];
            this.EncodePlaneBlock(
                Av1Plane.Y,
                blockOrigin,
                LumaTransformSize,
                this.lumaCoefficients[this.codedAreaLuma..],
                ref lumaState);

            this.codedAreaLuma += LumaTransformSize.GetSize2d();
            if (this.source.IsMonochrome)
            {
                return;
            }

            ObuColorConfig colorConfig = this.picture.Sequence.SequenceHeader.ColorConfig;
            int subsamplingX = colorConfig.SubSamplingX ? 1 : 0;
            int subsamplingY = colorConfig.SubSamplingY ? 1 : 0;
            Point chromaOrigin = new(blockOrigin.X >> subsamplingX, blockOrigin.Y >> subsamplingY);
            Av1TransformSize chromaTransformSize = BlockSize.GetMaxUvTransformSize(
                colorConfig.SubSamplingX,
                colorConfig.SubSamplingY);

            int chromaTransformIndex = this.codedAreaChroma /
                Av1EncoderCoefficientBuffer.TransformBlockUnitCoefficientCount;

            ref Av1EncoderTransformBlockState blueState = ref this.blueTransformBlocks[chromaTransformIndex];
            ref Av1EncoderTransformBlockState redState = ref this.redTransformBlocks[chromaTransformIndex];
            this.EncodePlaneBlock(
                Av1Plane.U,
                chromaOrigin,
                chromaTransformSize,
                this.blueCoefficients[this.codedAreaChroma..],
                ref blueState);

            this.EncodePlaneBlock(
                Av1Plane.V,
                chromaOrigin,
                chromaTransformSize,
                this.redCoefficients[this.codedAreaChroma..],
                ref redState);

            // Ordinary intra retains non-skip syntax even for empty transforms, matching live mode selection.
            this.codedAreaChroma += chromaTransformSize.GetSize2d();
        }

        private void EncodePlaneBlock(
            Av1Plane plane,
            Point blockOrigin,
            Av1TransformSize transformSize,
            Span<int> coefficients,
            ref Av1EncoderTransformBlockState state)
            => Av1IntraSuperblockEncoder.EncodePlaneBlock<TSample, TOperator>(
                this.source,
                this.reconstruction,
                this.blockWorkspace,
                this.quantization,
                this.bitDepth,
                plane,
                blockOrigin,
                transformSize,
                coefficients,
                ref state);
    }
}
