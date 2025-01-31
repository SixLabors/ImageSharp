// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Formats.Ani;

[StructLayout(LayoutKind.Sequential, Pack = 1, Size = Size)]
internal readonly struct AniHeader
{
    public const int Size = 36;

    public ushort Frames { get; }

    public ushort Steps { get; }

    public ushort Width { get; }

    public ushort Height { get; }

    public ushort BitCount { get; }

    public ushort Planes { get; }

    public ushort DisplayRate { get; }

    public ushort Flags { get; }

    public static AniHeader Parse(in ReadOnlySpan<byte> data)
    => MemoryMarshal.Cast<byte, AniHeader>(data)[0];

    public readonly unsafe void WriteTo(in Stream stream)
    => stream.Write(MemoryMarshal.Cast<AniHeader, byte>([this]));
}
