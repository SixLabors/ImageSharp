// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using SixLabors.ImageSharp.IO;

namespace SixLabors.ImageSharp.Tests.TestUtilities
{
    internal class SingleStreamFileSystem : IFileSystem
    {
        private readonly Stream stream;

        public SingleStreamFileSystem(Stream stream) => this.stream = stream;

        Stream IFileSystem.Create(string path) => this.stream;

        Stream IFileSystem.OpenRead(string path) => this.stream;
    }
}
