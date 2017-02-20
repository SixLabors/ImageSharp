using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ImageSharp.Benchmarks.Color.Bulk
{
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;

    using BenchmarkDotNet.Attributes;

    using Color = ImageSharp.Color;

    /// <summary>
    /// Benchmark to measure the effect of using virtual bulk-copy calls inside PixelAccessor methods
    /// </summary>
    public unsafe class PixelAccessorVirtualCopy
    {
        abstract class CopyExecutor
        {
            internal abstract void VirtualCopy(ArrayPointer<Color> destination, ArrayPointer<byte> source, int count);
        }

        class UnsafeCopyExecutor : CopyExecutor
        {
            [MethodImpl(MethodImplOptions.NoInlining)]
            internal override unsafe void VirtualCopy(ArrayPointer<Color> destination, ArrayPointer<byte> source, int count)
            {
                Unsafe.CopyBlock((void*)destination.PointerAtOffset, (void*)source.PointerAtOffset, (uint)count*4);
            }
        }
        
        private PixelAccessor<Color> pixelAccessor;

        private PixelArea<Color> area;

        private CopyExecutor executor;
        
        [Params(64, 256)]
        public int Width { get; set; }

        public int Height { get; set; } = 256;
        

        [Setup]
        public void Setup()
        {
            this.pixelAccessor = new PixelAccessor<ImageSharp.Color>(this.Width, this.Height);
            this.area = new PixelArea<Color>(this.Width / 2, this.Height, ComponentOrder.Xyzw);
            this.executor = new UnsafeCopyExecutor();
        }

        [Cleanup]
        public void Cleanup()
        {
            this.pixelAccessor.Dispose();
            this.area.Dispose();
        }

        [Benchmark(Baseline = true)]
        public void CopyRawUnsafeInlined()
        {
            uint byteCount = (uint)this.area.Width * 4;

            int targetX = this.Width / 4;
            int targetY = 0;

            for (int y = 0; y < this.Height; y++)
            {
                byte* source = this.area.PixelBase + (y * this.area.RowStride);
                byte* destination = this.GetRowPointer(targetX, targetY + y);

                Unsafe.CopyBlock(destination, source, byteCount);
            }
        }
        
        [Benchmark]
        public void CopyArrayPointerUnsafeInlined()
        {
            uint byteCount = (uint)this.area.Width * 4;

            int targetX = this.Width / 4;
            int targetY = 0;

            for (int y = 0; y < this.Height; y++)
            {
                ArrayPointer<byte> source = this.GetAreaRow(y);
                ArrayPointer<Color> destination = this.GetPixelAccessorRow(targetX, targetY + y);
                Unsafe.CopyBlock((void*)destination.PointerAtOffset, (void*)source.PointerAtOffset, byteCount);
            }
        }
        
        [Benchmark]
        public void CopyArrayPointerUnsafeVirtual()
        {
            int targetX = this.Width / 4;
            int targetY = 0;

            for (int y = 0; y < this.Height; y++)
            {
                ArrayPointer<byte> source = this.GetAreaRow(y);
                ArrayPointer<Color> destination = this.GetPixelAccessorRow(targetX, targetY + y);
                this.executor.VirtualCopy(destination, source, this.area.Width);
            }
        }
        
        private byte* GetRowPointer(int x, int y)
        {
            return (byte*)this.pixelAccessor.DataPointer + (((y * this.pixelAccessor.Width) + x) * Unsafe.SizeOf<Color>());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ArrayPointer<Color> GetPixelAccessorRow(int x, int y)
        {
            return new ArrayPointer<ImageSharp.Color>(
                this.pixelAccessor.PixelBuffer,
                (void*)this.pixelAccessor.DataPointer,
                (y * this.pixelAccessor.Width) + x
                );
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ArrayPointer<byte> GetAreaRow(int y)
        {
            return new ArrayPointer<byte>(this.area.Bytes, this.area.PixelBase, y * this.area.RowStride);
        }
    }
}
