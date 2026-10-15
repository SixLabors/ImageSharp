// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif;

/// <summary>
/// Specifies how the encoder balances file size against the quality setting. The modes that use a bit budget aim
/// for about 8.5 kilobits per frame. This budget is 256 kilobits per second at 30 frames per second. The frame delays
/// of the image do not change it. A still image gets the bits of one key frame.
/// </summary>
public enum HeifRateControl
{
    /// <summary>
    /// Each frame aims for the quality setting. The file size follows the content.
    /// </summary>
    ConstantQuality = 0,

    /// <summary>
    /// Frames spend the bit budget. Ordinary frames get no better quality than the quality setting. Key frames, and
    /// the frames that later frames predict from, can get better quality. When the frames so far needed much less
    /// than the budget, every frame gets better quality.
    /// </summary>
    ConstrainedQuality = 1,

    /// <summary>
    /// Frames spend the bit budget, and each frame takes the bits it needs. The quality stays within a few steps of
    /// the quality setting.
    /// </summary>
    VariableBitRate = 2,

    /// <summary>
    /// Frames spend the bit budget at a steady rate. The quality stays within a few steps of the quality setting.
    /// </summary>
    ConstantBitRate = 3
}
