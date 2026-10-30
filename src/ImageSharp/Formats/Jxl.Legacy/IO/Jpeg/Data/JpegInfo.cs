// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.IO.Jpeg.Data;

internal struct JpegInfo
{
    public int NumberOfAppMarkers { get; set; }

    public int NumberOfComMarkers { get; set; }

    public int NumberOfScans { get; set; }

    public int NumberOfIntermarkers { get; set; }

    public bool HasDri { get; set; }
}
