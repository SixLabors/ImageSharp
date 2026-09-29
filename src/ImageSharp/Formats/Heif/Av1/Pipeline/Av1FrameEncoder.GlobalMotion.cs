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
    /// Searches a global motion model for the references of an inter frame and stores the chosen models in the frame
    /// header. Reference: av1_compute_global_motion_facade().
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
        where TOperator : struct, IGlobalMotionSearchOperator<TSample>
    {
        // Every model starts as the identity. Reference: the default_warp_params reset of
        // update_valid_ref_frames_for_gm().
        Span<Av1GlobalMotionParameters> models = frameHeader.GetGlobalMotionParameters();
        models.Fill(Av1GlobalMotionParameters.Identity);
        Av1GlobalMotionSearchType searchType = search.SpeedSettings.GetGlobalMotionSearchType(search.Boosted);
        if (frameHeader.FrameType != ObuFrameType.InterFrame || !search.Enabled || searchType == Av1GlobalMotionSearchType.Disabled)
        {
            return;
        }

        // Past references come first, then future ones, each nearest first. Reference: setup_global_motion_info_params().
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

        // Reference: global_motion_estimation() and compute_global_motion_for_references().
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
    /// Lists the references that the global motion search visits, with their distance from the frame. With one pass
    /// and no statistics the encoder never recodes a frame, so a reference that the frame does not use is still
    /// searched. Reference: update_valid_ref_frames_for_gm().
    /// </summary>
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

        // The pruning of references applies to frames outside the temporal dependency model. Reference:
        // is_frame_eligible_for_ref_pruning() with is_frame_tpl_eligible().
        bool pruningEnabled = selectiveLevel > 0 &&
            search.UpdateType is not (Av1FrameUpdateType.Alternate or Av1FrameUpdateType.Golden or Av1FrameUpdateType.Key);

        // Reference: disable_gm_search_based_on_stats(), on for every good-quality speed.
        bool searchDisabledByStatistics = search.DisabledByStatistics;

        Buffer2DRegion<TSample> sourceLuma = source.CodedView.GetPlane(Av1Plane.Y);
        for (int frame = (int)Av1ReferenceFrameType.Alternate; frame >= (int)Av1ReferenceFrameType.Last; frame--)
        {
            Av1EncoderFrame<TSample> reference = references[frame];
            int slot = (int)slots[frame - 1];
            bool pruned = pruningEnabled && PrunesReferenceForGlobalMotion(frame, slots, search.SlotDisplayOrders, selectiveLevel);
            Buffer2DRegion<TSample> referenceLuma = reference.CodedView.GetPlane(Av1Plane.Y);
            if (reference.Width != source.Width ||
                reference.Height != source.Height ||
                referenceLuma.Stride != sourceLuma.Stride ||
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
    /// Returns whether the search type visits a reference. Reference: do_gm_search_logic().
    /// </summary>
    /// <param name="searchType">The search type.</param>
    /// <param name="frame">The reference type.</param>
    /// <returns><see langword="true"/> when the reference is searched.</returns>
    private static bool IsGlobalMotionSearched(Av1GlobalMotionSearchType searchType, int frame) => searchType switch
    {
        Av1GlobalMotionSearchType.SkipLast2Last3 => frame is not ((int)Av1ReferenceFrameType.Last2 or (int)Av1ReferenceFrameType.Last3),
        Av1GlobalMotionSearchType.SkipLast2Last3Alternate2 =>
            frame is not ((int)Av1ReferenceFrameType.Last2 or (int)Av1ReferenceFrameType.Last3 or (int)Av1ReferenceFrameType.Alternate2),
        Av1GlobalMotionSearchType.Disabled => false,
        _ => true,
    };

    /// <summary>
    /// Returns whether the selective reference search drops a single reference: from level two, LAST2 and LAST3 when
    /// they precede GOLDEN, and from level three, ALTREF2 and BWDREF when they precede LAST. The frame-level call has
    /// no block, so no predicted-vector result keeps a reference. Reference: prune_ref_by_selective_ref_frame() with a
    /// null macroblock, and prune_ref().
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

    /// <summary>
    /// Sorts references by distance, nearest first. The reference encoder sorts with the C library qsort, and the x64
    /// reference build is linked with the Microsoft C runtime, whose qsort sorts a list of up to eight entries by
    /// repeatedly moving the first largest entry to the end. Equal distances therefore end in that order, not in the
    /// order of the list. Every list here has at most seven entries. Reference: the qsort() calls with
    /// compare_distance() in setup_global_motion_info_params().
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

    /// <summary>
    /// Fits the models of each searched family to one reference and keeps the one whose warped error is the smallest
    /// share of the unwarped error, when that share also justifies the cost of coding the model against the model of
    /// the primary reference frame. Only rotation-zoom models are searched. A model that reduces to a translation is
    /// never taken, because the vector such a model gives a block has a published defect.
    /// Reference: compute_global_motion_for_ref_frame().
    /// </summary>
    private static void ComputeGlobalMotionForReference<TSample, TOperator>(
        MemoryAllocator allocator,
        Av1EncoderFrame<TSample> source,
        Av1EncoderFrame<TSample> reference,
        ObuFrameHeader frameHeader,
        int frame,
        Av1BitDepth bitDepth,
        Av1EncoderSpeedSettings speedSettings)
        where TSample : unmanaged
        where TOperator : struct, IGlobalMotionSearchOperator<TSample>
    {
        // The search measures the visible frame, not its coded extent. Reference: the y_crop_width and
        // y_crop_height of cpi->source that compute_global_motion_for_ref_frame() passes on.
        Buffer2DRegion<TSample> sourceLuma = source.CodedView.GetPlane(Av1Plane.Y);
        Buffer2DRegion<TSample> referenceLuma = reference.CodedView.GetPlane(Av1Plane.Y);
        int width = source.Width;
        int height = source.Height;
        int stride = sourceLuma.Stride;
        int depth = bitDepth.GetBitCount();
        ReadOnlySpan<TSample> sourcePlane = sourceLuma.Buffer.DangerousGetSingleSpan();
        ReadOnlySpan<TSample> referencePlane = referenceLuma.Buffer.DangerousGetSingleSpan();
        int sourceOrigin = (sourceLuma.Bounds.Y * stride) + sourceLuma.Bounds.X;
        int referenceOrigin = (referenceLuma.Bounds.Y * stride) + referenceLuma.Bounds.X;

        // The map holds one mark per error block, and a frame that does not divide evenly still has a partial block
        // at its right and bottom edges.
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

            // The error is measured only where the model was fitted, so the parts of the frame that move on their own
            // do not decide whether the model is worth coding.
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

            // Refinement can move a model down to a simpler family, so the family is read again.
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
        public GlobalMotionSearchInputs(
            bool enabled,
            Av1EncoderSpeedSettings speedSettings,
            Av1FrameUpdateType updateType,
            bool boosted,
            ReadOnlySpan<int> slotDisplayOrders,
            int displayOrder,
            ReadOnlySpan<int> slotPyramidLevels,
            int pyramidLevel,
            bool disabledByStatistics = false)
        {
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
        /// Reference: tool_cfg.enable_global_motion.
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
        /// Gets a value indicating whether the frame is a key, golden or alternate reference frame. Reference:
        /// frame_is_boosted().
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
        /// Gets a value indicating whether a group with an alternate reference found no global motion in its
        /// alternate, intermediate alternate and leaf frames so far. Reference: disable_gm_search_based_on_stats().
        /// </summary>
        public bool DisabledByStatistics { get; }
    }
}
