// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Lookahead;

/// <content>
/// The coding order of a golden frame group.
/// </content>
internal sealed partial class Av1SecondPass
{
    /// <summary>
    /// Builds the coding order of the golden frame group: a key or golden frame first when the group starts with
    /// one, then the alternate reference, a pyramid of internal alternate references over the frames before it,
    /// and its overlay; without an alternate reference the frames follow in display order in a low-delay pyramid.
    /// Reference: av1_gop_setup_structure() without an external rate control, a key frame pyramid limit or
    /// sharpness 3.
    /// </summary>
    private void SetupGopStructure()
    {
        Av1FrameUpdateType firstUpdateType = Av1FrameUpdateType.Alternate;
        if (this.framesSinceKey == 0)
        {
            firstUpdateType = Av1FrameUpdateType.Key;
        }
        else if (!this.arfGoldenBoostLast)
        {
            firstUpdateType = Av1FrameUpdateType.Golden;
        }

        this.group.Size = this.ConstructMultiLayerGoldenStructure(this.baselineGoldenInterval, firstUpdateType);
        if (this.group.MaxLayerDepthAllowed == 0)
        {
            this.SetLowDelayLayerDepth(this.baselineGoldenInterval);
        }
    }

    /// <summary>
    /// Fills the group entries of a golden interval and returns the number of coded frames.
    /// Reference: construct_multi_layer_gf_structure() for one frame context, without key frame filtering overlays
    /// or switch frames.
    /// </summary>
    /// <param name="baselineInterval">The golden interval.</param>
    /// <param name="firstUpdateType">The update role of the group start.</param>
    /// <returns>The number of coded frames.</returns>
    private int ConstructMultiLayerGoldenStructure(int baselineInterval, Av1FrameUpdateType firstUpdateType)
    {
        Av1GopStructure gop = this.group;
        int goldenInterval = baselineInterval - 1;
        int frameIndex = 0;
        int currentFrameIndex = 0;

        // The display order of the first frame of the group.
        int currentDisplayIndex = firstUpdateType == Av1FrameUpdateType.Key ? 0 : this.frameNumber;
        if (firstUpdateType == Av1FrameUpdateType.Key)
        {
            gop.Set(frameIndex, Av1FrameUpdateType.Key, 0, currentFrameIndex, 0, true, true);
            gop.MaxLayerDepth = 0;
            gop.DisplayIndices[frameIndex] = currentDisplayIndex;
            currentDisplayIndex++;
            ++frameIndex;
            currentFrameIndex++;
        }

        if (firstUpdateType == Av1FrameUpdateType.Golden)
        {
            gop.Set(frameIndex, Av1FrameUpdateType.Golden, 0, currentFrameIndex, 0, false, false);
            gop.MaxLayerDepth = 0;
            gop.DisplayIndices[frameIndex] = currentDisplayIndex;
            currentDisplayIndex++;
            ++frameIndex;
            ++currentFrameIndex;
        }

        // The alternate reference codes the last frame of the interval first.
        bool useAltRef = gop.MaxLayerDepthAllowed > 0;
        bool forwardKeyFrame = this.framesToForwardKeyFrame == goldenInterval;
        if (useAltRef)
        {
            int arfSourceOffset = goldenInterval - currentFrameIndex;
            gop.Set(frameIndex, Av1FrameUpdateType.Alternate, arfSourceOffset, currentFrameIndex, 1, forwardKeyFrame, false);
            gop.ArfBoosts[frameIndex] = this.goldenBoost;
            gop.MaxLayerDepth = 1;
            gop.ArfIndex = frameIndex;
            gop.DisplayIndices[frameIndex] = currentDisplayIndex + arfSourceOffset;
            ++frameIndex;
        }
        else
        {
            gop.ArfIndex = -1;
        }

        // The frames before the alternate reference.
        this.SetMultiLayerParameters(
            currentFrameIndex,
            goldenInterval,
            ref currentFrameIndex,
            ref frameIndex,
            ref currentDisplayIndex,
            (useAltRef ? 1 : 0) + 1);

        if (useAltRef)
        {
            gop.Set(frameIndex, Av1FrameUpdateType.Overlay, 0, currentFrameIndex, Av1GopStructure.MaximumArfLayers, false, forwardKeyFrame);
            gop.ArfBoosts[frameIndex] = NormalBoost;
            gop.DisplayIndices[frameIndex] = currentDisplayIndex;
            ++frameIndex;
        }
        else
        {
            for (; currentFrameIndex <= goldenInterval; ++currentFrameIndex)
            {
                gop.Set(frameIndex, Av1FrameUpdateType.Last, 0, currentFrameIndex, Av1GopStructure.MaximumArfLayers, false, false);
                gop.ArfBoosts[frameIndex] = NormalBoost;
                gop.MaxLayerDepth = Math.Max(gop.MaxLayerDepth, 2);
                gop.DisplayIndices[frameIndex] = currentDisplayIndex;
                currentDisplayIndex++;
                ++frameIndex;
            }
        }

        // The entries after the group describe leaf frames.
        for (int index = frameIndex; index < Av1GopStructure.MaximumLength; ++index)
        {
            gop.Set(index, Av1FrameUpdateType.Last, 0, index, Av1GopStructure.MaximumArfLayers, false, false);
            gop.ArfBoosts[index] = NormalBoost;
            gop.MaxLayerDepth = Math.Max(gop.MaxLayerDepth, 2);
        }

        return frameIndex;
    }

    /// <summary>
    /// Fills the entries of the frames between two bounds, excluding both: leaves when the pyramid is deep enough or
    /// fewer than three frames remain, else an internal alternate reference in the middle, the frames before it, its
    /// overlay and the frames after it. Reference: set_multi_layer_params() without parallel frames.
    /// </summary>
    /// <param name="start">The first frame.</param>
    /// <param name="end">The frame after the last.</param>
    /// <param name="currentFrameIndex">The number of shown frames of the group so far.</param>
    /// <param name="frameIndex">The next group entry.</param>
    /// <param name="currentDisplayIndex">The display order of the next shown frame.</param>
    /// <param name="layerDepth">The pyramid layer of the frames.</param>
    private void SetMultiLayerParameters(
        int start,
        int end,
        ref int currentFrameIndex,
        ref int frameIndex,
        ref int currentDisplayIndex,
        int layerDepth)
    {
        Av1GopStructure gop = this.group;
        int framesToProcess = end - start;

        // Either the last level of the pyramid, or too few frames between the bounds for one more level.
        if (layerDepth > gop.MaxLayerDepthAllowed || framesToProcess < 3)
        {
            // Leaf nodes.
            while (start < end)
            {
                gop.Set(frameIndex, Av1FrameUpdateType.Last, 0, currentFrameIndex, Av1GopStructure.MaximumArfLayers, false, false);
                gop.DisplayIndices[frameIndex] = currentDisplayIndex;
                gop.ArfBoosts[frameIndex] = this.CalculateArfBoost(this.statisticsPosition, start, end - start, 0, false, true);
                gop.MaxLayerDepth = Math.Max(gop.MaxLayerDepth, layerDepth);
                ++frameIndex;
                ++currentFrameIndex;
                ++currentDisplayIndex;
                ++start;
            }
        }
        else
        {
            int middle = (start + end - 1) / 2;

            // The internal alternate reference.
            int arfSourceOffset = middle - start;
            gop.Set(frameIndex, Av1FrameUpdateType.IntermediateAlternate, arfSourceOffset, currentFrameIndex, layerDepth, false, false);
            gop.DisplayIndices[frameIndex] = currentDisplayIndex + arfSourceOffset;
            gop.ArfBoosts[frameIndex] = this.CalculateArfBoost(this.statisticsPosition, middle, end - middle, middle - start, false, true);
            ++frameIndex;

            // The frames displayed before it.
            this.SetMultiLayerParameters(start, middle, ref currentFrameIndex, ref frameIndex, ref currentDisplayIndex, layerDepth + 1);

            // Its overlay.
            gop.Set(frameIndex, Av1FrameUpdateType.IntermediateOverlay, 0, currentFrameIndex, layerDepth, false, false);
            gop.DisplayIndices[frameIndex] = currentDisplayIndex;
            gop.ArfBoosts[frameIndex] = 0;
            ++frameIndex;
            ++currentFrameIndex;
            ++currentDisplayIndex;

            // The frames displayed after it.
            this.SetMultiLayerParameters(middle + 1, end, ref currentFrameIndex, ref frameIndex, ref currentDisplayIndex, layerDepth + 1);
        }
    }

    /// <summary>
    /// Replaces the layers of a group without an alternate reference by a low-delay pyramid: the layer falls by one
    /// for each trailing zero bit of the frame's index. Reference: set_ld_layer_depth().
    /// </summary>
    /// <param name="gopLength">The golden interval.</param>
    private void SetLowDelayLayerDepth(int gopLength)
    {
        int logGopLength = 0;
        while ((1 << logGopLength) < gopLength)
        {
            ++logGopLength;
        }

        for (int index = 0; index < this.group.Size; ++index)
        {
            int count = 0;
            for (; count < Av1GopStructure.MaximumArfLayers; ++count)
            {
                if (((index >> count) & 0x01) != 0)
                {
                    break;
                }
            }

            this.group.LayerDepths[index] = Math.Max(logGopLength - count, 0);
        }

        this.group.MaxLayerDepth = Math.Min(logGopLength, Av1GopStructure.MaximumArfLayers);
    }
}
