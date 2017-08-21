// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.PixelFormats;
using Moq;
using Xunit;

namespace SixLabors.ImageSharp.Tests
{
    /// <summary>
    /// Tests the configuration class.
    /// </summary>
    public class ConfigurationTests
    {
        public Configuration ConfigurationEmpty { get; private set; }
        public Configuration DefaultConfiguration { get; private set; }

        public ConfigurationTests()
        {
            this.DefaultConfiguration = Configuration.CreateDefaultInstance();
            this.ConfigurationEmpty = new Configuration();
        }

        [Fact]
        public void DefaultsToLocalFileSystem()
        {
            Assert.IsType<LocalFileSystem>(DefaultConfiguration.FileSystem);
            Assert.IsType<LocalFileSystem>(ConfigurationEmpty.FileSystem);
        }

        [Fact]
        public void IfAutoloadWellknwonFormatesIsTrueAllFormateAreLoaded()
        {
            Assert.Equal(4, DefaultConfiguration.ImageEncoders.Count());
            Assert.Equal(4, DefaultConfiguration.ImageDecoders.Count());
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

        [Fact]
        public void AddImageFormatDetectorNullthrows()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                DefaultConfiguration.AddImageFormatDetector(null);
            });
        }

        [Fact]
        public void RegisterNullMimeTypeEncoder()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                DefaultConfiguration.SetEncoder(null, new Mock<IImageEncoder>().Object);
            });
            Assert.Throws<ArgumentNullException>(() =>
            {
                DefaultConfiguration.SetEncoder(ImageFormats.Bmp, null);
            });
            Assert.Throws<ArgumentNullException>(() =>
            {
                DefaultConfiguration.SetEncoder(null, null);
            });
        }

        [Fact]
        public void RegisterNullSetDecoder()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                DefaultConfiguration.SetDecoder(null, new Mock<IImageDecoder>().Object);
            });
            Assert.Throws<ArgumentNullException>(() =>
            {
                DefaultConfiguration.SetDecoder(ImageFormats.Bmp, null);
            });
            Assert.Throws<ArgumentNullException>(() =>
            {
                DefaultConfiguration.SetDecoder(null, null);
            });
        }

        [Fact]
        public void RegisterMimeTypeEncoderReplacesLast()
        {
            var encoder1 = new Mock<IImageEncoder>().Object;
            ConfigurationEmpty.SetEncoder(TestFormat.GlobalTestFormat, encoder1);
            var found = ConfigurationEmpty.FindEncoder(TestFormat.GlobalTestFormat);
            Assert.Equal(encoder1, found);

            var encoder2 = new Mock<IImageEncoder>().Object;
            ConfigurationEmpty.SetEncoder(TestFormat.GlobalTestFormat, encoder2);
            var found2 = ConfigurationEmpty.FindEncoder(TestFormat.GlobalTestFormat);
            Assert.Equal(encoder2, found2);
            Assert.NotEqual(found, found2);
        }

        [Fact]
        public void RegisterMimeTypeDecoderReplacesLast()
        {
            var decoder1 = new Mock<IImageDecoder>().Object;
            ConfigurationEmpty.SetDecoder(TestFormat.GlobalTestFormat, decoder1);
            var found = ConfigurationEmpty.FindDecoder(TestFormat.GlobalTestFormat);
            Assert.Equal(decoder1, found);

            var decoder2 = new Mock<IImageDecoder>().Object;
            ConfigurationEmpty.SetDecoder(TestFormat.GlobalTestFormat, decoder2);
            var found2 = ConfigurationEmpty.FindDecoder(TestFormat.GlobalTestFormat);
            Assert.Equal(decoder2, found2);
            Assert.NotEqual(found, found2);
        }


        [Fact]
        public void ConstructorCallConfigureOnFormatProvider()
        {
            var provider = new Mock<IConfigurationModule>();
            var config = new Configuration(provider.Object);

            provider.Verify(x => x.Configure(config));
        }

        [Fact]
        public void AddFormatCallsConfig()
        {
            var provider = new Mock<IConfigurationModule>();
            var config = new Configuration();
            config.Configure(provider.Object);

            provider.Verify(x => x.Configure(config));
        }
    }
}