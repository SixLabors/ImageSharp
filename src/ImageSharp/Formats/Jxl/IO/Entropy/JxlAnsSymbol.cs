// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Formats.Jxl.IO.Entropy;

[StructLayout(LayoutKind.Sequential)]
internal struct JxlAnsSymbol(int value, int offset, int frequency)
{
    public int Value = value;
    public int Offset = offset;
    public int Frequency = frequency;
}
