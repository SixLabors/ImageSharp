// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.FilmGrain;

/// <summary>
/// Synthesizes the film-grain signal carried by one displayed AV1 image frame.
/// </summary>
/// <remarks>
/// Grain is applied after loop restoration to presentation samples and is not part of reference reconstruction.
/// </remarks>
internal sealed class Av1FilmGrainDecoder
{
    /// <summary>
    /// The luma width and height of one independently selected grain block.
    /// </summary>
    private const int LumaSubblockSize = 32;

    /// <summary>
    /// The maximum autoregressive lag reserved around generated grain templates.
    /// </summary>
    private const int AutoregressivePadding = 3;

    /// <summary>
    /// The template padding that precedes the autoregressive stabilization region.
    /// </summary>
    private const int TemplatePadding = 3;

    /// <summary>
    /// The number of random bits used to index the normative Gaussian sequence.
    /// </summary>
    private const int GaussianIndexBits = 11;

    /// <summary>
    /// The sequence-level bit-depth and chroma-sampling configuration.
    /// </summary>
    private readonly ObuSequenceHeader sequenceHeader;

    /// <summary>
    /// The frame-level film-grain synthesis parameters.
    /// </summary>
    private readonly ObuFrameHeader frameHeader;

    /// <summary>
    /// The restored planar samples that receive the synthesized grain.
    /// </summary>
    private readonly Av1FrameBuffer<byte> frameBuffer;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1FilmGrainDecoder"/> class.
    /// </summary>
    /// <param name="sequenceHeader">The sequence header defining bit depth and chroma sampling.</param>
    /// <param name="frameHeader">The frame header containing the complete grain parameters.</param>
    /// <param name="frameBuffer">The restored frame samples to which grain is added.</param>
    public Av1FilmGrainDecoder(
        ObuSequenceHeader sequenceHeader,
        ObuFrameHeader frameHeader,
        Av1FrameBuffer<byte> frameBuffer)
    {
        this.sequenceHeader = sequenceHeader;
        this.frameHeader = frameHeader;
        this.frameBuffer = frameBuffer;
    }

    /// <summary>
    /// Adds the signaled film grain to the visible luma and chroma samples.
    /// </summary>
    public void DecodeFrame()
    {
        ObuFilmGrainParameters parameters = this.frameHeader.FilmGrainParameters;

        // Film grain is a presentation process. A frame which does not signal it must retain the restored samples
        // byte-for-byte, so the decoder does not allocate templates or touch the padded frame planes in this case.
        if (!parameters.ApplyGrain)
        {
            return;
        }

        ObuColorConfig colorConfig = this.sequenceHeader.ColorConfig;
        bool isMonochrome = colorConfig.IsMonochrome;
        int subsamplingX = !isMonochrome && colorConfig.SubSamplingX ? 1 : 0;
        int subsamplingY = !isMonochrome && colorConfig.SubSamplingY ? 1 : 0;
        int visibleWidth = this.frameBuffer.Width;
        int visibleHeight = this.frameBuffer.Height;

        // Grain blocks are traversed in half-resolution luma coordinates and expanded in 2x2 sample groups.
        // Replicating an odd final row or column makes that traversal complete without changing the visible extent.
        int alignedWidth = Av1Math.AlignPowerOf2(visibleWidth, 1);
        int alignedHeight = Av1Math.AlignPowerOf2(visibleHeight, 1);

        Buffer2D<byte> lumaBuffer = this.frameBuffer.BufferY!;

        // Frame planes are allocated as bytes even for high-bit-depth pictures. Convert their byte strides to
        // native sample strides once so every later offset is expressed consistently in samples.
        int lumaStride = lumaBuffer.Width / this.frameBuffer.BytesPerSample;
        int chromaStride = isMonochrome
            ? 0
            : this.frameBuffer.BufferCb!.Width / this.frameBuffer.BytesPerSample;

        // Closing ApplyGrain over byte or ushort keeps synthesis in the frame buffer's native representation.
        // This avoids an intermediate converted image while allowing the JIT to remove the sample-type branches.
        if (this.frameBuffer.BytesPerSample == 2)
        {
            Span<ushort> luma = GetPlaneSamples<ushort>(
                lumaBuffer,
                this.frameBuffer.OriginX,
                this.frameBuffer.OriginY);

            Span<ushort> cb = isMonochrome
                ? Span<ushort>.Empty
                : GetPlaneSamples<ushort>(
                    this.frameBuffer.BufferCb!,
                    this.frameBuffer.OriginX >> subsamplingX,
                    this.frameBuffer.OriginY >> subsamplingY);

            Span<ushort> cr = isMonochrome
                ? Span<ushort>.Empty
                : GetPlaneSamples<ushort>(
                    this.frameBuffer.BufferCr!,
                    this.frameBuffer.OriginX >> subsamplingX,
                    this.frameBuffer.OriginY >> subsamplingY);

            ExtendPlane(luma, lumaStride, visibleWidth, visibleHeight, alignedWidth, alignedHeight);
            if (!isMonochrome)
            {
                int visibleChromaWidth = Av1Math.DivideLog2Ceiling(visibleWidth, subsamplingX);
                int visibleChromaHeight = Av1Math.DivideLog2Ceiling(visibleHeight, subsamplingY);
                int alignedChromaWidth = alignedWidth >> subsamplingX;
                int alignedChromaHeight = alignedHeight >> subsamplingY;
                ExtendPlane(cb, chromaStride, visibleChromaWidth, visibleChromaHeight, alignedChromaWidth, alignedChromaHeight);
                ExtendPlane(cr, chromaStride, visibleChromaWidth, visibleChromaHeight, alignedChromaWidth, alignedChromaHeight);
            }

            this.ApplyGrain(
                parameters,
                luma,
                cb,
                cr,
                alignedWidth,
                alignedHeight,
                lumaStride,
                chromaStride,
                subsamplingX,
                subsamplingY,
                isMonochrome);
        }
        else
        {
            Span<byte> luma = GetPlaneSamples<byte>(
                lumaBuffer,
                this.frameBuffer.OriginX,
                this.frameBuffer.OriginY);

            Span<byte> cb = isMonochrome
                ? Span<byte>.Empty
                : GetPlaneSamples<byte>(
                    this.frameBuffer.BufferCb!,
                    this.frameBuffer.OriginX >> subsamplingX,
                    this.frameBuffer.OriginY >> subsamplingY);

            Span<byte> cr = isMonochrome
                ? Span<byte>.Empty
                : GetPlaneSamples<byte>(
                    this.frameBuffer.BufferCr!,
                    this.frameBuffer.OriginX >> subsamplingX,
                    this.frameBuffer.OriginY >> subsamplingY);

            ExtendPlane(luma, lumaStride, visibleWidth, visibleHeight, alignedWidth, alignedHeight);
            if (!isMonochrome)
            {
                int visibleChromaWidth = Av1Math.DivideLog2Ceiling(visibleWidth, subsamplingX);
                int visibleChromaHeight = Av1Math.DivideLog2Ceiling(visibleHeight, subsamplingY);
                int alignedChromaWidth = alignedWidth >> subsamplingX;
                int alignedChromaHeight = alignedHeight >> subsamplingY;
                ExtendPlane(cb, chromaStride, visibleChromaWidth, visibleChromaHeight, alignedChromaWidth, alignedChromaHeight);
                ExtendPlane(cr, chromaStride, visibleChromaWidth, visibleChromaHeight, alignedChromaWidth, alignedChromaHeight);
            }

            this.ApplyGrain(
                parameters,
                luma,
                cb,
                cr,
                alignedWidth,
                alignedHeight,
                lumaStride,
                chromaStride,
                subsamplingX,
                subsamplingY,
                isMonochrome);
        }
    }

    /// <summary>
    /// Gets a plane span beginning at its first visible sample.
    /// </summary>
    /// <typeparam name="TSample">The native eight-bit or high-bit-depth sample type.</typeparam>
    /// <param name="buffer">The padded plane allocation.</param>
    /// <param name="originX">The horizontal visible origin in samples.</param>
    /// <param name="originY">The vertical visible origin in rows.</param>
    /// <returns>The sample span beginning at the visible origin and retaining the padded row stride.</returns>
    private static Span<TSample> GetPlaneSamples<TSample>(Buffer2D<byte> buffer, int originX, int originY)
        where TSample : unmanaged
    {
        Span<TSample> samples = MemoryMarshal.Cast<byte, TSample>(buffer.DangerousGetSingleSpan());
        int stride = buffer.Width / Unsafe.SizeOf<TSample>();

        // The returned span intentionally retains the allocation beyond the visible rectangle. Film-grain overlap
        // and odd-dimension extension use the frame buffer's existing right and bottom padding through this stride.
        return samples[((originY * stride) + originX)..];
    }

    /// <summary>
    /// Replicates the last visible row and column when film-grain block traversal requires even dimensions.
    /// </summary>
    /// <typeparam name="TSample">The native eight-bit or high-bit-depth sample type.</typeparam>
    /// <param name="plane">The plane span beginning at its visible origin.</param>
    /// <param name="stride">The number of samples between adjacent rows.</param>
    /// <param name="visibleWidth">The visible plane width.</param>
    /// <param name="visibleHeight">The visible plane height.</param>
    /// <param name="alignedWidth">The even width consumed by synthesis.</param>
    /// <param name="alignedHeight">The even height consumed by synthesis.</param>
    private static void ExtendPlane<TSample>(
        Span<TSample> plane,
        int stride,
        int visibleWidth,
        int visibleHeight,
        int alignedWidth,
        int alignedHeight)
        where TSample : unmanaged
    {
        if (visibleWidth != alignedWidth)
        {
            // The synthetic column is consumed only as the partner of the final visible sample in a 2x2 group.
            for (int row = 0; row < visibleHeight; row++)
            {
                int rowOffset = row * stride;
                plane[rowOffset + visibleWidth] = plane[rowOffset + visibleWidth - 1];
            }
        }

        if (visibleHeight != alignedHeight)
        {
            // Span.CopyTo uses the runtime's optimized bulk-copy path and preserves the already replicated edge column.
            plane.Slice((visibleHeight - 1) * stride, alignedWidth)
                .CopyTo(plane.Slice(visibleHeight * stride, alignedWidth));
        }
    }

    /// <summary>
    /// Generates reusable grain templates and adds selected blocks to one restored frame.
    /// </summary>
    /// <typeparam name="TSample">The native eight-bit or high-bit-depth sample type.</typeparam>
    /// <param name="parameters">The complete self-contained frame grain parameters.</param>
    /// <param name="luma">The luma plane beginning at its visible origin.</param>
    /// <param name="cb">The first chroma plane, or an empty span for monochrome input.</param>
    /// <param name="cr">The second chroma plane, or an empty span for monochrome input.</param>
    /// <param name="width">The even luma width processed by synthesis.</param>
    /// <param name="height">The even luma height processed by synthesis.</param>
    /// <param name="lumaStride">The luma row stride in samples.</param>
    /// <param name="chromaStride">The chroma row stride in samples.</param>
    /// <param name="subsamplingX">The horizontal chroma subsampling shift.</param>
    /// <param name="subsamplingY">The vertical chroma subsampling shift.</param>
    /// <param name="isMonochrome">Whether the frame has no chroma planes.</param>
    private void ApplyGrain<TSample>(
        ObuFilmGrainParameters parameters,
        Span<TSample> luma,
        Span<TSample> cb,
        Span<TSample> cr,
        int width,
        int height,
        int lumaStride,
        int chromaStride,
        int subsamplingX,
        int subsamplingY,
        bool isMonochrome)
        where TSample : unmanaged
    {
        // A template contains a selectable 64x64 luma region, the maximum three-sample autoregressive history,
        // and the fixed margins required by the block-offset process. Chroma dimensions contract with sampling.
        int chromaSubblockHeight = LumaSubblockSize >> subsamplingY;
        int chromaSubblockWidth = LumaSubblockSize >> subsamplingX;
        int lumaBlockHeight = TemplatePadding + (2 * AutoregressivePadding) + (2 * LumaSubblockSize);
        int lumaBlockWidth = TemplatePadding + (2 * AutoregressivePadding) + (2 * LumaSubblockSize) +
            (2 * AutoregressivePadding) + TemplatePadding;

        int chromaBlockHeight = TemplatePadding + ((2 >> subsamplingY) * AutoregressivePadding) +
            (2 * chromaSubblockHeight);

        int chromaBlockWidth = TemplatePadding + ((2 >> subsamplingX) * AutoregressivePadding) +
            (2 * chromaSubblockWidth) + ((2 >> subsamplingX) * AutoregressivePadding) + TemplatePadding;

        int lumaGrainLength = lumaBlockHeight * lumaBlockWidth;
        int chromaGrainLength = isMonochrome ? 0 : chromaBlockHeight * chromaBlockWidth;
        int scalingLength = isMonochrome ? 256 : 768;

        // Overlap keeps the outgoing two luma rows/columns, or their subsampled chroma equivalents, until the
        // adjacent block is selected. Frames without overlap do not reserve these line and column workspaces.
        int lumaLineLength = parameters.OverlapFlag ? lumaStride * 2 : 0;
        int chromaLineLength = parameters.OverlapFlag && !isMonochrome
            ? chromaStride * (2 >> subsamplingY)
            : 0;

        int lumaColumnLength = parameters.OverlapFlag ? (LumaSubblockSize + 2) * 2 : 0;
        int chromaColumnLength = parameters.OverlapFlag && !isMonochrome
            ? (chromaSubblockHeight + (2 >> subsamplingY)) * (2 >> subsamplingX)
            : 0;

        int scratchLength = scalingLength + lumaGrainLength + (2 * chromaGrainLength) +
            lumaLineLength + (2 * chromaLineLength) + lumaColumnLength + (2 * chromaColumnLength);

        // All frame-lifetime film-grain state shares one allocator-backed owner. The slices below are disjoint,
        // and their logical ordering mirrors lookup tables, templates, horizontal boundaries, then vertical boundaries.
        using IMemoryOwner<int> scratchOwner = this.frameBuffer.MemoryAllocator.Allocate<int>(scratchLength);
        Span<int> scratch = scratchOwner.GetSpan()[..scratchLength];
        int scratchOffset = 0;

        Span<int> scalingY = scratch.Slice(scratchOffset, 256);
        scratchOffset += 256;
        Span<int> scalingCb = isMonochrome ? Span<int>.Empty : scratch.Slice(scratchOffset, 256);
        scratchOffset += scalingCb.Length;
        Span<int> scalingCr = isMonochrome ? Span<int>.Empty : scratch.Slice(scratchOffset, 256);
        scratchOffset += scalingCr.Length;
        Span<int> lumaGrain = scratch.Slice(scratchOffset, lumaGrainLength);
        scratchOffset += lumaGrain.Length;
        Span<int> cbGrain = isMonochrome ? Span<int>.Empty : scratch.Slice(scratchOffset, chromaGrainLength);
        scratchOffset += cbGrain.Length;
        Span<int> crGrain = isMonochrome ? Span<int>.Empty : scratch.Slice(scratchOffset, chromaGrainLength);
        scratchOffset += crGrain.Length;
        Span<int> yLineBuffer = parameters.OverlapFlag ? scratch.Slice(scratchOffset, lumaLineLength) : Span<int>.Empty;
        scratchOffset += yLineBuffer.Length;
        Span<int> cbLineBuffer = parameters.OverlapFlag && !isMonochrome
            ? scratch.Slice(scratchOffset, chromaLineLength)
            : Span<int>.Empty;

        scratchOffset += cbLineBuffer.Length;
        Span<int> crLineBuffer = parameters.OverlapFlag && !isMonochrome
            ? scratch.Slice(scratchOffset, chromaLineLength)
            : Span<int>.Empty;

        scratchOffset += crLineBuffer.Length;
        Span<int> yColumnBuffer = parameters.OverlapFlag
            ? scratch.Slice(scratchOffset, lumaColumnLength)
            : Span<int>.Empty;

        scratchOffset += yColumnBuffer.Length;
        Span<int> cbColumnBuffer = parameters.OverlapFlag && !isMonochrome
            ? scratch.Slice(scratchOffset, chromaColumnLength)
            : Span<int>.Empty;

        scratchOffset += cbColumnBuffer.Length;
        Span<int> crColumnBuffer = parameters.OverlapFlag && !isMonochrome
            ? scratch.Slice(scratchOffset, chromaColumnLength)
            : Span<int>.Empty;

        // libaom zero-initializes the lookup structure before expanding control points. This matters
        // when chroma scaling is inherited from an empty luma scaling function.
        scalingY.Clear();
        scalingCb.Clear();
        scalingCr.Clear();

        int bitDepth = this.sequenceHeader.ColorConfig.BitDepth.GetBitCount();
        ushort randomRegister = (ushort)parameters.GrainSeed;

        // Luma consumes the seed's initial pseudo-random sequence. Chroma generation subsequently reinitializes
        // the same register with plane-specific row identities so its two templates remain deterministic and distinct.
        GenerateLumaGrain(
            parameters,
            ref randomRegister,
            lumaGrain,
            lumaBlockHeight,
            lumaBlockWidth,
            lumaBlockWidth,
            bitDepth);

        if (!isMonochrome)
        {
            GenerateChromaGrain(
                parameters,
                ref randomRegister,
                lumaGrain,
                cbGrain,
                crGrain,
                lumaBlockWidth,
                chromaBlockHeight,
                chromaBlockWidth,
                chromaBlockWidth,
                subsamplingX,
                subsamplingY,
                bitDepth);
        }

        // Signaled points describe piecewise-linear functions over the eight-bit domain. High-bit-depth samples
        // interpolate between these 256 entries later, rather than allocating larger per-depth lookup tables.
        InitializeScalingFunction(parameters.PointYValue, parameters.PointYScaling, (int)parameters.NumYPoints, scalingY);
        if (!isMonochrome)
        {
            if (parameters.ChromaScalingFromLuma)
            {
                scalingY.CopyTo(scalingCb);
                scalingY.CopyTo(scalingCr);
            }
            else
            {
                InitializeScalingFunction(
                    parameters.PointCbValue,
                    parameters.PointCbScaling,
                    (int)parameters.NumCbPoints,
                    scalingCb);

                InitializeScalingFunction(
                    parameters.PointCrValue,
                    parameters.PointCrScaling,
                    (int)parameters.NumCrPoints,
                    scalingCr);
            }
        }

        int grainMinimum = -(1 << (bitDepth - 1));
        int grainMaximum = (1 << (bitDepth - 1)) - 1;
        bool isIdentityMatrix = this.sequenceHeader.ColorConfig.MatrixCoefficients == ObuMatrixCoefficients.Identity;

        // Coordinates are halved because each iteration owns one 32x32 luma block but all frame offsets are even.
        // Keeping the loop in this domain also makes one-unit boundary adjustments represent two luma samples.
        for (int halfY = 0; halfY < height / 2; halfY += LumaSubblockSize >> 1)
        {
            // Block rows restart from a seed mixed with their luma row number. This makes a block's selection
            // independent of decoder traversal outside its row while remaining reproducible from the bitstream.
            InitializeRandomGenerator(ref randomRegister, halfY << 1, (ushort)parameters.GrainSeed);

            for (int halfX = 0; halfX < width / 2; halfX += LumaSubblockSize >> 1)
            {
                // The high and low nibbles choose an even luma offset inside the reusable 64x64 template region.
                // Chroma offsets use the corresponding subsampled position so all planes share the same selection.
                int randomOffset = GetRandomNumber(ref randomRegister, 8);
                int offsetX = (randomOffset >> 4) & 15;
                int offsetY = randomOffset & 15;
                int lumaOffsetY = TemplatePadding + (2 * AutoregressivePadding) + (offsetY << 1);
                int lumaOffsetX = TemplatePadding + (2 * AutoregressivePadding) + (offsetX << 1);
                int chromaOffsetY = TemplatePadding + ((2 >> subsamplingY) * AutoregressivePadding) +
                    (offsetY * (2 >> subsamplingY));

                int chromaOffsetX = TemplatePadding + ((2 >> subsamplingX) * AutoregressivePadding) +
                    (offsetX * (2 >> subsamplingX));

                if (parameters.OverlapFlag && halfX != 0)
                {
                    // Blend the incoming template columns with the outgoing columns saved by the block on the left.
                    // Writing back to the column buffers produces the exact grain region applied at this boundary.
                    Av1FilmGrainOverlap.Vertical(
                        yColumnBuffer,
                        2,
                        lumaGrain[((lumaOffsetY * lumaBlockWidth) + lumaOffsetX)..],
                        lumaBlockWidth,
                        yColumnBuffer,
                        2,
                        2,
                        Math.Min(LumaSubblockSize + 2, height - (halfY << 1)),
                        grainMinimum,
                        grainMaximum);

                    if (!isMonochrome)
                    {
                        int chromaOverlapWidth = 2 >> subsamplingX;
                        int chromaOverlapHeight = Math.Min(
                            chromaSubblockHeight + (2 >> subsamplingY),
                            (height - (halfY << 1)) >> subsamplingY);

                        Av1FilmGrainOverlap.Vertical(
                            cbColumnBuffer,
                            chromaOverlapWidth,
                            cbGrain[((chromaOffsetY * chromaBlockWidth) + chromaOffsetX)..],
                            chromaBlockWidth,
                            cbColumnBuffer,
                            chromaOverlapWidth,
                            chromaOverlapWidth,
                            chromaOverlapHeight,
                            grainMinimum,
                            grainMaximum);

                        Av1FilmGrainOverlap.Vertical(
                            crColumnBuffer,
                            chromaOverlapWidth,
                            crGrain[((chromaOffsetY * chromaBlockWidth) + chromaOffsetX)..],
                            chromaBlockWidth,
                            crColumnBuffer,
                            chromaOverlapWidth,
                            chromaOverlapWidth,
                            chromaOverlapHeight,
                            grainMinimum,
                            grainMaximum);
                    }

                    int rowAdjustment = halfY != 0 ? 1 : 0;

                    // The top overlap row, when present, is owned by the horizontal-boundary pass below. Skip it here
                    // so the corner and vertical boundary are each added to the decoded samples exactly once.
                    int destinationLumaOffset = (((halfY + rowAdjustment) << 1) * lumaStride) + (halfX << 1);
                    int destinationChromaOffset = (((halfY + rowAdjustment) << (1 - subsamplingY)) * chromaStride) +
                        (halfX << (1 - subsamplingX));

                    int columnGrainOffset = rowAdjustment * (2 - subsamplingY) * (2 - subsamplingX);
                    Span<TSample> destinationCb = isMonochrome
                        ? Span<TSample>.Empty
                        : cb[destinationChromaOffset..];

                    Span<TSample> destinationCr = isMonochrome
                        ? Span<TSample>.Empty
                        : cr[destinationChromaOffset..];

                    Span<int> columnCbGrain = isMonochrome
                        ? Span<int>.Empty
                        : cbColumnBuffer[columnGrainOffset..];

                    Span<int> columnCrGrain = isMonochrome
                        ? Span<int>.Empty
                        : crColumnBuffer[columnGrainOffset..];

                    Av1FilmGrainNoise.Apply(
                        parameters,
                        scalingY,
                        scalingCb,
                        scalingCr,
                        luma[destinationLumaOffset..],
                        destinationCb,
                        destinationCr,
                        lumaStride,
                        chromaStride,
                        yColumnBuffer[(rowAdjustment * 4)..],
                        columnCbGrain,
                        columnCrGrain,
                        2,
                        2 - subsamplingX,
                        Math.Min(LumaSubblockSize >> 1, (height / 2) - halfY) - rowAdjustment,
                        1,
                        bitDepth,
                        subsamplingX,
                        subsamplingY,
                        isMonochrome,
                        isIdentityMatrix);
                }

                if (parameters.OverlapFlag && halfY != 0)
                {
                    if (halfX != 0)
                    {
                        // At an interior corner, first combine the saved top boundary with the already blended left
                        // boundary. The resulting corner is then part of the horizontal boundary applied below.
                        Av1FilmGrainOverlap.Horizontal(
                            yLineBuffer[(halfX << 1)..],
                            lumaStride,
                            yColumnBuffer,
                            2,
                            yLineBuffer[(halfX << 1)..],
                            lumaStride,
                            2,
                            2,
                            grainMinimum,
                            grainMaximum);

                        if (!isMonochrome)
                        {
                            int chromaOverlapWidth = 2 >> subsamplingX;
                            int chromaOverlapHeight = 2 >> subsamplingY;
                            int chromaLineOffset = halfX * chromaOverlapWidth;
                            Av1FilmGrainOverlap.Horizontal(
                                cbLineBuffer[chromaLineOffset..],
                                chromaStride,
                                cbColumnBuffer,
                                chromaOverlapWidth,
                                cbLineBuffer[chromaLineOffset..],
                                chromaStride,
                                chromaOverlapWidth,
                                chromaOverlapHeight,
                                grainMinimum,
                                grainMaximum);

                            Av1FilmGrainOverlap.Horizontal(
                                crLineBuffer[chromaLineOffset..],
                                chromaStride,
                                crColumnBuffer,
                                chromaOverlapWidth,
                                crLineBuffer[chromaLineOffset..],
                                chromaStride,
                                chromaOverlapWidth,
                                chromaOverlapHeight,
                                grainMinimum,
                                grainMaximum);
                        }
                    }

                    int overlappedColumn = halfX != 0 ? halfX + 1 : 0;
                    int templateColumnAdjustment = halfX != 0 ? 2 : 0;

                    // The horizontal boundary excludes the two luma columns already emitted by vertical overlap.
                    // The same adjustment contracts to one column for horizontally subsampled chroma.
                    int horizontalWidth = Math.Min(
                        LumaSubblockSize - templateColumnAdjustment,
                        width - (overlappedColumn << 1));

                    Av1FilmGrainOverlap.Horizontal(
                        yLineBuffer[(overlappedColumn << 1)..],
                        lumaStride,
                        lumaGrain[((lumaOffsetY * lumaBlockWidth) + lumaOffsetX + templateColumnAdjustment)..],
                        lumaBlockWidth,
                        yLineBuffer[(overlappedColumn << 1)..],
                        lumaStride,
                        horizontalWidth,
                        2,
                        grainMinimum,
                        grainMaximum);

                    if (!isMonochrome)
                    {
                        int chromaColumnAdjustment = halfX != 0 ? 2 >> subsamplingX : 0;
                        int chromaDestinationOffset = overlappedColumn << (1 - subsamplingX);
                        int chromaWidth = Math.Min(
                            chromaSubblockWidth - chromaColumnAdjustment,
                            (width - (overlappedColumn << 1)) >> subsamplingX);

                        Av1FilmGrainOverlap.Horizontal(
                            cbLineBuffer[chromaDestinationOffset..],
                            chromaStride,
                            cbGrain[((chromaOffsetY * chromaBlockWidth) + chromaOffsetX + chromaColumnAdjustment)..],
                            chromaBlockWidth,
                            cbLineBuffer[chromaDestinationOffset..],
                            chromaStride,
                            chromaWidth,
                            2 >> subsamplingY,
                            grainMinimum,
                            grainMaximum);

                        Av1FilmGrainOverlap.Horizontal(
                            crLineBuffer[chromaDestinationOffset..],
                            chromaStride,
                            crGrain[((chromaOffsetY * chromaBlockWidth) + chromaOffsetX + chromaColumnAdjustment)..],
                            chromaBlockWidth,
                            crLineBuffer[chromaDestinationOffset..],
                            chromaStride,
                            chromaWidth,
                            2 >> subsamplingY,
                            grainMinimum,
                            grainMaximum);
                    }

                    int boundaryLumaOffset = ((halfY << 1) * lumaStride) + (halfX << 1);
                    int boundaryChromaOffset = ((halfY << (1 - subsamplingY)) * chromaStride) +
                        (halfX << (1 - subsamplingX));

                    Span<TSample> boundaryCb = isMonochrome
                        ? Span<TSample>.Empty
                        : cb[boundaryChromaOffset..];

                    Span<TSample> boundaryCr = isMonochrome
                        ? Span<TSample>.Empty
                        : cr[boundaryChromaOffset..];

                    Span<int> boundaryCbGrain = isMonochrome
                        ? Span<int>.Empty
                        : cbLineBuffer[(halfX << (1 - subsamplingX))..];

                    Span<int> boundaryCrGrain = isMonochrome
                        ? Span<int>.Empty
                        : crLineBuffer[(halfX << (1 - subsamplingX))..];

                    // Apply the completed top boundary as a one-unit half-height strip, which is two luma rows and
                    // one or two chroma rows depending on vertical subsampling.
                    Av1FilmGrainNoise.Apply(
                        parameters,
                        scalingY,
                        scalingCb,
                        scalingCr,
                        luma[boundaryLumaOffset..],
                        boundaryCb,
                        boundaryCr,
                        lumaStride,
                        chromaStride,
                        yLineBuffer[(halfX << 1)..],
                        boundaryCbGrain,
                        boundaryCrGrain,
                        lumaStride,
                        chromaStride,
                        1,
                        Math.Min(LumaSubblockSize >> 1, (width / 2) - halfX),
                        bitDepth,
                        subsamplingX,
                        subsamplingY,
                        isMonochrome,
                        isIdentityMatrix);
                }

                int interiorRowAdjustment = parameters.OverlapFlag && halfY != 0 ? 1 : 0;
                int interiorColumnAdjustment = parameters.OverlapFlag && halfX != 0 ? 1 : 0;

                // Move both the destination and template origins past boundary strips already applied above. This
                // leaves a disjoint interior rectangle, including clipped partial blocks at the right and bottom edges.
                int lumaGrainOffset = ((lumaOffsetY + (interiorRowAdjustment << 1)) * lumaBlockWidth) +
                    lumaOffsetX + (interiorColumnAdjustment << 1);

                int chromaGrainOffset = ((chromaOffsetY +
                    (interiorRowAdjustment << (1 - subsamplingY))) * chromaBlockWidth) +
                    chromaOffsetX + (interiorColumnAdjustment << (1 - subsamplingX));

                int interiorLumaOffset = (((halfY + interiorRowAdjustment) << 1) * lumaStride) +
                    ((halfX + interiorColumnAdjustment) << 1);

                int interiorChromaOffset = (((halfY + interiorRowAdjustment) << (1 - subsamplingY)) * chromaStride) +
                    ((halfX + interiorColumnAdjustment) << (1 - subsamplingX));

                Span<TSample> interiorCb = isMonochrome ? Span<TSample>.Empty : cb[interiorChromaOffset..];
                Span<TSample> interiorCr = isMonochrome ? Span<TSample>.Empty : cr[interiorChromaOffset..];
                Span<int> interiorCbGrain = isMonochrome ? Span<int>.Empty : cbGrain[chromaGrainOffset..];
                Span<int> interiorCrGrain = isMonochrome ? Span<int>.Empty : crGrain[chromaGrainOffset..];

                Av1FilmGrainNoise.Apply(
                    parameters,
                    scalingY,
                    scalingCb,
                    scalingCr,
                    luma[interiorLumaOffset..],
                    interiorCb,
                    interiorCr,
                    lumaStride,
                    chromaStride,
                    lumaGrain[lumaGrainOffset..],
                    interiorCbGrain,
                    interiorCrGrain,
                    lumaBlockWidth,
                    chromaBlockWidth,
                    Math.Min(LumaSubblockSize >> 1, (height / 2) - halfY) - interiorRowAdjustment,
                    Math.Min(LumaSubblockSize >> 1, (width / 2) - halfX) - interiorColumnAdjustment,
                    bitDepth,
                    subsamplingX,
                    subsamplingY,
                    isMonochrome,
                    isIdentityMatrix);

                if (parameters.OverlapFlag)
                {
                    if (halfX != 0)
                    {
                        // Preserve the completed corner in the line buffers before the column buffers are overwritten.
                        // It becomes the top input for the block at this column position on the next block row.
                        CopyArea(
                            yColumnBuffer[(LumaSubblockSize << 1)..],
                            2,
                            yLineBuffer[(halfX << 1)..],
                            lumaStride,
                            2,
                            2);

                        if (!isMonochrome)
                        {
                            int chromaOverlapWidth = 2 >> subsamplingX;
                            int chromaOverlapHeight = 2 >> subsamplingY;
                            int sourceOffset = chromaSubblockHeight << (1 - subsamplingX);
                            int destinationOffset = halfX << (1 - subsamplingX);
                            CopyArea(
                                cbColumnBuffer[sourceOffset..],
                                chromaOverlapWidth,
                                cbLineBuffer[destinationOffset..],
                                chromaStride,
                                chromaOverlapWidth,
                                chromaOverlapHeight);

                            CopyArea(
                                crColumnBuffer[sourceOffset..],
                                chromaOverlapWidth,
                                crLineBuffer[destinationOffset..],
                                chromaStride,
                                chromaOverlapWidth,
                                chromaOverlapHeight);
                        }
                    }

                    int lineDestinationColumn = halfX != 0 ? halfX + 1 : 0;
                    int lineTemplateAdjustment = halfX != 0 ? 2 : 0;

                    // Save the template's bottom boundary for the block directly below. Columns already represented
                    // by the corner are skipped so the line buffer remains one contiguous frame-width boundary.
                    int lineWidth = Math.Min(LumaSubblockSize, width - (halfX << 1)) - lineTemplateAdjustment;
                    CopyArea(
                        lumaGrain[(((lumaOffsetY + LumaSubblockSize) * lumaBlockWidth) +
                            lumaOffsetX + lineTemplateAdjustment)..],
                        lumaBlockWidth,
                        yLineBuffer[(lineDestinationColumn << 1)..],
                        lumaStride,
                        lineWidth,
                        2);

                    if (!isMonochrome)
                    {
                        int chromaLineAdjustment = halfX != 0 ? 2 >> subsamplingX : 0;
                        int chromaLineWidth = Math.Min(
                            chromaSubblockWidth,
                            (width - (halfX << 1)) >> subsamplingX) - chromaLineAdjustment;

                        int chromaLineDestination = lineDestinationColumn << (1 - subsamplingX);
                        CopyArea(
                            cbGrain[(((chromaOffsetY + chromaSubblockHeight) * chromaBlockWidth) +
                                chromaOffsetX + chromaLineAdjustment)..],
                            chromaBlockWidth,
                            cbLineBuffer[chromaLineDestination..],
                            chromaStride,
                            chromaLineWidth,
                            2 >> subsamplingY);

                        CopyArea(
                            crGrain[(((chromaOffsetY + chromaSubblockHeight) * chromaBlockWidth) +
                                chromaOffsetX + chromaLineAdjustment)..],
                            chromaBlockWidth,
                            crLineBuffer[chromaLineDestination..],
                            chromaStride,
                            chromaLineWidth,
                            2 >> subsamplingY);
                    }

                    // Finally retain the template's right boundary for the next block in this row. The extra two rows
                    // extend beyond the nominal block so a later corner blend has both horizontal overlap rows available.
                    CopyArea(
                        lumaGrain[((lumaOffsetY * lumaBlockWidth) + lumaOffsetX + LumaSubblockSize)..],
                        lumaBlockWidth,
                        yColumnBuffer,
                        2,
                        2,
                        Math.Min(LumaSubblockSize + 2, height - (halfY << 1)));

                    if (!isMonochrome)
                    {
                        int chromaOverlapWidth = 2 >> subsamplingX;
                        int chromaOverlapHeight = Math.Min(
                            chromaSubblockHeight + (2 >> subsamplingY),
                            (height - (halfY << 1)) >> subsamplingY);

                        CopyArea(
                            cbGrain[((chromaOffsetY * chromaBlockWidth) +
                                chromaOffsetX + chromaSubblockWidth)..],
                            chromaBlockWidth,
                            cbColumnBuffer,
                            chromaOverlapWidth,
                            chromaOverlapWidth,
                            chromaOverlapHeight);

                        CopyArea(
                            crGrain[((chromaOffsetY * chromaBlockWidth) +
                                chromaOffsetX + chromaSubblockWidth)..],
                            chromaBlockWidth,
                            crColumnBuffer,
                            chromaOverlapWidth,
                            chromaOverlapWidth,
                            chromaOverlapHeight);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Generates the luma grain template and applies its autoregressive filter.
    /// </summary>
    /// <param name="parameters">The frame grain parameters.</param>
    /// <param name="randomRegister">The pseudo-random register advanced while filling the template.</param>
    /// <param name="grain">The luma grain template.</param>
    /// <param name="height">The template height.</param>
    /// <param name="width">The template width.</param>
    /// <param name="stride">The template row stride.</param>
    /// <param name="bitDepth">The decoded sample bit depth.</param>
    private static void GenerateLumaGrain(
        ObuFilmGrainParameters parameters,
        ref ushort randomRegister,
        Span<int> grain,
        int height,
        int width,
        int stride,
        int bitDepth)
    {
        if (parameters.NumYPoints == 0)
        {
            // Without a luma scaling function no luma grain is ever applied. A zero template is still required when
            // chroma autoregression is present because its optional luma predictor must then contribute zero.
            grain.Clear();
            return;
        }

        // The fixed Gaussian table has 12-bit amplitude. GrainScaleShift and the decoded bit depth reduce it to
        // the signed working range before the causal autoregressive filter changes its spatial correlation.
        int gaussianShift = 12 - bitDepth + (int)parameters.GrainScaleShift;
        int gaussianRounding = (1 << gaussianShift) >> 1;
        ReadOnlySpan<short> gaussian = Av1FilmGrainGaussianSequence.Samples;
        for (int row = 0; row < height; row++)
        {
            for (int column = 0; column < width; column++)
            {
                int randomIndex = GetRandomNumber(ref randomRegister, GaussianIndexBits);
                grain[(row * stride) + column] = (gaussian[randomIndex] + gaussianRounding) >> gaussianShift;
            }
        }

        int lag = (int)parameters.ArCoeffLag;
        int coefficientShift = (int)parameters.ArCoeffShiftMinus6 + 6;
        int roundingOffset = 1 << (coefficientShift - 1);
        int grainMinimum = -(1 << (bitDepth - 1));
        int grainMaximum = (1 << (bitDepth - 1)) - 1;
        ReadOnlySpan<byte> coefficients = parameters.ArCoeffsYPlus128;

        // TemplatePadding leaves every lag-one through lag-three predecessor addressable without a boundary branch.
        // Raster order guarantees that all rows above and all samples to the left have already been filtered.
        for (int row = TemplatePadding; row < height; row++)
        {
            for (int column = TemplatePadding; column < width - TemplatePadding; column++)
            {
                int weightedSum = 0;
                int coefficientIndex = 0;

                // The AV1 coefficient order walks the complete rows above the current sample before
                // the already generated samples to its left on the current row.
                for (int relativeRow = -lag; relativeRow < 0; relativeRow++)
                {
                    for (int relativeColumn = -lag; relativeColumn <= lag; relativeColumn++)
                    {
                        weightedSum += ((int)coefficients[coefficientIndex++] - 128) *
                            grain[((row + relativeRow) * stride) + column + relativeColumn];
                    }
                }

                for (int relativeColumn = -lag; relativeColumn < 0; relativeColumn++)
                {
                    weightedSum += ((int)coefficients[coefficientIndex++] - 128) *
                        grain[(row * stride) + column + relativeColumn];
                }

                int grainIndex = (row * stride) + column;
                grain[grainIndex] = Av1Math.Clamp(
                    grain[grainIndex] + ((weightedSum + roundingOffset) >> coefficientShift),
                    grainMinimum,
                    grainMaximum);
            }
        }
    }

    /// <summary>
    /// Generates both chroma grain templates and applies their autoregressive filters.
    /// </summary>
    /// <param name="parameters">The frame grain parameters.</param>
    /// <param name="randomRegister">The pseudo-random register used for chroma template generation.</param>
    /// <param name="lumaGrain">The completed luma grain template.</param>
    /// <param name="cbGrain">The first chroma grain template.</param>
    /// <param name="crGrain">The second chroma grain template.</param>
    /// <param name="lumaStride">The luma grain-template row stride.</param>
    /// <param name="height">The chroma template height.</param>
    /// <param name="width">The chroma template width.</param>
    /// <param name="stride">The chroma template row stride.</param>
    /// <param name="subsamplingX">The horizontal chroma subsampling shift.</param>
    /// <param name="subsamplingY">The vertical chroma subsampling shift.</param>
    /// <param name="bitDepth">The decoded sample bit depth.</param>
    private static void GenerateChromaGrain(
        ObuFilmGrainParameters parameters,
        ref ushort randomRegister,
        ReadOnlySpan<int> lumaGrain,
        Span<int> cbGrain,
        Span<int> crGrain,
        int lumaStride,
        int height,
        int width,
        int stride,
        int subsamplingX,
        int subsamplingY,
        int bitDepth)
    {
        bool applyCb = parameters.NumCbPoints != 0 || parameters.ChromaScalingFromLuma;
        bool applyCr = parameters.NumCrPoints != 0 || parameters.ChromaScalingFromLuma;
        int gaussianShift = 12 - bitDepth + (int)parameters.GrainScaleShift;
        int gaussianRounding = (1 << gaussianShift) >> 1;
        ReadOnlySpan<short> gaussian = Av1FilmGrainGaussianSequence.Samples;
        if (applyCb)
        {
            // The fixed luma-line identities seven and eleven decorrelate the two chroma pseudo-random sequences
            // from each other and from the luma template while retaining deterministic generation from GrainSeed.
            InitializeRandomGenerator(ref randomRegister, 7 << 5, (ushort)parameters.GrainSeed);
            FillGaussianGrain(ref randomRegister, cbGrain, height, width, stride, gaussian, gaussianShift, gaussianRounding);
        }
        else
        {
            cbGrain.Clear();
        }

        if (applyCr)
        {
            InitializeRandomGenerator(ref randomRegister, 11 << 5, (ushort)parameters.GrainSeed);
            FillGaussianGrain(ref randomRegister, crGrain, height, width, stride, gaussian, gaussianShift, gaussianRounding);
        }
        else
        {
            crGrain.Clear();
        }

        int lag = (int)parameters.ArCoeffLag;
        int coefficientShift = (int)parameters.ArCoeffShiftMinus6 + 6;
        int roundingOffset = 1 << (coefficientShift - 1);
        int grainMinimum = -(1 << (bitDepth - 1));
        int grainMaximum = (1 << (bitDepth - 1)) - 1;
        ReadOnlySpan<byte> cbCoefficients = parameters.ArCoeffsCbPlus128;
        ReadOnlySpan<byte> crCoefficients = parameters.ArCoeffsCrPlus128;

        // Cb and Cr share the same causal predecessor walk, so both accumulators advance one coefficient index
        // together. A disabled plane stays zero but does not alter the coefficient ordering of the enabled plane.
        for (int row = TemplatePadding; row < height; row++)
        {
            for (int column = TemplatePadding; column < width - TemplatePadding; column++)
            {
                int weightedCb = 0;
                int weightedCr = 0;
                int coefficientIndex = 0;
                for (int relativeRow = -lag; relativeRow < 0; relativeRow++)
                {
                    for (int relativeColumn = -lag; relativeColumn <= lag; relativeColumn++)
                    {
                        int grainIndex = ((row + relativeRow) * stride) + column + relativeColumn;
                        if (applyCb)
                        {
                            weightedCb += ((int)cbCoefficients[coefficientIndex] - 128) * cbGrain[grainIndex];
                        }

                        if (applyCr)
                        {
                            weightedCr += ((int)crCoefficients[coefficientIndex] - 128) * crGrain[grainIndex];
                        }

                        coefficientIndex++;
                    }
                }

                for (int relativeColumn = -lag; relativeColumn < 0; relativeColumn++)
                {
                    int grainIndex = (row * stride) + column + relativeColumn;
                    if (applyCb)
                    {
                        weightedCb += ((int)cbCoefficients[coefficientIndex] - 128) * cbGrain[grainIndex];
                    }

                    if (applyCr)
                    {
                        weightedCr += ((int)crCoefficients[coefficientIndex] - 128) * crGrain[grainIndex];
                    }

                    coefficientIndex++;
                }

                if (parameters.NumYPoints != 0)
                {
                    // Chroma has one additional autoregressive predictor when luma grain exists. Average the luma
                    // template footprint represented by this chroma sample before applying that final coefficient.
                    int lumaRow = ((row - TemplatePadding) << subsamplingY) + TemplatePadding;
                    int lumaColumn = ((column - TemplatePadding) << subsamplingX) + TemplatePadding;
                    int averageLuma = 0;
                    for (int relativeRow = 0; relativeRow <= subsamplingY; relativeRow++)
                    {
                        for (int relativeColumn = 0; relativeColumn <= subsamplingX; relativeColumn++)
                        {
                            averageLuma += lumaGrain[((lumaRow + relativeRow) * lumaStride) +
                                lumaColumn + relativeColumn];
                        }
                    }

                    int averagingShift = subsamplingX + subsamplingY;
                    averageLuma = (averageLuma + ((1 << averagingShift) >> 1)) >> averagingShift;
                    if (applyCb)
                    {
                        weightedCb += ((int)cbCoefficients[coefficientIndex] - 128) * averageLuma;
                    }

                    if (applyCr)
                    {
                        weightedCr += ((int)crCoefficients[coefficientIndex] - 128) * averageLuma;
                    }
                }

                int currentIndex = (row * stride) + column;
                if (applyCb)
                {
                    cbGrain[currentIndex] = Av1Math.Clamp(
                        cbGrain[currentIndex] + ((weightedCb + roundingOffset) >> coefficientShift),
                        grainMinimum,
                        grainMaximum);
                }

                if (applyCr)
                {
                    crGrain[currentIndex] = Av1Math.Clamp(
                        crGrain[currentIndex] + ((weightedCr + roundingOffset) >> coefficientShift),
                        grainMinimum,
                        grainMaximum);
                }
            }
        }
    }

    /// <summary>
    /// Fills one chroma template from the normative Gaussian sequence.
    /// </summary>
    /// <param name="randomRegister">The pseudo-random register advanced for each sample.</param>
    /// <param name="grain">The destination grain template.</param>
    /// <param name="height">The template height.</param>
    /// <param name="width">The template width.</param>
    /// <param name="stride">The template row stride.</param>
    /// <param name="gaussian">The normative Gaussian sample sequence.</param>
    /// <param name="gaussianShift">The bit-depth-dependent Gaussian scaling shift.</param>
    /// <param name="gaussianRounding">The Gaussian scaling rounding offset.</param>
    private static void FillGaussianGrain(
        ref ushort randomRegister,
        Span<int> grain,
        int height,
        int width,
        int stride,
        ReadOnlySpan<short> gaussian,
        int gaussianShift,
        int gaussianRounding)
    {
        // Eleven pseudo-random bits address all 2,048 Gaussian entries with no modulo operation or distribution skew.
        for (int row = 0; row < height; row++)
        {
            for (int column = 0; column < width; column++)
            {
                int randomIndex = GetRandomNumber(ref randomRegister, GaussianIndexBits);
                grain[(row * stride) + column] = (gaussian[randomIndex] + gaussianRounding) >> gaussianShift;
            }
        }
    }

    /// <summary>
    /// Expands signaled control points into the 256-entry film-grain scaling lookup table.
    /// </summary>
    /// <param name="pointValues">The strictly increasing input coordinates.</param>
    /// <param name="pointScalings">The scaling value at each input coordinate.</param>
    /// <param name="pointCount">The number of populated control points.</param>
    /// <param name="lookup">The destination scaling lookup table.</param>
    private static void InitializeScalingFunction(
        ReadOnlySpan<byte> pointValues,
        ReadOnlySpan<byte> pointScalings,
        int pointCount,
        Span<int> lookup)
    {
        if (pointCount == 0)
        {
            return;
        }

        // Values outside the first and last control points extend their nearest endpoint rather than extrapolating.
        lookup[..(int)pointValues[0]].Fill((int)pointScalings[0]);
        for (int point = 0; point < pointCount - 1; point++)
        {
            int deltaY = (int)pointScalings[point + 1] - (int)pointScalings[point];
            int deltaX = (int)pointValues[point + 1] - (int)pointValues[point];

            // A rounded Q16 reciprocal performs the piecewise-linear interpolation using integer arithmetic. The
            // 32768 bias below rounds each reconstructed scaling value when it returns to integer precision.
            long delta = deltaY * ((65536 + (deltaX >> 1)) / deltaX);
            for (int x = 0; x < deltaX; x++)
            {
                lookup[(int)pointValues[point] + x] = (int)pointScalings[point] +
                    (int)(((x * delta) + 32768) >> 16);
            }
        }

        lookup[(int)pointValues[pointCount - 1]..].Fill((int)pointScalings[pointCount - 1]);
    }

    /// <summary>
    /// Copies a rectangular grain region while preserving independent source and destination strides.
    /// </summary>
    /// <param name="source">The source region.</param>
    /// <param name="sourceStride">The source row stride.</param>
    /// <param name="destination">The destination region.</param>
    /// <param name="destinationStride">The destination row stride.</param>
    /// <param name="width">The number of values copied per row.</param>
    /// <param name="height">The number of rows copied.</param>
    private static void CopyArea(
        ReadOnlySpan<int> source,
        int sourceStride,
        Span<int> destination,
        int destinationStride,
        int width,
        int height)
    {
        for (int row = 0; row < height; row++)
        {
            source.Slice(row * sourceStride, width)
                .CopyTo(destination.Slice(row * destinationStride, width));
        }
    }

    /// <summary>
    /// Initializes the film-grain linear-feedback shift register for one luma block row.
    /// </summary>
    /// <param name="randomRegister">The register to initialize.</param>
    /// <param name="lumaLine">The zero-based luma row represented by the block row.</param>
    /// <param name="seed">The frame grain seed.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void InitializeRandomGenerator(ref ushort randomRegister, int lumaLine, ushort seed)
    {
        randomRegister = seed;
        int lumaBlock = lumaLine >> 5;

        // The two affine mixes inject the block-row identity into both bytes of the 16-bit LFSR state.
        randomRegister ^= (ushort)((((lumaBlock * 37) + 178) & 255) << 8);
        randomRegister ^= (ushort)(((lumaBlock * 173) + 105) & 255);
    }

    /// <summary>
    /// Advances the film-grain linear-feedback shift register and returns its high-order bits.
    /// </summary>
    /// <param name="randomRegister">The register to advance.</param>
    /// <param name="bitCount">The number of result bits.</param>
    /// <returns>A value in the range zero through <c>2^bitCount - 1</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetRandomNumber(ref ushort randomRegister, int bitCount)
    {
        // AV1 uses taps 0, 1, 3, and 12 of the current register. Shifting right exposes the requested high bits
        // after feedback has entered bit 15, matching both Gaussian indexing and block-offset selection.
        int feedback = (randomRegister ^ (randomRegister >> 1) ^ (randomRegister >> 3) ^
            (randomRegister >> 12)) & 1;

        randomRegister = (ushort)((randomRegister >> 1) | (feedback << 15));
        return (randomRegister >> (16 - bitCount)) & ((1 << bitCount) - 1);
    }
}
