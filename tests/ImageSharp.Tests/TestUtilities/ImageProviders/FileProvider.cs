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
            private static ConcurrentDictionary<string, Image<TColor>> cache =
                new ConcurrentDictionary<string, Image<TColor>>();

            private string filePath;

            public FileProvider(string filePath)
            {
                this.filePath = filePath;
            }

            public override string SourceFileOrDescription => this.filePath;

            public override Image<TColor> GetImage()
            {
                var cachedImage = cache.GetOrAdd(
                    this.filePath,
                    fn =>
                        {
                            var testFile = TestFile.Create(this.filePath);
                            return this.Factory.CreateImage(testFile.Bytes);
                        });

                return new Image<TColor>(cachedImage);
            }
        }
    }
}