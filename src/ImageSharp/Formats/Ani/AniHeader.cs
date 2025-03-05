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

    public void WriteTo(in Stream stream) => stream.Write(MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(in this, 1)));
}

/// <summary>
/// Flags for the ANI header.
/// </summary>
[Flags]
public enum AniHeaderFlags : uint
{
    /// <summary>
    /// If set, the ANI file's "icon" chunk contains an ICO or CUR file, otherwise it contains a BMP file.
    /// </summary>
    IsIcon = 1,

    /// <summary>
    /// If set, the ANI file contains a "seq " chunk.
    /// </summary>
    ContainsSeq = 2
}
