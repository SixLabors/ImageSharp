namespace ImageSharp.Benchmarks.General
{
    using System.Numerics;
    using System.Runtime.CompilerServices;

    using BenchmarkDotNet.Attributes;

    public abstract unsafe class PixelIndexing
    {
        /// <summary>
        /// https://github.com/dotnet/corefx/blob/master/src/System.Memory/src/System/Pinnable.cs
        /// </summary>
        protected class Pinnable<T>
        {
            public T Data;
        }

        /// <summary>
        /// The indexer methods are encapsulated into a struct to make sure everything is inlined.
        /// </summary>
        internal struct Data
        {
            private Vector4* pointer;

            private Pinnable<Vector4> pinnable;

            private int width;

            public Data(PinnedImageBuffer<Vector4> buffer)
            {
                this.pointer = (Vector4*)buffer.Pointer;
                this.pinnable = Unsafe.As<Pinnable<Vector4>>(buffer.Array);
                this.width = buffer.Width;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Vector4 GetPointersBasicImpl(int x, int y)
            {
                return this.pointer[y * this.width + x];
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Vector4 GetPointersSrcsUnsafeImpl(int x, int y)
            {
                return Unsafe.Read<Vector4>((byte*)this.pointer + (((y * this.width) + x) * Unsafe.SizeOf<Vector4>()));
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Vector4 GetReferencesImpl(int x, int y)
            {
                int elementOffset = (y * this.width) + x;
                return Unsafe.Add(ref this.pinnable.Data, elementOffset);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ref Vector4 GetReferencesRefReturnsImpl(int x, int y)
            {
                int elementOffset = (y * this.width) + x;
                return ref Unsafe.Add(ref this.pinnable.Data, elementOffset);
            }


            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void IndexWithPointersBasicImpl(int x, int y, Vector4 v)
            {
                this.pointer[y * this.width + x] = v;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void IndexWithPointersSrcsUnsafeImpl(int x, int y, Vector4 v)
            {
                Unsafe.Write((byte*)this.pointer + (((y * this.width) + x) * Unsafe.SizeOf<Vector4>()), v);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void IndexWithReferencesImpl(int x, int y, Vector4 v)
            {
                int elementOffset = (y * this.width) + x;
                Unsafe.Add(ref this.pinnable.Data, elementOffset) = v;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ref Vector4 IndexWithReferencesRefReturnImpl(int x, int y)
            {
                int elementOffset = (y * this.width) + x;
                return ref Unsafe.Add(ref this.pinnable.Data, elementOffset);
            }
        }

        internal PinnedImageBuffer<Vector4> buffer;

        protected int width;

        protected int startIndex;

        protected int endIndex;

        protected Vector4* pointer;

        protected Vector4[] array;

        protected Pinnable<Vector4> pinnable;

        [Params(64, 256, 1024)]
        public int Count { get; set; }

        [Setup]
        public void Setup()
        {
            this.width = 2048;
            this.buffer = new PinnedImageBuffer<Vector4>(2048, 2048);
            this.pointer = (Vector4*)this.buffer.Pointer;
            this.array = this.buffer.Array;
            this.pinnable = Unsafe.As<Pinnable<Vector4>>(this.array);

            this.startIndex = 2048 / 2 - (this.Count / 2);
            this.endIndex = 2048 / 2 + (this.Count / 2);
        }

        [Cleanup]
        public void Cleanup()
        {
            this.buffer.Dispose();
        }

    }

    public unsafe class PixelIndexingGetter : PixelIndexing
    {
        [Benchmark(Description = "Index.Get: Pointers+arithmetics", Baseline = true)]
        public Vector4 IndexWithPointersBasic()
        {
            Vector4 sum = Vector4.Zero;
            Data data = new Data(this.buffer);

            for (int i = this.startIndex; i < this.endIndex; i++)
            {
                sum += data.GetPointersBasicImpl(i, i);
            }

            return sum;
        }

        [Benchmark(Description = "Index.Get: Pointers+SRCS.Unsafe")]
        public Vector4 IndexWithPointersSrcsUnsafe()
        {
            Vector4 sum = Vector4.Zero;
            Data data = new Data(this.buffer);

            for (int i = this.startIndex; i < this.endIndex; i++)
            {
                sum += data.GetPointersSrcsUnsafeImpl(i, i);
            }

            return sum;
        }

        [Benchmark(Description = "Index.Get: References")]
        public Vector4 IndexWithReferences()
        {
            Vector4 sum = Vector4.Zero;
            Data data = new Data(this.buffer);

            for (int i = this.startIndex; i < this.endIndex; i++)
            {
                sum += data.GetReferencesImpl(i, i);
            }

            return sum;
        }

        [Benchmark(Description = "Index.Get: References+refreturns")]
        public Vector4 IndexWithReferencesRefReturns()
        {
            Vector4 sum = Vector4.Zero;
            Data data = new Data(this.buffer);

            for (int i = this.startIndex; i < this.endIndex; i++)
            {
                sum += data.GetReferencesRefReturnsImpl(i, i);
            }

            return sum;
        }
    }

    public unsafe class PixelIndexingSetter : PixelIndexing
    {
        [Benchmark(Description = "Index.Set: Pointers+arithmetics", Baseline = true)]
        public void IndexWithPointersBasic()
        {
            Vector4 v = new Vector4(1, 2, 3, 4);
            Data data = new Data(this.buffer);

            for (int i = this.startIndex; i < this.endIndex; i++)
            {
                data.IndexWithPointersBasicImpl(i, i, v);
            }
        }

        [Benchmark(Description = "Index.Set: Pointers+SRCS.Unsafe")]
        public void IndexWithPointersSrcsUnsafe()
        {
            Vector4 v = new Vector4(1, 2, 3, 4);
            Data data = new Data(this.buffer);

            for (int i = this.startIndex; i < this.endIndex; i++)
            {
                data.IndexWithPointersSrcsUnsafeImpl(i, i, v);
            }
        }

        [Benchmark(Description = "Index.Set: References")]
        public void IndexWithReferencesBasic()
        {
            Vector4 v = new Vector4(1, 2, 3, 4);
            Data data = new Data(this.buffer);

            for (int i = this.startIndex; i < this.endIndex; i++)
            {
                data.IndexWithReferencesImpl(i, i, v);
            }
        }

        [Benchmark(Description = "Index.Set: References+refreturn")]
        public void IndexWithReferencesRefReturn()
        {
            Vector4 v = new Vector4(1, 2, 3, 4);
            Data data = new Data(this.buffer);

            for (int i = this.startIndex; i < this.endIndex; i++)
            {
                data.IndexWithReferencesRefReturnImpl(i, i) = v;
            }
        }

    }
}