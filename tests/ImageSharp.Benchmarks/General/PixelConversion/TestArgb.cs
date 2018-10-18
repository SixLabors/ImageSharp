using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Benchmarks.General.PixelConversion
{
    [StructLayout(LayoutKind.Sequential)]
    struct TestArgb : ITestPixel<TestArgb>
    {
        private byte a, r, g, b;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void FromRgba32(Rgba32 p)
        {
            this.r = p.R;
            this.g = p.G;
            this.b = p.B;
            this.a = p.A;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void FromRgba32(ref Rgba32 p)
        {
            this.r = p.R;
            this.g = p.G;
            this.b = p.B;
            this.a = p.A;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void FromBytes(byte r, byte g, byte b, byte a)
        {
            this.r = r;
            this.g = g;
            this.b = b;
            this.a = a;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void FromVector4(Vector4 p)
        {
            this.r = (byte)p.X;
            this.g = (byte)p.Y;
            this.b = (byte)p.Z;
            this.a = (byte)p.W;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void FromVector4(ref Vector4 p)
        {
            this.r = (byte)p.X;
            this.g = (byte)p.Y;
            this.b = (byte)p.Z;
            this.a = (byte)p.W;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Rgba32 ToRgba32()
        {
            return new Rgba32(this.r, this.g, this.b, this.a);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyToRgba32(ref Rgba32 dest)
        {
            dest.R = this.r;
            dest.G = this.g;
            dest.B = this.b;
            dest.A = this.a;
        }
    }
}