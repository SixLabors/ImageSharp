// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics;

namespace SixLabors.ImageSharp.Formats.OpenExr;

[DebuggerDisplay("xMin: {XMin}, yMin: {YMin}, xMax: {XMax}, yMax: {YMax}")]
internal struct ExrBox2i
{
    public ExrBox2i(int xMin, int yMin, int xMax, int yMax)
    {
        this.XMin = xMin;
        this.YMin = yMin;
        this.XMax = xMax;
        this.YMax = yMax;
    }

    public int XMin { get; }

    public int YMin { get; }

    public int XMax { get; }

    public int YMax { get; }
}
