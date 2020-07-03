// Copyright (c) Six Labors.
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
        /// Gets or sets the name of the TPixel in the owner <see cref="TestImageProvider{TPixel}"/>
        /// </summary>
        public string PixelTypeName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the name of the file which is provided by <see cref="TestImageProvider{TPixel}"/>
        /// Or a short string describing the image in the case of a non-file based image provider.
        /// </summary>
        public string SourceFileOrDescription { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the test group name.
        /// By default this is the name of the test class, but it's possible to change it.
        /// </summary>
        public string TestGroupName { get; set; } = string.Empty;

        public string OutputSubfolderName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the name of the test case (by default).
        /// </summary>
        public string TestName { get; set; } = string.Empty;

        private string GetTestOutputFileNameImpl(
            string extension,
            string details,
            bool appendPixelTypeToFileName,
            bool appendSourceFileOrDescription)
        {
            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = null;
            }

            string fn = appendSourceFileOrDescription
                            ? Path.GetFileNameWithoutExtension(this.SourceFileOrDescription)
                            : string.Empty;

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

            if (fn != string.Empty)
            {
                fn = '_' + fn;
            }

            string pixName = string.Empty;

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

            return TestUtils.AsInvariantString($"{this.GetTestOutputDir()}/{this.TestName}{pixName}{fn}{details}{extension}");
        }

        /// <summary>
        /// Gets the recommended file name for the output of the test
        /// </summary>
        /// <param name="extension">The required extension</param>
        /// <param name="testOutputDetails">The settings modifying the output path</param>
        /// <param name="appendPixelTypeToFileName">A boolean indicating whether to append the pixel type to output file name.</param>
        /// <param name="appendSourceFileOrDescription">A boolean indicating whether to append <see cref="ITestImageProvider.SourceFileOrDescription"/> to the test output file name.</param>
        /// <returns>The file test name</returns>
        public string GetTestOutputFileName(
            string extension = null,
            object testOutputDetails = null,
            bool appendPixelTypeToFileName = true,
            bool appendSourceFileOrDescription = true)
        {
            string detailsString = null;

            if (testOutputDetails is FormattableString fs)
            {
                detailsString = fs.AsInvariantString();
            }
            else if (testOutputDetails is string s)
            {
                detailsString = s;
            }
            else if (testOutputDetails != null)
            {
                Type type = testOutputDetails.GetType();
                TypeInfo info = type.GetTypeInfo();
                if (info.IsPrimitive || info.IsEnum || type == typeof(decimal))
                {
                    detailsString = TestUtils.AsInvariantString($"{testOutputDetails}");
                }
                else
                {
                    IEnumerable<PropertyInfo> properties = testOutputDetails.GetType().GetRuntimeProperties();

                    detailsString = string.Join(
                        "_",
                        properties.ToDictionary(x => x.Name, x => x.GetValue(testOutputDetails))
                            .Select(x => TestUtils.AsInvariantString($"{x.Key}-{x.Value}")));
                }
            }

            return this.GetTestOutputFileNameImpl(
                extension,
                detailsString,
                appendPixelTypeToFileName,
                appendSourceFileOrDescription);
        }

        /// <summary>
        /// Encodes image by the format matching the required extension, than saves it to the recommended output file.
        /// </summary>
        /// <param name="image">The image instance.</param>
        /// <param name="extension">The requested extension.</param>
        /// <param name="encoder">Optional encoder.</param>
        /// <param name="testOutputDetails">Additional information to append to the test output file name.</param>
        /// <param name="appendPixelTypeToFileName">A value indicating whether to append the pixel type to the test output file name.</param>
        /// <param name="appendSourceFileOrDescription">A boolean indicating whether to append <see cref="ITestImageProvider.SourceFileOrDescription"/> to the test output file name.</param>
        /// <returns>The path to the saved image file.</returns>
        public string SaveTestOutputFile(
            Image image,
            string extension = null,
            IImageEncoder encoder = null,
            object testOutputDetails = null,
            bool appendPixelTypeToFileName = true,
            bool appendSourceFileOrDescription = true)
        {
            string path = this.GetTestOutputFileName(
                extension,
                testOutputDetails,
                appendPixelTypeToFileName,
                appendSourceFileOrDescription);

            encoder = encoder ?? TestEnvironment.GetReferenceEncoder(path);

            using (FileStream stream = File.OpenWrite(path))
            {
                image.Save(stream, encoder);
            }

            return path;
        }

        public IEnumerable<string> GetTestOutputFileNamesMultiFrame(
            int frameCount,
            string extension = null,
            object testOutputDetails = null,
            bool appendPixelTypeToFileName = true,
            bool appendSourceFileOrDescription = true)
        {
            string baseDir = this.GetTestOutputFileName(string.Empty, testOutputDetails, appendPixelTypeToFileName, appendSourceFileOrDescription);

            if (!Directory.Exists(baseDir))
            {
                Directory.CreateDirectory(baseDir);
            }

            for (int i = 0; i < frameCount; i++)
            {
                string filePath = $"{baseDir}/{i:D2}.{extension}";
                yield return filePath;
            }
        }

        public string[] SaveTestOutputFileMultiFrame<TPixel>(
            Image<TPixel> image,
            string extension = "png",
            IImageEncoder encoder = null,
            object testOutputDetails = null,
            bool appendPixelTypeToFileName = true)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            encoder = encoder ?? TestEnvironment.GetReferenceEncoder($"foo.{extension}");

            string[] files = this.GetTestOutputFileNamesMultiFrame(
                image.Frames.Count,
                extension,
                testOutputDetails,
                appendPixelTypeToFileName).ToArray();

            for (int i = 0; i < image.Frames.Count; i++)
            {
                using (Image<TPixel> frameImage = image.Frames.CloneFrame(i))
                {
                    string filePath = files[i];
                    using (FileStream stream = File.OpenWrite(filePath))
                    {
                        frameImage.Save(stream, encoder);
                    }
                }
            }

            return files;
        }

        internal string GetReferenceOutputFileName(
            string extension,
            object testOutputDetails,
            bool appendPixelTypeToFileName,
            bool appendSourceFileOrDescription)
        {
            return TestEnvironment.GetReferenceOutputFileName(
                this.GetTestOutputFileName(extension, testOutputDetails, appendPixelTypeToFileName, appendSourceFileOrDescription));
        }

        public string[] GetReferenceOutputFileNamesMultiFrame(
            int frameCount,
            string extension,
            object testOutputDetails,
            bool appendPixelTypeToFileName = true)
        {
            return this.GetTestOutputFileNamesMultiFrame(frameCount, extension, testOutputDetails)
                .Select(TestEnvironment.GetReferenceOutputFileName).ToArray();
        }

        internal void Init(string typeName, string methodName, string outputSubfolderName)
        {
            this.TestGroupName = typeName;
            this.TestName = methodName;
            this.OutputSubfolderName = outputSubfolderName;
        }

        internal string GetTestOutputDir()
        {
            string testGroupName = Path.GetFileNameWithoutExtension(this.TestGroupName);

            if (!string.IsNullOrEmpty(this.OutputSubfolderName))
            {
                testGroupName = Path.Combine(this.OutputSubfolderName, testGroupName);
            }

            return TestEnvironment.CreateOutputDirectory(testGroupName);
        }

        public static void ModifyPixel<TPixel>(Image<TPixel> img, int x, int y, byte perChannelChange)
            where TPixel : unmanaged, IPixel<TPixel> => ModifyPixel(img.Frames.RootFrame, x, y, perChannelChange);

        public static void ModifyPixel<TPixel>(ImageFrame<TPixel> img, int x, int y, byte perChannelChange)
        where TPixel : unmanaged, IPixel<TPixel>
        {
            TPixel pixel = img[x, y];
            Rgba64 rgbaPixel = default;
            rgbaPixel.FromScaledVector4(pixel.ToScaledVector4());
            ushort change = (ushort)Math.Round((perChannelChange / 255F) * 65535F);

            if (rgbaPixel.R + perChannelChange <= 255)
            {
                rgbaPixel.R += change;
            }
            else
            {
                rgbaPixel.R -= change;
            }

            if (rgbaPixel.G + perChannelChange <= 255)
            {
                rgbaPixel.G += change;
            }
            else
            {
                rgbaPixel.G -= change;
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

            pixel.FromRgba64(rgbaPixel);
            img[x, y] = pixel;
        }
    }
}
