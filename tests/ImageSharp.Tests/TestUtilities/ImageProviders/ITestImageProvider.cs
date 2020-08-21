// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Tests
{
    public interface ITestImageProvider
    {
        PixelTypes PixelType { get; }

        ImagingTestCaseUtility Utility { get; }

        string SourceFileOrDescription { get; }

        Configuration Configuration { get; set; }
    }
}
