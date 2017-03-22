// <copyright file="TestImage.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using ImageSharp.Formats;
    using Xunit;

    /// <summary>
    /// A test image file.
    /// </summary>
    public class TestFormat : ImageSharp.Formats.IImageFormat
    {
        public static TestFormat GlobalTestFormat { get; } = new TestFormat();

        public static void RegisterGloablTestFormat()
        {
            Configuration.Default.AddImageFormat(GlobalTestFormat);
        }

        public TestFormat()
        {
            this.Encoder = new TestEncoder(this); ;
            this.Decoder = new TestDecoder(this); ;
        }

        public List<DecodeOperation> DecodeCalls { get; } = new List<DecodeOperation>();

        public IImageEncoder Encoder { get; }

        public IImageDecoder Decoder { get; }

        private byte[] header = Guid.NewGuid().ToByteArray();

        public MemoryStream CreateStream(byte[] marker = null)
        {
            MemoryStream ms = new MemoryStream();
            byte[] data = this.header;
            ms.Write(data, 0, data.Length);
            if (marker != null)
            {
                ms.Write(marker, 0, marker.Length);
            }
            ms.Position = 0;
            return ms;
        }

        Dictionary<Type, object> _sampleImages = new Dictionary<Type, object>();


        public void VerifyDecodeCall(byte[] marker, IDecoderOptions options, Configuration config)
        {
            DecodeOperation[] discovered = this.DecodeCalls.Where(x => x.IsMatch(marker, options, config)).ToArray();


            Assert.True(discovered.Any(), "No calls to decode on this formate with the proveded options happend");

            foreach (DecodeOperation d in discovered) {
                this.DecodeCalls.Remove(d);
            }
        }

        public Image<TColor> Sample<TColor>()
            where TColor : struct, IPixel<TColor>
        {
            lock (this._sampleImages)
            {
                if (!this._sampleImages.ContainsKey(typeof(TColor)))
                {
                    this._sampleImages.Add(typeof(TColor), new Image<TColor>(1, 1));
                }
            
                return (Image<TColor>)this._sampleImages[typeof(TColor)];
            }
        }

        public string MimeType => "img/test";

        public string Extension => "test_ext";

        public IEnumerable<string> SupportedExtensions => new[] { "test_ext" };

        public int HeaderSize => this.header.Length;

        public bool IsSupportedFileFormat(byte[] header)
        {
            if (header.Length < this.header.Length)
            {
                return false;
            }
            for (int i = 0; i < this.header.Length; i++)
            {
                if (header[i] != this.header[i])
                {
                    return false;
                }
            }
            return true;
        }
        public struct DecodeOperation
        {
            public byte[] marker;
            public IDecoderOptions options;
            internal Configuration config;

             public bool IsMatch(byte[] testMarker, IDecoderOptions testOptions, Configuration config)
            {
                if (this.options != testOptions)
                {
                    return false;
                }

                if (this.config != config)
                {
                    return false;
                }

                if (testMarker.Length != this.marker.Length)
                {
                    return false;
                }

                for (int i = 0; i < this.marker.Length; i++)
                {
                    if (testMarker[i] != this.marker[i])
                    {
                        return false;
                    }
                }
                return true;
            }
        }

        public class TestDecoder : ImageSharp.Formats.IImageDecoder
        {
            private TestFormat testFormat;

            public TestDecoder(TestFormat testFormat)
            {
                this.testFormat = testFormat;
            }


            public Image<TColor> Decode<TColor>(Stream stream, IDecoderOptions options, Configuration config) where TColor : struct, IPixel<TColor>

            {
                var ms = new MemoryStream();
                stream.CopyTo(ms);
                var marker = ms.ToArray().Skip(this.testFormat.header.Length).ToArray();
                this.testFormat.DecodeCalls.Add(new DecodeOperation
                {
                    marker = marker,
                    options = options,
                    config = config
                });

                // TODO record this happend so we an verify it.
                return this.testFormat.Sample<TColor>();
            }
        }

        public class TestEncoder : ImageSharp.Formats.IImageEncoder
        {
            private TestFormat testFormat;

            public TestEncoder(TestFormat testFormat)
            {
                this.testFormat = testFormat;
            }

            public void Encode<TColor>(Image<TColor> image, Stream stream, IEncoderOptions options) where TColor : struct, IPixel<TColor>
            {
                // TODO record this happend so we an verify it.
            }
        }
    }
}
