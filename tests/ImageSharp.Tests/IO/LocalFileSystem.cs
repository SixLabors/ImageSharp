// <copyright file="LittleEndianBitConverterTests.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests.IO
{
    using System;
    using System.IO;
    using ImageSharp.IO;

    using Xunit;

    public class LocalFileSystemTests
    {
        [Fact]
        public void OpenRead()
        {
            string path = Path.GetTempFileName();
            string testData = Guid.NewGuid().ToString();
            File.WriteAllText(path, testData);

            LocalFileSystem fs = new LocalFileSystem();

            using (StreamReader r = new StreamReader(fs.OpenRead(path)))
            {
                string data = r.ReadToEnd();

                Assert.Equal(testData, data);
            }

            File.Delete(path);
        }

        [Fact]
        public void Create()
        {
            string path = Path.GetTempFileName();
            string testData = Guid.NewGuid().ToString();
            LocalFileSystem fs = new LocalFileSystem();

            using (StreamWriter r = new StreamWriter(fs.Create(path)))
            {
                r.Write(testData);
            }

            string data = File.ReadAllText(path);
            Assert.Equal(testData, data);

            File.Delete(path);
        }
    }
}