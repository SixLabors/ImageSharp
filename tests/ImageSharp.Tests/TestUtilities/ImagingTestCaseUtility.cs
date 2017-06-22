// <copyright file="ImagingTestCaseUtility.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Reflection;

    using ImageSharp.Formats;
    using ImageSharp.PixelFormats;

    /// <summary>
    /// Utility class to provide information about the test image & the test case for the test code,
    /// and help managing IO.
    /// </summary>
    public class ImagingTestCaseUtility : TestBase
    {
        /// <summary>
        /// Name of the TPixel in the owner <see cref="TestImageProvider{TPixel}"/>
        /// </summary>
        public string PixelTypeName { get; set; } = string.Empty;

        /// <summary>
        /// The name of the file which is provided by <see cref="TestImageProvider{TPixel}"/>
        /// Or a short string describing the image in the case of a non-file based image provider.
        /// </summary>
        public string SourceFileOrDescription { get; set; } = string.Empty;

        /// <summary>
        /// By default this is the name of the test class, but it's possible to change it
        /// </summary>
        public string TestGroupName { get; set; } = string.Empty;

        /// <summary>
        /// The name of the test case (by default)
        /// </summary>
        public string TestName { get; set; } = string.Empty;

        /// <summary>
        /// Gets the recommended file name for the output of the test
        /// </summary>
        /// <param name="extension"></param>
        /// <returns>The required extension</returns>
        public string GetTestOutputFileName(string extension = null, string tag = null)
        {
            string fn = string.Empty;

            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = null;
            }

            fn = Path.GetFileNameWithoutExtension(this.SourceFileOrDescription);

            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = Path.GetExtension(this.SourceFileOrDescription);
            }

            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = ".bmp";
            }

            if (extension[0] != '.')
            {
                extension = '.' + extension;
            }

            if (fn != string.Empty) fn = '_' + fn;

            string pixName = this.PixelTypeName;
            if (pixName != string.Empty)
            {
                pixName = '_' + pixName;
            }

            tag = tag ?? string.Empty;
            if (tag != string.Empty)
            {
                tag = '_' + tag;
            }


            return $"{this.GetTestOutputDir()}/{this.TestName}{pixName}{fn}{tag}{extension}";
        }

        /// <summary>
        /// Encodes image by the format matching the required extension, than saves it to the recommended output file.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format of the image</typeparam>
        /// <param name="image">The image instance</param>
        /// <param name="extension">The requested extension</param>
        /// <param name="encoder">Optional encoder</param>
        /// <param name="options">Optional encoder options</param>
        public void SaveTestOutputFile<TPixel>(Image<TPixel> image, string extension = null, IImageEncoder encoder = null, string tag = null)
            where TPixel : struct, IPixel<TPixel>
        {
            string path = this.GetTestOutputFileName(extension: extension, tag:tag);
            extension = Path.GetExtension(path);
            encoder = encoder ?? GetImageFormatByExtension(extension);

            using (FileStream stream = File.OpenWrite(path))
            {
                image.Save(stream, encoder);
            }
        }

        internal void Init(string typeName, string methodName)
        {
            this.TestGroupName = typeName;
            this.TestName = methodName;
        }

        internal void Init(MethodInfo method)
        {
            this.Init(method.DeclaringType.Name, method.Name);
        }

        private static IImageEncoder GetImageFormatByExtension(string extension)
        {
            extension = extension?.TrimStart('.');
            return Configuration.Default.FindFileExtensionsEncoder(extension);
        }

        private string GetTestOutputDir()
        {
            string testGroupName = Path.GetFileNameWithoutExtension(this.TestGroupName);

            return CreateOutputDirectory(testGroupName);
        }
    }
}