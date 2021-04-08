// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Runtime.InteropServices;
using Xunit;

#pragma warning disable IDE0022 // Use expression body for methods
namespace SixLabors.ImageSharp.Tests.Helpers
{
    public class RuntimeEnvironmentTests
    {
        [Fact]
        public void CanDetectNetCore()
        {
#if NET5_0_OR_GREATER
            Assert.False(RuntimeEnvironment.IsNetCore);
#elif NETCOREAPP
            Assert.True(RuntimeEnvironment.IsNetCore);
#else
            Assert.False(RuntimeEnvironment.IsNetCore);
#endif
        }

        [Fact]
        public void CanDetectOSPlatform()
        {
            if (TestEnvironment.IsLinux)
            {
                Assert.True(RuntimeEnvironment.IsOSPlatform(OSPlatform.Linux));
            }
            else if (TestEnvironment.IsOSX)
            {
                Assert.True(RuntimeEnvironment.IsOSPlatform(OSPlatform.OSX));
            }
            else if (TestEnvironment.IsWindows)
            {
                Assert.True(RuntimeEnvironment.IsOSPlatform(OSPlatform.Windows));
            }
        }
    }
}
