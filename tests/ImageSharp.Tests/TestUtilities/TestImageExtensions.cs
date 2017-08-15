// <copyright file="TestImageExtensions.cs" company="James Jackson-South">
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

    using ImageSharp.PixelFormats;
    using ImageSharp.Tests.TestUtilities.ImageComparison;
    using ImageSharp.Tests.TestUtilities.ReferenceCodecs;

    public static class TestImageExtensions
    {
        /// <summary>
        /// Saves the image only when not running in the CI server.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format</typeparam>
        /// <param name="image">The image</param>
        /// <param name="provider">The image provider</param>
        /// <param name="testOutputDetails">Details to be concatenated to the test output file, describing the parameters of the test.</param>
        /// <param name="extension">The extension</param>
        /// /// <param name="grayscale">A boolean indicating whether we should save a smaller in size.</param>
        public static Image<TPixel> DebugSave<TPixel>(
            this Image<TPixel> image,
            ITestImageProvider provider,
            object testOutputDetails = null,
            string extension = "png",
            bool grayscale = false)
            where TPixel : struct, IPixel<TPixel>
        {
            if (TestEnvironment.RunsOnCI)
            {
                return image;
            }
            
            // We are running locally then we want to save it out
            provider.Utility.SaveTestOutputFile(
                image,
                extension,
                testOutputDetails: testOutputDetails,
                grayscale: grayscale);
            return image;
        }

        /// <summary>
        /// Compares the image against the expected Reference output, throws an exception if the images are not similar enough.
        /// The output file should be named identically to the output produced by <see cref="DebugSave{TPixel}(Image{TPixel}, ITestImageProvider, object, string, bool)"/>.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format</typeparam>
        /// <param name="image">The image</param>
        /// <param name="provider">The image provider</param>
        /// <param name="testOutputDetails">Details to be concatenated to the test output file, describing the parameters of the test.</param>
        /// <param name="extension">The extension</param>
        /// <param name="grayscale">A boolean indicating whether we should debug save + compare against a grayscale image, smaller in size.</param>
        /// <param name="customImageComparer">A custom <see cref="ImageComparer"/> if exact equlity is not the expected behaviour</param>
        /// <returns></returns>
        public static Image<TPixel> CompareToReferenceOutput<TPixel>(
            this Image<TPixel> image,
            ITestImageProvider provider,
            object testOutputDetails = null,
            string extension = "png",
            bool grayscale = false)
            where TPixel : struct, IPixel<TPixel>
        {
            string referenceOutputFile = provider.Utility.GetReferenceOutputFileName(extension, testOutputDetails);

            if (!TestEnvironment.RunsOnCI)
            {
                provider.Utility.SaveTestOutputFile(
                    image,
                    extension,
                    testOutputDetails: testOutputDetails,
                    grayscale: grayscale);
            }

            if (!File.Exists(referenceOutputFile))
            {
                throw new Exception("Reference output file missing: " + referenceOutputFile);
            }

            using (Image<Rgba32> referenceImage = Image.Load<Rgba32>(referenceOutputFile, ReferenceDecoder.Instance))
            {
                ImageComparer comparer = ImageComparer.Exact;
                comparer.CompareImages(referenceImage, image);
            }
            
            return image;
        }
    }
}
