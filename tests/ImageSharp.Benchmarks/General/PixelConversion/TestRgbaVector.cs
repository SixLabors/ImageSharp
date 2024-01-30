// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Benchmarks.General.PixelConversion;

[StructLayout(LayoutKind.Sequential)]
public struct TestRgbaVector : ITestPixel<TestRgbaVector>
{
    private Vector4 v;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private TestRgbaVector(Vector4 source) => this.v = source;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void FromVector4(Vector4 source) => this.v = source;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TestRgbaVector StaticFromVector4(Vector4 source) => new(source);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void FromVector4(ref Vector4 source) => this.v = source;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector4 ToVector4() => this.v;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void CopyToVector4(ref Vector4 destination) => destination = this.v;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void FromRgba32(Rgba32 source) => this.v = source.ToScaledVector4();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TestRgbaVector StaticFromRgba32(Rgba32 source) => new(source.ToScaledVector4());

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void FromRgba32(ref Rgba32 source) => this.v = source.ToScaledVector4();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void FromBytes(byte r, byte g, byte b, byte a) => this.v = new Rgba32(r, g, b, a).ToScaledVector4();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Rgba32 ToRgba32() => Rgba32.FromScaledVector4(this.v);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void CopyToRgba32(ref Rgba32 destination) => destination = Rgba32.FromScaledVector4(this.v);
}
