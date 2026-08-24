// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Reflection;
using Xunit.v3;

namespace SixLabors.ImageSharp.Tests;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public class ValidateDisposedMemoryAllocationsAttribute : BeforeAfterTestAttribute
{
    private readonly int expected = 0;

    public ValidateDisposedMemoryAllocationsAttribute()
        : this(0)
    {
    }

    public ValidateDisposedMemoryAllocationsAttribute(int expected)
        => this.expected = expected;

    public override void Before(MethodInfo methodUnderTest, IXunitTest test)
        => MemoryAllocatorValidator.MonitorAllocations();

    public override void After(MethodInfo methodUnderTest, IXunitTest test)
    {
        MemoryAllocatorValidator.ValidateAllocations(this.expected);
        MemoryAllocatorValidator.StopMonitoringAllocations();
    }
}
