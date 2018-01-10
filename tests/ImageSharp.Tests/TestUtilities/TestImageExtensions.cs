// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;
using SixLabors.ImageSharp.Tests.TestUtilities.ReferenceCodecs;

namespace SixLabors.ImageSharp.Tests
{
    using System.Numerics;
    using SixLabors.ImageSharp.Advanced;
    using SixLabors.ImageSharp.Memory;

    using Xunit;

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
        /// <param name="appendPixelTypeToFileName">A boolean indicating whether to append the pixel type to the  output file name.</param>
        public static Image<TPixel> DebugSave<TPixel>(
            this Image<TPixel> image,
            ITestImageProvider provider,
            object testOutputDetails = null,
            string extension = "png",
            bool appendPixelTypeToFileName = true)
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
                appendPixelTypeToFileName: appendPixelTypeToFileName);
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
        /// <param name="appendPixelTypeToFileName">A boolean indicating whether to append the pixel type to the  output file name.</param>
        /// <returns></returns>
        public static Image<TPixel> CompareToReferenceOutput<TPixel>(
            this Image<TPixel> image,
            ITestImageProvider provider,
            object testOutputDetails = null,
            string extension = "png",
            bool grayscale = false,
            bool appendPixelTypeToFileName = true)
            where TPixel : struct, IPixel<TPixel>
        {
            return CompareToReferenceOutput(
                image,
                provider,
                ImageComparer.Tolerant(),
                testOutputDetails,
                extension,
                grayscale,
                appendPixelTypeToFileName);
        }
        
        /// <summary>
        /// Compares the image against the expected Reference output, throws an exception if the images are not similar enough.
        /// The output file should be named identically to the output produced by <see cref="DebugSave{TPixel}(Image{TPixel}, ITestImageProvider, object, string, bool)"/>.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format</typeparam>
        /// <param name="image">The image</param>
        /// <param name="provider">The image provider</param>
        /// <param name="comparer">The <see cref="ImageComparer"/> to use</param>
        /// <param name="testOutputDetails">Details to be concatenated to the test output file, describing the parameters of the test.</param>
        /// <param name="extension">The extension</param>
        /// <param name="grayscale">A boolean indicating whether we should debug save + compare against a grayscale image, smaller in size.</param>
        /// <param name="appendPixelTypeToFileName">A boolean indicating whether to append the pixel type to the  output file name.</param>
        /// <returns></returns>
        public static Image<TPixel> CompareToReferenceOutput<TPixel>(
            this Image<TPixel> image,
            ITestImageProvider provider,
            ImageComparer comparer,
            object testOutputDetails = null,
            string extension = "png",
            bool grayscale = false,
            bool appendPixelTypeToFileName = true)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> referenceImage = GetReferenceOutputImage<TPixel>(
                provider,
                testOutputDetails,
                extension,
                appendPixelTypeToFileName)) 
            {
                comparer.VerifySimilarity(referenceImage, image);
            }

            return image;
        }

        public static Image<TPixel> GetReferenceOutputImage<TPixel>(this ITestImageProvider provider,
                                                                    object testOutputDetails = null,
                                                                    string extension = "png",
                                                                    bool appendPixelTypeToFileName = true)
            where TPixel : struct, IPixel<TPixel>
        {
            string referenceOutputFile = provider.Utility.GetReferenceOutputFileName(extension, testOutputDetails, appendPixelTypeToFileName);

            if (!File.Exists(referenceOutputFile))
            {
                throw new Exception("Reference output file missing: " + referenceOutputFile);
            }

            IImageDecoder decoder = TestEnvironment.GetReferenceDecoder(referenceOutputFile);

            return Image.Load<TPixel>(referenceOutputFile, decoder);
        }

        public static IEnumerable<ImageSimilarityReport> GetReferenceOutputSimilarityReports<TPixel>(
            this Image<TPixel> image,
            ITestImageProvider provider,
            ImageComparer comparer,
            object testOutputDetails = null,
            string extension = "png",
            bool appendPixelTypeToFileName = true)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> referenceImage = provider.GetReferenceOutputImage<TPixel>(
                testOutputDetails,
                extension,
                appendPixelTypeToFileName))
            {
                return comparer.CompareImages(referenceImage, image);
            }
        }

        public static Image<TPixel> ComparePixelBufferTo<TPixel>(
            this Image<TPixel> image,
            Span<TPixel> expectedPixels)
            where TPixel : struct, IPixel<TPixel>
        {
            Span<TPixel> actual = image.GetPixelSpan();

            Assert.True(expectedPixels.Length == actual.Length, "Buffer sizes are not equal!");

            for (int i = 0; i < expectedPixels.Length; i++)
            {
                Assert.True(expectedPixels[i].Equals(actual[i]), $"Pixels are different on position {i}!");
            }

            return image;
        }

        public static ImageFrame<TPixel> ComparePixelBufferTo<TPixel>(
                    this ImageFrame<TPixel> image,
                    Span<TPixel> expectedPixels)
                    where TPixel : struct, IPixel<TPixel>
        {
            Span<TPixel> actual = image.GetPixelSpan();

            Assert.True(expectedPixels.Length == actual.Length, "Buffer sizes are not equal!");

            for (int i = 0; i < expectedPixels.Length; i++)
            {
                Assert.True(expectedPixels[i].Equals(actual[i]), $"Pixels are different on position {i}!");
            }

            return image;
        }

        public static Image<TPixel> CompareToOriginal<TPixel>(
            this Image<TPixel> image,
            ITestImageProvider provider)
            where TPixel : struct, IPixel<TPixel>
        {
            return CompareToOriginal(image, provider, ImageComparer.Tolerant());
        }
        
        public static Image<TPixel> CompareToOriginal<TPixel>(
            this Image<TPixel> image,
            ITestImageProvider provider,
            ImageComparer comparer)
            where TPixel : struct, IPixel<TPixel>
        {
            string path = TestImageProvider<TPixel>.GetFilePathOrNull(provider);
            if (path == null)
            {
                throw new InvalidOperationException("CompareToOriginal() works only with file providers!");
            }

            var testFile = TestFile.Create(path);

            IImageDecoder referenceDecoder = TestEnvironment.GetReferenceDecoder(path);
            IImageFormat format = TestEnvironment.GetImageFormat(path);
            IImageDecoder defaultDecoder = Configuration.Default.FindDecoder(format);

            //if (referenceDecoder.GetType() == defaultDecoder.GetType())
            //{
            //    throw new InvalidOperationException($"Can't use CompareToOriginal(): no actual reference decoder registered for {format.Name}");
            //}

            using (var original = Image.Load<TPixel>(testFile.Bytes, referenceDecoder))
            {
                comparer.VerifySimilarity(original, image);
            }

            return image;
        }

        internal static Image<Rgba32> ToGrayscaleImage(this Buffer2D<float> buffer, float scale)
        {
            var image = new Image<Rgba32>(buffer.Width, buffer.Height);

            Span<Rgba32> pixels = image.Frames.RootFrame.GetPixelSpan();

            for (int i = 0; i < buffer.Length; i++)
            {
                float value = buffer[i] * scale;
                var v = new Vector4(value, value, value, 1f);
                pixels[i].PackFromVector4(v);
            }

            return image;
        }
    }
}