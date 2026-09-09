// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.LoopRestoration;

/// <content>
/// Visits restoration units in tile and superblock coding order.
/// </content>
internal static partial class Av1LoopRestorationEncoder
{
    /// <summary>
    /// Searches one plane at a fixed unit size and chooses its frame restoration mode.
    /// </summary>
    /// <typeparam name="TSample">The physical component sample type.</typeparam>
    /// <param name="picture">The frame geometry and tile layout.</param>
    /// <param name="context">The active plane and reusable trial workspace.</param>
    /// <param name="settings">The frame restoration search policy.</param>
    /// <param name="unitSize">The candidate restoration unit size.</param>
    /// <param name="writer">The retained costs and shared coefficient syntax calculator.</param>
    /// <param name="tile">The existing tile cursor.</param>
    /// <param name="rateMultiplier">The frame rate weight.</param>
    /// <param name="varianceThreshold">The minimum source variance for Wiener fitting.</param>
    /// <param name="results">The unit candidates retained for this plane.</param>
    /// <param name="correlation">The source-correlation workspace.</param>
    /// <param name="covariance">The covariance workspace.</param>
    /// <param name="filtered0">The radius-two projection workspace.</param>
    /// <param name="filtered1">The radius-one projection workspace.</param>
    /// <param name="bits">The selected frame mode's total probability cost.</param>
    /// <param name="error">The selected frame mode's total squared error.</param>
    /// <returns>The selected mode in none, Wiener, self-guided, switchable order.</returns>
    private static int SearchPlane<TSample>(
        Av1PictureControlSet picture,
        in UnitSearchContext<TSample> context,
        SearchSettings settings,
        int unitSize,
        Av1SymbolEncoder writer,
        Av1TileInfo tile,
        int rateMultiplier,
        long varianceThreshold,
        Span<UnitSearchResult> results,
        Span<long> correlation,
        Span<long> covariance,
        Span<int> filtered0,
        Span<int> filtered1,
        out long bits,
        out long error)
        where TSample : unmanaged
    {
        ObuFrameHeader header = picture.Parent.FrameHeader;
        ObuSequenceHeader sequence = picture.Sequence.SequenceHeader;
        ObuTileGroupHeader layout = header.TilesInfo;
        int columns = Math.Max(1, (context.Source.Width + (unitSize >> 1)) / unitSize);
        int rows = Math.Max(1, (context.Source.Height + (unitSize >> 1)) / unitSize);
        bool allowSwitchable = columns * rows > 1 && settings.EnableWiener && settings.EnableSelfGuided;
        int superblockStep = sequence.SuperblockModeInfoSize;
        int pixelShiftX = Av1Constants.ModeInfoSizeLog2 - context.SubsamplingX;
        int pixelShiftY = Av1Constants.ModeInfoSizeLog2 - context.SubsamplingY;
        bool superResolution = header.FrameSize.FrameWidth != header.FrameSize.SuperResolutionUpscaledWidth;
        int horizontalScale = superResolution ? header.FrameSize.SuperResolutionDenominator : 1;
        int horizontalDivisor = unitSize * (superResolution ? Av1Constants.ScaleNumerator : 1);
        InlineArray4<long> totalBits = default;
        InlineArray4<long> totalErrors = default;
        Av1ModeCosts costs = writer.ModeCosts;

        for (int tileRow = 0; tileRow < layout.TileRowCount; tileRow++)
        {
            tile.SetTileRow(layout, header.ModeInfoRowCount, tileRow);
            for (int tileColumn = 0; tileColumn < layout.TileColumnCount; tileColumn++)
            {
                tile.SetTileColumn(layout, header.ModeInfoColumnCount, tileColumn);
                Av1LoopRestorationUnit fixedReference = Av1LoopRestorationUnit.CreateDefault();
                Av1LoopRestorationUnit switchableReference = Av1LoopRestorationUnit.CreateDefault();
                for (int miRow = tile.ModeInfoRowStart; miRow < tile.ModeInfoRowEnd; miRow += superblockStep)
                {
                    int firstRow = ((miRow << pixelShiftY) + unitSize - 1) / unitSize;
                    int lastRow = Math.Min(rows, (((miRow + superblockStep) << pixelShiftY) + unitSize - 1) / unitSize);
                    for (int miColumn = tile.ModeInfoColumnStart; miColumn < tile.ModeInfoColumnEnd; miColumn += superblockStep)
                    {
                        // A unit belongs to the superblock containing its coded upper-left corner.
                        // Horizontal super-resolution changes that ownership scale, while the
                        // vertical stripe offset changes the filtered rectangle only.
                        int firstColumn = (((miColumn << pixelShiftX) * horizontalScale) + horizontalDivisor - 1) / horizontalDivisor;
                        int lastColumn = Math.Min(
                            columns,
                            ((((miColumn + superblockStep) << pixelShiftX) * horizontalScale) + horizontalDivisor - 1) / horizontalDivisor);

                        for (int row = firstRow; row < lastRow; row++)
                        {
                            for (int column = firstColumn; column < lastColumn; column++)
                            {
                                Rectangle bounds = GetUnitBounds(
                                    column, row, unitSize, new Size(context.Source.Width, context.Source.Height), context.SubsamplingY);

                                results[(row * columns) + column] = SearchUnit(
                                    in context,
                                    bounds,
                                    settings,
                                    writer,
                                    costs,
                                    rateMultiplier,
                                    varianceThreshold,
                                    allowSwitchable,
                                    correlation,
                                    covariance,
                                    filtered0,
                                    filtered1,
                                    ref fixedReference,
                                    ref switchableReference,
                                    totalBits,
                                    totalErrors);
                            }
                        }
                    }
                }
            }
        }

        int bestMode = 0;
        double bestCost = GetRestorationCost(rateMultiplier, totalBits[0], totalErrors[0], context.BitDepth);
        for (int mode = 1; mode < 4; mode++)
        {
            bool enabled = mode == 1 ? settings.EnableWiener : mode == 2 ? settings.EnableSelfGuided : allowSwitchable;
            if (!enabled)
            {
                continue;
            }

            double cost = GetRestorationCost(rateMultiplier, totalBits[mode], totalErrors[mode], context.BitDepth);
            if (cost < bestCost)
            {
                bestCost = cost;
                bestMode = mode;
            }
        }

        bits = totalBits[bestMode];
        error = totalErrors[bestMode];
        return bestMode;
    }
}
