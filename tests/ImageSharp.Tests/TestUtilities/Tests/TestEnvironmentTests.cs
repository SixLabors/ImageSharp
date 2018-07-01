// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;

using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Tests.TestUtilities.ReferenceCodecs;

using Xunit;
using Xunit.Abstractions;
// ReSharper disable InconsistentNaming

namespace SixLabors.ImageSharp.Tests
{
    public class TestEnvironmentTests
    {
        public TestEnvironmentTests(ITestOutputHelper output)
        {
            this.Output = output;
        }

        private ITestOutputHelper Output { get; }

        private void CheckPath(string path)
        {
            this.Output.WriteLine(path);
            Assert.True(Directory.Exists(path));
        }

        /// <summary>
        /// We need this test to make sure that the netcoreapp2.1 test execution actually covers the netcoreapp2.1 build configuration of ImageSharp.
        /// </summary>
        [Fact]
        public void ImageSharpAssemblyUnderTest_MatchesExpectedTargetFramework()
        {
            this.Output.WriteLine("NetCoreVersion: " + TestEnvironment.NetCoreVersion);
            this.Output.WriteLine("ImageSharpBuiltAgainst: " + TestHelpers.ImageSharpBuiltAgainst);

            if (string.IsNullOrEmpty(TestEnvironment.NetCoreVersion))
            {
                this.Output.WriteLine("Not running under .NET Core!");
            }
            else if (TestEnvironment.NetCoreVersion.StartsWith("2.1"))
            {
                Assert.Equal("netcoreapp2.1", TestHelpers.ImageSharpBuiltAgainst);
            }
            else
            {
                Assert.Equal("netstandard2.0", TestHelpers.ImageSharpBuiltAgainst);
            }
        }

        [Fact]
        public void SolutionDirectoryFullPath()
        {
            this.CheckPath(TestEnvironment.SolutionDirectoryFullPath);
        }

        [Fact]
        public void InputImagesDirectoryFullPath()
        {
            this.CheckPath(TestEnvironment.InputImagesDirectoryFullPath);
        }

        [Fact]
        public void ExpectedOutputDirectoryFullPath()
        {
            this.CheckPath(TestEnvironment.ReferenceOutputDirectoryFullPath);
        }

        [Fact]
        public void GetReferenceOutputFileName()
        {
            string actual = Path.Combine(TestEnvironment.ActualOutputDirectoryFullPath, @"foo\bar\lol.jpeg");
            string expected = TestEnvironment.GetReferenceOutputFileName(actual);

            this.Output.WriteLine(expected);
            Assert.Contains(TestEnvironment.ReferenceOutputDirectoryFullPath, expected);
        }

        [Theory]
        [InlineData("lol/foo.png", typeof(SystemDrawingReferenceEncoder))]
        [InlineData("lol/Rofl.bmp", typeof(SystemDrawingReferenceEncoder))]
        [InlineData("lol/Baz.JPG", typeof(JpegEncoder))]
        [InlineData("lol/Baz.gif", typeof(GifEncoder))]
        public void GetReferenceEncoder_ReturnsCorrectEncoders_Windows(string fileName, Type expectedEncoderType)
        {
            if (TestEnvironment.IsLinux) return;

            IImageEncoder encoder = TestEnvironment.GetReferenceEncoder(fileName);
            Assert.IsType(expectedEncoderType, encoder);
        }

        [Theory]
        [InlineData("lol/foo.png", typeof(MagickReferenceDecoder))]
        [InlineData("lol/Rofl.bmp", typeof(SystemDrawingReferenceDecoder))]
        [InlineData("lol/Baz.JPG", typeof(JpegDecoder))]
        [InlineData("lol/Baz.gif", typeof(GifDecoder))]
        public void GetReferenceDecoder_ReturnsCorrectDecoders_Windows(string fileName, Type expectedDecoderType)
        {
            if (TestEnvironment.IsLinux) return;

            IImageDecoder decoder = TestEnvironment.GetReferenceDecoder(fileName);
            Assert.IsType(expectedDecoderType, decoder);
        }

        [Theory]
        [InlineData("lol/foo.png", typeof(PngEncoder))]
        [InlineData("lol/Rofl.bmp", typeof(BmpEncoder))]
        [InlineData("lol/Baz.JPG", typeof(JpegEncoder))]
        [InlineData("lol/Baz.gif", typeof(GifEncoder))]
        public void GetReferenceEncoder_ReturnsCorrectEncoders_Linux(string fileName, Type expectedEncoderType)
        {
            if (!TestEnvironment.IsLinux) return;

            IImageEncoder encoder = TestEnvironment.GetReferenceEncoder(fileName);
            Assert.IsType(expectedEncoderType, encoder);
        }

        [Theory]
        [InlineData("lol/foo.png", typeof(MagickReferenceDecoder))]
        [InlineData("lol/Rofl.bmp", typeof(SystemDrawingReferenceDecoder))]
        [InlineData("lol/Baz.JPG", typeof(JpegDecoder))]
        [InlineData("lol/Baz.gif", typeof(GifDecoder))]
        public void GetReferenceDecoder_ReturnsCorrectDecoders_Linux(string fileName, Type expectedDecoderType)
        {
            if (!TestEnvironment.IsLinux) return;

            IImageDecoder decoder = TestEnvironment.GetReferenceDecoder(fileName);
            Assert.IsType(expectedDecoderType, decoder);
        }
    }
}
