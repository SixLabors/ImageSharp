// --------------------------------------------------------------------------------------------------------------------
// <copyright file="IOHelper.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Provides helper method for traversing the file system.
//   <remarks>
//   Adapted from identically named class within <see href="https://github.com/umbraco/Umbraco-CMS" />
//   </remarks>
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Common.Helpers
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Reflection;

    using ImageProcessor.Common.Extensions;

    /// <summary>
    /// Provides helper method for traversing the file system.
    /// <remarks>
    /// Adapted from identically named class within <see href="https://github.com/umbraco/Umbraco-CMS"/>
    /// </remarks>
    /// </summary>
    internal class IOHelper
    {
        /// <summary>
        /// The root directory.
        /// </summary>
        private static string rootDirectory;

        /// <summary>
        /// Maps a virtual path to a physical path.
        /// </summary>
        /// <param name="virtualPath">
        /// The virtual path to map.
        /// </param>
        /// <returns>
        /// The <see cref="string"/> representing the physical path.
        /// </returns>
        public static string MapPath(string virtualPath)
        {
            // Check if the path is already mapped
            // UNC Paths start with "\\". If the site is running off a network drive mapped paths 
            // will look like "\\Whatever\Boo\Bar"
            if ((virtualPath.Length >= 2 && virtualPath[1] == Path.VolumeSeparatorChar)
                || virtualPath.StartsWith(@"\\"))
            {
                return virtualPath;
            }

            char separator = Path.DirectorySeparatorChar;
            string root = GetRootDirectorySafe();
            string newPath = virtualPath.TrimStart('~', '/').Replace('/', separator);
            return root + separator.ToString(CultureInfo.InvariantCulture) + newPath;
        }

        /// <summary>
        /// Gets the root directory bin folder for the currently running application.
        /// </summary>
        /// <returns>
        /// The <see cref="string"/> representing the root directory bin folder.
        /// </returns>
        public static string GetRootDirectoryBinFolder()
        {
            string binFolder = string.Empty;
            if (string.IsNullOrEmpty(rootDirectory))
            {
                DirectoryInfo directoryInfo = Assembly.GetExecutingAssembly().GetAssemblyFile().Directory;
                if (directoryInfo != null)
                {
                    binFolder = directoryInfo.FullName;
                }

                return binFolder;
            }

            binFolder = Path.Combine(GetRootDirectorySafe(), "bin");

#if DEBUG
            string debugFolder = Path.Combine(binFolder, "debug");
            if (Directory.Exists(debugFolder))
            {
                return debugFolder;
            }
#endif
            string releaseFolder = Path.Combine(binFolder, "release");
            if (Directory.Exists(releaseFolder))
            {
                return releaseFolder;
            }

            if (Directory.Exists(binFolder))
            {
                return binFolder;
            }

            return rootDirectory;
        }

        /// <summary>
        /// Returns the path to the root of the application, by getting the path to where the assembly where this
        /// method is included is present, then traversing until it's past the /bin directory. I.e. this makes it work
        /// even if the assembly is in a /bin/debug or /bin/release folder
        /// </summary>
        /// <returns>
        /// The <see cref="string"/> representing the root path of the currently running application.</returns>
        internal static string GetRootDirectorySafe()
        {
            if (string.IsNullOrEmpty(rootDirectory) == false)
            {
                return rootDirectory;
            }

            string codeBase = Assembly.GetExecutingAssembly().CodeBase;
            Uri uri = new Uri(codeBase);
            string path = uri.LocalPath;
            string baseDirectory = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(baseDirectory))
            {
                throw new Exception(
                    "No root directory could be resolved. Please ensure that your solution is correctly configured.");
            }

            rootDirectory = baseDirectory.Contains("bin")
                           ? baseDirectory.Substring(0, baseDirectory.LastIndexOf("bin", StringComparison.OrdinalIgnoreCase) - 1)
                           : baseDirectory;

            return rootDirectory;
        }
    }
}
