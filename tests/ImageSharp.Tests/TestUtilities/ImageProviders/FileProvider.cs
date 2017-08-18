// <copyright file="FileProvider.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
{
    using System;
    using System.Collections.Concurrent;

    using ImageSharp.Formats;
    using ImageSharp.PixelFormats;

    using Xunit.Abstractions;

    public abstract partial class TestImageProvider<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        private class FileProvider : TestImageProvider<TPixel>, IXunitSerializable
        {
            // Need PixelTypes in the dictionary key, because result images of TestImageProvider<TPixel>.FileProvider
            // are shared between PixelTypes.Color & PixelTypes.Rgba32
            private class Key : Tuple<PixelTypes, string, Type>
            {
                public Key(PixelTypes pixelType, string filePath, Type customDecoderType = null)
                    : base(pixelType, filePath, customDecoderType)
                {
                }
            }

            private static readonly ConcurrentDictionary<Key, Image<TPixel>> cache = new ConcurrentDictionary<Key, Image<TPixel>>();
            
            public FileProvider(string filePath)
            {
                this.FilePath = filePath;
            }

            public FileProvider()
            {
            }

            public string FilePath { get; private set; }

            public override string SourceFileOrDescription => this.FilePath;

            public override Image<TPixel> GetImage()
            {
                IImageDecoder decoder = TestEnvironment.GetReferenceDecoder(this.FilePath);
                return this.GetImage(decoder);
            }

            public override Image<TPixel> GetImage(IImageDecoder decoder)
            {
                Guard.NotNull(decoder, nameof(decoder));

                Key key = new Key(this.PixelType, this.FilePath, decoder.GetType());

                Image<TPixel> cachedImage = cache.GetOrAdd(
                    key,
                    fn =>
                        {
                            TestFile testFile = TestFile.Create(this.FilePath);
                            return Image.Load<TPixel>(testFile.Bytes, decoder);
                        });

                return cachedImage.Clone();
            }

            public override void Deserialize(IXunitSerializationInfo info)
            {
                this.FilePath = info.GetValue<string>("path");

                base.Deserialize(info); // must be called last
            }

            public override void Serialize(IXunitSerializationInfo info)
            {
                base.Serialize(info);
                info.AddValue("path", this.FilePath);
            }
        }

        public static string GetFilePathOrNull(ITestImageProvider provider)
        {
            var fileProvider = provider as FileProvider;
            return fileProvider?.FilePath;
        }
    }
}