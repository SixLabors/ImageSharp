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
            // the shallow copy of configuration should behave exactly like the default configuration,
            // so by using the copy, we test both the default and the copy.
            this.DefaultConfiguration = Configuration.CreateDefaultInstance().ShallowCopy();
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
        /// Test that the default configuration read origin options is set to begin.
        /// </summary>
        [Fact]
        public void TestDefultConfigurationReadOriginIsCurrent()
        {
            Assert.True(Configuration.Default.ReadOrigin == ReadOrigin.Current);
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