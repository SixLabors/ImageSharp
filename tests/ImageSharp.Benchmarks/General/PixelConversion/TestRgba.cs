// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Benchmarks.General.PixelConversion
{
    [StructLayout(LayoutKind.Sequential)]
    public struct TestRgba : ITestPixel<TestRgba>
    {
        public byte R;
        public byte G;
        public byte B;
        public byte A;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void FromRgba32(Rgba32 source)
        {
            this = Unsafe.As<Rgba32, TestRgba>(ref source);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void FromRgba32(ref Rgba32 source)
        {
            this = Unsafe.As<Rgba32, TestRgba>(ref source);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void FromBytes(byte r, byte g, byte b, byte a)
        {
            this.R = r;
            this.G = g;
            this.B = b;
            this.A = a;
        }

        public void FromVector4(Vector4 source)
        {
            throw new System.NotImplementedException();
        }

        public void FromVector4(ref Vector4 source)
        {
            throw new System.NotImplementedException();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Rgba32 ToRgba32()
        {
            return Unsafe.As<TestRgba, Rgba32>(ref this);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyToRgba32(ref Rgba32 dest)
        {
            dest = Unsafe.As<TestRgba, Rgba32>(ref this);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector4 ToVector4()
        {
            return new Vector4(this.R, this.G, this.B, this.A) * new Vector4(1f / 255f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyToVector4(ref Vector4 dest)
        {
            var tmp = new Vector4(this.R, this.G, this.B, this.A);
            tmp *= new Vector4(1f / 255f);
            dest = tmp;
        }
    }
}
