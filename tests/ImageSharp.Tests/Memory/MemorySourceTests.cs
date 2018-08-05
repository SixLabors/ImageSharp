// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Memory;
using Xunit;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests.Memory
{
    public class MemorySourceTests
    {
        public class Construction
        {
            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public void InitializeAsOwner(bool isInternalMemorySource)
            {
                var data = new Rgba32[21];
                var mmg = new TestMemoryManager<Rgba32>(data);

                var a = new MemorySource<Rgba32>(mmg, isInternalMemorySource);

                Assert.Equal(mmg, a.MemoryOwner);
                Assert.Equal(mmg.Memory, a.Memory);
                Assert.Equal(isInternalMemorySource, a.HasSwappableContents);
            }

            [Fact]
            public void InitializeAsObserver_MemoryOwner_IsNull()
            {
                var data = new Rgba32[21];
                var mmg = new TestMemoryManager<Rgba32>(data);

                var a = new MemorySource<Rgba32>(mmg.Memory);

                Assert.Null(a.MemoryOwner);
                Assert.Equal(mmg.Memory, a.Memory);
                Assert.False(a.HasSwappableContents);
            }
        }

        public class Dispose
        {
            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public void WhenOwnershipIsTransfered_ShouldDisposeMemoryOwner(bool isInternalMemorySource)
            {
                var mmg = new TestMemoryManager<int>(new int[10]);
                var bmg = new MemorySource<int>(mmg, isInternalMemorySource);
                
                bmg.Dispose();
                Assert.True(mmg.IsDisposed);
            }

            [Fact]
            public void WhenMemoryObserver_ShouldNotDisposeAnything()
            {
                var mmg = new TestMemoryManager<int>(new int[10]);
                var bmg = new MemorySource<int>(mmg.Memory);

                bmg.Dispose();
                Assert.False(mmg.IsDisposed);
            }
        }
        
        public class SwapOrCopyContent
        {
            private MemoryAllocator MemoryAllocator { get; } = new TestMemoryAllocator();

            private MemorySource<T> AllocateMemorySource<T>(int length, AllocationOptions options = AllocationOptions.None)
                where T : struct
            {
                IMemoryOwner<T> owner = this.MemoryAllocator.Allocate<T>(length, options);
                return new MemorySource<T>(owner, true);
            }

            [Fact]
            public void WhenBothAreMemoryOwners_ShouldSwap()
            {
                MemorySource<int> a = this.AllocateMemorySource<int>(13);
                MemorySource<int> b = this.AllocateMemorySource<int>(17);

                IMemoryOwner<int> aa = a.MemoryOwner;
                IMemoryOwner<int> bb = b.MemoryOwner;

                Memory<int> aaa = a.Memory;
                Memory<int> bbb = b.Memory;

                MemorySource<int>.SwapOrCopyContent(ref a, ref b);

                Assert.Equal(bb, a.MemoryOwner);
                Assert.Equal(aa, b.MemoryOwner);

                Assert.Equal(bbb, a.Memory);
                Assert.Equal(aaa, b.Memory);
                Assert.NotEqual(a.Memory, b.Memory);
            }

            [Theory]
            [InlineData(false, false)]
            [InlineData(true, true)]
            [InlineData(true, false)]
            public void WhenDestIsNotMemoryOwner_SameSize_ShouldCopy(bool sourceIsOwner, bool isInternalMemorySource)
            {
                var data = new Rgba32[21];
                var color = new Rgba32(1, 2, 3, 4);

                var destOwner = new TestMemoryManager<Rgba32>(data);
                var dest = new MemorySource<Rgba32>(destOwner.Memory);

                IMemoryOwner<Rgba32> sourceOwner = this.MemoryAllocator.Allocate<Rgba32>(21);

                MemorySource<Rgba32> source = sourceIsOwner
                                                   ? new MemorySource<Rgba32>(sourceOwner, isInternalMemorySource)
                                                   : new MemorySource<Rgba32>(sourceOwner.Memory);
                
                sourceOwner.Memory.Span[10] = color;

                // Act:
                MemorySource<Rgba32>.SwapOrCopyContent(ref dest, ref source);

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
                var dest = new MemorySource<Rgba32>(destOwner.Memory);

                IMemoryOwner<Rgba32> sourceOwner = this.MemoryAllocator.Allocate<Rgba32>(22);

                MemorySource<Rgba32> source = sourceIsOwner
                                                   ? new MemorySource<Rgba32>(sourceOwner, true)
                                                   : new MemorySource<Rgba32>(sourceOwner.Memory);
                sourceOwner.Memory.Span[10] = color;

                // Act:
                Assert.ThrowsAny<InvalidOperationException>(
                    () => MemorySource<Rgba32>.SwapOrCopyContent(ref dest, ref source)
                );
                
                Assert.Equal(color, source.Memory.Span[10]);
                Assert.NotEqual(color, dest.Memory.Span[10]);
            }
        }
    }
}