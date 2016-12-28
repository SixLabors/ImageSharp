// <copyright file="FileProvider.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
{
    using System;
    using System.Collections.Concurrent;

    public abstract partial class TestImageProvider<TColor>
        where TColor : struct, IPackedPixel, IEquatable<TColor>
    {
        private class FileProvider : TestImageProvider<TColor>
        {
            // Need PixelTypes in the dictionary key, because result images of TestImageProvider<TColor>.FileProvider 
            // are shared between PixelTypes.Color & PixelTypes.StandardImageClass
            private class Key : Tuple<PixelTypes, string>
            {
                public Key(PixelTypes item1, string item2)
                    : base(item1, item2)
                {
                }
            }

            private static ConcurrentDictionary<Key, Image<TColor>> cache =
                new ConcurrentDictionary<Key, Image<TColor>>();

            private string filePath;

            public FileProvider(string filePath)
            {
                this.filePath = filePath;
            }

            public override string SourceFileOrDescription => this.filePath;

            public override Image<TColor> GetImage()
            {
                var key = new Key(this.PixelType, this.filePath);

                var cachedImage = cache.GetOrAdd(
                    key,
                    fn =>
                        {
                            var testFile = TestFile.Create(this.filePath);
                            return this.Factory.CreateImage(testFile.Bytes);
                        });

                return this.Factory.CreateImage(cachedImage);
            }
        }
    }
}