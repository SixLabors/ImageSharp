// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Benchmarks.General.PixelConversion
{
    [StructLayout(LayoutKind.Sequential)]
    public struct TestArgb : ITestPixel<TestArgb>
    {
        public byte A;
        public byte R;
        public byte G;
        public byte B;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void FromRgba32(Rgba32 p)
        {
            this.R = p.R;
            this.G = p.G;
            this.B = p.B;
            this.A = p.A;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void FromRgba32(ref Rgba32 p)
        {
            this.R = p.R;
            this.G = p.G;
            this.B = p.B;
            this.A = p.A;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void FromBytes(byte r, byte g, byte b, byte a)
        {
            this.R = r;
            this.G = g;
            this.B = b;
            this.A = a;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void FromVector4(Vector4 p)
        {
            this.R = (byte)p.X;
            this.G = (byte)p.Y;
            this.B = (byte)p.Z;
            this.A = (byte)p.W;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void FromVector4(ref Vector4 p)
        {
            this.R = (byte)p.X;
            this.G = (byte)p.Y;
            this.B = (byte)p.Z;
            this.A = (byte)p.W;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Rgba32 ToRgba32()
        {
            return new Rgba32(this.R, this.G, this.B, this.A);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyToRgba32(ref Rgba32 dest)
        {
            dest.R = this.R;
            dest.G = this.G;
            dest.B = this.B;
            dest.A = this.A;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector4 ToVector4()
        {
            return new Vector4(this.R, this.G, this.B, this.A);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyToVector4(ref Vector4 dest)
        {
            dest.X = this.R;
            dest.Y = this.G;
            dest.Z = this.B;
            dest.W = this.A;
        }
    }
}
