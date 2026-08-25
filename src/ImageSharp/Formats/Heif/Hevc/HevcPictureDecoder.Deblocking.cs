// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Hevc;

/// <content>
/// Implements picture-level HEVC deblocking traversal and threshold derivation.
/// </content>
internal sealed partial class HevcPictureDecoder
{
    /// <summary>
    /// Defines the orientation-dependent boundary lookup and four-sample filter dispatch.
    /// </summary>
    private interface IDeblockingDirection
    {
        /// <summary>
        /// Gets a value indicating whether the boundary is vertical.
        /// </summary>
        public static abstract bool IsVertical { get; }

        /// <summary>
        /// Gets whether the selected four-sample segment is a transform or prediction boundary.
        /// </summary>
        /// <param name="state">The decoded deblocking boundary state.</param>
        /// <param name="plane">The primary coding plane.</param>
        /// <param name="x">The segment left luma coordinate.</param>
        /// <param name="y">The segment top luma coordinate.</param>
        /// <returns><see langword="true"/> when the segment is a filter candidate; otherwise, <see langword="false"/>.</returns>
        public static abstract bool IsBoundary(HevcDeblockingState state, HevcPlane plane, int x, int y);

        /// <summary>
        /// Applies the orientation-specific luma kernel.
        /// </summary>
        /// <param name="picture">The reconstructed picture.</param>
        /// <param name="plane">The component plane.</param>
        /// <param name="x">The first Q-side sample X coordinate.</param>
        /// <param name="y">The first Q-side sample Y coordinate.</param>
        /// <param name="beta">The scaled discontinuity threshold.</param>
        /// <param name="tc">The scaled clipping threshold.</param>
        /// <param name="partPNoFilter">Whether the P-side block retains its original samples.</param>
        /// <param name="partQNoFilter">Whether the Q-side block retains its original samples.</param>
        /// <param name="bitDepth">The component sample precision.</param>
        public static abstract void FilterLuma(
            HevcPictureBuffer picture,
            HevcPlane plane,
            int x,
            int y,
            int beta,
            int tc,
            bool partPNoFilter,
            bool partQNoFilter,
            int bitDepth);

        /// <summary>
        /// Applies the orientation-specific chroma kernel.
        /// </summary>
        /// <param name="picture">The reconstructed picture.</param>
        /// <param name="plane">The Cb or Cr component plane.</param>
        /// <param name="x">The first Q-side sample X coordinate.</param>
        /// <param name="y">The first Q-side sample Y coordinate.</param>
        /// <param name="tc">The scaled clipping threshold.</param>
        /// <param name="partPNoFilter">Whether the P-side block retains its original samples.</param>
        /// <param name="partQNoFilter">Whether the Q-side block retains its original samples.</param>
        /// <param name="bitDepth">The component sample precision.</param>
        /// <param name="count">The number of samples in the edge segment.</param>
        public static abstract void FilterChroma(
            HevcPictureBuffer picture,
            HevcPlane plane,
            int x,
            int y,
            int tc,
            bool partPNoFilter,
            bool partQNoFilter,
            int bitDepth,
            int count);
    }

    /// <summary>
    /// Gets the H.265 Table 8-20 clipping thresholds indexed by the effective boundary quantization parameter.
    /// </summary>
    private static ReadOnlySpan<byte> DeblockingTcTable =>
    [
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 3, 3, 3, 3, 4,
        4, 4, 5, 5, 6, 6, 7, 8, 9, 10, 11, 13, 14, 16, 18, 20, 22, 24,
    ];

    /// <summary>
    /// Gets the H.265 Table 8-20 discontinuity thresholds indexed by the effective boundary quantization parameter.
    /// </summary>
    private static ReadOnlySpan<byte> DeblockingBetaTable =>
    [
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 20, 22, 24, 26,
        28, 30, 32, 34, 36, 38, 40, 42, 44, 46, 48, 50, 52, 54, 56, 58, 60, 62, 64,
    ];

    /// <summary>
    /// Applies vertical edges across the complete picture before applying any horizontal edge.
    /// </summary>
    /// <param name="tileLayout">The picture tile mapping.</param>
    private void ApplyDeblockingFilter(in HevcTileLayout tileLayout)
    {
        this.ApplyDeblockingDirection<VerticalDeblockingDirection>(in tileLayout);
        this.ApplyDeblockingDirection<HorizontalDeblockingDirection>(in tileLayout);
    }

    /// <summary>
    /// Applies one closed deblocking direction to every coded component plane.
    /// </summary>
    /// <typeparam name="TDirection">The vertical or horizontal boundary operator.</typeparam>
    /// <param name="tileLayout">The picture tile mapping.</param>
    private void ApplyDeblockingDirection<TDirection>(in HevcTileLayout tileLayout)
        where TDirection : struct, IDeblockingDirection
    {
        if (this.sequenceParameterSet.SeparateColorPlaneFlag)
        {
            for (int planeIndex = 0; planeIndex < 3; planeIndex++)
            {
                this.ApplyLumaDeblocking<TDirection>((HevcPlane)planeIndex, planeIndex, in tileLayout);
            }

            return;
        }

        this.ApplyLumaDeblocking<TDirection>(HevcPlane.Y, 0, in tileLayout);
        if (this.sequenceParameterSet.ChromaFormat != 0)
        {
            this.ApplyChromaDeblocking<TDirection>(HevcPlane.Cb, in tileLayout);
            this.ApplyChromaDeblocking<TDirection>(HevcPlane.Cr, in tileLayout);
        }
    }

    /// <summary>
    /// Applies one deblocking direction with the luma kernel to a primary coded plane.
    /// </summary>
    /// <typeparam name="TDirection">The vertical or horizontal boundary operator.</typeparam>
    /// <param name="plane">The primary coded plane.</param>
    /// <param name="codingTreeStateIndex">The coding-tree state selected for the plane.</param>
    /// <param name="tileLayout">The picture tile mapping.</param>
    private void ApplyLumaDeblocking<TDirection>(HevcPlane plane, int codingTreeStateIndex, in HevcTileLayout tileLayout)
        where TDirection : struct, IDeblockingDirection
    {
        int width = this.Picture.GetWidth(plane);
        int height = this.Picture.GetHeight(plane);
        int acrossLimit = TDirection.IsVertical ? width : height;
        int alongLimit = TDirection.IsVertical ? height : width;
        int bitDepth = this.Picture.GetBitDepth(plane);
        int bitDepthScale = 1 << (bitDepth - 8);
        int codingTreeBlockSize = 1 << this.sequenceParameterSet.CodingTreeBlockLog2;
        HevcCodingTreeState codingTreeState = this.codingTreeStates[codingTreeStateIndex];

        // Deblocking visits only eight-sample grid lines, but each candidate is retained at four-sample resolution
        // because transform and prediction boundaries can differ between the two halves of that grid interval.
        for (int edge = 8; edge < acrossLimit; edge += 8)
        {
            for (int along = 0; along < alongLimit; along += 4)
            {
                int x = TDirection.IsVertical ? edge : along;
                int y = TDirection.IsVertical ? along : edge;
                if (!TDirection.IsBoundary(this.deblockingState, plane, x, y))
                {
                    continue;
                }

                int rasterAddress = ((y / codingTreeBlockSize) * tileLayout.Width) + (x / codingTreeBlockSize);
                HevcLoopFilterRegion region = this.sampleAdaptiveOffsetState.GetLoopFilterRegion(rasterAddress, plane);
                if (region.DeblockingFilterDisabled
                    || !this.IsDeblockingCtbBoundaryAvailable<TDirection>(rasterAddress, plane, x, y, codingTreeBlockSize, in tileLayout))
                {
                    continue;
                }

                int pX = x - (TDirection.IsVertical ? 1 : 0);
                int pY = y - (TDirection.IsVertical ? 0 : 1);
                int qX = x;
                int qY = y;
                int quantizationParameterP = codingTreeState.GetQuantizationParameter(pX, pY);
                int quantizationParameterQ = codingTreeState.GetQuantizationParameter(qX, qY);
                int averageQuantizationParameter = (quantizationParameterP + quantizationParameterQ + 1) >> 1;
                int tcIndex = Math.Clamp(averageQuantizationParameter + 2 + (region.DeblockingFilterTcOffsetDiv2 << 1), 0, 53);
                int betaIndex = Math.Clamp(averageQuantizationParameter + (region.DeblockingFilterBetaOffsetDiv2 << 1), 0, 51);
                int tc = DeblockingTcTable[tcIndex] * bitDepthScale;
                int beta = DeblockingBetaTable[betaIndex] * bitDepthScale;
                bool partPNoFilter = this.IsDeblockingSuppressed(codingTreeState, pX, pY);
                bool partQNoFilter = this.IsDeblockingSuppressed(codingTreeState, qX, qY);

                TDirection.FilterLuma(this.Picture, plane, x, y, beta, tc, partPNoFilter, partQNoFilter, bitDepth);
            }
        }
    }

    /// <summary>
    /// Applies one deblocking direction with the chroma kernel to a combined Cb or Cr plane.
    /// </summary>
    /// <typeparam name="TDirection">The vertical or horizontal boundary operator.</typeparam>
    /// <param name="plane">The Cb or Cr component plane.</param>
    /// <param name="tileLayout">The picture tile mapping.</param>
    private void ApplyChromaDeblocking<TDirection>(HevcPlane plane, in HevcTileLayout tileLayout)
        where TDirection : struct, IDeblockingDirection
    {
        int subsamplingX = this.Picture.GetSubsamplingX(plane);
        int subsamplingY = this.Picture.GetSubsamplingY(plane);
        int width = this.Picture.GetWidth(plane);
        int height = this.Picture.GetHeight(plane);
        int acrossLimit = TDirection.IsVertical ? width : height;
        int alongLimit = TDirection.IsVertical ? height : width;
        int alongSubsampling = TDirection.IsVertical ? subsamplingY : subsamplingX;
        int segmentLength = 4 >> alongSubsampling;
        int bitDepth = this.Picture.GetBitDepth(plane);
        int bitDepthScale = 1 << (bitDepth - 8);
        int codingTreeBlockSize = 1 << this.sequenceParameterSet.CodingTreeBlockLog2;
        HevcCodingTreeState codingTreeState = this.codingTreeStates[0];

        // Chroma deblocking uses eight-sample component-grid edges. A two-lane segment in subsampled directions still
        // enters the SIMD kernel, but only its valid low lanes are committed because QP and suppression state can change next.
        for (int edge = 8; edge < acrossLimit; edge += 8)
        {
            for (int along = 0; along < alongLimit; along += segmentLength)
            {
                int x = TDirection.IsVertical ? edge : along;
                int y = TDirection.IsVertical ? along : edge;
                int lumaX = x << subsamplingX;
                int lumaY = y << subsamplingY;
                if (!TDirection.IsBoundary(this.deblockingState, HevcPlane.Y, lumaX, lumaY))
                {
                    continue;
                }

                int rasterAddress = ((lumaY / codingTreeBlockSize) * tileLayout.Width) + (lumaX / codingTreeBlockSize);
                HevcLoopFilterRegion region = this.sampleAdaptiveOffsetState.GetLoopFilterRegion(rasterAddress, HevcPlane.Y);
                if (region.DeblockingFilterDisabled
                    || !this.IsDeblockingCtbBoundaryAvailable<TDirection>(
                        rasterAddress,
                        HevcPlane.Y,
                        lumaX,
                        lumaY,
                        codingTreeBlockSize,
                        in tileLayout))
                {
                    continue;
                }

                int pX = lumaX - (TDirection.IsVertical ? 1 : 0);
                int pY = lumaY - (TDirection.IsVertical ? 0 : 1);
                int qX = lumaX;
                int qY = lumaY;
                int quantizationParameterP = codingTreeState.GetQuantizationParameter(pX, pY);
                int quantizationParameterQ = codingTreeState.GetQuantizationParameter(qX, qY);
                int averageQuantizationParameter = (quantizationParameterP + quantizationParameterQ + 1) >> 1;
                int componentOffset = codingTreeState.GetChromaQuantizationOffset(plane, qX, qY);
                int chromaQuantizationParameter = HevcQuantizationParameters.GetChromaQuantizationParameter(
                    averageQuantizationParameter,
                    componentOffset,
                    0,
                    this.sequenceParameterSet.ChromaFormat);

                int tcIndex = Math.Clamp(chromaQuantizationParameter + 2 + (region.DeblockingFilterTcOffsetDiv2 << 1), 0, 53);
                int tc = DeblockingTcTable[tcIndex] * bitDepthScale;
                bool partPNoFilter = this.IsDeblockingSuppressed(codingTreeState, pX, pY);
                bool partQNoFilter = this.IsDeblockingSuppressed(codingTreeState, qX, qY);

                TDirection.FilterChroma(this.Picture, plane, x, y, tc, partPNoFilter, partQNoFilter, bitDepth, segmentLength);
            }
        }
    }

    /// <summary>
    /// Gets whether an edge crossing a coding-tree-block boundary is permitted by slice and tile rules.
    /// </summary>
    /// <typeparam name="TDirection">The vertical or horizontal boundary operator.</typeparam>
    /// <param name="rasterAddress">The Q-side coding-tree-block raster address.</param>
    /// <param name="plane">The primary coding plane.</param>
    /// <param name="x">The edge luma X coordinate.</param>
    /// <param name="y">The edge luma Y coordinate.</param>
    /// <param name="codingTreeBlockSize">The coding-tree-block side in luma samples.</param>
    /// <param name="tileLayout">The picture tile mapping.</param>
    /// <returns><see langword="true"/> for an internal or permitted external boundary; otherwise, <see langword="false"/>.</returns>
    private bool IsDeblockingCtbBoundaryAvailable<TDirection>(
        int rasterAddress,
        HevcPlane plane,
        int x,
        int y,
        int codingTreeBlockSize,
        in HevcTileLayout tileLayout)
        where TDirection : struct, IDeblockingDirection
    {
        int acrossCoordinate = TDirection.IsVertical ? x : y;
        if (acrossCoordinate % codingTreeBlockSize != 0)
        {
            return true;
        }

        HevcLoopFilterBoundaryAvailability availability = this.sampleAdaptiveOffsetState.GetLoopFilterBoundaryAvailability(
            rasterAddress,
            plane,
            tileLayout.Width,
            tileLayout.Height,
            this.pictureParameterSet.LoopFilterAcrossTilesEnabled);

        return TDirection.IsVertical ? availability.Left : availability.Above;
    }

    /// <summary>
    /// Gets whether PCM or transform-bypass syntax preserves one side of a filtered boundary.
    /// </summary>
    /// <param name="state">The coding-tree state for the selected primary plane.</param>
    /// <param name="x">The luma sample X coordinate.</param>
    /// <param name="y">The luma sample Y coordinate.</param>
    /// <returns><see langword="true"/> when the reconstructed side must not be modified; otherwise, <see langword="false"/>.</returns>
    private bool IsDeblockingSuppressed(HevcCodingTreeState state, int x, int y)
        => (this.sequenceParameterSet.PcmLoopFilterDisabled && state.IsPcm(x, y))
            || (this.pictureParameterSet.TransquantizationBypassEnabled && state.IsTransquantBypass(x, y));

    /// <summary>
    /// Selects vertical boundary lookup and filtering.
    /// </summary>
    private readonly struct VerticalDeblockingDirection : IDeblockingDirection
    {
        /// <inheritdoc/>
        public static bool IsVertical => true;

        /// <inheritdoc/>
        public static bool IsBoundary(HevcDeblockingState state, HevcPlane plane, int x, int y)
            => state.IsVerticalBoundary(plane, x, y);

        /// <inheritdoc/>
        public static void FilterLuma(
            HevcPictureBuffer picture,
            HevcPlane plane,
            int x,
            int y,
            int beta,
            int tc,
            bool partPNoFilter,
            bool partQNoFilter,
            int bitDepth)
            => HevcDeblockingFilter.FilterVerticalLuma(picture, plane, x, y, beta, tc, partPNoFilter, partQNoFilter, bitDepth);

        /// <inheritdoc/>
        public static void FilterChroma(
            HevcPictureBuffer picture,
            HevcPlane plane,
            int x,
            int y,
            int tc,
            bool partPNoFilter,
            bool partQNoFilter,
            int bitDepth,
            int count)
            => HevcDeblockingFilter.FilterVerticalChroma(picture, plane, x, y, tc, partPNoFilter, partQNoFilter, bitDepth, count);
    }

    /// <summary>
    /// Selects horizontal boundary lookup and filtering.
    /// </summary>
    private readonly struct HorizontalDeblockingDirection : IDeblockingDirection
    {
        /// <inheritdoc/>
        public static bool IsVertical => false;

        /// <inheritdoc/>
        public static bool IsBoundary(HevcDeblockingState state, HevcPlane plane, int x, int y)
            => state.IsHorizontalBoundary(plane, x, y);

        /// <inheritdoc/>
        public static void FilterLuma(
            HevcPictureBuffer picture,
            HevcPlane plane,
            int x,
            int y,
            int beta,
            int tc,
            bool partPNoFilter,
            bool partQNoFilter,
            int bitDepth)
            => HevcDeblockingFilter.FilterHorizontalLuma(picture, plane, x, y, beta, tc, partPNoFilter, partQNoFilter, bitDepth);

        /// <inheritdoc/>
        public static void FilterChroma(
            HevcPictureBuffer picture,
            HevcPlane plane,
            int x,
            int y,
            int tc,
            bool partPNoFilter,
            bool partQNoFilter,
            int bitDepth,
            int count)
            => HevcDeblockingFilter.FilterHorizontalChroma(picture, plane, x, y, tc, partPNoFilter, partQNoFilter, bitDepth, count);
    }
}
