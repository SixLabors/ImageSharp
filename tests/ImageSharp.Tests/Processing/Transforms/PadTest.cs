// <copyright file="PadTest.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests.Processing.Transforms
{
    using System;
    using ImageSharp.PixelFormats;

    using Xunit;

    public class PadTest : BaseImageOperationsExtensionTest
    {
        [Fact(Skip = "Skip this is a helper around resize, skip until resize can be refactord")]
        public void Pad_width_height_ResizeProcessorWithCorrectOPtionsSet()
        {
            throw new NotImplementedException("Write test here");
        }
    }
}