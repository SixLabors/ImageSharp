// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Processing.Processors.Convolution;

namespace SixLabors.ImageSharp.Tests.Processing.Processors.Convolution;

[Trait("Category", "Processors")]
[GroupOutput("Convolution")]
public class KernelSamplingMapTests
{
    public static readonly TheoryData<BorderWrappingMode, int, int> BoundsSmallerThanKernel = new()
    {
        { BorderWrappingMode.Repeat, 8, 5 },
        { BorderWrappingMode.Mirror, 8, 5 },
        { BorderWrappingMode.Bounce, 8, 5 },
        { BorderWrappingMode.Wrap, 8, 5 },
        { BorderWrappingMode.Repeat, 1, 1 },
        { BorderWrappingMode.Mirror, 1, 1 },
        { BorderWrappingMode.Bounce, 1, 1 },
        { BorderWrappingMode.Wrap, 1, 1 },
        { BorderWrappingMode.Repeat, 30, 30 },
        { BorderWrappingMode.Mirror, 30, 30 },
        { BorderWrappingMode.Bounce, 30, 30 },
        { BorderWrappingMode.Wrap, 30, 30 },
    };

    [Theory]
    [MemberData(nameof(BoundsSmallerThanKernel))]
    public void BuildSamplingOffsetMap_BoundsSmallerThanKernelRadius_OffsetsStayInBounds(BorderWrappingMode mode, int width, int height)
    {
        // A 61-tap kernel has radius 30, so these bounds are covered entirely by the border
        // regions and offsets can overshoot the bounds by more than one sampling extent.
        const int kernelSize = 61;
        Rectangle bounds = new(100, 200, width, height);

        using KernelSamplingMap map = new(Configuration.Default.MemoryAllocator);
        map.BuildSamplingOffsetMap(kernelSize, kernelSize, bounds, mode, mode);

        foreach (int x in map.GetColumnOffsetSpan())
        {
            Assert.InRange(x, bounds.Left, bounds.Right - 1);
        }

        foreach (int y in map.GetRowOffsetSpan())
        {
            Assert.InRange(y, bounds.Top, bounds.Bottom - 1);
        }
    }
}
