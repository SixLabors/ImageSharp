// <copyright file="ImagingTestCaseUtility.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>
namespace ImageSharp.Tests.TestUtilities
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Reflection;

    public class ImagingTestCaseUtility
    {
        public string PixelTypeName { get; set; } = string.Empty;

        public string SourceFileOrDescription { get; set; } = string.Empty;

        public string TestGroupName { get; set; } = string.Empty;

        public string TestName { get; set; } = string.Empty;

        public string TestOutputRoot { get; set; } = FileTestBase.TestOutputRoot;

        public string GetTestOutputDir()
        {
            string testGroupName = Path.GetFileNameWithoutExtension(this.TestGroupName);

            string dir = $@"{this.TestOutputRoot}{testGroupName}";
            Directory.CreateDirectory(dir);
            return dir;
        }

        public string GetTestOutputFileName(string extension = null)
        {
            string fn = string.Empty;

            fn = Path.GetFileNameWithoutExtension(this.SourceFileOrDescription);
            extension = extension ?? Path.GetExtension(this.SourceFileOrDescription);
            extension = extension ?? ".bmp";

            if (extension[0] != '.')
            {
                extension = '.' + extension;
            }

            if (fn != string.Empty) fn = '_' + fn;

            string pixName = this.PixelTypeName;
            if (pixName != string.Empty)
            {
                pixName = '_' + pixName + ' ';
            }

            return $"{this.GetTestOutputDir()}/{this.TestName}{pixName}{fn}{extension}";
        }

        // TODO: This is messy, need to refactor all the output writing logic out from TestImageFactory
        public void SaveTestOutputFile<TColor, TPacked>(Image<TColor, TPacked> image, string extension = null)
            where TColor : struct, IPackedPixel<TPacked> where TPacked : struct, IEquatable<TPacked>
        {
            string path = this.GetTestOutputFileName(extension);

            var format = Bootstrapper.Instance.ImageFormats.First(f => f.Encoder.IsSupportedFileExtension(extension));

            using (var stream = File.OpenWrite(path))
            {
                image.Save(stream, format);
            }
        }

        internal void Init(MethodInfo method)
        {
            this.TestGroupName = method.DeclaringType.Name;
            this.TestName = method.Name;
        }
    }
}