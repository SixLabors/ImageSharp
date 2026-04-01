// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Formats.Ani;

internal readonly struct AniHeader
{
    public uint Size { get; }

    public uint Frames { get; }

    public uint Steps { get; }

    public uint Width { get; }

    public uint Height { get; }

    public uint BitCount { get; }

    public uint Planes { get; }

    public uint DisplayRate { get; }

    public AniHeaderFlags Flags { get; }

    public static ref AniHeader Parse(ReadOnlySpan<byte> data) => ref Unsafe.As<byte, AniHeader>(ref MemoryMarshal.GetReference(data));

    public void WriteTo(Stream stream) => stream.Write(MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(in this, 1)));
}
