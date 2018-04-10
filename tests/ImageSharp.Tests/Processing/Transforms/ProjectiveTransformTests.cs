// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;
using Xunit.Abstractions;

namespace SixLabors.ImageSharp.Tests.Processing.Transforms
{
    public class ProjectiveTransformTests
    {
        private readonly ITestOutputHelper Output;

        private static readonly ImageComparer ValidatorComparer = ImageComparer.TolerantPercentage(0.005f, 3);
    }
}
