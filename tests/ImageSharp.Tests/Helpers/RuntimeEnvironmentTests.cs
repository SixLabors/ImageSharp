// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Helpers
{
    public class RuntimeEnvironmentTests
    {
        [Fact]
        public void CanDetectNetCore() => Assert.False(RuntimeEnvironment.IsNetCore);

        [Fact]
        public void CanDetectOSPlatform()
        {
            if (TestEnvironment.IsLinux)
            {
                Assert.True(RuntimeEnvironment.IsOSPlatform(OSPlatform.Linux));
            }
            else if (TestEnvironment.IsMacOS)
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
