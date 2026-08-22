// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing;

[StructLayout(LayoutKind.Sequential)]
internal struct JxlNoiseIndexAndFraction(int index, float fraction)
{
    public int Index = index;

    public float Fraction = fraction;
}
