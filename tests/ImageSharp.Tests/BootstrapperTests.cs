// <copyright file="BootstrapperTests.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
{
    using System;
    using System.Collections.Generic;
    using ImageSharp.Formats;
    using Xunit;
    using System.Linq;

    public class BootstrapperTests
    {
        private class TestFormat : IImageFormat
        {
            private IImageDecoder decoder;
            private IImageEncoder encoder;
            private string mimeType;
            private string extension;
            private IEnumerable<string> supportedExtensions;

            public TestFormat()
            {
                this.decoder = new JpegDecoder();
                this.encoder = new JpegEncoder();
                this.extension = "jpg";
                this.mimeType = "image/test";
                this.supportedExtensions = new string[] { "jpg" };
            }

            public IImageDecoder Decoder { get { return this.decoder; } set { this.decoder = value; } }

            public IImageEncoder Encoder { get { return this.encoder; } set { this.encoder = value; } }

            public string MimeType { get { return this.mimeType; } set { this.mimeType = value; } }

            public string Extension { get { return this.extension; } set { this.extension = value; } }

            public IEnumerable<string> SupportedExtensions { get { return this.supportedExtensions; } set { this.supportedExtensions = value; } }
        }

        [Fact]
        public void AddImageFormatGuardNull()
        {
            ArgumentException exception;

            exception = Assert.Throws<ArgumentNullException>(() =>
            {
                Bootstrapper.AddImageFormat(null);
            });

            var format = new TestFormat();
            format.Decoder = null;

            exception = Assert.Throws<ArgumentNullException>(() =>
            {
                Bootstrapper.AddImageFormat(format);
            });
            Assert.Contains("decoder", exception.Message);

            format = new TestFormat();
            format.Encoder = null;

            exception = Assert.Throws<ArgumentNullException>(() =>
            {
                Bootstrapper.AddImageFormat(format);
            });
            Assert.Contains("encoder", exception.Message);

            format = new TestFormat();
            format.MimeType = null;

            exception = Assert.Throws<ArgumentNullException>(() =>
            {
                Bootstrapper.AddImageFormat(format);
            });
            Assert.Contains("mime type", exception.Message);

            format = new TestFormat();
            format.MimeType = "";

            exception = Assert.Throws<ArgumentException>(() =>
            {
                Bootstrapper.AddImageFormat(format);
            });
            Assert.Contains("mime type", exception.Message);

            format = new TestFormat();
            format.Extension = null;

            exception = Assert.Throws<ArgumentNullException>(() =>
            {
                Bootstrapper.AddImageFormat(format);
            });
            Assert.Contains("extension", exception.Message);

            format = new TestFormat();
            format.Extension = "";

            exception = Assert.Throws<ArgumentException>(() =>
            {
                Bootstrapper.AddImageFormat(format);
            });
            Assert.Contains("extension", exception.Message);

            format = new TestFormat();
            format.SupportedExtensions = null;

            exception = Assert.Throws<ArgumentNullException>(() =>
            {
                Bootstrapper.AddImageFormat(format);
            });
            Assert.Contains("supported extensions", exception.Message);

            format = new TestFormat();
            format.SupportedExtensions = Enumerable.Empty<string>();

            exception = Assert.Throws<ArgumentException>(() =>
            {
                Bootstrapper.AddImageFormat(format);
            });
            Assert.Contains("supported extensions", exception.Message);
        }

        [Fact]
        public void AddImageFormatChecks()
        {
            var format = new TestFormat();

            var exception = Assert.Throws<ArgumentException>(() =>
            {
                Bootstrapper.AddImageFormat(format);
            });
            Assert.Contains("format with the same", exception.Message);

            format.Extension = "test";
            exception = Assert.Throws<ArgumentException>(() =>
            {
                Bootstrapper.AddImageFormat(format);
            });
            Assert.Contains("should contain", exception.Message);

            format.SupportedExtensions = new string[] { "test", "jpg" };
            exception = Assert.Throws<ArgumentException>(() =>
            {
                Bootstrapper.AddImageFormat(format);
            });
            Assert.Contains("supports the same", exception.Message);

            format.SupportedExtensions = new string[] { "test", "" };
            exception = Assert.Throws<ArgumentException>(() =>
            {
                Bootstrapper.AddImageFormat(format);
            });
            Assert.Contains("empty values", exception.Message);
        }
    }
}
