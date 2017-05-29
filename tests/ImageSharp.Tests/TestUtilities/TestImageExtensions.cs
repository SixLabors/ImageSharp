// <copyright file="TestImageExtensions.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;

    using ImageSharp.PixelFormats;

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
        public static void DebugSave<TPixel>(this Image<TPixel> image, ITestImageProvider provider, object settings = null, string extension = "png")
            where TPixel : struct, IPixel<TPixel>
        {
            if (bool.TryParse(Environment.GetEnvironmentVariable("CI"), out bool isCi) && isCi)
            {
                return;
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
                if (settings.GetType().GetTypeInfo().IsPrimitive)
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
        }
    }
}
