// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Quantizers;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.SuperResolution;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.LoopRestoration;

/// <content>
/// Searches restoration units in coding order and applies the selected frame parameters.
/// </content>
internal static partial class Av1LoopRestorationEncoder
{
    /// <summary>
    /// Preserves the deblocked internal stripe rows before CDEF changes the reconstruction.
    /// </summary>
    /// <typeparam name="TSample">The physical component sample type.</typeparam>
    /// <param name="picture">The active frame geometry.</param>
    /// <param name="reconstruction">The deblocked component planes.</param>
    /// <param name="boundary">The worker's retained stripe storage.</param>
    public static void SaveBoundaryRows<TSample>(
        Av1PictureControlSet picture,
        Av1EncoderFrame<TSample> reconstruction,
        Av1LoopRestorationBoundary boundary)
        where TSample : unmanaged
    {
        ObuSequenceHeader sequence = picture.Sequence.SequenceHeader;
        ObuFrameHeader header = picture.Parent.FrameHeader;
        int planeCount = sequence.ColorConfig.PlaneCount;
        boundary.PrepareFrame(sequence, header, Unsafe.SizeOf<TSample>(), (1 << planeCount) - 1);
        for (int plane = 0; plane < planeCount; plane++)
        {
            Buffer2DRegion<TSample> region = reconstruction.View.GetPlane((Av1Plane)plane);
            int subX = plane == 0 ? 0 : reconstruction.ChromaSubsamplingX;
            int subY = plane == 0 ? 0 : reconstruction.ChromaSubsamplingY;
            int border = header.FrameSize.FrameWidth != header.FrameSize.SuperResolutionUpscaledWidth
                ? Av1SuperResolutionFilter.SourceBorder
                : 0;

            int origin = (region.Bounds.Y * region.Stride) + region.Bounds.X - border;
            Span<TSample> samples = region.Buffer.DangerousGetSingleSpan()[origin..];
            Span<byte> low = default;
            Span<ushort> high = default;
            if (typeof(TSample) == typeof(byte))
            {
                low = MemoryMarshal.Cast<TSample, byte>(samples);
            }
            else
            {
                high = MemoryMarshal.Cast<TSample, ushort>(samples);
            }

            boundary.SaveDeblockedPlane(header, plane, subX, subY, reconstruction.LumaBitDepth, low, high, region.Stride, border);
        }
    }

    /// <summary>
    /// Selects restoration parameters and replaces the reconstruction with the selected filtered samples.
    /// </summary>
    /// <typeparam name="TSample">The physical component sample type.</typeparam>
    /// <param name="workspace">The worker retaining restoration storage.</param>
    /// <param name="picture">The retained frame coding state.</param>
    /// <param name="source">The original component samples.</param>
    /// <param name="reconstruction">The samples after CDEF.</param>
    /// <param name="boundary">The preserved deblocked stripe rows.</param>
    /// <param name="writer">The frame entropy costs and coefficient syntax calculator.</param>
    /// <param name="tile">The existing tile cursor used to visit units in coding order.</param>
    public static void ApplyFrame<TSample>(
        Av1EncoderBlockWorkspace workspace,
        Av1PictureControlSet picture,
        Av1EncoderFrame<TSample> source,
        Av1EncoderFrame<TSample> reconstruction,
        Av1LoopRestorationBoundary boundary,
        Av1SymbolEncoder writer,
        Av1TileInfo tile)
        where TSample : unmanaged
    {
        ObuSequenceHeader sequence = picture.Sequence.SequenceHeader;
        ObuFrameHeader header = picture.Parent.FrameHeader;
        int planeCount = sequence.ColorConfig.PlaneCount;
        int bitDepth = source.LumaBitDepth;
        int qIndex = header.QuantizationParameters.BaseQIndex;
        SearchSettings settings = new(
            picture.Parent.EncodingSpeed,
            sequence.IsStillPicture,
            new Size(source.Width, source.Height),
            qIndex,
            picture.Parent.SpeedSettings.IsBoosted,
            picture.Parent.FrameUpdateType == Av1FrameUpdateType.IntermediateAlternate,
            header.AllowScreenContentTools,
            1 << sequence.SuperblockSizeLog2);

        for (int plane = 0; plane < planeCount; plane++)
        {
            header.LoopRestorationParameters.Items[plane].Type = ObuRestorationType.None;
            Buffer2DRegion<TSample> region = reconstruction.View.GetPlane((Av1Plane)plane);
            int origin = (region.Bounds.Y * region.Stride) + region.Bounds.X;
            ReadOnlySpan<TSample> samples = region.Buffer.DangerousGetSingleSpan()[origin..];
            ReadOnlySpan<byte> low = default;
            ReadOnlySpan<ushort> high = default;
            if (typeof(TSample) == typeof(byte))
            {
                low = MemoryMarshal.Cast<TSample, byte>(samples);
            }
            else
            {
                high = MemoryMarshal.Cast<TSample, ushort>(samples);
            }

            boundary.SaveFrameEdgePlane(plane, region.Height, low, high, region.Stride);
        }

        // Fitting reads post-CDEF samples outside the visible unit. Boundary substitution is only
        // needed for actual trial filtering, where the shared stripe kernel restores every replaced row.
        reconstruction.ExtendBorders();
        Av1ColorFormat colorFormat = sequence.ColorConfig.GetColorFormat();

        Av1EncoderFrame<TSample> trialFrame = workspace.GetRestorationTrial(source, colorFormat);
        MemoryAllocator allocator = workspace.MemoryAllocator;
        InlineArray3<int> searchOffsets = default;
        int searchCapacity = 0;
        for (int plane = 0; plane < planeCount; plane++)
        {
            Buffer2DRegion<TSample> region = source.View.GetPlane((Av1Plane)plane);
            searchOffsets[plane] = searchCapacity;
            searchCapacity += Math.Max(1, (region.Width + (settings.MinimumUnitSize >> 1)) / settings.MinimumUnitSize) *
                Math.Max(1, (region.Height + (settings.MinimumUnitSize >> 1)) / settings.MinimumUnitSize);
        }

        using IMemoryOwner<UnitSearchResult> searchOwner = allocator.Allocate<UnitSearchResult>(searchCapacity);
        using IMemoryOwner<long> statisticsOwner = allocator.Allocate<long>(49 + (49 * 49));
        Span<long> statistics = statisticsOwner.Memory.Span;
        Span<long> correlation = statistics[..49];
        Span<long> covariance = statistics.Slice(49, 49 * 49);

        // A short final unit merges into its predecessor. The vertical stripe offset can add
        // eight rows to the last unit; size the two retained projections for that exact maximum.
        int maximumUnitWidth = Math.Min(source.Width, ((settings.MaximumUnitSize * 3) / 2) - 1);
        int maximumUnitHeight = Math.Min(source.Height, ((settings.MaximumUnitSize * 3) / 2) + 7);
        int projectionLength = maximumUnitWidth * maximumUnitHeight;
        int selfGuidedLength = Av1SelfGuidedFilter.GetScratchLength(64, 64);
        int wienerLength = Av1WienerFilter.GetScratchLength(64, 64);
        using IMemoryOwner<int> filterOwner = allocator.Allocate<int>(
            (2 * projectionLength) + selfGuidedLength + ((wienerLength + 1) / 2));

        Span<int> filterStorage = filterOwner.Memory.Span;
        Span<int> filtered0 = filterStorage[..projectionLength];
        Span<int> filtered1 = filterStorage.Slice(projectionLength, projectionLength);
        Span<int> selfGuidedScratch = filterStorage.Slice(2 * projectionLength, selfGuidedLength);
        Span<ushort> wienerScratch = MemoryMarshal.Cast<int, ushort>(filterStorage[((2 * projectionLength) + selfGuidedLength)..])[..wienerLength];
        Span<TSample> savedRows = MemoryMarshal.Cast<ushort, TSample>(boundary.GetStripeSaveBuffer());
        int rateQIndex = qIndex + header.QuantizationParameters.DeltaQDc[0];

        // The restoration search reads the frame multiplier. Reference: x->rdmult = cpi->rd.RDMULT in
        // av1_pick_filter_restoration().
        int rateMultiplier = picture.Parent.GetRateMultiplier(rateQIndex, sequence.ColorConfig.BitDepth);

        int quantizer = Av1QuantizationLookup.GetDcQuant(qIndex, 0, sequence.ColorConfig.BitDepth) >> 3;
        long varianceThreshold = ((long)quantizer * quantizer * settings.WienerVariancePruning) >> 4;
        int searchedPlanes = settings.EnableChroma ? planeCount : 1;
        int bestUnitSize = settings.MaximumUnitSize;
        double bestCost = double.MaxValue;
        InlineArray3<int> selectedModes = default;

        for (int unitSize = settings.MaximumUnitSize; unitSize >= settings.MinimumUnitSize; unitSize >>= 1)
        {
            long totalBits = 0;
            long totalError = 0;
            for (int plane = 0; plane < searchedPlanes; plane++)
            {
                int subX = plane == 0 ? 0 : source.ChromaSubsamplingX;
                int subY = plane == 0 ? 0 : source.ChromaSubsamplingY;
                UnitSearchContext<TSample> context = new(
                    source.View.GetPlane((Av1Plane)plane),
                    reconstruction.View.GetPlane((Av1Plane)plane),
                    trialFrame.View.GetPlane((Av1Plane)plane),
                    boundary,
                    plane,
                    bitDepth,
                    subX,
                    subY,
                    savedRows,
                    wienerScratch,
                    selfGuidedScratch);

                selectedModes[plane] = SearchPlane(
                    picture,
                    in context,
                    settings,
                    unitSize,
                    writer,
                    tile,
                    rateMultiplier,
                    varianceThreshold,
                    searchOwner.Memory.Span[searchOffsets[plane]..],
                    correlation,
                    covariance,
                    filtered0,
                    filtered1,
                    out long planeBits,
                    out long planeError);

                totalBits += planeBits;
                totalError += planeError;
            }

            double cost = GetRestorationCost(rateMultiplier, totalBits, totalError, bitDepth);
            if (cost >= bestCost)
            {
                break;
            }

            bestCost = cost;
            bestUnitSize = unitSize;
            bool allNone = true;
            for (int plane = 0; plane < searchedPlanes; plane++)
            {
                int mode = selectedModes[plane];
                header.LoopRestorationParameters.Items[plane].Type = mode switch
                {
                    1 => ObuRestorationType.Wiener,
                    2 => ObuRestorationType.SgrProj,
                    3 => ObuRestorationType.Switchable,
                    _ => ObuRestorationType.None
                };

                if (mode == 0)
                {
                    continue;
                }

                allNone = false;
                Buffer2DRegion<TSample> region = source.View.GetPlane((Av1Plane)plane);
                int count = Math.Max(1, (region.Width + (unitSize >> 1)) / unitSize) *
                    Math.Max(1, (region.Height + (unitSize >> 1)) / unitSize);

                ReadOnlySpan<UnitSearchResult> results = searchOwner.Memory.Span.Slice(searchOffsets[plane], count);
                Span<Av1LoopRestorationUnit> retained = picture.RestorationUnits[plane].Span;
                for (int index = 0; index < count; index++)
                {
                    retained[index] = results[index].Parameters;
                    retained[index].FilterType = results[index].Choices[mode - 1];
                }
            }

            // If no plane benefits at this size, smaller units are not searched. Likewise, the
            // first worse size ends the search. Preserve the winning parameters before reusing results.
            if (allNone)
            {
                break;
            }
        }

        header.LoopRestorationParameters.UsesLoopRestoration = false;
        header.LoopRestorationParameters.UsesChromaLoopRestoration = false;
        header.LoopRestorationParameters.UnitShift = Av1Math.MostSignificantBit((uint)bestUnitSize) - 6;
        header.LoopRestorationParameters.UVShift = 0;
        for (int plane = 0; plane < planeCount; plane++)
        {
            ref ObuLoopRestorationItem item = ref header.LoopRestorationParameters.Items[plane];
            item.Size = bestUnitSize;
            if (item.Type == ObuRestorationType.None)
            {
                continue;
            }

            header.LoopRestorationParameters.UsesLoopRestoration = true;
            header.LoopRestorationParameters.UsesChromaLoopRestoration |= plane != 0;
            Buffer2DRegion<TSample> region = reconstruction.View.GetPlane((Av1Plane)plane);
            int subX = plane == 0 ? 0 : source.ChromaSubsamplingX;
            int subY = plane == 0 ? 0 : source.ChromaSubsamplingY;
            Buffer2DRegion<TSample> trial = trialFrame.View.GetPlane((Av1Plane)plane);
            UnitSearchContext<TSample> context = new(
                source.View.GetPlane((Av1Plane)plane),
                region,
                trial,
                boundary,
                plane,
                bitDepth,
                subX,
                subY,
                savedRows,
                wienerScratch,
                selfGuidedScratch);

            int columns = Math.Max(1, (region.Width + (bestUnitSize >> 1)) / bestUnitSize);
            int rows = Math.Max(1, (region.Height + (bestUnitSize >> 1)) / bestUnitSize);
            ReadOnlySpan<Av1LoopRestorationUnit> retained = picture.RestorationUnits[plane].Span;
            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    Rectangle bounds = GetUnitBounds(column, row, bestUnitSize, region.Size, subY);
                    context.FilterUnit(bounds, retained[(row * columns) + column]);
                }
            }

            // The complete plane must be filtered before replacing its source. Adjacent units
            // consume the same reconstruction even when they choose different filter families.
            for (int row = 0; row < region.Height; row++)
            {
                trial.DangerousGetRowSpan(row).CopyTo(region.DangerousGetRowSpan(row));
            }
        }
    }

    /// <summary>
    /// Gets a restoration unit's visible rectangle, including merged tails and the stripe offset.
    /// </summary>
    /// <param name="column">The unit column.</param>
    /// <param name="row">The unit row.</param>
    /// <param name="unitSize">The plane restoration unit size.</param>
    /// <param name="planeSize">The visible plane dimensions.</param>
    /// <param name="subsamplingY">The vertical chroma shift.</param>
    /// <returns>The exact area filtered by the unit.</returns>
    private static Rectangle GetUnitBounds(int column, int row, int unitSize, Size planeSize, int subsamplingY)
    {
        int x = column * unitSize;
        int y = row * unitSize;
        int remainingWidth = planeSize.Width - x;
        int remainingHeight = planeSize.Height - y;
        int width = remainingWidth < (unitSize * 3) / 2 ? remainingWidth : unitSize;
        int height = remainingHeight < (unitSize * 3) / 2 ? remainingHeight : unitSize;
        int bottom = y + height;
        int offset = Av1LoopRestorationBoundary.ProcessingStripeOffset >> subsamplingY;
        y = Math.Max(0, y - offset);
        if (bottom < planeSize.Height)
        {
            bottom -= offset;
        }

        return new Rectangle(x, y, width, bottom - y);
    }
}
