// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using Microsoft.DotNet.RemoteExecutor;
using SixLabors.ImageSharp.Memory.Internals;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Memory.Allocators
{
    public class RefCountingLifetimeGuardTests
    {
        [Theory]
        [InlineData(1)]
        [InlineData(3)]
        public void Dispose_ResultsInSingleRelease(int disposeCount)
        {
            var guard = new MockLifetimeGuard();
            Assert.Equal(0, guard.ReleaseInvocationCount);

            for (int i = 0; i < disposeCount; i++)
            {
                guard.Dispose();
            }

            Assert.Equal(1, guard.ReleaseInvocationCount);
        }

        [Fact]
        public void Finalize_ResultsInSingleRelease()
        {
            RemoteExecutor.Invoke(RunTest).Dispose();

            static void RunTest()
            {
                Assert.Equal(0, MockLifetimeGuard.GlobalReleaseInvocationCount);
                LeakGuard(false);
                GC.Collect();
                GC.WaitForPendingFinalizers();
                Assert.Equal(1, MockLifetimeGuard.GlobalReleaseInvocationCount);
            }
        }

        [Theory]
        [InlineData(1)]
        [InlineData(3)]
        public void AddRef_PreventsReleaseOnDispose(int addRefCount)
        {
            var guard = new MockLifetimeGuard();

            for (int i = 0; i < addRefCount; i++)
            {
                guard.AddRef();
            }

            guard.Dispose();

            for (int i = 0; i < addRefCount; i++)
            {
                Assert.Equal(0, guard.ReleaseInvocationCount);
                guard.ReleaseRef();
            }

            Assert.Equal(1, guard.ReleaseInvocationCount);
        }

        [Fact]
        public void AddRef_PreventsReleaseOnFinalize()
        {
            RemoteExecutor.Invoke(RunTest).Dispose();

            static void RunTest()
            {
                LeakGuard(true);
                GC.Collect();
                GC.WaitForPendingFinalizers();
                Assert.Equal(0, MockLifetimeGuard.GlobalReleaseInvocationCount);
            }
        }

        [Fact]
        public void AddRefReleaseRefMisuse_DoesntLeadToMultipleReleases()
        {
            var guard = new MockLifetimeGuard();
            guard.Dispose();
            guard.AddRef();
            guard.ReleaseRef();

            Assert.Equal(1, guard.ReleaseInvocationCount);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void LeakGuard(bool addRef)
        {
            var guard = new MockLifetimeGuard();
            if (addRef)
            {
                guard.AddRef();
            }
        }

        private class MockLifetimeGuard : RefCountedLifetimeGuard
        {
            public int ReleaseInvocationCount { get; private set; }

            public static int GlobalReleaseInvocationCount { get; private set; }

            protected override void Release()
            {
                this.ReleaseInvocationCount++;
                GlobalReleaseInvocationCount++;
            }
        }
    }
}
