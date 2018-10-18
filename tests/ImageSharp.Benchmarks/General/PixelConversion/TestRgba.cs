using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Benchmarks.General.PixelConversion
{
    [StructLayout(LayoutKind.Sequential)]
    struct TestRgba : ITestPixel<TestRgba>
    {
        private byte r, g, b, a;

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
            this.r = r;
            this.g = g;
            this.b = b;
            this.a = a;
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
    }
}