// <copyright file="TestImage.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

using System.IO;

namespace ImageSharp.Tests
{
    public class TestFile
    {
        private readonly Image image;
        private readonly string file;

        public TestFile(string file)
        {
            this.file = file;

            this.Bytes = File.ReadAllBytes(file);
            this.image = new Image(this.Bytes);
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
