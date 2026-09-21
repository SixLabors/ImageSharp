// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <summary>
/// Identifies a picture's role in the reference-frame update schedule.
/// </summary>
internal enum Av1FrameUpdateType : byte
{
    /// <summary>Replaces all reference frames.</summary>
    Key,

    /// <summary>Updates the most recent displayed reference.</summary>
    Last,

    /// <summary>Updates the golden reference.</summary>
    Golden,

    /// <summary>Updates the alternate reference before its display time.</summary>
    Alternate,

    /// <summary>Displays the alternate reference at its scheduled time.</summary>
    Overlay,

    /// <summary>Displays an intermediate alternate reference.</summary>
    IntermediateOverlay,

    /// <summary>Updates an intermediate alternate reference.</summary>
    IntermediateAlternate,

    /// <summary>The number of frame update roles.</summary>
    Count
}
