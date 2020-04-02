// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;

using Xunit.Abstractions;

namespace SixLabors.ImageSharp.Tests.PixelFormats.PixelOperations
{
    public partial class PixelOperationsTests
    {
        public class Bgra5551OperationsTests : PixelOperationsTests<Bgra5551>
        {
            public Bgra5551OperationsTests(ITestOutputHelper output)
                : base(output)
            {
            }
        }
    }
}
