// Copyright (c) Six Labors and contributors.
// Licensed under the GNU Affero General Public License, Version 3.

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
