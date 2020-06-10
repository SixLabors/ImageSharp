// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.Memory;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Memory.DiscontiguousBuffers
{
    public partial class MemoryGroupTests
    {
        public class View : MemoryGroupTestsBase
        {
            [Fact]
            public void RefersToOwnerGroupContent()
            {
                using MemoryGroup<int> group = this.CreateTestGroup(240, 80, true);

                MemoryGroupView<int> view = group.View;
                Assert.True(view.IsValid);
                Assert.Equal(group.Count, view.Count);
                Assert.Equal(group.BufferLength, view.BufferLength);
                Assert.Equal(group.TotalLength, view.TotalLength);
                int cnt = 1;
                foreach (Memory<int> memory in view)
                {
                    Span<int> span = memory.Span;
                    foreach (int t in span)
                    {
                        Assert.Equal(cnt, t);
                        cnt++;
                    }
                }
            }

            [Fact]
            public void IsInvalidatedOnOwnerGroupDispose()
            {
                MemoryGroupView<int> view;
                using (MemoryGroup<int> group = this.CreateTestGroup(240, 80, true))
                {
                    view = group.View;
                }

                Assert.False(view.IsValid);

                Assert.ThrowsAny<InvalidMemoryOperationException>(() =>
                {
                    _ = view.Count;
                });

                Assert.ThrowsAny<InvalidMemoryOperationException>(() =>
                {
                    _ = view.BufferLength;
                });

                Assert.ThrowsAny<InvalidMemoryOperationException>(() =>
                {
                    _ = view.TotalLength;
                });

                Assert.ThrowsAny<InvalidMemoryOperationException>(() =>
                {
                    _ = view[0];
                });
            }

            [Fact]
            public void WhenInvalid_CanNotUseMemberMemory()
            {
                Memory<int> memory;
                using (MemoryGroup<int> group = this.CreateTestGroup(240, 80, true))
                {
                    memory = group.View[0];
                }

                Assert.ThrowsAny<InvalidMemoryOperationException>(() =>
                {
                    _ = memory.Span;
                });
            }
        }
    }
}
