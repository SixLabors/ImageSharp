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
    using Moq;
    using Xunit;

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
            Assert.Equal(6, DefaultConfiguration.ImageEncoders.Count());
            Assert.Equal(6, DefaultConfiguration.ImageDecoders.Count());
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
        public void AddMimeTypeDetectorNullthrows()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                DefaultConfiguration.AddMimeTypeDetector(null);
            });
        }

        [Fact]
        public void RegisterNullMimeTypeEncoder()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                DefaultConfiguration.SetMimeTypeEncoder(null, new Mock<IImageEncoder>().Object);
            });
            Assert.Throws<ArgumentNullException>(() =>
            {
                DefaultConfiguration.SetMimeTypeEncoder("sdsdsd", null);
            });
            Assert.Throws<ArgumentNullException>(() =>
            {
                DefaultConfiguration.SetMimeTypeEncoder(null, null);
            });
        }

        [Fact]
        public void RegisterNullFileExtEncoder()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                DefaultConfiguration.SetFileExtensionToMimeTypeMapping(null, "str");
            });
            Assert.Throws<ArgumentNullException>(() =>
            {
                DefaultConfiguration.SetFileExtensionToMimeTypeMapping("sdsdsd", null);
            });
            Assert.Throws<ArgumentNullException>(() =>
            {
                DefaultConfiguration.SetFileExtensionToMimeTypeMapping(null, null);
            });
        }

        [Fact]
        public void RegisterNullMimeTypeDecoder()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                DefaultConfiguration.SetMimeTypeDecoder(null, new Mock<IImageDecoder>().Object);
            });
            Assert.Throws<ArgumentNullException>(() =>
            {
                DefaultConfiguration.SetMimeTypeDecoder("sdsdsd", null);
            });
            Assert.Throws<ArgumentNullException>(() =>
            {
                DefaultConfiguration.SetMimeTypeDecoder(null, null);
            });
        }

        [Fact]
        public void RegisterMimeTypeEncoderReplacesLast()
        {
            var encoder1 = new Mock<IImageEncoder>().Object;
            ConfigurationEmpty.SetMimeTypeEncoder("test", encoder1);
            var found = ConfigurationEmpty.FindMimeTypeEncoder("TEST");
            Assert.Equal(encoder1, found);

            var encoder2 = new Mock<IImageEncoder>().Object;
            ConfigurationEmpty.SetMimeTypeEncoder("TEST", encoder2);
            var found2 = ConfigurationEmpty.FindMimeTypeEncoder("test");
            Assert.Equal(encoder2, found2);
            Assert.NotEqual(found, found2);
        }

        [Fact]
        public void RegisterFileExtEnecoderReplacesLast()
        {
            var encoder1 = "mime1";
            ConfigurationEmpty.SetFileExtensionToMimeTypeMapping("TEST", encoder1);
            var found = ConfigurationEmpty.FindFileExtensionsMimeType("test");
            Assert.Equal(encoder1, found);

            var encoder2 = "mime2";
            ConfigurationEmpty.SetFileExtensionToMimeTypeMapping("test", encoder2);
            var found2 = ConfigurationEmpty.FindFileExtensionsMimeType("TEST");
            Assert.Equal(encoder2, found2);
            Assert.NotEqual(found, found2);
        }

        [Fact]
        public void RegisterMimeTypeDecoderReplacesLast()
        {
            var decoder1 = new Mock<IImageDecoder>().Object;
            ConfigurationEmpty.SetMimeTypeDecoder("test", decoder1);
            var found = ConfigurationEmpty.FindMimeTypeDecoder("TEST");
            Assert.Equal(decoder1, found);

            var decoder2 = new Mock<IImageDecoder>().Object;
            ConfigurationEmpty.SetMimeTypeDecoder("TEST", decoder2);
            var found2 = ConfigurationEmpty.FindMimeTypeDecoder("test");
            Assert.Equal(decoder2, found2);
            Assert.NotEqual(found, found2);
        }


        [Fact]
        public void ConstructorCallConfigureOnFormatProvider()
        {
            var provider = new Mock<IImageFormatProvider>();
            var config = new Configuration(provider.Object);

            provider.Verify(x => x.Configure(config));
        }

        [Fact]
        public void AddFormatCallsConfig()
        {
            var provider = new Mock<IImageFormatProvider>();
            var config = new Configuration();
            config.AddImageFormat(provider.Object);

            provider.Verify(x => x.Configure(config));
        }
    }
}