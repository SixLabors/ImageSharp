// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Linq;
using Moq;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.IO;
using Xunit;
// ReSharper disable InconsistentNaming

namespace SixLabors.ImageSharp.Tests
{
    /// <summary>
    /// Tests the configuration class.
    /// </summary>
    public class ConfigurationTests
    {
        public Configuration ConfigurationEmpty { get; }
        public Configuration DefaultConfiguration { get; }

        public ConfigurationTests()
        {
            // the shallow copy of configuration should behave exactly like the default configuration,
            // so by using the copy, we test both the default and the copy.
            this.DefaultConfiguration = Configuration.CreateDefaultInstance().Clone();
            this.ConfigurationEmpty = new Configuration();
        }

        [Fact]
        public void DefaultsToLocalFileSystem()
        {
            Assert.IsType<LocalFileSystem>(this.DefaultConfiguration.FileSystem);
            Assert.IsType<LocalFileSystem>(this.ConfigurationEmpty.FileSystem);
        }

        /// <summary>
        /// Test that the default configuration is not null.
        /// </summary>
        [Fact]
        public void TestDefaultConfigurationIsNotNull() => Assert.True(Configuration.Default != null);

        /// <summary>
        /// Test that the default configuration read origin options is set to begin.
        /// </summary>
        [Fact]
        public void TestDefaultConfigurationReadOriginIsCurrent() => Assert.True(Configuration.Default.ReadOrigin == ReadOrigin.Current);

        /// <summary>
        /// Test that the default configuration parallel options max degrees of parallelism matches the
        /// environment processor count.
        /// </summary>
        [Fact]
        public void TestDefaultConfigurationMaxDegreeOfParallelism()
        {
            Assert.True(Configuration.Default.MaxDegreeOfParallelism == Environment.ProcessorCount);

            var cfg = new Configuration();
            Assert.True(cfg.MaxDegreeOfParallelism == Environment.ProcessorCount);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-42)]
        public void Set_MaxDegreeOfParallelism_ToNonPositiveValue_Throws(int value)
        {
            var cfg = new Configuration();
            Assert.Throws<ArgumentOutOfRangeException>(() => cfg.MaxDegreeOfParallelism = value);
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

        [Fact]
        public void ConfigurationCannotAddDuplicates()
        {
            const int count = 4;
            Configuration config = Configuration.Default;

            Assert.Equal(count, config.ImageFormats.Count());

            config.ImageFormatsManager.AddImageFormat(BmpFormat.Instance);

            Assert.Equal(count, config.ImageFormats.Count());
        }

        [Fact]
        public void DefaultConfigurationHasCorrectFormatCount()
        {
            Configuration config = Configuration.Default;

            Assert.Equal(4, config.ImageFormats.Count());
        }
    }
}