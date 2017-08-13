// <copyright file="ImagingTestCaseUtility.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
{
    using System;
    using System.Collections.Generic;
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

        
        private string GetTestOutputFileNameImpl(string extension, string tag)
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
        /// Gets the recommended file name for the output of the test
        /// </summary>
        /// <param name="extension">The required extension</param>
        /// <param name="settings">The settings modifying the output path</param>
        /// <returns>The file test name</returns>
        public string GetTestOutputFileName(string extension = null, object settings = null)
        {
            string tag = null;
            string s = settings as string;

            if (s != null)
            {
                tag = s;
            }
            else if (settings != null)
            {
                Type type = settings.GetType();
                TypeInfo info = type.GetTypeInfo();
                if (info.IsPrimitive || info.IsEnum || type == typeof(decimal))
                {
                    tag = settings.ToString();
                }
                else
                {
                    IEnumerable<PropertyInfo> properties = settings.GetType().GetRuntimeProperties();

                    tag = string.Join("_", properties.ToDictionary(x => x.Name, x => x.GetValue(settings)).Select(x => $"{x.Key}-{x.Value}"));
                }
            }
            return this.GetTestOutputFileNameImpl(extension, tag);
        }


        /// <summary>
        /// Encodes image by the format matching the required extension, than saves it to the recommended output file.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format of the image</typeparam>
        /// <param name="image">The image instance</param>
        /// <param name="extension">The requested extension</param>
        /// <param name="encoder">Optional encoder</param>
        public void SaveTestOutputFile<TPixel>(
            Image<TPixel> image,
            string extension = null,
            IImageEncoder encoder = null,
            object settings = null)
            where TPixel : struct, IPixel<TPixel>
        {
            string path = this.GetTestOutputFileName(extension: extension, settings: settings);
            string extension1 = Path.GetExtension(path);
            encoder = encoder ?? GetImageFormatByExtension(extension1);
            
            using (FileStream stream = File.OpenWrite(path))
            {
                image.Save(stream, encoder);
            }
        }

        internal string GetReferenceOutputFileName(string extension = null, object settings = null) 
            => this.GetTestOutputFileName(extension, settings).Replace("TestOutput", "ReferenceOutput");

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
            var format = Configuration.Default.FindFormatByFileExtensions(extension);
            return Configuration.Default.FindEncoder(format);
        }

        private string GetTestOutputDir()
        {
            string testGroupName = Path.GetFileNameWithoutExtension(this.TestGroupName);
            return this.CreateOutputDirectory(testGroupName);
        }
    }
}