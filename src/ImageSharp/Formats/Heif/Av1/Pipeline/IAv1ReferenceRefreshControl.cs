// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <summary>
/// Adjusts the reference slots that a coded frame refreshes, after its blocks are coded and before its header is
/// written.
/// </summary>
internal interface IAv1ReferenceRefreshControl
{
    /// <summary>
    /// Adjusts the refreshed slots of the frame that was just coded.
    /// </summary>
    /// <param name="parent">The frame state, with the motion statistics of the coded frame.</param>
    void AdjustRefresh(Av1PictureParentControlSet parent);
}
