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
          // Here for code coverage tests.
          string directory = "TestImages/Formats/";
          if (Directory.Exists(directory))
          {
              return directory;
          }
          return "../../../../TestImages/Formats/";
        }

        private readonly Image image;
        private readonly string file;

        private TestFile(string file, bool decodeImage)
        {
            this.file = file;

            this.Bytes = File.ReadAllBytes(file);
            if (decodeImage)
            {
                this.image = new Image(this.Bytes);
            }
            
        }

        public static TestFile Create(string file) => CreateImpl(file, true);

        // No need to decode the image when used by TestImageProvider!
        internal static TestFile CreateWithoutImage(string file) => CreateImpl(file, false);
        
        private static TestFile CreateImpl(string file, bool decodeImage)
        {
            return cache.GetOrAdd(file, (string fileName) =>
            {
                return new TestFile(FormatsDirectory + fileName, decodeImage);
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
            if (this.image == null)
            {
                throw new InvalidOperationException("TestFile.CreateImage() is invalid because instance has been created with decodeImage = false!");
            }
            return new Image(this.image);
        }
    }
}
