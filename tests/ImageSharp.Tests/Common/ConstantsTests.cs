// <copyright file="ConstantsTests.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests.Common
{
    using Xunit;

    public class ConstantsTests
    {
        [Fact]
        public void Epsilon()
        {
            Assert.Equal(Constants.Epsilon, 0.001f);
        }
    }
}
