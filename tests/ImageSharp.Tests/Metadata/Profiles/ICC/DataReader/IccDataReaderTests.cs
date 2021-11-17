// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Metadata.Profiles.ICC.DataReader
{
    [Trait("Profile", "Icc")]
    public class IccDataReaderTests
    {
        [Fact]
        public void ConstructorThrowsNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new IccDataReader(null));
        }
    }
}
