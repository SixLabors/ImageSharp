// <copyright file="TestImage.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

using ImageSharp.Drawing;
using System.IO;

namespace ImageSharp.Tests
{
    public class TestFont
    {
        private readonly Font font;
        private readonly string file;

        public TestFont(string file)
        {
            this.file = file;

            using (FileStream stream = File.OpenRead(file))
            {
                this.font = new Font(stream);
            }
        }

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

        public Font CreateFont()
        {
            return new Font(this.font);
        }
    }
}
