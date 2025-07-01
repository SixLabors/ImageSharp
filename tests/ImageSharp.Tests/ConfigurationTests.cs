// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using Microsoft.DotNet.RemoteExecutor;
using Moq;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests;

/// <summary>
/// Tests the configuration class.
/// </summary>
public class ConfigurationTests
{
    public Configuration ConfigurationEmpty { get; }

    public Configuration DefaultConfiguration { get; }

    private readonly int expectedDefaultConfigurationCount = 11;

    public ConfigurationTests()
    {
        // The shallow copy of configuration should behave exactly like the default configuration,
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
    public void TestDefaultConfigurationIsNotNull() => Assert.True(this.DefaultConfiguration != null);

    /// <summary>
    /// Test that the default configuration read origin options is set to begin.
    /// </summary>
    [Fact]
    public void TestDefaultConfigurationReadOriginIsCurrent() => Assert.True(this.DefaultConfiguration.ReadOrigin == ReadOrigin.Current);

    /// <summary>
    /// Test that the default configuration parallel options max degrees of parallelism matches the
    /// environment processor count.
    /// </summary>
    [Fact]
    public void TestDefaultConfigurationMaxDegreeOfParallelism()
    {
        Assert.True(this.DefaultConfiguration.MaxDegreeOfParallelism == Environment.ProcessorCount);

        Configuration cfg = new();
        Assert.True(cfg.MaxDegreeOfParallelism == Environment.ProcessorCount);
    }

    [Theory]
    [InlineData(-3, true)]
    [InlineData(-2, true)]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(5, false)]
    public void MaxDegreeOfParallelism_CompatibleWith_ParallelOptions(int maxDegreeOfParallelism, bool throws)
    {
        Configuration cfg = new();
        if (throws)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => cfg.MaxDegreeOfParallelism = maxDegreeOfParallelism);
        }
        else
        {
            cfg.MaxDegreeOfParallelism = maxDegreeOfParallelism;
            Assert.Equal(maxDegreeOfParallelism, cfg.MaxDegreeOfParallelism);
        }
    }

    [Fact]
    public void ConstructorCallConfigureOnFormatProvider()
    {
        Mock<IImageFormatConfigurationModule> provider = new();
        Configuration config = new(provider.Object);

        provider.Verify(x => x.Configure(config));
    }

    [Fact]
    public void AddFormatCallsConfig()
    {
        Mock<IImageFormatConfigurationModule> provider = new();
        Configuration config = new();
        config.Configure(provider.Object);

        provider.Verify(x => x.Configure(config));
    }

    [Fact]
    public void ConfigurationCannotAddDuplicates()
    {
        Configuration config = this.DefaultConfiguration;

        Assert.Equal(this.expectedDefaultConfigurationCount, config.ImageFormats.Count());

        config.ImageFormatsManager.AddImageFormat(BmpFormat.Instance);

        Assert.Equal(this.expectedDefaultConfigurationCount, config.ImageFormats.Count());
    }

    [Fact]
    public void DefaultConfigurationHasCorrectFormatCount()
    {
        Configuration config = Configuration.CreateDefaultInstance();

        Assert.Equal(this.expectedDefaultConfigurationCount, config.ImageFormats.Count());
    }

    [Fact]
    public void WorkingBufferSizeHint_DefaultIsCorrect()
    {
        Configuration config = this.DefaultConfiguration;
        Assert.True(config.WorkingBufferSizeHintInBytes > 1024);
    }

    [Fact]
    public void StreamBufferSize_DefaultIsCorrect()
    {
        Configuration config = this.DefaultConfiguration;
        Assert.True(config.StreamProcessingBufferSize == 8096);
    }

    [Fact]
    public void StreamBufferSize_CannotGoBelowMinimum()
    {
        Configuration config = new();

        Assert.Throws<ArgumentOutOfRangeException>(
                () => config.StreamProcessingBufferSize = 0);
    }

    [Fact]
    public void MemoryAllocator_Setter_Roundtrips()
    {
        MemoryAllocator customAllocator = new SimpleGcMemoryAllocator();
        Configuration config = new() { MemoryAllocator = customAllocator };
        Assert.Same(customAllocator, config.MemoryAllocator);
    }

    [Fact]
    public void MemoryAllocator_SetNull_ThrowsArgumentNullException()
    {
        Configuration config = new();
        Assert.Throws<ArgumentNullException>(() => config.MemoryAllocator = null);
    }

    [Fact]
    public void InheritsDefaultMemoryAllocatorInstance()
    {
        RemoteExecutor.Invoke(RunTest).Dispose();

        static void RunTest()
        {
            Configuration c1 = new();
            Configuration c2 = new(new MockConfigurationModule());
            Configuration c3 = Configuration.CreateDefaultInstance();

            Assert.Same(MemoryAllocator.Default, Configuration.Default.MemoryAllocator);
            Assert.Same(MemoryAllocator.Default, c1.MemoryAllocator);
            Assert.Same(MemoryAllocator.Default, c2.MemoryAllocator);
            Assert.Same(MemoryAllocator.Default, c3.MemoryAllocator);
        }
    }

    private class MockConfigurationModule : IImageFormatConfigurationModule
    {
        public void Configure(Configuration configuration)
        {
        }
    }
}
