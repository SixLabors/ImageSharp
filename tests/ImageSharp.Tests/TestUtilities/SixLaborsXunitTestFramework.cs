// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Environments;
using SixLabors.ImageSharp.Tests.TestUtilities;
using Xunit.v3;

[assembly: Xunit.TestFramework(typeof(SixLaborsXunitTestFramework))]

namespace SixLabors.ImageSharp.Tests.TestUtilities;

public class SixLaborsXunitTestFramework : XunitTestFramework
{
    public SixLaborsXunitTestFramework()
    {
        Console.Error.WriteLine(HostEnvironmentInfo.GetInformation());
    }
}
