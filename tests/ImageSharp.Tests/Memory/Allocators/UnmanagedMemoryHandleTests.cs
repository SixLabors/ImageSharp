// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using Microsoft.DotNet.RemoteExecutor;
using SixLabors.ImageSharp.Memory.Internals;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Memory.Allocators
{
    public class UnmanagedMemoryHandleTests
    {
        [Fact]
        public unsafe void Constructor_AllocatesReadWriteMemory()
        {
            using var h = new UnmanagedMemoryHandle(128);
            Assert.False(h.IsClosed);
            Assert.False(h.IsInvalid);
            byte* ptr = (byte*)h.DangerousGetHandle();
            for (int i = 0; i < 128; i++)
            {
                ptr[i] = (byte)i;
            }

            for (int i = 0; i < 128; i++)
            {
                Assert.Equal((byte)i, ptr[i]);
            }
        }

        [Fact]
        public void Dispose_ClosesHandle()
        {
            var h = new UnmanagedMemoryHandle(128);
            h.Dispose();
            Assert.True(h.IsClosed);
            Assert.True(h.IsInvalid);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(13)]
        public void CreateDispose_TracksAllocations(int count)
        {
            RemoteExecutor.Invoke(RunTest, count.ToString()).Dispose();

            static void RunTest(string countStr)
            {
                int countInner = int.Parse(countStr);
                var l = new List<UnmanagedMemoryHandle>();
                for (int i = 0; i < countInner; i++)
                {
                    Assert.Equal(i, UnmanagedMemoryHandle.TotalOutstandingHandles);
                    var h = new UnmanagedMemoryHandle(42);
                    Assert.Equal(i + 1, UnmanagedMemoryHandle.TotalOutstandingHandles);
                    l.Add(h);
                }

                for (int i = 0; i < countInner; i++)
                {
                    Assert.Equal(countInner - i, UnmanagedMemoryHandle.TotalOutstandingHandles);
                    l[i].Dispose();
                    Assert.Equal(countInner - i - 1, UnmanagedMemoryHandle.TotalOutstandingHandles);
                }
            }
        }

        [Theory]
        [InlineData(2)]
        [InlineData(12)]
        public void CreateFinalize_TracksAllocations(int count)
        {
            RemoteExecutor.Invoke(RunTest, count.ToString()).Dispose();

            static void RunTest(string countStr)
            {
                int countInner = int.Parse(countStr);
                List<UnmanagedMemoryHandle> l = FillList(countInner);

                l.RemoveRange(0, l.Count / 2);

                GC.Collect();
                GC.WaitForPendingFinalizers();

                Assert.Equal(countInner / 2, l.Count); // This is here to prevent eager finalization of the list's elements
                Assert.Equal(countInner / 2, UnmanagedMemoryHandle.TotalOutstandingHandles);
            }

            static List<UnmanagedMemoryHandle> FillList(int countInner)
            {
                var l = new List<UnmanagedMemoryHandle>();
                for (int i = 0; i < countInner; i++)
                {
                    var h = new UnmanagedMemoryHandle(42);
                    l.Add(h);
                }

                return l;
            }
        }

        [Fact]
        public void Resurrect_PreventsFinalization()
        {
            RemoteExecutor.Invoke(RunTest).Dispose();

            static void RunTest()
            {
                AllocateResurrect();
                Assert.Equal(1, UnmanagedMemoryHandle.TotalOutstandingHandles);
                GC.Collect();
                GC.WaitForPendingFinalizers();
                Assert.Equal(1, UnmanagedMemoryHandle.TotalOutstandingHandles);
                GC.Collect();
                GC.WaitForPendingFinalizers();
                Assert.Equal(1, UnmanagedMemoryHandle.TotalOutstandingHandles);
            }

            static void AllocateResurrect()
            {
                var h = new UnmanagedMemoryHandle(42);
                h.Resurrect();
            }
        }

        private static UnmanagedMemoryHandle resurrectedHandle;

        private class HandleOwner
        {
            private UnmanagedMemoryHandle handle;

            public HandleOwner(UnmanagedMemoryHandle handle) => this.handle = handle;

            ~HandleOwner()
            {
                resurrectedHandle = this.handle;
                this.handle.Resurrect();
            }
        }

        [Fact]
        public void AssignedToNewOwner_ReRegistersForFinalization()
        {
            RemoteExecutor.Invoke(RunTest).Dispose();

            static void RunTest()
            {
                AllocateAndForget();
                Assert.Equal(1, UnmanagedMemoryHandle.TotalOutstandingHandles);
                GC.Collect();
                GC.WaitForPendingFinalizers();
                VerifyResurrectedHandle(true);
                GC.Collect();
                GC.WaitForPendingFinalizers();
                VerifyResurrectedHandle(false);
                GC.Collect();
                GC.WaitForPendingFinalizers();

                Assert.Equal(0, UnmanagedMemoryHandle.TotalOutstandingHandles);
            }

            static void AllocateAndForget()
            {
                _ = new HandleOwner(new UnmanagedMemoryHandle(42));
            }

            static void VerifyResurrectedHandle(bool reAssign)
            {
                Assert.NotNull(resurrectedHandle);
                Assert.Equal(1, UnmanagedMemoryHandle.TotalOutstandingHandles);
                Assert.False(resurrectedHandle.IsClosed);
                Assert.False(resurrectedHandle.IsInvalid);
                resurrectedHandle.AssignedToNewOwner();
                if (reAssign)
                {
                    _ = new HandleOwner(resurrectedHandle);
                }

                resurrectedHandle = null;
            }
        }
    }
}
