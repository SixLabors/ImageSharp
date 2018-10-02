// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;

using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Memory;
using SixLabors.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;

using Xunit;

namespace SixLabors.ImageSharp.Tests
{
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
            MemoryAllocator memoryAllocator = ctx.MemoryAllocator;

            ctx.Apply(img =>
            {
                using (Buffer2D<Vector4> temp = memoryAllocator.Allocate2D<Vector4>(img.Width, img.Height))
                {
                    Span<Vector4> tempSpan = temp.GetSpan();
                    foreach (ImageFrame<TPixel> frame in img.Frames)
                    {
                        Span<TPixel> pixelSpan = frame.GetPixelSpan();

                        PixelOperations<TPixel>.Instance.ToScaledVector4(pixelSpan, tempSpan, pixelSpan.Length);

                        for (int i = 0; i < tempSpan.Length; i++)
                        {
                            ref Vector4 v = ref tempSpan[i];
                            v.W = 1F;
                        }

                        PixelOperations<TPixel>.Instance.PackFromScaledVector4(tempSpan, pixelSpan, pixelSpan.Length);
                    }
                }
            });
        }

        public static Image<TPixel> DebugSave<TPixel>(
            this Image<TPixel> image,
            ITestImageProvider provider,
            FormattableString testOutputDetails,
            string extension = "png",
            bool appendPixelTypeToFileName = true,
            bool appendSourceFileOrDescription = true)
            where TPixel : struct, IPixel<TPixel>
        {
            return image.DebugSave(
                provider,
                (object)testOutputDetails,
                extension,
                appendPixelTypeToFileName,
                appendSourceFileOrDescription);
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
        /// <param name="appendSourceFileOrDescription">A boolean indicating whether to append <see cref="ITestImageProvider.SourceFileOrDescription"/> to the test output file name.</param>
        public static Image<TPixel> DebugSave<TPixel>(
            this Image<TPixel> image,
            ITestImageProvider provider,
            object testOutputDetails = null,
            string extension = "png",
            bool appendPixelTypeToFileName = true,
            bool appendSourceFileOrDescription = true)
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
                appendPixelTypeToFileName: appendPixelTypeToFileName,
                appendSourceFileOrDescription: appendSourceFileOrDescription);
            return image;
        }

        public static Image<TPixel> DebugSave<TPixel>(
            this Image<TPixel> image,
            ITestImageProvider provider,
            IImageEncoder encoder,
            FormattableString testOutputDetails,
            bool appendPixelTypeToFileName = true)
            where TPixel : struct, IPixel<TPixel>
        {
            return image.DebugSave(provider, encoder, (object)testOutputDetails, appendPixelTypeToFileName);
        }

        /// <summary>
        /// Saves the image only when not running in the CI server.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format</typeparam>
        /// <param name="image">The image</param>
        /// <param name="provider">The image provider</param>
        /// <param name="encoder">The image encoder</param>
        /// <param name="testOutputDetails">Details to be concatenated to the test output file, describing the parameters of the test.</param>
        /// <param name="appendPixelTypeToFileName">A boolean indicating whether to append the pixel type to the  output file name.</param>
        public static Image<TPixel> DebugSave<TPixel>(
            this Image<TPixel> image,
            ITestImageProvider provider,
            IImageEncoder encoder,
            object testOutputDetails = null,
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
                encoder: encoder,
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

        public static Image<TPixel> CompareToReferenceOutput<TPixel>(
            this Image<TPixel> image,
            ITestImageProvider provider,
            FormattableString testOutputDetails,
            string extension = "png",
            bool grayscale = false,
            bool appendPixelTypeToFileName = true,
            bool appendSourceFileOrDescription = true)
            where TPixel : struct, IPixel<TPixel>
        {
            return image.CompareToReferenceOutput(
                provider,
                (object)testOutputDetails,
                extension,
                grayscale,
                appendPixelTypeToFileName,
                appendSourceFileOrDescription);
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
        /// <param name="appendSourceFileOrDescription">A boolean indicating whether to append <see cref="ITestImageProvider.SourceFileOrDescription"/> to the test output file name.</param>
        /// <returns></returns>
        public static Image<TPixel> CompareToReferenceOutput<TPixel>(
            this Image<TPixel> image,
            ITestImageProvider provider,
            object testOutputDetails = null,
            string extension = "png",
            bool grayscale = false,
            bool appendPixelTypeToFileName = true,
            bool appendSourceFileOrDescription = true)
            where TPixel : struct, IPixel<TPixel>
        {
            return CompareToReferenceOutput(
                image,
                ImageComparer.Tolerant(),
                provider,
                testOutputDetails,
                extension,
                grayscale,
                appendPixelTypeToFileName,
                appendSourceFileOrDescription);
        }

        public static Image<TPixel> CompareToReferenceOutput<TPixel>(
            this Image<TPixel> image,
            ImageComparer comparer,
            ITestImageProvider provider,
            FormattableString testOutputDetails,
            string extension = "png",
            bool grayscale = false,
            bool appendPixelTypeToFileName = true)
            where TPixel : struct, IPixel<TPixel>
        {
            return image.CompareToReferenceOutput(
                comparer,
                provider,
                (object)testOutputDetails,
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
        /// <param name="appendSourceFileOrDescription">A boolean indicating whether to append <see cref="ITestImageProvider.SourceFileOrDescription"/> to the test output file name.</param>
        /// <returns></returns>
        public static Image<TPixel> CompareToReferenceOutput<TPixel>(
            this Image<TPixel> image,
            ImageComparer comparer,
            ITestImageProvider provider,
            object testOutputDetails = null,
            string extension = "png",
            bool grayscale = false,
            bool appendPixelTypeToFileName = true,
            bool appendSourceFileOrDescription = true)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> referenceImage = GetReferenceOutputImage<TPixel>(
                provider,
                testOutputDetails,
                extension,
                appendPixelTypeToFileName,
                appendSourceFileOrDescription))
            {
                comparer.VerifySimilarity(referenceImage, image);
            }

            return image;
        }

        public static Image<TPixel> CompareFirstFrameToReferenceOutput<TPixel>(
            this Image<TPixel> image,
            ImageComparer comparer,
            ITestImageProvider provider,
            FormattableString testOutputDetails,
            string extension = "png",
            bool grayscale = false,
            bool appendPixelTypeToFileName = true,
            bool appendSourceFileOrDescription = true)
            where TPixel : struct, IPixel<TPixel>
        {
            return image.CompareFirstFrameToReferenceOutput(
                comparer,
                provider,
                (object)testOutputDetails,
                extension,
                grayscale,
                appendPixelTypeToFileName,
                appendSourceFileOrDescription);
        }

        public static Image<TPixel> CompareFirstFrameToReferenceOutput<TPixel>(
            this Image<TPixel> image,
            ImageComparer comparer,
            ITestImageProvider provider,
            object testOutputDetails = null,
            string extension = "png",
            bool grayscale = false,
            bool appendPixelTypeToFileName = true,
            bool appendSourceFileOrDescription = true)
            where TPixel : struct, IPixel<TPixel>
        {
            using (var firstFrameOnlyImage = new Image<TPixel>(image.Width, image.Height))
            using (Image<TPixel> referenceImage = GetReferenceOutputImage<TPixel>(
                provider,
                testOutputDetails,
                extension,
                appendPixelTypeToFileName,
                appendSourceFileOrDescription))
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
                                                                    bool appendPixelTypeToFileName = true,
                                                                    bool appendSourceFileOrDescription = true)
            where TPixel : struct, IPixel<TPixel>
        {
            string referenceOutputFile = provider.Utility.GetReferenceOutputFileName(
                extension,
                testOutputDetails,
                appendPixelTypeToFileName,
                appendSourceFileOrDescription);

            if (!File.Exists(referenceOutputFile))
            {
                throw new System.IO.FileNotFoundException("Reference output file missing: " + referenceOutputFile, referenceOutputFile);
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
            Span<TPixel> actualPixels = image.GetPixelSpan();

            CompareBuffers(expectedPixels, actualPixels);

            return image;
        }

        public static void CompareBuffers<T>(Span<T> expected, Span<T> actual)
            where T : struct, IEquatable<T>
        {
            Assert.True(expected.Length == actual.Length, "Buffer sizes are not equal!");

            for (int i = 0; i < expected.Length; i++)
            {
                T x = expected[i];
                T a = actual[i];

                Assert.True(x.Equals(a), $"Buffers differ at position {i}! Expected: {x} | Actual: {a}");
            }
        }

        /// <summary>
        /// All pixels in all frames should be exactly equal to 'expectedPixel'.
        /// </summary>
        public static Image<TPixel> ComparePixelBufferTo<TPixel>(this Image<TPixel> image, TPixel expectedPixel)
            where TPixel : struct, IPixel<TPixel>
        {
            foreach (ImageFrame<TPixel> imageFrame in image.Frames)
            {
                imageFrame.ComparePixelBufferTo(expectedPixel);
            }
            
            return image;
        }

        /// <summary>
        /// All pixels in the frame should be exactly equal to 'expectedPixel'.
        /// </summary>
        public static ImageFrame<TPixel> ComparePixelBufferTo<TPixel>(this ImageFrame<TPixel> imageFrame, TPixel expectedPixel)
            where TPixel : struct, IPixel<TPixel>
        {
            Span<TPixel> actualPixels = imageFrame.GetPixelSpan();

            for (int i = 0; i < actualPixels.Length; i++)
            {
                Assert.True(expectedPixel.Equals(actualPixels[i]), $"Pixels are different on position {i}!");
            }

            return imageFrame;
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
            ITestImageProvider provider,
            IImageDecoder referenceDecoder = null)
            where TPixel : struct, IPixel<TPixel>
        {
            return CompareToOriginal(image, provider, ImageComparer.Tolerant(), referenceDecoder);
        }

        public static Image<TPixel> CompareToOriginal<TPixel>(
            this Image<TPixel> image,
            ITestImageProvider provider,
            ImageComparer comparer,
            IImageDecoder referenceDecoder = null)
            where TPixel : struct, IPixel<TPixel>
        {
            string path = TestImageProvider<TPixel>.GetFilePathOrNull(provider);
            if (path == null)
            {
                throw new InvalidOperationException("CompareToOriginal() works only with file providers!");
            }

            var testFile = TestFile.Create(path);

            referenceDecoder = referenceDecoder ?? TestEnvironment.GetReferenceDecoder(path);
            
            using (var original = Image.Load<TPixel>(testFile.Bytes, referenceDecoder))
            {
                comparer.VerifySimilarity(original, image);
            }

            return image;
        }

        /// <summary>
        /// Utility method for doing the following in one step:
        /// 1. Executing an operation (taken as a delegate)
        /// 2. Executing DebugSave()
        /// 3. Executing CopareToReferenceOutput()
        /// </summary>
        internal static void VerifyOperation<TPixel>(
            this TestImageProvider<TPixel> provider,
            ImageComparer comparer,
            Action<Image<TPixel>> operation,
            FormattableString testOutputDetails,
            bool appendPixelTypeToFileName = true,
            bool appendSourceFileOrDescription = true)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                operation(image);

                image.DebugSave(
                    provider,
                    testOutputDetails,
                    appendPixelTypeToFileName: appendPixelTypeToFileName,
                    appendSourceFileOrDescription: appendSourceFileOrDescription);

                image.CompareToReferenceOutput(comparer, 
                    provider,
                    testOutputDetails,
                    appendPixelTypeToFileName: appendPixelTypeToFileName,
                    appendSourceFileOrDescription: appendSourceFileOrDescription);
            }
        }

        /// <summary>
        /// Utility method for doing the following in one step:
        /// 1. Executing an operation (taken as a delegate)
        /// 2. Executing DebugSave()
        /// 3. Executing CopareToReferenceOutput()
        /// </summary>
        internal static void VerifyOperation<TPixel>(
            this TestImageProvider<TPixel> provider,
            Action<Image<TPixel>> operation,
            FormattableString testOutputDetails,
            bool appendPixelTypeToFileName = true,
            bool appendSourceFileOrDescription = true)
            where TPixel : struct, IPixel<TPixel>
        {
            provider.VerifyOperation(
                ImageComparer.Tolerant(),
                operation,
                testOutputDetails,
                appendPixelTypeToFileName,
                appendSourceFileOrDescription);
        }

        /// <summary>
        /// Utility method for doing the following in one step:
        /// 1. Executing an operation (taken as a delegate)
        /// 2. Executing DebugSave()
        /// 3. Executing CopareToReferenceOutput()
        /// </summary>
        internal static void VerifyOperation<TPixel>(
            this TestImageProvider<TPixel> provider,
            ImageComparer comparer,
            Action<Image<TPixel>> operation,
            bool appendPixelTypeToFileName = true,
            bool appendSourceFileOrDescription = true)
            where TPixel : struct, IPixel<TPixel>
        {
            provider.VerifyOperation(
                comparer,
                operation,
                $"",
                appendPixelTypeToFileName,
                appendSourceFileOrDescription);
        }

        /// <summary>
        /// Utility method for doing the following in one step:
        /// 1. Executing an operation (taken as a delegate)
        /// 2. Executing DebugSave()
        /// 3. Executing CopareToReferenceOutput()
        /// </summary>
        internal static void VerifyOperation<TPixel>(
            this TestImageProvider<TPixel> provider,
            Action<Image<TPixel>> operation,
            bool appendPixelTypeToFileName = true,
            bool appendSourceFileOrDescription = true)
            where TPixel : struct, IPixel<TPixel>
        {
            provider.VerifyOperation(operation, $"", appendPixelTypeToFileName, appendSourceFileOrDescription);
        }

        /// <summary>
        /// Loads the expected image with a reference decoder + compares it to <paramref name="image"/>.
        /// Also performs a debug save using <see cref="ImagingTestCaseUtility.SaveTestOutputFile{TPixel}"/>.
        /// </summary>
        internal static void VerifyEncoder<TPixel>(
            this Image<TPixel> image,
            ITestImageProvider provider,
            string extension,
            object testOutputDetails,
            IImageEncoder encoder,
            ImageComparer customComparer = null,
            bool appendPixelTypeToFileName = true,
            string referenceImageExtension = null,
            IImageDecoder referenceDecoder = null)
            where TPixel : struct, IPixel<TPixel>
        {
            string actualOutputFile = provider.Utility.SaveTestOutputFile(
                image,
                extension,
                encoder,
                testOutputDetails,
                appendPixelTypeToFileName);

            referenceDecoder = referenceDecoder ?? TestEnvironment.GetReferenceDecoder(actualOutputFile);

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

            Span<float> bufferSpan = buffer.GetSpan();

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