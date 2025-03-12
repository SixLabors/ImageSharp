// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Formats.Webp;

internal readonly struct RiffOrListChunkHeader
{
    public const int HeaderSize = 12;

    public readonly uint FourCc;

    public readonly uint Size;

    public readonly uint FormType;

    public ReadOnlySpan<byte> FourCcBytes => MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<uint, byte>(ref Unsafe.AsRef(in this.FourCc)), sizeof(uint));

    public bool IsRiff => this.FourCc is 0x52_49_46_46; // "RIFF"

    public bool IsList => this.FourCc is 0x4C_49_53_54; // "LIST"

    public static ref RiffOrListChunkHeader Parse(ReadOnlySpan<byte> data) => ref Unsafe.As<byte, RiffOrListChunkHeader>(ref MemoryMarshal.GetReference(data));
}
