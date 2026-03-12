// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Benchmarks.General.PixelConversion;

[StructLayout(LayoutKind.Sequential)]
public struct TestArgb : ITestPixel<TestArgb>
{
    public byte A;
    public byte R;
    public byte G;
    public byte B;

    private static readonly Vector4 MaxBytes = Vector128.Create(255f).AsVector4();
    private static readonly Vector4 Half = Vector128.Create(.5f).AsVector4();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private TestArgb(Rgba32 source)
    {
        this.R = source.R;
        this.G = source.G;
        this.B = source.B;
        this.A = source.A;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private TestArgb(byte r, byte g, byte b, byte a)
    {
        this.R = r;
        this.G = g;
        this.B = b;
        this.A = a;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void FromRgba32(Rgba32 source)
    {
        this.R = source.R;
        this.G = source.G;
        this.B = source.B;
        this.A = source.A;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void FromRgba32(ref Rgba32 source)
    {
        this.R = source.R;
        this.G = source.G;
        this.B = source.B;
        this.A = source.A;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TestArgb StaticFromRgba32(Rgba32 source) => new(source);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void FromBytes(byte r, byte g, byte b, byte a)
    {
        this.R = r;
        this.G = g;
        this.B = b;
        this.A = a;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void FromVector4(Vector4 source) => this = Pack(source);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TestArgb StaticFromVector4(Vector4 source) => Pack(source);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void FromVector4(ref Vector4 source) => this = Pack(source);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Rgba32 ToRgba32() => new(this.R, this.G, this.B, this.A);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void CopyToRgba32(ref Rgba32 destination)
    {
        destination.R = this.R;
        destination.G = this.G;
        destination.B = this.B;
        destination.A = this.A;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector4 ToVector4() => new Vector4(this.R, this.G, this.B, this.A) / MaxBytes;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void CopyToVector4(ref Vector4 destination) => destination = this.ToVector4();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static TestArgb Pack(Vector4 vector)
    {
        vector *= MaxBytes;
        vector += Half;
        vector = Numerics.Clamp(vector, Vector4.Zero, MaxBytes);

        Vector128<byte> result = Vector128.ConvertToInt32(vector.AsVector128()).AsByte();
        return new TestArgb(result.GetElement(0), result.GetElement(4), result.GetElement(8), result.GetElement(12));
    }
}
