// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Environments;
using SixLabors.ImageSharp.Tests.TestUtilities;
using Xunit.Abstractions;
using Xunit.Sdk;

[assembly: Xunit.TestFramework(SixLaborsXunitTestFramework.Type, SixLaborsXunitTestFramework.Assembly)]

namespace SixLabors.ImageSharp.Tests.TestUtilities;

public class SixLaborsXunitTestFramework : XunitTestFramework
{
    public const string Type = "SixLabors.ImageSharp.Tests.TestUtilities.SixLaborsXunitTestFramework";
    public const string Assembly = "SixLabors.ImageSharp.Tests";

    public SixLaborsXunitTestFramework(IMessageSink messageSink)
        : base(messageSink)
    {
        DiagnosticMessage message = new DiagnosticMessage(HostEnvironmentInfo.GetInformation());
        messageSink.OnMessage(message);
    }
}
