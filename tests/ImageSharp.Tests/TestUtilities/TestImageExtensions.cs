// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests
{
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
        public static Image<TPixel> DebugSave<TPixel>(this Image<TPixel> image, ITestImageProvider provider, object settings = null, string extension = "png")
            where TPixel : struct, IPixel<TPixel>
        {
            if (bool.TryParse(Environment.GetEnvironmentVariable("CI"), out bool isCi) && isCi)
            {
                return image;
            }

            // We are running locally then we want to save it out
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

            provider.Utility.SaveTestOutputFile(image, extension, tag: tag);
            return image;
        }
    }
}
