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
    using SixLabors.ImageSharp.MetaData;
    using SixLabors.ImageSharp.Processing;

    using Xunit;

    public static class TestImageExtensions
    {
        /// <summary>
        /// TODO: This should be a common processing method! The image.Opacity(val) multiplies the alpha channel!
        /// </summary>
        /// <typeparam name="TPixel"></typeparam>
        /// <param name="ctx"></param>
        public static void MakeOpaque<TPixel>(this IImageProcessingContext<TPixel> ctx)
            where TPixel : struct, IPixel<TPixel>
        {
            MemoryManager memoryManager = ctx.MemoryManager;
            ctx.Apply(
                img =>
                    {
                        using (Buffer2D<Vector4> temp = memoryManager.Allocate2D<Vector4>(img.Width, img.Height))
                        {
                            Span<Vector4> tempSpan = temp.Span;
                            foreach (ImageFrame<TPixel> frame in img.Frames)
                            {
                                Span<TPixel> pixelSpan = frame.GetPixelSpan();

                                PixelOperations<TPixel>.Instance.ToVector4(pixelSpan, tempSpan, pixelSpan.Length);

                                for (int i = 0; i < tempSpan.Length; i++)
                                {
                                    ref Vector4 v = ref tempSpan[i];
                                    v.W = 1.0f;
                                }

                                PixelOperations<TPixel>.Instance.PackFromVector4(tempSpan, pixelSpan, pixelSpan.Length);
                            }
                        }
                    });
        }

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

        public static Image<TPixel> DebugSaveMultiFrame<TPixel>(
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
            provider.Utility.SaveTestOutputFileMultiFrame(
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
        /// <param name="comparer">A custom <see cref="ImageComparer"/> for the verification</param>
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
                ImageComparer.Tolerant(),
                provider,
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
        /// <param name="comparer">The <see cref="ImageComparer"/> to use</param>
        /// <param name="provider">The image provider</param>
        /// <param name="testOutputDetails">Details to be concatenated to the test output file, describing the parameters of the test.</param>
        /// <param name="extension">The extension</param>
        /// <param name="grayscale">A boolean indicating whether we should debug save + compare against a grayscale image, smaller in size.</param>
        /// <param name="appendPixelTypeToFileName">A boolean indicating whether to append the pixel type to the  output file name.</param>
        /// <returns></returns>
        public static Image<TPixel> CompareToReferenceOutput<TPixel>(
            this Image<TPixel> image,
            ImageComparer comparer,
            ITestImageProvider provider,
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

        public static Image<TPixel> CompareFirstFrameToReferenceOutput<TPixel>(
            this Image<TPixel> image,
            ImageComparer comparer,
            ITestImageProvider provider,
            object testOutputDetails = null,
            string extension = "png",
            bool grayscale = false,
            bool appendPixelTypeToFileName = true)
            where TPixel : struct, IPixel<TPixel>
        {
            using (var firstFrameOnlyImage = new Image<TPixel>(image.Width, image.Height))
            using (Image<TPixel> referenceImage = GetReferenceOutputImage<TPixel>(
                provider,
                testOutputDetails,
                extension,
                appendPixelTypeToFileName))
            {
                firstFrameOnlyImage.Frames.AddFrame(image.Frames.RootFrame);
                firstFrameOnlyImage.Frames.RemoveFrame(0);

                comparer.VerifySimilarity(referenceImage, firstFrameOnlyImage);
            }

            return image;
        }

        public static Image<TPixel> CompareToReferenceOutputMultiFrame<TPixel>(
            this Image<TPixel> image,
            ITestImageProvider provider,
            ImageComparer comparer,
            object testOutputDetails = null,
            string extension = "png",
            bool grayscale = false,
            bool appendPixelTypeToFileName = true)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> referenceImage = GetReferenceOutputImageMultiFrame<TPixel>(
                provider,
                image.Frames.Count,
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

        public static Image<TPixel> GetReferenceOutputImageMultiFrame<TPixel>(this ITestImageProvider provider,
                                                                             int frameCount,
                                                                    object testOutputDetails = null,
                                                                    string extension = "png",
                                                                    bool appendPixelTypeToFileName = true)
            where TPixel : struct, IPixel<TPixel>
        {
            string[] frameFiles = provider.Utility.GetReferenceOutputFileNamesMultiFrame(
                frameCount,
                extension,
                testOutputDetails,
                appendPixelTypeToFileName);

            var temporaryFrameImages = new List<Image<TPixel>>();

            IImageDecoder decoder = TestEnvironment.GetReferenceDecoder(frameFiles[0]);

            foreach (string path in frameFiles)
            {
                if (!File.Exists(path))
                {
                    throw new Exception("Reference output file missing: " + path);
                }

                var tempImage = Image.Load<TPixel>(path, decoder);
                temporaryFrameImages.Add(tempImage);
            }

            Image<TPixel> firstTemp = temporaryFrameImages[0];
            
            var result = new Image<TPixel>(firstTemp.Width, firstTemp.Height);

            foreach (Image<TPixel> fi in temporaryFrameImages)
            {
                result.Frames.AddFrame(fi.Frames.RootFrame);
                fi.Dispose();
            }

            // remove the initial empty frame:
            result.Frames.RemoveFrame(0);
            return result;
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
            IImageDecoder defaultDecoder = Configuration.Default.ImageFormatsManager.FindDecoder(format);

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

        /// <summary>
        /// Loads the expected image with a reference decoder + compares it to <paramref name="image"/>.
        /// Also performs a debug save using <see cref="ImagingTestCaseUtility.SaveTestOutputFile{TPixel}"/>.
        /// </summary>
        internal static void VerifyEncoder<TPixel>(this Image<TPixel> image,
                                                   ITestImageProvider provider,
                                                   string extension,
                                                   object testOutputDetails,
                                                   IImageEncoder encoder,
                                                   ImageComparer customComparer = null,
                                                   bool appendPixelTypeToFileName = true,
                                                   string referenceImageExtension = null
                                                   )
            where TPixel : struct, IPixel<TPixel>
        {
            string actualOutputFile = provider.Utility.SaveTestOutputFile(image, extension, encoder, testOutputDetails, appendPixelTypeToFileName);
            IImageDecoder referenceDecoder = TestEnvironment.GetReferenceDecoder(actualOutputFile);

            using (var actualImage = Image.Load<TPixel>(actualOutputFile, referenceDecoder))
            {
                ImageComparer comparer = customComparer ?? ImageComparer.Exact;
                comparer.VerifySimilarity(actualImage, image);
            }
        }

        internal static Image<Rgba32> ToGrayscaleImage(this Buffer2D<float> buffer, float scale)
        {
            var image = new Image<Rgba32>(buffer.Width, buffer.Height);

            Span<Rgba32> pixels = image.Frames.RootFrame.GetPixelSpan();

            Span<float> bufferSpan = buffer.Span;

            for (int i = 0; i < bufferSpan.Length; i++)
            {
                float value = bufferSpan[i] * scale;
                var v = new Vector4(value, value, value, 1f);
                pixels[i].PackFromVector4(v);
            }

            return image;
        }

    }
}