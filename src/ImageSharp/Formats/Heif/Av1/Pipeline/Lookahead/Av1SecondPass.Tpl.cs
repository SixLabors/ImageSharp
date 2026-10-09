// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Lookahead;

/// <content>
/// Exposes the rate-control state that the temporal dependency model reads when it measures a golden group.
/// </content>
internal sealed partial class Av1SecondPass
{
    /// <summary>
    /// Gets the number of frames shown since the last key frame.
    /// </summary>
    public int FrameNumber => this.frameNumber;

    /// <summary>
    /// Gets the effective look-ahead depth. A request of 32 to 38 frames becomes 39.
    /// </summary>
    public int LagInFrames => this.lagInFrames;

    /// <summary>
    /// Gets the golden interval of the current group.
    /// </summary>
    public int BaselineGoldenInterval => this.baselineGoldenInterval;

    /// <summary>
    /// Gets the lowest allowed quantizer index.
    /// </summary>
    public int BestQuality => this.bestQuality;

    /// <summary>
    /// Gets the highest allowed quantizer index.
    /// </summary>
    public int WorstQuality => this.worstQuality;

    /// <summary>
    /// Attaches the temporal dependency test of long golden intervals, once the lookahead it reads exists.
    /// </summary>
    /// <param name="evaluator">The test.</param>
    public void AttachGopLengthEvaluator(IGopLengthEvaluator evaluator) => this.gopLengthEvaluator = evaluator;

    /// <summary>
    /// Records the screen content type that the quantizer choices read. An intra frame sets this type before its filtering and its temporal dependency model.
    /// </summary>
    /// <param name="screenContent">Whether the frames are classified as screen content.</param>
    public void SetScreenContentType(bool screenContent) => this.screenContentType = screenContent;

    /// <summary>
    /// Estimates the quantizer of every frame of the group from the current frame on. The temporal dependency model codes each frame at this quantizer.
    /// The group length test reads the screen content type of the last coded frame. The coding run reads the type of the current frame.
    /// </summary>
    public void PreloadTplQuantizers()
    {
        for (int index = this.groupFrameIndex; index < this.group.Size; ++index)
        {
            this.group.QValues[index] = this.PickQIndex(index, this.screenContentType);
        }
    }
}
