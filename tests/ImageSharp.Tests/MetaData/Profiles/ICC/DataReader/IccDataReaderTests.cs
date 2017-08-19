// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Icc
{
    public class IccDataReaderTests
    {
        [Fact]
        public void ConstructorThrowsNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new IccDataReader(null));
        }
    }
}
