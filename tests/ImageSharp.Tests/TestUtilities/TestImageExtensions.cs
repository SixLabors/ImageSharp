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
    using ImageSharp.Tests.TestUtilities.ReferenceCodecs;

    public static class TestImageExtensions
    {
        /// <summary>
        /// Saves the image only when not running in the CI server.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format</typeparam>
        /// <param name="image">The image</param>
        /// <param name="provider">The image provider</param>
        /// <param name="settings">The settings</param>
        /// <param name="extension">The extension</param>
        public static Image<TPixel> DebugSave<TPixel>(
            this Image<TPixel> image,
            ITestImageProvider provider,
            object settings = null,
            string extension = "png")
            where TPixel : struct, IPixel<TPixel>
        {
            if (TestEnvironment.RunsOnCI)
            {
                return image;
            }

            
            // We are running locally then we want to save it out
            provider.Utility.SaveTestOutputFile(image, extension, settings: settings);
            return image;
        }

        public static Image<TPixel> CompareToReferenceOutput<TPixel>(
            this Image<TPixel> image,
            ITestImageProvider provider,
            object settings = null,
            string extension = "png",
            float imageTheshold = ImageComparer.DefaultImageThreshold,
            byte segmentThreshold = ImageComparer.DefaultSegmentThreshold,
            int scalingFactor = ImageComparer.DefaultScalingFactor)
            where TPixel : struct, IPixel<TPixel>
        {
            string referenceOutputFile = provider.Utility.GetReferenceOutputFileName(extension, settings);

            if (!TestEnvironment.RunsOnCI)
            {
                provider.Utility.SaveTestOutputFile(image, extension, settings: settings);
            }

            if (!File.Exists(referenceOutputFile))
            {
                throw new Exception("Reference output file missing: " + referenceOutputFile);
            }

            using (Image<Rgba32> referenceImage = Image.Load<Rgba32>(referenceOutputFile, ReferenceDecoder.Instance))
            {
                ImageComparer.VerifySimilarity(referenceImage, image, imageTheshold, segmentThreshold, scalingFactor);
            }
            
            return image;
        }
    }
}
