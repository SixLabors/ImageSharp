// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Splines;

/// <summary>
/// A simple pair of first and second 32-bit signed integers
/// that represent a single control point.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct JxlControlPoint(int first, int second)
{
    public int First = first;
    public int Second = second;
}
