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
        public static string CairPath { get; private set; }

        /// <summary>
        /// Gets the cair image path.
        /// </summary>
        public static string CairImagePath { get; private set; }

        /// <summary>
        /// Registers the embedded CAIR executable.
        /// </summary>
        public static void RegisterCairExecutable()
        {
            // None of the tools used here are called using dllimport so we don't go through the normal registration channel. 
            string folder = ImageProcessorBootstrapper.Instance.NativeBinaryFactory.Is64BitEnvironment ? "x64" : "x86";
            Assembly assembly = Assembly.GetExecutingAssembly();
            string targetBasePath = new Uri(assembly.Location).LocalPath;
            string multithreaderTargetPath = Path.GetFullPath(Path.Combine(targetBasePath, "..\\" + folder + "\\" + "pthreadVSE2.dll"));

            // Set the global variable.
            CairPath = Path.GetFullPath(Path.Combine(targetBasePath, "..\\" + folder + "\\" + "CAIR.exe"));
            CairImagePath = Path.GetFullPath(Path.Combine(targetBasePath, "..\\" + folder + "\\" + "cairimages\\"));

            // Get the resources and copy them across.
            const string CairResourcePath = "ImageProcessor.Plugins.Cair.Resources.Unmanaged.x86.CAIR.exe";
            const string MultithreaderResourcePath = "ImageProcessor.Plugins.Cair.Resources.Unmanaged.x86.pthreadVSE2.dll";

            // Write the two files out to the bin folder.
            // Copy out the threading binary.
            using (Stream resourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(MultithreaderResourcePath))
            {
                if (resourceStream != null)
                {
                    // ReSharper disable once AssignNullToNotNullAttribute
                    DirectoryInfo threaderDirectoryInfo = new DirectoryInfo(Path.GetDirectoryName(multithreaderTargetPath));
                    if (!threaderDirectoryInfo.Exists)
                    {
                        threaderDirectoryInfo.Create();
                    }

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
                    // ReSharper disable once AssignNullToNotNullAttribute
                    DirectoryInfo cairDirectoryInfo = new DirectoryInfo(Path.GetDirectoryName(CairPath));
                    if (!cairDirectoryInfo.Exists)
                    {
                        cairDirectoryInfo.Create();
                    }

                    using (FileStream fileStream = File.OpenWrite(CairPath))
                    {
                        resourceStream.CopyTo(fileStream);
                    }
                }
            }

            // Lastly create the image folder for storing temporary images.
            // ReSharper disable once AssignNullToNotNullAttribute
            DirectoryInfo directoryInfo = new DirectoryInfo(Path.GetDirectoryName(CairImagePath));
            if (!directoryInfo.Exists)
            {
                directoryInfo.Create();
            }
        }
    }
}
