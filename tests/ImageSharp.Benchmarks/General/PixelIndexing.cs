namespace SixLabors.ImageSharp.Benchmarks.General
{
    using System.Numerics;
    using System.Runtime.CompilerServices;

    using BenchmarkDotNet.Attributes;

    using SixLabors.ImageSharp.Memory;

    // Pixel indexing benchmarks compare different methods for getting/setting all pixel values in a subsegment of a single pixel row.
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

            private Vector4[] array;

            private int width;

            public Data(Buffer2D<Vector4> buffer)
            {
                this.pointer = (Vector4*)buffer.Pin();
                this.pinnable = Unsafe.As<Pinnable<Vector4>>(buffer.Array);
                this.array = buffer.Array;
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
                // This is the original solution in PixelAccessor:
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
            public void IndexWithReferencesOnPinnableIncorrectImpl(int x, int y, Vector4 v)
            {
                int elementOffset = (y * this.width) + x;
                // Incorrect, because also should add a runtime-specific byte offset here. See https://github.com/dotnet/corefx/blob/master/src/System.Memory/src/System/Span.cs#L68
                Unsafe.Add(ref this.pinnable.Data, elementOffset) = v;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ref Vector4 IndexWithReferencesOnPinnableIncorrectRefReturnImpl(int x, int y)
            {
                int elementOffset = (y * this.width) + x;
                // Incorrect, because also should add a runtime-specific byte offset here. See https://github.com/dotnet/corefx/blob/master/src/System.Memory/src/System/Span.cs#L68
                return ref Unsafe.Add(ref this.pinnable.Data, elementOffset);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void IndexWithUnsafeReferenceArithmeticsOnArray0Impl(int x, int y, Vector4 v)
            {
                int elementOffset = (y * this.width) + x;
                Unsafe.Add(ref this.array[0], elementOffset) = v;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ref Vector4 IndexWithUnsafeReferenceArithmeticsOnArray0RefReturnImpl(int x, int y)
            {
                int elementOffset = (y * this.width) + x;
                return ref Unsafe.Add(ref this.array[0], elementOffset);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void IndexSetArrayStraightforward(int x, int y, Vector4 v)
            {
                // No magic.
                // We just index right into the array as normal people do.
                // And it looks like this is the fastest way!
                this.array[(y * this.width) + x] = v;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ref Vector4 IndexWithReferencesOnArrayStraightforwardRefReturnImpl(int x, int y)
            {
                // No magic.
                // We just index right into the array as normal people do.
                // And it looks like this is the fastest way!
                return ref this.array[(y * this.width) + x];
            }
        }

        internal Buffer2D<Vector4> buffer;

        protected int width;

        protected int startIndex;

        protected int endIndex;

        protected Vector4* pointer;

        protected Vector4[] array;

        protected Pinnable<Vector4> pinnable;

        // [Params(1024)]
        public int Count { get; set; } = 1024;

        [GlobalSetup]
        public void Setup()
        {
            this.width = 2048;
            this.buffer = new Buffer2D<Vector4>(2048, 2048);
            this.pointer = (Vector4*)this.buffer.Pin();
            this.array = this.buffer.Array;
            this.pinnable = Unsafe.As<Pinnable<Vector4>>(this.array);

            this.startIndex = 2048 / 2 - (this.Count / 2);
            this.endIndex = 2048 / 2 + (this.Count / 2);
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            this.buffer.Dispose();
        }

    }

    public class PixelIndexingGetter : PixelIndexing
    {
        [Benchmark(Description = "Index.Get: Pointers+arithmetics", Baseline = true)]
        public Vector4 IndexWithPointersBasic()
        {
            Vector4 sum = Vector4.Zero;
            Data data = new Data(this.buffer);
            int y = this.startIndex;
            for (int x = this.startIndex; x < this.endIndex; x++)
            {
                sum += data.GetPointersBasicImpl(x, y);
            }

            return sum;
        }

        [Benchmark(Description = "Index.Get: Pointers+SRCS.Unsafe")]
        public Vector4 IndexWithPointersSrcsUnsafe()
        {
            Vector4 sum = Vector4.Zero;
            Data data = new Data(this.buffer);

            int y = this.startIndex;
            for (int x = this.startIndex; x < this.endIndex; x++)
            {
                sum += data.GetPointersSrcsUnsafeImpl(x, y);
            }

            return sum;
        }

        [Benchmark(Description = "Index.Get: References")]
        public Vector4 IndexWithReferences()
        {
            Vector4 sum = Vector4.Zero;
            Data data = new Data(this.buffer);

            int y = this.startIndex;
            for (int x = this.startIndex; x < this.endIndex; x++)
            {
                sum += data.GetReferencesImpl(x, y);
            }

            return sum;
        }

        [Benchmark(Description = "Index.Get: References|refreturns")]
        public Vector4 IndexWithReferencesRefReturns()
        {
            Vector4 sum = Vector4.Zero;
            Data data = new Data(this.buffer);

            int y = this.startIndex;
            for (int x = this.startIndex; x < this.endIndex; x++)
            {
                sum += data.GetReferencesRefReturnsImpl(x, y);
            }

            return sum;
        }
    }

    public class PixelIndexingSetter : PixelIndexing
    {
        [Benchmark(Description = "!!! Index.Set: Pointers|arithmetics", Baseline = true)]
        public void IndexWithPointersBasic()
        {
            Vector4 v = new Vector4(1, 2, 3, 4);
            Data data = new Data(this.buffer);

            int y = this.startIndex;
            for (int x = this.startIndex; x < this.endIndex; x++)
            {
                data.IndexWithPointersBasicImpl(x, y, v);
            }
        }

        [Benchmark(Description = "Index.Set: Pointers|SRCS.Unsafe")]
        public void IndexWithPointersSrcsUnsafe()
        {
            Vector4 v = new Vector4(1, 2, 3, 4);
            Data data = new Data(this.buffer);

            int y = this.startIndex;
            for (int x = this.startIndex; x < this.endIndex; x++)
            {
                data.IndexWithPointersSrcsUnsafeImpl(x, y, v);
            }
        }

        [Benchmark(Description = "Index.Set: References|IncorrectPinnable")]
        public void IndexWithReferencesPinnableBasic()
        {
            Vector4 v = new Vector4(1, 2, 3, 4);
            Data data = new Data(this.buffer);

            int y = this.startIndex;
            for (int x = this.startIndex; x < this.endIndex; x++)
            {
                data.IndexWithReferencesOnPinnableIncorrectImpl(x, y, v);
            }
        }

        [Benchmark(Description = "Index.Set: References|IncorrectPinnable|refreturn")]
        public void IndexWithReferencesPinnableRefReturn()
        {
            Vector4 v = new Vector4(1, 2, 3, 4);
            Data data = new Data(this.buffer);

            int y = this.startIndex;
            for (int x = this.startIndex; x < this.endIndex; x++)
            {
                data.IndexWithReferencesOnPinnableIncorrectRefReturnImpl(x, y) = v;
            }
        }

        [Benchmark(Description = "Index.Set: References|Array[0]Unsafe")]
        public void IndexWithReferencesArrayBasic()
        {
            Vector4 v = new Vector4(1, 2, 3, 4);
            Data data = new Data(this.buffer);

            int y = this.startIndex;
            for (int x = this.startIndex; x < this.endIndex; x++)
            {
                data.IndexWithUnsafeReferenceArithmeticsOnArray0Impl(x, y, v);
            }
        }

        [Benchmark(Description = "Index.Set: References|Array[0]Unsafe|refreturn")]
        public void IndexWithReferencesArrayRefReturn()
        {
            Vector4 v = new Vector4(1, 2, 3, 4);
            Data data = new Data(this.buffer);

            int y = this.startIndex;
            for (int x = this.startIndex; x < this.endIndex; x++)
            {
                data.IndexWithUnsafeReferenceArithmeticsOnArray0RefReturnImpl(x, y) = v;
            }
        }

        [Benchmark(Description = "!!! Index.Set: References|Array+Straight")]
        public void IndexWithReferencesArrayStraightforward()
        {
            Vector4 v = new Vector4(1, 2, 3, 4);
            Data data = new Data(this.buffer);

            int y = this.startIndex;
            for (int x = this.startIndex; x < this.endIndex; x++)
            {
                // No magic.
                // We just index right into the array as normal people do.
                // And it looks like this is the fastest way!
                data.IndexSetArrayStraightforward(x, y, v);
            }
        }


        [Benchmark(Description = "!!! Index.Set: References|Array+Straight|refreturn")]
        public void IndexWithReferencesArrayStraightforwardRefReturn()
        {
            Vector4 v = new Vector4(1, 2, 3, 4);
            Data data = new Data(this.buffer);
            
            int y = this.startIndex;
            for (int x = this.startIndex; x < this.endIndex; x++)
            {
                // No magic.
                // We just index right into the array as normal people do.
                // And it looks like this is the fastest way!
                data.IndexWithReferencesOnArrayStraightforwardRefReturnImpl(x, y) = v;
            }
        }

        [Benchmark(Description = "!!! Index.Set: SmartUnsafe")]
        public void SmartUnsafe()
        {
            Vector4 v = new Vector4(1, 2, 3, 4);
            Data data = new Data(this.buffer);

            // This method is basically an unsafe variant of .GetRowSpan(y) + indexing individual pixels in the row.
            // If a user seriously needs by-pixel manipulation to be performant, we should provide this option.
            
            ref Vector4 rowStart = ref data.IndexWithReferencesOnArrayStraightforwardRefReturnImpl(this.startIndex, this.startIndex);

            for (int i = 0; i < this.Count; i++)
            {
                // We don't have to add 'Width * y' here!
                Unsafe.Add(ref rowStart, i) = v;
            }
        }
    }
}