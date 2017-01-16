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

    /// <summary>
    /// Utility class to provide information about the test image & the test case for the test code,
    /// and help managing IO.
    /// </summary>
    public class ImagingTestCaseUtility : TestBase
    {
        /// <summary>
        /// Name of the TColor in the owner <see cref="TestImageProvider{TColor}"/>
        /// </summary>
        public string PixelTypeName { get; set; } = string.Empty;

        /// <summary>
        /// The name of the file which is provided by <see cref="TestImageProvider{TColor}"/>
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
                pixName = '_' + pixName;
            }

            return $"{this.GetTestOutputDir()}/{this.TestName}{pixName}{fn}{extension}";
        }

        /// <summary>
        /// Encodes image by the format matching the required extension, than saves it to the recommended output file.
        /// </summary>
        /// <typeparam name="TColor">The pixel format of the image</typeparam>
        /// <param name="image">The image instance</param>
        /// <param name="extension">The requested extension</param>
        /// <param name="encoder">Optional encoder</param>
        public void SaveTestOutputFile<TColor>(Image<TColor> image, string extension = null, IImageEncoder encoder = null)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            string path = this.GetTestOutputFileName(extension);

            var format = GetImageFormatByExtension(extension);

            encoder = encoder ?? format.Encoder;

            using (var stream = File.OpenWrite(path))
            {
                image.Save(stream, encoder);
            }
        }

        internal void Init(MethodInfo method)
        {
            this.TestGroupName = method.DeclaringType.Name;
            this.TestName = method.Name;
        }

        private static IImageFormat GetImageFormatByExtension(string extension)
        {
            extension = extension.ToLower();
            return Configuration.Default.ImageFormats.First(f => f.SupportedExtensions.Contains(extension));
        }

        private string GetTestOutputDir()
        {
            string testGroupName = Path.GetFileNameWithoutExtension(this.TestGroupName);

            return CreateOutputDirectory(testGroupName);
        }
    }
}