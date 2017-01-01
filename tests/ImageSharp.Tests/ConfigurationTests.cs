// <copyright file="ConfigurationTests.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using ImageSharp.Formats;

    using Xunit;

    /// <summary>
    /// Tests the configuration class.
    /// </summary>
    public class ConfigurationTests
    {
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
            Assert.True(Configuration.Default.ImageFormats != null);
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
                Configuration.Default.AddImageFormat(null);
            });
        }

        /// <summary>
        /// Tests the <see cref="M:Configuration.AddImageFormat"/> method throws an exception
        /// when the encoder is null.
        /// </summary>
        [Fact]
        public void TestAddImageFormatThrowsWithNullEncoder()
        {
            TestFormat format = new TestFormat { Encoder = null };

            Assert.Throws<ArgumentNullException>(() =>
            {
                Configuration.Default.AddImageFormat(format);
            });
        }

        /// <summary>
        /// Tests the <see cref="M:Configuration.AddImageFormat"/> method throws an exception
        /// when the decoder is null.
        /// </summary>
        [Fact]
        public void TestAddImageFormatThrowsWithNullDecoder()
        {
            TestFormat format = new TestFormat { Decoder = null };

            Assert.Throws<ArgumentNullException>(() =>
            {
                Configuration.Default.AddImageFormat(format);
            });
        }

        /// <summary>
        /// Tests the <see cref="M:Configuration.AddImageFormat"/> method throws an exception
        /// when the mime type is null or an empty string.
        /// </summary>
        [Fact]
        public void TestAddImageFormatThrowsWithNullOrEmptyMimeType()
        {
            TestFormat format = new TestFormat { MimeType = null };

            Assert.Throws<ArgumentNullException>(() =>
            {
                Configuration.Default.AddImageFormat(format);
            });

            format = new TestFormat { MimeType = string.Empty };

            Assert.Throws<ArgumentException>(() =>
            {
                Configuration.Default.AddImageFormat(format);
            });
        }

        /// <summary>
        /// Tests the <see cref="M:Configuration.AddImageFormat"/> method throws an exception
        /// when the extension is null or an empty string.
        /// </summary>
        [Fact]
        public void TestAddImageFormatThrowsWithNullOrEmptyExtension()
        {
            TestFormat format = new TestFormat { Extension = null };

            Assert.Throws<ArgumentNullException>(() =>
            {
                Configuration.Default.AddImageFormat(format);
            });

            format = new TestFormat { Extension = string.Empty };

            Assert.Throws<ArgumentException>(() =>
            {
                Configuration.Default.AddImageFormat(format);
            });
        }

        /// <summary>
        /// Tests the <see cref="M:Configuration.AddImageFormat"/> method throws an exception
        /// when the supported extensions list is null or empty.
        /// </summary>
        [Fact]
        public void TestAddImageFormatThrowsWenSupportedExtensionsIsNullOrEmpty()
        {
            TestFormat format = new TestFormat { SupportedExtensions = null };

            Assert.Throws<ArgumentNullException>(() =>
            {
                Configuration.Default.AddImageFormat(format);
            });

            format = new TestFormat { SupportedExtensions = Enumerable.Empty<string>() };

            Assert.Throws<ArgumentException>(() =>
            {
                Configuration.Default.AddImageFormat(format);
            });
        }

        /// <summary>
        /// Tests the <see cref="M:Configuration.AddImageFormat"/> method throws an exception
        /// when the supported extensions list does not contain the default extension.
        /// </summary>
        [Fact]
        public void TestAddImageFormatThrowsWithoutDefaultExtension()
        {
            TestFormat format = new TestFormat { Extension = "test" };

            Assert.Throws<ArgumentException>(() =>
            {
                Configuration.Default.AddImageFormat(format);
            });
        }

        /// <summary>
        /// Tests the <see cref="M:Configuration.AddImageFormat"/> method throws an exception
        /// when the supported extensions list contains an empty string.
        /// </summary>
        [Fact]
        public void TestAddImageFormatThrowsWithEmptySupportedExtension()
        {
            TestFormat format = new TestFormat
            {
                Extension = "test",
                SupportedExtensions = new[] { "test", string.Empty }
            };

            Assert.Throws<ArgumentException>(() =>
            {
                Configuration.Default.AddImageFormat(format);
            });
        }

        /// <summary>
        /// Test that the <see cref="M:Configuration.AddImageFormat"/> method ignores adding duplicate image formats.
        /// </summary>
        [Fact]
        public void TestConfigurationIgnoresDuplicateImageFormats()
        {
            Configuration.Default.AddImageFormat(new PngFormat());
            Configuration.Default.AddImageFormat(new PngFormat());

            Assert.True(Configuration.Default.ImageFormats.Count(i => i.GetType() == typeof(PngFormat)) == 1);
        }

        /// <summary>
        /// Test that the default image constructors use default configuration.
        /// </summary>
        [Fact]
        public void TestImageUsesDefaultConfiguration()
        {
            Configuration.Default.AddImageFormat(new PngFormat());

            Image image = new Image(1, 1);
            Assert.Equal(image.Configuration.ParallelOptions, Configuration.Default.ParallelOptions);
            Assert.Equal(image.Configuration.ImageFormats, Configuration.Default.ImageFormats);
        }

        /// <summary>
        /// Test that the default image constructor copies the configuration.
        /// </summary>
        [Fact]
        public void TestImageCopiesConfiguration()
        {
            Configuration.Default.AddImageFormat(new PngFormat());

            Image image = new Image(1, 1);
            Image image2 = new Image(image);
            Assert.Equal(image2.Configuration.ParallelOptions, image.Configuration.ParallelOptions);
            Assert.True(image2.Configuration.ImageFormats.SequenceEqual(image.Configuration.ImageFormats));
        }

        /// <summary>
        /// A test image format for testing the configuration.
        /// </summary>
        private class TestFormat : IImageFormat
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="TestFormat"/> class.
            /// </summary>
            public TestFormat()
            {
                this.Decoder = new JpegDecoder();
                this.Encoder = new JpegEncoder();
                this.Extension = "jpg";
                this.MimeType = "image/test";
                this.SupportedExtensions = new[] { "jpg" };
            }

            /// <inheritdoc />
            public IImageDecoder Decoder { get; set; }

            /// <inheritdoc />
            public IImageEncoder Encoder { get; set; }

            /// <inheritdoc />
            public string MimeType { get; set; }

            /// <inheritdoc />
            public string Extension { get; set; }

            /// <inheritdoc />
            public IEnumerable<string> SupportedExtensions { get; set; }

            /// <inheritdoc />
            public int HeaderSize
            {
                get
                {
                    throw new NotImplementedException();
                }
            }

            /// <inheritdoc />
            public bool IsSupportedFileFormat(byte[] header)
            {
                throw new NotImplementedException();
            }
        }
    }
}