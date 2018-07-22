// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Memory;

using Xunit;
// ReSharper disable InconsistentNaming

namespace SixLabors.ImageSharp.Tests.Memory
{
    public class BufferManagerTests
    {
        public class Construction
        {
            [Fact]
            public void InitializeAsOwner_MemoryOwner_IsPresent()
            {
                var data = new Rgba32[21];
                var mmg = new TestMemoryManager<Rgba32>(data);

                var a = new BufferManager<Rgba32>(mmg);

                Assert.Equal(mmg, a.MemoryOwner);
                Assert.Equal(mmg.Memory, a.Memory);
                Assert.True(a.OwnsMemory);
            }

            [Fact]
            public void InitializeAsObserver_MemoryOwner_IsNull()
            {
                var data = new Rgba32[21];
                var mmg = new TestMemoryManager<Rgba32>(data);

                var a = new BufferManager<Rgba32>(mmg.Memory);

                Assert.Null(a.MemoryOwner);
                Assert.Equal(mmg.Memory, a.Memory);
                Assert.False(a.OwnsMemory);
            }
        }

        public class Dispose
        {
            [Fact]
            public void WhenOwnershipIsTransfered_ShouldDisposeMemoryOwner()
            {
                var mmg = new TestMemoryManager<int>(new int[10]);
                var bmg = new BufferManager<int>(mmg);
                
                bmg.Dispose();
                Assert.True(mmg.IsDisposed);
            }

            [Fact]
            public void WhenMemoryObserver_ShouldNotDisposeAnything()
            {
                var mmg = new TestMemoryManager<int>(new int[10]);
                var bmg = new BufferManager<int>(mmg.Memory);

                bmg.Dispose();
                Assert.False(mmg.IsDisposed);
            }
        }
        
        public class SwapOrCopyContent
        {
            private MemoryAllocator MemoryAllocator { get; } = new TestMemoryAllocator();

            private BufferManager<T> AllocateBufferManager<T>(int length, AllocationOptions options = AllocationOptions.None)
                where T : struct
            {
                var owner = (IMemoryOwner<T>)this.MemoryAllocator.Allocate<T>(length, options);
                return new BufferManager<T>(owner);
            }

            [Fact]
            public void WhenBothAreMemoryOwners_ShouldSwap()
            {
                BufferManager<int> a = this.AllocateBufferManager<int>(13);
                BufferManager<int> b = this.AllocateBufferManager<int>(17);

                IMemoryOwner<int> aa = a.MemoryOwner;
                IMemoryOwner<int> bb = b.MemoryOwner;

                Memory<int> aaa = a.Memory;
                Memory<int> bbb = b.Memory;

                BufferManager<int>.SwapOrCopyContent(ref a, ref b);

                Assert.Equal(bb, a.MemoryOwner);
                Assert.Equal(aa, b.MemoryOwner);

                Assert.Equal(bbb, a.Memory);
                Assert.Equal(aaa, b.Memory);
                Assert.NotEqual(a.Memory, b.Memory);
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public void WhenDestIsNotMemoryOwner_SameSize_ShouldCopy(bool sourceIsOwner)
            {
                var data = new Rgba32[21];
                var color = new Rgba32(1, 2, 3, 4);

                var destOwner = new TestMemoryManager<Rgba32>(data);
                var dest = new BufferManager<Rgba32>(destOwner.Memory);

                var sourceOwner = (IMemoryOwner<Rgba32>)this.MemoryAllocator.Allocate<Rgba32>(21);

                BufferManager<Rgba32> source = sourceIsOwner
                                                   ? new BufferManager<Rgba32>(sourceOwner)
                                                   : new BufferManager<Rgba32>(sourceOwner.Memory);
                
                sourceOwner.Memory.Span[10] = color;

                // Act:
                BufferManager<Rgba32>.SwapOrCopyContent(ref dest, ref source);

                // Assert:
                Assert.Equal(color, dest.Memory.Span[10]);
                Assert.NotEqual(sourceOwner, dest.MemoryOwner);
                Assert.NotEqual(destOwner, source.MemoryOwner);
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public void WhenDestIsNotMemoryOwner_DifferentSize_Throws(bool sourceIsOwner)
            {
                var data = new Rgba32[21];
                var color = new Rgba32(1, 2, 3, 4);

                var destOwner = new TestMemoryManager<Rgba32>(data);
                var dest = new BufferManager<Rgba32>(destOwner.Memory);

                var sourceOwner = (IMemoryOwner<Rgba32>)this.MemoryAllocator.Allocate<Rgba32>(22);

                BufferManager<Rgba32> source = sourceIsOwner
                                                   ? new BufferManager<Rgba32>(sourceOwner)
                                                   : new BufferManager<Rgba32>(sourceOwner.Memory);
                sourceOwner.Memory.Span[10] = color;

                // Act:
                Assert.ThrowsAny<InvalidOperationException>(
                    () => BufferManager<Rgba32>.SwapOrCopyContent(ref dest, ref source)
                );
                
                Assert.Equal(color, source.Memory.Span[10]);
                Assert.NotEqual(color, dest.Memory.Span[10]);
            }
        }
    }
}