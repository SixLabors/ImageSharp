// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Tests
{
    /// <summary>
    /// Various utility and extension methods.
    /// </summary>
    public static class TestUtils
    {
        private static readonly Dictionary<Type, PixelTypes> ClrTypes2PixelTypes = new Dictionary<Type, PixelTypes>();

        private static readonly Assembly ImageSharpAssembly = typeof(Rgba32).GetTypeInfo().Assembly;

        private static readonly Dictionary<PixelTypes, Type> PixelTypes2ClrTypes = new Dictionary<PixelTypes, Type>();

        private static readonly PixelTypes[] AllConcretePixelTypes = GetAllPixelTypes()
            .Except(new[] { PixelTypes.Undefined, PixelTypes.All })
            .ToArray();

        static TestUtils()
        {
            // Add Rgba32 Our default.
            Type defaultPixelFormatType = typeof(Rgba32);
            PixelTypes2ClrTypes[PixelTypes.Rgba32] = defaultPixelFormatType;
            ClrTypes2PixelTypes[defaultPixelFormatType] = PixelTypes.Rgba32;

            // Add PixelFormat types
            string nameSpace = typeof(Alpha8).FullName;
            nameSpace = nameSpace.Substring(0, nameSpace.Length - typeof(Alpha8).Name.Length - 1);
            foreach (PixelTypes pt in AllConcretePixelTypes.Where(pt => pt != PixelTypes.Rgba32))
            {
                string typeName = $"{nameSpace}.{pt}";
                Type t = ImageSharpAssembly.GetType(typeName);
                PixelTypes2ClrTypes[pt] = t ?? throw new InvalidOperationException($"Could not find: {typeName}");
                ClrTypes2PixelTypes[t] = pt;
            }
        }

        public static bool HasFlag(this PixelTypes pixelTypes, PixelTypes flag) => (pixelTypes & flag) == flag;

        public static bool IsEquivalentTo<TPixel>(this Image<TPixel> a, Image<TPixel> b, bool compareAlpha = true)
            where TPixel : struct, IPixel<TPixel>
        {
            if (a.Width != b.Width || a.Height != b.Height)
            {
                return false;
            }

            var rgb1 = default(Rgb24);
            var rgb2 = default(Rgb24);

            Buffer2D<TPixel> pixA = a.GetRootFramePixelBuffer();
            Buffer2D<TPixel> pixB = b.GetRootFramePixelBuffer();
            for (int y = 0; y < a.Height; y++)
            {
                for (int x = 0; x < a.Width; x++)
                {
                    TPixel ca = pixA[x, y];
                    TPixel cb = pixB[x, y];

                    if (compareAlpha)
                    {
                        if (!ca.Equals(cb))
                        {
                            return false;
                        }
                    }
                    else
                    {
                        ca.ToRgb24(ref rgb1);
                        cb.ToRgb24(ref rgb2);

                        if (rgb1.R != rgb2.R || rgb1.G != rgb2.G || rgb1.B != rgb2.B)
                        {
                            return false;
                        }
                    }
                }
            }

            return true;
        }

        public static string ToCsv<T>(this IEnumerable<T> items, string separator = ",")
        {
            return String.Join(separator, items.Select(o => String.Format(CultureInfo.InvariantCulture, "{0}", o)));
        }

        public static Type GetClrType(this PixelTypes pixelType) => PixelTypes2ClrTypes[pixelType];

        /// <summary>
        /// Returns the <see cref="PixelTypes"/> enumerations for the given type.
        /// </summary>
        /// <param name="colorStructClrType"></param>
        /// <returns></returns>
        public static PixelTypes GetPixelType(this Type colorStructClrType) => ClrTypes2PixelTypes[colorStructClrType];

        public static IEnumerable<KeyValuePair<PixelTypes, Type>> ExpandAllTypes(this PixelTypes pixelTypes)
        {
            if (pixelTypes == PixelTypes.Undefined)
            {
                return Enumerable.Empty<KeyValuePair<PixelTypes, Type>>();
            }
            else if (pixelTypes == PixelTypes.All)
            {
                // TODO: Need to return unknown types here without forcing CLR to load all types in ImageSharp assembly
                return PixelTypes2ClrTypes;
            }

            var result = new Dictionary<PixelTypes, Type>();
            foreach (PixelTypes pt in AllConcretePixelTypes)
            {
                if (pixelTypes.HasAll(pt))
                {
                    result[pt] = pt.GetClrType();
                }
            }
            return result;
        }

        internal static bool HasAll(this PixelTypes pixelTypes, PixelTypes flagsToCheck) =>
            (pixelTypes & flagsToCheck) == flagsToCheck;

        /// <summary>
        /// Enumerate all available <see cref="PixelTypes"/>-s
        /// </summary>
        /// <returns>The pixel types</returns>
        internal static PixelTypes[] GetAllPixelTypes() => (PixelTypes[])Enum.GetValues(typeof(PixelTypes));

        internal static TPixel GetPixelOfNamedColor<TPixel>(string colorName)
            where TPixel : struct, IPixel<TPixel>
        {
            return (TPixel)typeof(NamedColors<TPixel>).GetTypeInfo().GetField(colorName).GetValue(null);
        }

        /// <summary>
        /// Utility for testing image processor extension methods:
        /// 1. Run a processor defined by 'process'
        /// 2. Run 'DebugSave()' to save the output locally
        /// 3. Run 'CompareToReferenceOutput()' to compare the results to the expected output
        /// </summary>
        /// <param name="provider">The <see cref="TestImageProvider{TPixel}"/></param>
        /// <param name="process">The image processing method to test. (As a delegate)</param>
        /// <param name="testOutputDetails">The value to append to the test output.</param>
        /// <param name="comparer">The custom image comparer to use</param>
        /// <param name="appendPixelTypeToFileName"></param>
        /// <param name="appendSourceFileOrDescription"></param>
        internal static void RunValidatingProcessorTest<TPixel>(
            this TestImageProvider<TPixel> provider,
            Action<IImageProcessingContext<TPixel>> process,
            object testOutputDetails = null,
            ImageComparer comparer = null,
            bool appendPixelTypeToFileName = true,
            bool appendSourceFileOrDescription = true)
            where TPixel : struct, IPixel<TPixel>
        {
            if (comparer == null)
            {
                comparer = ImageComparer.TolerantPercentage(0.001f);
            }

            using (Image<TPixel> image = provider.GetImage())
            {
                image.Mutate(process);

                image.DebugSave(
                    provider,
                    testOutputDetails,
                    appendPixelTypeToFileName: appendPixelTypeToFileName,
                    appendSourceFileOrDescription: appendSourceFileOrDescription);

                // TODO: Investigate the cause of pixel inaccuracies under Linux
                if (TestEnvironment.IsWindows)
                {
                    image.CompareToReferenceOutput(
                        comparer,
                        provider,
                        testOutputDetails,
                        appendPixelTypeToFileName: appendPixelTypeToFileName,
                        appendSourceFileOrDescription: appendSourceFileOrDescription);
                }
            }
        }

        public static void RunValidatingProcessorTestOnWrappedMemoryImage<TPixel>(
            this TestImageProvider<TPixel> provider,
            Action<IImageProcessingContext<TPixel>> process,
            object testOutputDetails = null,
            ImageComparer comparer = null,
            string useReferenceOutputFrom = null,
            bool appendPixelTypeToFileName = true,
            bool appendSourceFileOrDescription = true)
            where TPixel : struct, IPixel<TPixel>
        {
            if (comparer == null)
            {
                comparer = ImageComparer.TolerantPercentage(0.001f);
            }

            using (Image<TPixel> image0 = provider.GetImage())
            {
                var mmg = TestMemoryManager<TPixel>.CreateAsCopyOf(image0.GetPixelSpan());

                using (var image1 = Image.WrapMemory(mmg.Memory, image0.Width, image0.Height))
                {
                    image1.Mutate(process);
                    image1.DebugSave(
                        provider,
                        testOutputDetails,
                        appendPixelTypeToFileName: appendPixelTypeToFileName,
                        appendSourceFileOrDescription: appendSourceFileOrDescription);

                    // TODO: Investigate the cause of pixel inaccuracies under Linux
                    if (TestEnvironment.IsWindows)
                    {
                        string testNameBackup = provider.Utility.TestName;

                        if (useReferenceOutputFrom != null)
                        {
                            provider.Utility.TestName = useReferenceOutputFrom;
                        }

                        image1.CompareToReferenceOutput(
                            comparer,
                            provider,
                            testOutputDetails,
                            appendPixelTypeToFileName: appendPixelTypeToFileName,
                            appendSourceFileOrDescription: appendSourceFileOrDescription);

                        provider.Utility.TestName = testNameBackup;
                    }
                }
            }
        }

        /// <summary>
        /// Same as <see cref="RunValidatingProcessorTest{TPixel}"/> but with an additional <see cref="Rectangle"/> parameter passed to 'process'
        /// </summary>
        internal static void RunRectangleConstrainedValidatingProcessorTest<TPixel>(
            this TestImageProvider<TPixel> provider,
            Action<IImageProcessingContext<TPixel>, Rectangle> process,
            object testOutputDetails = null,
            ImageComparer comparer = null)
            where TPixel : struct, IPixel<TPixel>
        {
            if (comparer == null)
            {
                comparer = ImageComparer.TolerantPercentage(0.001f);
            }

            using (Image<TPixel> image = provider.GetImage())
            {
                var bounds = new Rectangle(image.Width / 4, image.Width / 4, image.Width / 2, image.Height / 2);
                image.Mutate(x => process(x, bounds));
                image.DebugSave(provider, testOutputDetails);
                image.CompareToReferenceOutput(comparer, provider, testOutputDetails);
            }
        }

        /// <summary>
        /// Same as <see cref="RunValidatingProcessorTest{TPixel}"/> but without the 'CompareToReferenceOutput()' step.
        /// </summary>
        internal static void RunProcessorTest<TPixel>(
            this TestImageProvider<TPixel> provider,
            Action<IImageProcessingContext<TPixel>> process,
            object testOutputDetails = null)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                image.Mutate(process);
                image.DebugSave(provider, testOutputDetails);
            }
        }

        public static string AsInvariantString(this FormattableString formattable) => System.FormattableString.Invariant(formattable);
    }
}