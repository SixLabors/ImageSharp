// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using SixLabors.ImageSharp.Memory;
using Xunit;
using static SixLabors.ImageSharp.Tests.Common.TestStructs;

namespace SixLabors.ImageSharp.Tests.Common
{
    public unsafe class BufferTests
    {
        // ReSharper disable once ClassNeverInstantiated.Local
        private class Assert : Xunit.Assert
        {
            public static void SpanPointsTo<T>(Span<T> span, Buffer<T> buffer, int bufferOffset = 0)
                where T : struct
            {
                ref T actual = ref span.DangerousGetPinnableReference();
                ref T expected = ref Unsafe.Add(ref buffer[0], bufferOffset);

                Assert.True(Unsafe.AreSame(ref expected, ref actual), "span does not point to the expected position");
            }

            public static void Equal(void* expected, void* actual)
            {
                Assert.Equal((IntPtr)expected, (IntPtr)actual);
            }
        }

        [Theory]
        [InlineData(42)]
        [InlineData(1111)]
        public void ConstructWithOwnArray(int count)
        {
            using (Buffer<Foo> buffer = new Buffer<Foo>(count))
            {
                Assert.False(buffer.IsDisposedOrLostArrayOwnership);
                Assert.NotNull(buffer.Array);
                Assert.Equal(count, buffer.Length);
                Assert.True(buffer.Array.Length >= count);
            }
        }
        
        [Theory]
        [InlineData(42)]
        [InlineData(1111)]
        public void ConstructWithExistingArray(int count)
        {
            Foo[] array = new Foo[count];
            using (Buffer<Foo> buffer = new Buffer<Foo>(array))
            {
                Assert.False(buffer.IsDisposedOrLostArrayOwnership);
                Assert.Equal(array, buffer.Array);
                Assert.Equal(count, buffer.Length);
            }
        }

        [Fact]
        public void Clear()
        {
            Foo[] a = { new Foo() { A = 1, B = 2 }, new Foo() { A = 3, B = 4 } };
            using (Buffer<Foo> buffer = new Buffer<Foo>(a))
            {
                buffer.Clear();

                Assert.Equal(default(Foo), a[0]);
                Assert.Equal(default(Foo), a[1]);
            }
        }

        [Fact]
        public void CreateClean()
        {
            for (int i = 0; i < 100; i++)
            {
                using (Buffer<int> buffer = Buffer<int>.CreateClean(42))
                {
                    for (int j = 0; j < buffer.Length; j++)
                    {
                        Assert.Equal(0, buffer.Array[j]);
                        buffer.Array[j] = 666;
                    }
                }
            }
        }

        public class Indexer
        {
            public static readonly TheoryData<int, int> IndexerData =
                new TheoryData<int,int>()
                    {
                        { 10, 0 },
                        { 16, 3 },
                        { 10, 9 }
                    };

            [Theory]
            [MemberData(nameof(IndexerData))]
            public void Read(int length, int index)
            {
                Foo[] a = Foo.CreateArray(length);
                
                using (Buffer<Foo> buffer = new Buffer<Foo>(a))
                {
                    Foo element = buffer[index];

                    Assert.Equal(a[index], element);
                }
            }

            [Theory]
            [MemberData(nameof(IndexerData))]
            public void Write(int length, int index)
            {
                Foo[] a = Foo.CreateArray(length);

                using (Buffer<Foo> buffer = new Buffer<Foo>(a))
                {
                    buffer[index] = new Foo(666, 666);

                    Assert.Equal(new Foo(666, 666), a[index]);
                }
            }
        }

        [Fact]
        public void Dispose()
        {
            Buffer<Foo> buffer = new Buffer<Foo>(42);
            buffer.Dispose();

            Assert.True(buffer.IsDisposedOrLostArrayOwnership);
        }
        
        [Theory]
        [InlineData(7)]
        [InlineData(123)]
        public void CastToSpan(int bufferLength)
        {
            using (Buffer<Foo> buffer = new Buffer<Foo>(bufferLength))
            {
                Span<Foo> span = buffer;

                //Assert.Equal(buffer.Array, span.ToArray());
                //Assert.Equal(0, span.Start);
                Assert.SpanPointsTo(span, buffer);
                Assert.Equal(span.Length, bufferLength);
            }
        }

        [Fact]
        public void Span()
        {
            using (Buffer<Foo> buffer = new Buffer<Foo>(42))
            {
                Span<Foo> span = buffer.Span;

                // Assert.Equal(buffer.Array, span.ToArray());
                // Assert.Equal(0, span.Start);
                Assert.SpanPointsTo(span, buffer);
                Assert.Equal(42, span.Length);
            }
        }

        public class Slice
        {

            [Theory]
            [InlineData(7, 2)]
            [InlineData(123, 17)]
            public void WithStartOnly(int bufferLength, int start)
            {
                using (Buffer<Foo> buffer = new Buffer<Foo>(bufferLength))
                {
                    Span<Foo> span = buffer.Slice(start);

                    Assert.SpanPointsTo(span, buffer, start);
                    Assert.Equal(span.Length, bufferLength - start);
                }
            }

            [Theory]
            [InlineData(7, 2, 5)]
            [InlineData(123, 17, 42)]
            public void WithStartAndLength(int bufferLength, int start, int spanLength)
            {
                using (Buffer<Foo> buffer = new Buffer<Foo>(bufferLength))
                {
                    Span<Foo> span = buffer.Slice(start, spanLength);

                    Assert.SpanPointsTo(span, buffer, start);
                    Assert.Equal(span.Length, spanLength);
                }
            }
        }
        
        [Fact]
        public void UnPinAndTakeArrayOwnership()
        {
            Foo[] data = null;
            using (Buffer<Foo> buffer = new Buffer<Foo>(42))
            {
                data = buffer.TakeArrayOwnership();
                Assert.True(buffer.IsDisposedOrLostArrayOwnership);
            }

            Assert.NotNull(data);
            Assert.True(data.Length >= 42);
        }

        public class Pin
        {
            [Fact]
            public void ReturnsPinnedPointerToTheBeginningOfArray()
            {
                using (Buffer<Foo> buffer = new Buffer<Foo>(42))
                {
                    Foo* actual = (Foo*)buffer.Pin();
                    fixed (Foo* expected = buffer.Array)
                    {
                        Assert.Equal(expected, actual);
                    }
                }
            }

            [Fact]
            public void SecondCallReturnsTheSamePointer()
            {
                using (Buffer<Foo> buffer = new Buffer<Foo>(42))
                {
                    IntPtr ptr1 = buffer.Pin();
                    IntPtr ptr2 = buffer.Pin();

                    Assert.Equal(ptr1, ptr2);
                }
            }

            [Fact]
            public void WhenCalledOnDisposedBuffer_ThrowsInvalidOperationException()
            {
                Buffer<Foo> buffer = new Buffer<Foo>(42);
                buffer.Dispose();

                Assert.Throws<InvalidOperationException>(() => buffer.Pin());
            }
        }
    }
}