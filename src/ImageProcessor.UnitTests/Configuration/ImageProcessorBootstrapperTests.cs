// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ImageProcessorBootrapperTests.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Test harness for the ImageProcessor bootstrapper tests
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.UnitTests.Configuration
{
    using System;
    using System.Linq;

    using FluentAssertions;

    using ImageProcessor.Configuration;

    using NUnit.Framework;

    /// <summary>
    /// Test harness for the ImageProcessor bootstrapper tests
    /// </summary>
    [TestFixture]
    public class ImageProcessorBootstrapperTests
    {
        /// <summary>
        /// Test to see that the bootstrapper singleton is instantiated.
        /// </summary>
        [Test]
        public void BootstrapperSingletonIsInstantiated()
        {
            ImageProcessorBootstrapper.Instance.SupportedImageFormats.Count().Should().BeGreaterThan(0, "because there should be supported image formats");

            ImageProcessorBootstrapper.Instance.NativeBinaryFactory.Is64BitEnvironment.Should().Be(Environment.Is64BitProcess);
        }
    }
}