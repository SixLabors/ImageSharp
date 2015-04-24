// --------------------------------------------------------------------------------------------------------------------
// <copyright file="PostProcessorBootstrapper.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   The postprocessor bootstrapper.
//   Many thanks to Azure Image Optimizer <see href="https://github.com/ligershark/AzureJobs"/>
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Web.PostProcessor
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection;

    using ImageProcessor.Configuration;

    /// <summary>
    /// The postprocessor bootstrapper.
    /// Many thanks to Azure Image Optimizer <see href="https://github.com/ligershark/AzureJobs"/>
    /// </summary>
    internal static class PostProcessorBootstrapper
    {
        /// <summary>
        /// Initializes static members of the <see cref="PostProcessorBootstrapper"/> class.
        /// </summary>
        static PostProcessorBootstrapper()
        {
            RegisterExecutables();
        }

        /// <summary>
        /// Gets the working directory path.
        /// </summary>
        public static string WorkingPath { get; private set; }

        /// <summary>
        /// Registers the embedded executables.
        /// </summary>
        public static void RegisterExecutables()
        {
            // None of the tools used here are called using dllimport so we don't go through the normal registration channel. 
            string folder = ImageProcessorBootstrapper.Instance.NativeBinaryFactory.Is64BitEnvironment ? "x64" : "x86";
            Assembly assembly = Assembly.GetExecutingAssembly();
            WorkingPath = Path.GetFullPath(Path.Combine(new Uri(assembly.Location).LocalPath, "..\\imageprocessor.postprocessor\\"));

            // Create the folder for storing temporary images.
            // ReSharper disable once AssignNullToNotNullAttribute
            DirectoryInfo directoryInfo = new DirectoryInfo(Path.GetDirectoryName(WorkingPath));
            if (!directoryInfo.Exists)
            {
                directoryInfo.Create();
            }

            // Get the resources and copy them across.
            Dictionary<string, string> resources = new Dictionary<string, string>
            {
                { "gifsicle.exe", "ImageProcessor.Web.PostProcessor.Resources.Unmanaged." + folder + ".gifsicle.exe" },
                { "jpegtran.exe", "ImageProcessor.Web.PostProcessor.Resources.Unmanaged.x86.jpegtran.exe" },
                { "optipng.exe", "ImageProcessor.Web.PostProcessor.Resources.Unmanaged.x86.optipng.exe" },
                { "pngout.exe", "ImageProcessor.Web.PostProcessor.Resources.Unmanaged.x86.pngout.exe" },
                { "png.cmd", "ImageProcessor.Web.PostProcessor.Resources.Unmanaged.x86.png.cmd" }
            };

            // Write the files out to the bin folder.
            foreach (KeyValuePair<string, string> resource in resources)
            {
                using (Stream resourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource.Value))
                {
                    if (resourceStream != null)
                    {
                        using (FileStream fileStream = File.OpenWrite(Path.Combine(WorkingPath, resource.Key)))
                        {
                            resourceStream.CopyTo(fileStream);
                        }
                    }
                }
            }
        }
    }
}