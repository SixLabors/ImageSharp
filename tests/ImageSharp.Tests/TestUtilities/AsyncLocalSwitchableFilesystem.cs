// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using System.Threading;
using SixLabors.ImageSharp.IO;

namespace SixLabors.ImageSharp.Tests.TestUtilities
{
    public class AsyncLocalSwitchableFilesystem : IFileSystem
    {
        private static readonly LocalFileSystem LocalFile = new LocalFileSystem();

        private static readonly AsyncLocalSwitchableFilesystem Instance = new AsyncLocalSwitchableFilesystem();

        internal static void ConfigureDefaultFileSystem(IFileSystem fileSystem)
        {
            Configuration.Default.FileSystem = Instance;
            Instance.FileSystem = fileSystem;
        }

        internal static void ConfigureFileSystemStream(Stream stream)
        {
            Configuration.Default.FileSystem = Instance;
            Instance.FileSystem = new SingleStreamFileSystem(stream);
        }

        private readonly AsyncLocal<IFileSystem> asyncLocal = new AsyncLocal<IFileSystem>();

        private IFileSystem FileSystem
        {
            get => this.asyncLocal.Value ?? LocalFile;
            set => this.asyncLocal.Value = value;
        }

        public Stream Create(string path) => this.FileSystem.Create(path);

        public Stream OpenRead(string path) => this.FileSystem.OpenRead(path);

        public class SingleStreamFileSystem : IFileSystem
        {
            private readonly Stream stream;

            public SingleStreamFileSystem(Stream stream) => this.stream = stream;

            Stream IFileSystem.Create(string path) => this.stream;

            Stream IFileSystem.OpenRead(string path) => this.stream;
        }
    }
}
