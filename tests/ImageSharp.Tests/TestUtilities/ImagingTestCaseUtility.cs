// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests
{
    /// <summary>
    /// Utility class to provide information about the test image & the test case for the test code,
    /// and help managing IO.
    /// </summary>
    public class ImagingTestCaseUtility
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

        private string GetTestOutputFileNameImpl(string extension, string details, bool appendPixelTypeToFileName)
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
            extension = extension.ToLower();

            if (extension[0] != '.')
            {
                extension = '.' + extension;
            }

            if (fn != string.Empty) fn = '_' + fn;

            string pixName = "";

            if (appendPixelTypeToFileName)
            {
                pixName = this.PixelTypeName;

                if (pixName != string.Empty)
                {
                    pixName = '_' + pixName;
                }
            }

            details = details ?? string.Empty;
            if (details != string.Empty)
            {
                details = '_' + details;
            }

            return $"{this.GetTestOutputDir()}/{this.TestName}{pixName}{fn}{details}{extension}";
        }

        /// <summary>
        /// Gets the recommended file name for the output of the test
        /// </summary>
        /// <param name="extension">The required extension</param>
        /// <param name="testOutputDetails">The settings modifying the output path</param>
        /// <param name="appendPixelTypeToFileName">A boolean indicating whether to append the pixel type to output file name.</param>
        /// <returns>The file test name</returns>
        public string GetTestOutputFileName(string extension = null, object testOutputDetails = null, bool appendPixelTypeToFileName = true)
        {
            string detailsString = null;
            string s = testOutputDetails as string;

            if (s != null)
            {
                detailsString = s;
            }
            else if (testOutputDetails != null)
            {
                Type type = testOutputDetails.GetType();
                TypeInfo info = type.GetTypeInfo();
                if (info.IsPrimitive || info.IsEnum || type == typeof(decimal))
                {
                    detailsString = testOutputDetails.ToString();
                }
                else
                {
                    IEnumerable<PropertyInfo> properties = testOutputDetails.GetType().GetRuntimeProperties();

                    detailsString = String.Join("_", properties.ToDictionary(x => x.Name, x => x.GetValue(testOutputDetails)).Select(x => $"{x.Key}-{x.Value}"));
                }
            }
            return this.GetTestOutputFileNameImpl(extension, detailsString, appendPixelTypeToFileName);
        }


        /// <summary>
        /// Encodes image by the format matching the required extension, than saves it to the recommended output file.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format of the image</typeparam>
        /// <param name="image">The image instance</param>
        /// <param name="extension">The requested extension</param>
        /// <param name="encoder">Optional encoder</param>
        public string SaveTestOutputFile<TPixel>(
            Image<TPixel> image,
            string extension = null,
            IImageEncoder encoder = null,
            object testOutputDetails = null,
            bool appendPixelTypeToFileName = true)
            where TPixel : struct, IPixel<TPixel>
        {
            string path = this.GetTestOutputFileName(extension, testOutputDetails, appendPixelTypeToFileName);
            encoder = encoder ?? TestEnvironment.GetReferenceEncoder(path);

            using (FileStream stream = File.OpenWrite(path))
            {
                image.Save(stream, encoder);
            }
            return path;
        }

        internal string GetReferenceOutputFileName(
            string extension,
            object settings,
            bool appendPixelTypeToFileName)
        {
            return TestEnvironment.GetReferenceOutputFileName(
                this.GetTestOutputFileName(extension, settings, appendPixelTypeToFileName)
                );
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

        //private static IImageEncoder GetEncoderByExtension(string extension, bool grayscale)
        //{
        //    extension = extension?.TrimStart('.');
        //    var format = Configuration.Default.FindFormatByFileExtension(extension);
        //    IImageEncoder encoder = Configuration.Default.FindEncoder(format);
        //    PngEncoder pngEncoder = encoder as PngEncoder;
        //    if (pngEncoder != null)
        //    {
        //        pngEncoder = new PngEncoder();
        //        encoder = pngEncoder;
        //        pngEncoder.CompressionLevel = 9;

        //        if (grayscale)
        //        {
        //            pngEncoder.PngColorType = PngColorType.Grayscale;
        //        }
        //    }

        //    return encoder;
        //}

        internal string GetTestOutputDir()
        {
            string testGroupName = Path.GetFileNameWithoutExtension(this.TestGroupName);
            return TestEnvironment.CreateOutputDirectory(testGroupName);
        }

        public static void ModifyPixel<TPixel>(Image<TPixel> img, int x, int y, byte perChannelChange)
            where TPixel : struct, IPixel<TPixel>
        {
            ModifyPixel((ImageFrame<TPixel>)img, x, y, perChannelChange);
        }

        public static void ModifyPixel<TPixel>(ImageFrame<TPixel> img, int x, int y, byte perChannelChange)
        where TPixel : struct, IPixel<TPixel>
        {
            TPixel pixel = img[x, y];
            var rgbaPixel = default(Rgba32);
            pixel.ToRgba32(ref rgbaPixel);

            if (rgbaPixel.R + perChannelChange <= 255)
            {
                rgbaPixel.R += perChannelChange;
            }
            else
            {
                rgbaPixel.R -= perChannelChange;
            }

            if (rgbaPixel.G + perChannelChange <= 255)
            {
                rgbaPixel.G += perChannelChange;
            }
            else
            {
                rgbaPixel.G -= perChannelChange;
            }

            if (rgbaPixel.B + perChannelChange <= 255)
            {
                rgbaPixel.B += perChannelChange;
            }
            else
            {
                rgbaPixel.B -= perChannelChange;
            }

            if (rgbaPixel.A + perChannelChange <= 255)
            {
                rgbaPixel.A += perChannelChange;
            }
            else
            {
                rgbaPixel.A -= perChannelChange;
            }

            pixel.PackFromRgba32(rgbaPixel);
            img[x, y] = pixel;
        }
    }
}