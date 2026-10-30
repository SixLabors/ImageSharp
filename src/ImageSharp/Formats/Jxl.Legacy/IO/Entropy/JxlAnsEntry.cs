// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Formats.Jxl.IO.Entropy;

[StructLayout(LayoutKind.Sequential)]
internal struct JxlAnsEntry
{
    public byte Cutoff;
    public byte RightValue;
    public ushort Frequency0;
    public ushort Offsets1;
    public ushort Frequency1XorFrequency0;
}
