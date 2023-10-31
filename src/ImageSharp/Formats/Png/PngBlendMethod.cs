// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Png;

/// <summary>
/// Specifies whether the frame is to be alpha blended into the current output buffer content,
/// or whether it should completely replace its region in the output buffer.
/// </summary>
public enum PngBlendMethod
{
    /// <summary>
    /// All color components of the frame, including alpha, overwrite the current contents of the frame's output buffer region.
    /// </summary>
    Source,

    /// <summary>
    /// The frame should be composited onto the output buffer based on its alpha, using a simple OVER operation as
    /// described in the "Alpha Channel Processing" section of the PNG specification [PNG-1.2].
    /// </summary>
    Over
}
