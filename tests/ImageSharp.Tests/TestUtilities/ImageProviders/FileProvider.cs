// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Concurrent;
using SixLabors.ImageSharp.PixelFormats;
using Xunit.Abstractions;

namespace SixLabors.ImageSharp.Tests
{
    public abstract partial class TestImageProvider<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        private class FileProvider : TestImageProvider<TPixel>, IXunitSerializable
        {
            // Need PixelTypes in the dictionary key, because result images of TestImageProvider<TPixel>.FileProvider
            // are shared between PixelTypes.Color & PixelTypes.Rgba32
            private class Key : Tuple<PixelTypes, string>
            {
                public Key(PixelTypes item1, string item2)
                    : base(item1, item2)
                {
                }
            }

            private static readonly ConcurrentDictionary<Key, Image<TPixel>> cache = new ConcurrentDictionary<Key, Image<TPixel>>();

            private string filePath;

            public FileProvider(string filePath)
            {
                this.filePath = filePath;
            }

            public FileProvider()
            {
            }

            public override string SourceFileOrDescription => this.filePath;

            public override Image<TPixel> GetImage()
            {
                Key key = new Key(this.PixelType, this.filePath);

                Image<TPixel> cachedImage = cache.GetOrAdd(
                    key,
                    fn =>
                        {
                            TestFile testFile = TestFile.Create(this.filePath);
                            return this.Factory.CreateImage(testFile.Bytes);
                        });

                return this.Factory.CreateImage(cachedImage);
            }

            public override void Deserialize(IXunitSerializationInfo info)
            {
                this.filePath = info.GetValue<string>("path");

                base.Deserialize(info); // must be called last
            }

            public override void Serialize(IXunitSerializationInfo info)
            {
                base.Serialize(info);
                info.AddValue("path", this.filePath);
            }
        }
    }
}