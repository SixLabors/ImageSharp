// <copyright file="ConfigurationTests.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    using ImageSharp.Formats;
    using ImageSharp.IO;
    using ImageSharp.PixelFormats;

    using Xunit;

    /// <summary>
    /// Tests the configuration class.
    /// </summary>
    public class ConfigurationTests
    {
        [Fact]
        public void DefaultsToLocalFileSystem()
        {
            var configuration = Configuration.CreateDefaultInstance();

            ImageSharp.IO.IFileSystem fs = configuration.FileSystem;

            Assert.IsType<LocalFileSystem>(fs);
        }

        [Fact]
        public void IfAutoloadWellknwonFormatesIsTrueAllFormateAreLoaded()
        {
            var configuration = Configuration.CreateDefaultInstance();

            Assert.Equal(4, configuration.ImageDecoders.Count);
            Assert.Equal(4, configuration.ImageDecoders.Count);
        }

        /// <summary>
        /// Test that the default configuration is not null.
        /// </summary>
        [Fact]
        public void TestDefultConfigurationIsNotNull()
        {
            Assert.True(Configuration.Default != null);
        }

        /// <summary>
        /// Test that the default configuration parallel options is not null.
        /// </summary>
        [Fact]
        public void TestDefultConfigurationParallelOptionsIsNotNull()
        {
            Assert.True(Configuration.Default.ParallelOptions != null);
        }

        /// <summary>
        /// Test that the default configuration parallel options max degrees of parallelism matches the
        /// environment processor count.
        /// </summary>
        [Fact]
        public void TestDefultConfigurationMaxDegreeOfParallelism()
        {
            Assert.True(Configuration.Default.ParallelOptions.MaxDegreeOfParallelism == Environment.ProcessorCount);
        }

        /// <summary>
        /// Test that the default configuration parallel options is not null.
        /// </summary>
        [Fact]
        public void TestDefultConfigurationImageFormatsIsNotNull()
        {
            Assert.True(Configuration.Default.ImageDecoders != null);
            Assert.True(Configuration.Default.ImageEncoders != null);
        }

        /// <summary>
        /// Tests the <see cref="M:Configuration.AddImageFormat"/> method throws an exception
        /// when the format is null.
        /// </summary>
        [Fact]
        public void TestAddImageFormatThrowsWithNullFormat()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                Configuration.Default.AddImageFormat((IImageEncoder)null);
            });
            Assert.Throws<ArgumentNullException>(() =>
            {
                Configuration.Default.AddImageFormat((IImageDecoder)null);
            });
        }

        /// <summary>
        /// Test that the default image constructors use default configuration.
        /// </summary>
        [Fact]
        public void TestImageUsesDefaultConfiguration()
        {
            Configuration.Default.AddImageFormat(new PngDecoder());

            var image = new Image<Rgba32>(1, 1);
            Assert.Equal(image.Configuration.ParallelOptions, Configuration.Default.ParallelOptions);
            Assert.Equal(image.Configuration.ImageDecoders, Configuration.Default.ImageDecoders);
        }

        /// <summary>
        /// Test that the default image constructor copies the configuration.
        /// </summary>
        [Fact]
        public void TestImageCopiesConfiguration()
        {
            Configuration.Default.AddImageFormat(new PngDecoder());

            var image = new Image<Rgba32>(1, 1);
            var image2 = new Image<Rgba32>(image);
            Assert.Equal(image2.Configuration.ParallelOptions, image.Configuration.ParallelOptions);
            Assert.True(image2.Configuration.ImageDecoders.SequenceEqual(image.Configuration.ImageDecoders));
        }
    }
}