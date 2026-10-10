// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <content>
/// Defines the frame-level global motion search.
/// </content>
internal static partial class Av1FrameEncoder
{
    /// <summary>
    /// Searches a global motion model for the references of an inter frame and stores the chosen models in the frame header.
    /// </summary>
    /// <typeparam name="TSample">The component sample type.</typeparam>
    /// <typeparam name="TOperator">The sample-specific measures the search needs.</typeparam>
    /// <param name="allocator">The allocator of every buffer the search uses.</param>
    /// <param name="source">The frame being coded.</param>
    /// <param name="references">The reference frame of each reference type, indexed by reference type.</param>
    /// <param name="frameHeader">The header that receives the chosen models. Its previous models must be set.</param>
    /// <param name="bitDepth">The coded sample depth.</param>
    /// <param name="search">The per-frame inputs of the search.</param>
    private static void ComputeGlobalMotion<TSample, TOperator>(
        MemoryAllocator allocator,
        Av1EncoderFrame<TSample> source,
        ReadOnlySpan<Av1EncoderFrame<TSample>> references,
        ObuFrameHeader frameHeader,
        Av1BitDepth bitDepth,
        in GlobalMotionSearchInputs search)
        where TSample : unmanaged
        where TOperator : struct, Av1GlobalMotionSearch.IAv1GlobalMotionOperator<TSample>
    {
        // Every model starts as the identity. A frame that does not search, or a reference that the search skips, keeps the identity.
        Span<Av1GlobalMotionParameters> models = frameHeader.GetGlobalMotionParameters();
        models.Fill(Av1GlobalMotionParameters.Identity);
        Av1GlobalMotionSearchType searchType = search.SpeedSettings.GetGlobalMotionSearchType(search.Boosted);
        if (frameHeader.FrameType != ObuFrameType.InterFrame || !search.Enabled || searchType == Av1GlobalMotionSearchType.Disabled)
        {
            return;
        }

        // The search visits past references first, then future references. Each list starts with the nearest reference.
        Span<int> pastFrames = stackalloc int[Av1Constants.ReferencesPerFrame];
        Span<int> pastDistances = stackalloc int[Av1Constants.ReferencesPerFrame];
        Span<int> futureFrames = stackalloc int[Av1Constants.ReferencesPerFrame];
        Span<int> futureDistances = stackalloc int[Av1Constants.ReferencesPerFrame];
        int pastCount = 0;
        int futureCount = 0;
        UpdateValidReferenceFrames(
            source, references, frameHeader, searchType, in search, pastFrames, pastDistances, ref pastCount, futureFrames, futureDistances, ref futureCount);

        SortByDistance(pastFrames[..pastCount], pastDistances[..pastCount]);
        SortByDistance(futureFrames[..futureCount], futureDistances[..futureCount]);
        if (searchType == Av1GlobalMotionSearchType.ClosestReferencesOnly)
        {
            if (futureCount > 0)
            {
                pastCount = Math.Min(pastCount, 1);
                futureCount = Math.Min(futureCount, 1);
            }
            else
            {
                pastCount = Math.Min(pastCount, 2);
            }
        }

        // When pruning is on, the search of one direction stops at the first reference whose model is a translation or the identity.
        bool prune = search.SpeedSettings.PrunesGlobalMotionReferences(search.Boosted);
        for (int direction = 0; direction < 2; direction++)
        {
            ReadOnlySpan<int> frames = direction == 0 ? pastFrames[..pastCount] : futureFrames[..futureCount];
            for (int i = 0; i < frames.Length; i++)
            {
                int frame = frames[i];
                ComputeGlobalMotionForReference<TSample, TOperator>(
                    allocator, source, references[frame], frameHeader, frame, bitDepth, search.SpeedSettings);

                if (prune && models[frame - 1].Type <= Av1GlobalMotionType.Translation)
                {
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Lists the references that the global motion search visits, with their distance from the frame. With one pass and no statistics, the
    /// encoder never recodes a frame. In that case, the search also visits a reference that the frame does not use. An encoder with first-pass
    /// statistics can recode a frame, and it skips such a reference.
    /// </summary>
    /// <typeparam name="TSample">The component sample type.</typeparam>
    /// <param name="source">The frame being coded.</param>
    /// <param name="references">The reference frame of each reference type, indexed by reference type.</param>
    /// <param name="frameHeader">The header of the frame being coded.</param>
    /// <param name="searchType">The search type, which selects the reference types to visit.</param>
    /// <param name="search">The per-frame inputs of the search.</param>
    /// <param name="pastFrames">Receives the reference types that precede the frame in display order.</param>
    /// <param name="pastDistances">Receives the display order distance of each past reference.</param>
    /// <param name="pastCount">The number of past references, incremented for each added reference.</param>
    /// <param name="futureFrames">Receives the reference types that follow the frame in display order.</param>
    /// <param name="futureDistances">Receives the display order distance of each future reference.</param>
    /// <param name="futureCount">The number of future references, incremented for each added reference.</param>
    private static void UpdateValidReferenceFrames<TSample>(
        Av1EncoderFrame<TSample> source,
        ReadOnlySpan<Av1EncoderFrame<TSample>> references,
        ObuFrameHeader frameHeader,
        Av1GlobalMotionSearchType searchType,
        in GlobalMotionSearchInputs search,
        Span<int> pastFrames,
        Span<int> pastDistances,
        ref int pastCount,
        Span<int> futureFrames,
        Span<int> futureDistances,
        ref int futureCount)
        where TSample : unmanaged
    {
        ReadOnlySpan<uint> slots = frameHeader.GetReferenceFrameIndices();
        int selectiveLevel = search.SpeedSettings.SelectiveReferenceFrameLevel;

        // The pruning of references applies only to frames outside the temporal dependency model. Thus it does not apply to key, golden and
        // alternate reference frames.
        bool pruningEnabled = selectiveLevel > 0 &&
            search.UpdateType is not (Av1FrameUpdateType.Alternate or Av1FrameUpdateType.Golden or Av1FrameUpdateType.Key);

        // The statistics of earlier frames in the group can turn the search off. Every good-quality speed uses this check.
        bool searchDisabledByStatistics = search.DisabledByStatistics;

        // Only the size of the reference must match. All frame buffers of the encoder have the same border. Thus a reference of that size also has
        // the stride of the source, and the estimator uses this stride for both planes.
        for (int frame = (int)Av1ReferenceFrameType.Alternate; frame >= (int)Av1ReferenceFrameType.Last; frame--)
        {
            Av1EncoderFrame<TSample> reference = references[frame];
            int slot = (int)slots[frame - 1];
            bool pruned = pruningEnabled && PrunesReferenceForGlobalMotion(frame, slots, search.SlotDisplayOrders, selectiveLevel);
            if ((search.RecodeAllowed && (search.ReferenceFrameFlags & (1 << frame)) == 0) ||
                reference.Width != source.Width ||
                reference.Height != source.Height ||
                !IsGlobalMotionSearched(searchType, frame) ||
                pruned ||
                search.SlotPyramidLevels[slot] > search.PyramidLevel ||
                searchDisabledByStatistics)
            {
                continue;
            }

            int distance = search.SlotDisplayOrders[slot] - search.DisplayOrder;
            if (distance < 0)
            {
                pastFrames[pastCount] = frame;
                pastDistances[pastCount++] = -distance;
            }
            else if (distance > 0)
            {
                futureFrames[futureCount] = frame;
                futureDistances[futureCount++] = distance;
            }
        }
    }

    /// <summary>
    /// Returns whether the search type visits a reference.
    /// </summary>
    /// <param name="searchType">The search type.</param>
    /// <param name="frame">The reference type.</param>
    /// <returns><see langword="true"/> when the reference is searched.</returns>
    private static bool IsGlobalMotionSearched(Av1GlobalMotionSearchType searchType, int frame) => searchType switch
    {
        Av1GlobalMotionSearchType.SkipLast2Last3Alternate2 =>
            frame is not ((int)Av1ReferenceFrameType.Last2 or (int)Av1ReferenceFrameType.Last3 or (int)Av1ReferenceFrameType.Alternate2),
        Av1GlobalMotionSearchType.Disabled => false,
        _ => true,
    };

    /// <summary>
    /// Returns whether the selective reference search drops a single reference. From level 2, it drops LAST2 and LAST3 when they precede GOLDEN
    /// in display order. From level 3, it also drops ALTREF2 and BWDREF when they precede LAST. The frame-level search has no block, so no
    /// predicted motion vector keeps a reference.
    /// </summary>
    /// <param name="frame">The reference type.</param>
    /// <param name="slots">The slot of each reference type.</param>
    /// <param name="slotDisplayOrders">The display order of the frame in each slot.</param>
    /// <param name="level">The selective reference level.</param>
    /// <returns><see langword="true"/> when the reference is dropped.</returns>
    private static bool PrunesReferenceForGlobalMotion(int frame, ReadOnlySpan<uint> slots, ReadOnlySpan<int> slotDisplayOrders, int level)
    {
        int order = slotDisplayOrders[(int)slots[frame - 1]];
        if (level >= 2 &&
            frame is (int)Av1ReferenceFrameType.Last3 or (int)Av1ReferenceFrameType.Last2 &&
            order < slotDisplayOrders[(int)slots[(int)Av1ReferenceFrameType.Golden - 1]])
        {
            return true;
        }

        return level >= 3 &&
            frame is (int)Av1ReferenceFrameType.Alternate2 or (int)Av1ReferenceFrameType.Backward &&
            order < slotDisplayOrders[(int)slots[(int)Av1ReferenceFrameType.Last - 1]];
    }

#pragma warning disable CA1517 // False positive: https://github.com/dotnet/sdk/issues/53388
    /// <summary>
    /// Sorts references by distance, nearest first. The sort repeatedly moves the first largest entry to the end of the unsorted part. Thus equal
    /// distances end in the order that this method gives, not in the order of the list. This matches the output of the x64 AVIF reference
    /// encoder, whose C runtime sorts lists of up to eight entries with this method. Every list here has at most seven entries.
    /// </summary>
    /// <param name="frames">The reference types, sorted in place.</param>
    /// <param name="distances">The distance of each reference, sorted in place.</param>
    private static void SortByDistance(Span<int> frames, Span<int> distances)
    {
        for (int high = frames.Length - 1; high > 0; high--)
        {
            int largest = 0;
            for (int index = 1; index <= high; index++)
            {
                if (distances[index] > distances[largest])
                {
                    largest = index;
                }
            }

            (frames[largest], frames[high]) = (frames[high], frames[largest]);
            (distances[largest], distances[high]) = (distances[high], distances[largest]);
        }
    }
#pragma warning restore CA1517

    /// <summary>
    /// Fits the models of each searched family to one reference. It keeps the model whose warped error is the smallest share of the unwarped
    /// error, if that share also justifies the cost to code the model against the model of the primary reference frame. The search fits only
    /// rotation-zoom models. It never takes a model that reduces to a translation, because the vector that such a model gives a block has a
    /// published defect.
    /// </summary>
    /// <typeparam name="TSample">The component sample type.</typeparam>
    /// <typeparam name="TOperator">The sample-specific measures the search needs.</typeparam>
    /// <param name="allocator">The allocator of every buffer the search uses.</param>
    /// <param name="source">The frame being coded.</param>
    /// <param name="reference">The reference frame to fit the models to.</param>
    /// <param name="frameHeader">The header that receives the chosen model. Its previous models must be set.</param>
    /// <param name="frame">The reference type of <paramref name="reference"/>.</param>
    /// <param name="bitDepth">The coded sample depth.</param>
    /// <param name="speedSettings">The speed settings of the frame.</param>
    private static void ComputeGlobalMotionForReference<TSample, TOperator>(
        MemoryAllocator allocator,
        Av1EncoderFrame<TSample> source,
        Av1EncoderFrame<TSample> reference,
        ObuFrameHeader frameHeader,
        int frame,
        Av1BitDepth bitDepth,
        Av1EncoderSpeedSettings speedSettings)
        where TSample : unmanaged
        where TOperator : struct, Av1GlobalMotionSearch.IAv1GlobalMotionOperator<TSample>
    {
        // The search measures the visible frame, not its coded extent.
        Av1PlaneRegion<TSample> sourceLuma = source.CodedView.GetPlane(Av1Plane.Y);
        Av1PlaneRegion<TSample> referenceLuma = reference.CodedView.GetPlane(Av1Plane.Y);
        int width = source.Width;
        int height = source.Height;
        int stride = sourceLuma.Stride;
        int depth = bitDepth.GetBitCount();
        ReadOnlySpan<TSample> sourcePlane = sourceLuma.Samples;
        ReadOnlySpan<TSample> referencePlane = referenceLuma.Samples;
        int sourceOrigin = (sourceLuma.Bounds.Y * stride) + sourceLuma.Bounds.X;
        int referenceOrigin = (referenceLuma.Bounds.Y * stride) + referenceLuma.Bounds.X;

        // The map holds one mark per error block. A frame that does not divide evenly also has a partial block at its right and bottom edges.
        int mapWidth = (width + Av1GlobalMotionSearch.ErrorBlock - 1) >> Av1GlobalMotionSearch.ErrorBlockLog;
        int mapHeight = (height + Av1GlobalMotionSearch.ErrorBlock - 1) >> Av1GlobalMotionSearch.ErrorBlockLog;
        using IMemoryOwner<byte> mapOwner = allocator.Allocate<byte>(mapWidth * mapHeight);
        Span<byte> map = mapOwner.Memory.Span;

        // The best share starts at the threshold, so a model must meet the threshold to replace the identity.
        const int ErrorAdvantageLevel = 0;
        double threshold = Av1GlobalMotionSearch.GetErrorAdvantageThreshold(ErrorAdvantageLevel);
        double bestErrorAdvantage = threshold;
        Av1GlobalMotionParameters previous = frameHeader.GetPreviousGlobalMotionParameters()[frame - 1];
        Av1MotionModel[] fitted = [new Av1MotionModel()];
        if (!Av1GlobalMotionEstimator.Compute<TSample, TOperator, Av1Ransac.RotationZoomModel>(
            allocator, sourcePlane, referencePlane, width, height, stride, sourceOrigin, depth, speedSettings.GlobalMotionDownsampleLevel, fitted))
        {
            return;
        }

        for (int motion = 0; motion < fitted.Length; motion++)
        {
            if (fitted[motion].InlierCount == 0)
            {
                continue;
            }

            Av1GlobalMotionParameters candidate = Av1GlobalMotionSearch.ConvertModelToParameters(fitted[motion].Parameters);
            candidate.UpdateShearParameters();
            if (candidate.IsInvalid || candidate.Type <= Av1GlobalMotionType.Translation)
            {
                continue;
            }

            // The search measures the error only in the blocks where the model fits. Thus the parts of the frame that move on their own do not
            // decide whether the model is worth its cost.
            Av1GlobalMotionSearch.ComputeFeatureSegmentationMap(map, mapWidth, mapHeight, fitted[motion].Inliers);
            long referenceError = Av1GlobalMotionSearch.GetSegmentedFrameError<TSample, TOperator>(
                referencePlane[referenceOrigin..], stride, sourcePlane[sourceOrigin..], stride, width, height, map, mapWidth);

            if (referenceError == 0)
            {
                continue;
            }

            long warpError = Av1GlobalMotionSearch.RefineIntegerizedParameters<TSample, TOperator>(
                allocator,
                ref candidate,
                candidate.Type,
                referencePlane[referenceOrigin..],
                stride,
                sourcePlane[sourceOrigin..],
                stride,
                width,
                height,
                speedSettings.GlobalMotionRefinementSteps,
                depth,
                referenceError,
                map,
                mapWidth,
                threshold);

            // Refinement can move a model down to a simpler family, so the loop reads the family again.
            if (candidate.Type <= Av1GlobalMotionType.Translation)
            {
                continue;
            }

            double errorAdvantage = (double)warpError / referenceError;
            int parametersCost = ObuWriter.GetGlobalMotionParameterBitCount(
                candidate, previous, frameHeader.AllowHighPrecisionMotionVector) << Av1ProbabilityCost.CostShift;

            if (!Av1GlobalMotionSearch.IsEnoughErrorAdvantage(errorAdvantage, parametersCost, threshold))
            {
                continue;
            }

            if (errorAdvantage < bestErrorAdvantage)
            {
                bestErrorAdvantage = errorAdvantage;
                frameHeader.GetGlobalMotionParameters()[frame - 1] = candidate;
            }
        }
    }

    /// <summary>
    /// Holds the per-frame inputs of the global motion search.
    /// </summary>
    private readonly ref struct GlobalMotionSearchInputs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GlobalMotionSearchInputs"/> struct.
        /// </summary>
        /// <param name="enabled">Whether the encoder configuration searches global motion.</param>
        /// <param name="speedSettings">The speed settings of the frame.</param>
        /// <param name="updateType">The update type of the frame.</param>
        /// <param name="boosted">Whether the frame is a key, golden or alternate reference frame.</param>
        /// <param name="slotDisplayOrders">The display order of the frame in each reference slot.</param>
        /// <param name="displayOrder">The display order of the frame being coded.</param>
        /// <param name="slotPyramidLevels">The pyramid level of the frame in each reference slot.</param>
        /// <param name="pyramidLevel">The pyramid level of the frame being coded.</param>
        /// <param name="referenceFrameFlags">The references the frame uses, one bit per reference type.</param>
        /// <param name="recodeAllowed">Whether the encoder can recode a frame.</param>
        /// <param name="disabledByStatistics">Whether the statistics of earlier frames in the group turn the search off.</param>
        public GlobalMotionSearchInputs(
            bool enabled,
            Av1EncoderSpeedSettings speedSettings,
            Av1FrameUpdateType updateType,
            bool boosted,
            ReadOnlySpan<int> slotDisplayOrders,
            int displayOrder,
            ReadOnlySpan<int> slotPyramidLevels,
            int pyramidLevel,
            byte referenceFrameFlags,
            bool recodeAllowed,
            bool disabledByStatistics)
        {
            this.ReferenceFrameFlags = referenceFrameFlags;
            this.RecodeAllowed = recodeAllowed;
            this.DisabledByStatistics = disabledByStatistics;
            this.Enabled = enabled;
            this.SpeedSettings = speedSettings;
            this.UpdateType = updateType;
            this.Boosted = boosted;
            this.SlotDisplayOrders = slotDisplayOrders;
            this.DisplayOrder = displayOrder;
            this.SlotPyramidLevels = slotPyramidLevels;
            this.PyramidLevel = pyramidLevel;
        }

        /// <summary>
        /// Gets a value indicating whether the encoder configuration searches global motion. Real-time usage does not.
        /// </summary>
        public bool Enabled { get; }

        /// <summary>
        /// Gets the speed settings of the frame.
        /// </summary>
        public Av1EncoderSpeedSettings SpeedSettings { get; }

        /// <summary>
        /// Gets the update type of the frame.
        /// </summary>
        public Av1FrameUpdateType UpdateType { get; }

        /// <summary>
        /// Gets a value indicating whether the frame is a key, golden or alternate reference frame.
        /// </summary>
        public bool Boosted { get; }

        /// <summary>
        /// Gets the display order of the frame in each reference slot.
        /// </summary>
        public ReadOnlySpan<int> SlotDisplayOrders { get; }

        /// <summary>
        /// Gets the display order of the frame being coded.
        /// </summary>
        public int DisplayOrder { get; }

        /// <summary>
        /// Gets the pyramid level of the frame in each reference slot.
        /// </summary>
        public ReadOnlySpan<int> SlotPyramidLevels { get; }

        /// <summary>
        /// Gets the pyramid level of the frame being coded.
        /// </summary>
        public int PyramidLevel { get; }

        /// <summary>
        /// Gets the references the frame uses, one bit per reference type.
        /// </summary>
        public byte ReferenceFrameFlags { get; }

        /// <summary>
        /// Gets a value indicating whether the encoder can recode a frame. It can whenever it has first-pass statistics.
        /// </summary>
        public bool RecodeAllowed { get; }

        /// <summary>
        /// Gets a value indicating whether a group with an alternate reference found no global motion so far in its alternate, intermediate
        /// alternate and leaf frames.
        /// </summary>
        public bool DisabledByStatistics { get; }
    }
}
