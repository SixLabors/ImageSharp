// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ImageProcessorBootstrapperTests.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------
namespace ImageProcessor.UnitTests.Configuration
{
    using System;
    using System.Linq;

    using FluentAssertions;
    using NUnit.Framework;

    using ImageProcessor.Configuration;

    /// <summary>
    /// Test harness for the ImageProcessor bootstrapper tests
    /// </summary>
    [TestFixture]
    public class ImageProcessorBootrapperTests
    {
        [Test]
        public void Singleton_is_instantiated()
        {
            ImageProcessorBootstrapper.Instance.SupportedImageFormats.Count().Should().BeGreaterThan(0, "because there should be supported image formats");

            ImageProcessorBootstrapper.Instance.NativeBinaryFactory.Is64BitEnvironment.Should().Be(Environment.Is64BitProcess);
        }
    }
}