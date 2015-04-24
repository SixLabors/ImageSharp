// --------------------------------------------------------------------------------------------------------------------
// <copyright file="CairBootstrapper.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Defines the CairBootstrapper type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Plugins.Cair
{
    using System;
    using System.IO;
    using System.Reflection;

    using ImageProcessor.Configuration;

    /// <summary>
    /// The cair bootstrapper.
    /// </summary>
    internal static class CairBootstrapper
    {
        /// <summary>
        /// Initializes static members of the <see cref="CairBootstrapper"/> class.
        /// </summary>
        static CairBootstrapper()
        {
            RegisterCairExecutable();
        }

        /// <summary>
        /// Gets the cair path.
        /// </summary>
        public static string CairExecutablePath { get; private set; }

        /// <summary>
        /// Gets the cair base path.
        /// </summary>
        public static string CairPath { get; private set; }

        /// <summary>
        /// Registers the embedded CAIR executable.
        /// </summary>
        public static void RegisterCairExecutable()
        {
            // None of the tools used here are called using dllimport so we don't go through the normal registration channel. 
            string folder = ImageProcessorBootstrapper.Instance.NativeBinaryFactory.Is64BitEnvironment ? "x64" : "x86";
            Assembly assembly = Assembly.GetExecutingAssembly();
            CairPath = Path.GetFullPath(Path.Combine(new Uri(assembly.Location).LocalPath, "..\\" + folder + "\\imageprocessor.cair\\"));
            CairExecutablePath = Path.Combine(CairPath, "CAIR.exe");
            string multithreaderTargetPath = Path.Combine(CairPath, "pthreadVSE2.dll");

            // Create the folder for storing temporary images.
            // ReSharper disable once AssignNullToNotNullAttribute
            DirectoryInfo directoryInfo = new DirectoryInfo(Path.GetDirectoryName(CairPath));
            if (!directoryInfo.Exists)
            {
                directoryInfo.Create();
            }

            // Get the resources and copy them across.
            const string CairResourcePath = "ImageProcessor.Plugins.Cair.Resources.Unmanaged.x86.CAIR.exe";
            const string MultithreaderResourcePath = "ImageProcessor.Plugins.Cair.Resources.Unmanaged.x86.pthreadVSE2.dll";

            // Write the two files out to the bin folder.
            // Copy out the threading binary.
            using (Stream resourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(MultithreaderResourcePath))
            {
                if (resourceStream != null)
                {
                    using (FileStream fileStream = File.OpenWrite(multithreaderTargetPath))
                    {
                        resourceStream.CopyTo(fileStream);
                    }
                }
            }

            // Copy out the cair executable.
            using (Stream resourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(CairResourcePath))
            {
                if (resourceStream != null)
                {
                    using (FileStream fileStream = File.OpenWrite(CairExecutablePath))
                    {
                        resourceStream.CopyTo(fileStream);
                    }
                }
            }
        }
    }
}