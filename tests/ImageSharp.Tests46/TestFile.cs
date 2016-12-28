// <copyright file="TestImage.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>
namespace ImageSharp.Tests
{
    using System;
    using System.Collections.Concurrent;
    using System.IO;

    public class TestFile
    {
        private static readonly ConcurrentDictionary<string, TestFile> cache = new ConcurrentDictionary<string, TestFile>();
        private static readonly string FormatsDirectory = GetFormatsDirectory();

        private static string GetFormatsDirectory()
        {
          return "../../../ImageSharp.Tests/TestImages/Formats/";
        }

        private readonly Image image;
        private readonly string file;

        private TestFile(string file)
        {
            this.file = file;

            this.Bytes = File.ReadAllBytes(file);
            this.image = new Image(this.Bytes);
        }

        public static string GetPath(string file)
        {
            return Path.Combine(FormatsDirectory, file);
        }

        public static TestFile Create(string file)
        {
            return cache.GetOrAdd(file, (string fileName) =>
            {
                return new TestFile(FormatsDirectory + fileName);
            });
        }

        public byte[] Bytes { get; }

        public string FileName
        {
            get
            {
                return Path.GetFileName(this.file);
            }
        }

        public string FileNameWithoutExtension
        {
            get
            {
                return Path.GetFileNameWithoutExtension(this.file);
            }
        }

        public string GetFileName(object value)
        {
            return this.FileNameWithoutExtension + "-" + value + Path.GetExtension(this.file);
        }

        public string GetFileNameWithoutExtension(object value)
        {
            return this.FileNameWithoutExtension + "-" + value;
        }

        public Image CreateImage()
        {
            return new Image(this.image);
        }
    }
}
