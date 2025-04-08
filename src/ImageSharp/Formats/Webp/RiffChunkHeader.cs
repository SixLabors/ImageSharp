// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Formats.Webp;

internal readonly struct RiffChunkHeader
{
    public readonly uint FourCc;

    public readonly uint Size;

    public ReadOnlySpan<byte> FourCcBytes => MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<uint, byte>(ref Unsafe.AsRef(in this.FourCc)), sizeof(uint));
}
