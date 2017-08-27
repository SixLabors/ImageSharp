// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Tests.Memory
{
    using System;
    using System.Runtime.CompilerServices;

    using SixLabors.ImageSharp.Memory;

    using Xunit;

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
            using (Buffer<TestStructs.Foo> buffer = new Buffer<TestStructs.Foo>(count))
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
            TestStructs.Foo[] array = new TestStructs.Foo[count];
            using (Buffer<TestStructs.Foo> buffer = new Buffer<TestStructs.Foo>(array))
            {
                Assert.False(buffer.IsDisposedOrLostArrayOwnership);
                Assert.Equal(array, buffer.Array);
                Assert.Equal(count, buffer.Length);
            }
        }

        [Fact]
        public void Clear()
        {
            TestStructs.Foo[] a = { new TestStructs.Foo() { A = 1, B = 2 }, new TestStructs.Foo() { A = 3, B = 4 } };
            using (Buffer<TestStructs.Foo> buffer = new Buffer<TestStructs.Foo>(a))
            {
                buffer.Clear();

                Assert.Equal(default(TestStructs.Foo), a[0]);
                Assert.Equal(default(TestStructs.Foo), a[1]);
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
                TestStructs.Foo[] a = TestStructs.Foo.CreateArray(length);
                
                using (Buffer<TestStructs.Foo> buffer = new Buffer<TestStructs.Foo>(a))
                {
                    TestStructs.Foo element = buffer[index];

                    Assert.Equal(a[index], element);
                }
            }

            [Theory]
            [MemberData(nameof(IndexerData))]
            public void Write(int length, int index)
            {
                TestStructs.Foo[] a = TestStructs.Foo.CreateArray(length);

                using (Buffer<TestStructs.Foo> buffer = new Buffer<TestStructs.Foo>(a))
                {
                    buffer[index] = new TestStructs.Foo(666, 666);

                    Assert.Equal(new TestStructs.Foo(666, 666), a[index]);
                }
            }
        }

        [Fact]
        public void Dispose()
        {
            Buffer<TestStructs.Foo> buffer = new Buffer<TestStructs.Foo>(42);
            buffer.Dispose();

            Assert.True(buffer.IsDisposedOrLostArrayOwnership);
        }
        
        [Theory]
        [InlineData(7)]
        [InlineData(123)]
        public void CastToSpan(int bufferLength)
        {
            using (Buffer<TestStructs.Foo> buffer = new Buffer<TestStructs.Foo>(bufferLength))
            {
                Span<TestStructs.Foo> span = buffer;

                //Assert.Equal(buffer.Array, span.ToArray());
                //Assert.Equal(0, span.Start);
                Assert.SpanPointsTo(span, buffer);
                Assert.Equal(span.Length, bufferLength);
            }
        }

        [Fact]
        public void Span()
        {
            using (Buffer<TestStructs.Foo> buffer = new Buffer<TestStructs.Foo>(42))
            {
                Span<TestStructs.Foo> span = buffer.Span;

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
                using (Buffer<TestStructs.Foo> buffer = new Buffer<TestStructs.Foo>(bufferLength))
                {
                    Span<TestStructs.Foo> span = buffer.Slice(start);

                    Assert.SpanPointsTo(span, buffer, start);
                    Assert.Equal(span.Length, bufferLength - start);
                }
            }

            [Theory]
            [InlineData(7, 2, 5)]
            [InlineData(123, 17, 42)]
            public void WithStartAndLength(int bufferLength, int start, int spanLength)
            {
                using (Buffer<TestStructs.Foo> buffer = new Buffer<TestStructs.Foo>(bufferLength))
                {
                    Span<TestStructs.Foo> span = buffer.Slice(start, spanLength);

                    Assert.SpanPointsTo(span, buffer, start);
                    Assert.Equal(span.Length, spanLength);
                }
            }
        }
        
        [Fact]
        public void UnPinAndTakeArrayOwnership()
        {
            TestStructs.Foo[] data = null;
            using (Buffer<TestStructs.Foo> buffer = new Buffer<TestStructs.Foo>(42))
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
                using (Buffer<TestStructs.Foo> buffer = new Buffer<TestStructs.Foo>(42))
                {
                    TestStructs.Foo* actual = (TestStructs.Foo*)buffer.Pin();
                    fixed (TestStructs.Foo* expected = buffer.Array)
                    {
                        Assert.Equal(expected, actual);
                    }
                }
            }

            [Fact]
            public void SecondCallReturnsTheSamePointer()
            {
                using (Buffer<TestStructs.Foo> buffer = new Buffer<TestStructs.Foo>(42))
                {
                    IntPtr ptr1 = buffer.Pin();
                    IntPtr ptr2 = buffer.Pin();

                    Assert.Equal(ptr1, ptr2);
                }
            }

            [Fact]
            public void WhenCalledOnDisposedBuffer_ThrowsInvalidOperationException()
            {
                Buffer<TestStructs.Foo> buffer = new Buffer<TestStructs.Foo>(42);
                buffer.Dispose();

                Assert.Throws<InvalidOperationException>(() => buffer.Pin());
            }
        }
    }
}