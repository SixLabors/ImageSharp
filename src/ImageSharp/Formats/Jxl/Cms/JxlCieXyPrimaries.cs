// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles;

namespace SixLabors.ImageSharp.Formats.Jxl.Cms;

/// <summary>
/// RGB primaries for CIEXY
/// </summary>
internal struct JxlCieXyPrimaries
{
    /// <summary>
    /// Gets or sets the R component
    /// </summary>
    public CieXyChromaticityCoordinates R { get; set; }

    /// <summary>
    /// Gets or sets the G component
    /// </summary>
    public CieXyChromaticityCoordinates G { get; set; }

    /// <summary>
    /// Gets or sets the B component
    /// </summary>
    public CieXyChromaticityCoordinates B { get; set; }
}
