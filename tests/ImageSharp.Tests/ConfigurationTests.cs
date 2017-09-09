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
            Assert.IsType<LocalFileSystem>(this.DefaultConfiguration.FileSystem);
            Assert.IsType<LocalFileSystem>(this.ConfigurationEmpty.FileSystem);
        }

        [Fact]
        public void IfAutoloadWellknwonFormatesIsTrueAllFormateAreLoaded()
        {
            Assert.Equal(4, this.DefaultConfiguration.ImageEncoders.Count());
            Assert.Equal(4, this.DefaultConfiguration.ImageDecoders.Count());
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
                this.DefaultConfiguration.AddImageFormatDetector(null);
            });
        }

        [Fact]
        public void RegisterNullMimeTypeEncoder()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
               this.DefaultConfiguration.SetEncoder(null, new Mock<IImageEncoder>().Object);
            });
            Assert.Throws<ArgumentNullException>(() =>
            {
                this.DefaultConfiguration.SetEncoder(ImageFormats.Bmp, null);
            });
            Assert.Throws<ArgumentNullException>(() =>
            {
                this.DefaultConfiguration.SetEncoder(null, null);
            });
        }

        [Fact]
        public void RegisterNullSetDecoder()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                this.DefaultConfiguration.SetDecoder(null, new Mock<IImageDecoder>().Object);
            });
            Assert.Throws<ArgumentNullException>(() =>
            {
                this.DefaultConfiguration.SetDecoder(ImageFormats.Bmp, null);
            });
            Assert.Throws<ArgumentNullException>(() =>
            {
                this.DefaultConfiguration.SetDecoder(null, null);
            });
        }

        [Fact]
        public void RegisterMimeTypeEncoderReplacesLast()
        {
            IImageEncoder encoder1 = new Mock<IImageEncoder>().Object;
            this.ConfigurationEmpty.SetEncoder(TestFormat.GlobalTestFormat, encoder1);
            IImageEncoder found = this.ConfigurationEmpty.FindEncoder(TestFormat.GlobalTestFormat);
            Assert.Equal(encoder1, found);

            IImageEncoder encoder2 = new Mock<IImageEncoder>().Object;
            this.ConfigurationEmpty.SetEncoder(TestFormat.GlobalTestFormat, encoder2);
            IImageEncoder found2 = this.ConfigurationEmpty.FindEncoder(TestFormat.GlobalTestFormat);
            Assert.Equal(encoder2, found2);
            Assert.NotEqual(found, found2);
        }

        [Fact]
        public void RegisterMimeTypeDecoderReplacesLast()
        {
            IImageDecoder decoder1 = new Mock<IImageDecoder>().Object;
            this.ConfigurationEmpty.SetDecoder(TestFormat.GlobalTestFormat, decoder1);
            IImageDecoder found = this.ConfigurationEmpty.FindDecoder(TestFormat.GlobalTestFormat);
            Assert.Equal(decoder1, found);

            IImageDecoder decoder2 = new Mock<IImageDecoder>().Object;
            this.ConfigurationEmpty.SetDecoder(TestFormat.GlobalTestFormat, decoder2);
            IImageDecoder found2 = this.ConfigurationEmpty.FindDecoder(TestFormat.GlobalTestFormat);
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