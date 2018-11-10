// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Advanced
{
    public class AotCompilerTests
    {
        [Fact]
        public void AotCompiler_NoExceptions()
        {
            AotCompiler.Seed<Rgba32>();
        }
    }
}
