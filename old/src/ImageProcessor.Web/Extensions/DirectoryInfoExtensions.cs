// --------------------------------------------------------------------------------------------------------------------
// <copyright file="DirectoryInfoExtensions.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Provides extension methods to the <see cref="System.IO.DirectoryInfo" /> type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Web.Extensions
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides extension methods to the <see cref="System.IO.DirectoryInfo"/> type.
    /// </summary>
    public static class DirectoryInfoExtensions
    {
        /// <summary>
        /// Returns an enumerable collection of directory information that matches a specified search pattern and search subdirectory option.
        /// Will return an empty enumerable on exception. Quick and dirty but does what I need just now.
        /// </summary>
        /// <param name="directoryInfo">
        /// The <see cref="System.IO.DirectoryInfo"/> that this method extends.
        /// </param>
        /// <param name="searchPattern">
        /// The search string to match against the names of directories. This parameter can contain a combination of valid literal path 
        /// and wildcard (* and ?) characters (see Remarks), but doesn't support regular expressions. The default pattern is "*", which returns all files.
        /// </param>
        /// <param name="searchOption">
        /// One of the enumeration values that specifies whether the search operation should include only 
        /// the current directory or all subdirectories. The default value is TopDirectoryOnly.
        /// </param>
        /// <returns>
        /// An enumerable collection of directories that matches searchPattern and searchOption.
        /// </returns>
        public static Task<IEnumerable<DirectoryInfo>> SafeEnumerateDirectoriesAsync(
            this DirectoryInfo directoryInfo,
            string searchPattern = "*",
            SearchOption searchOption = SearchOption.TopDirectoryOnly)
        {
            return Task.Run(() => SafeEnumerateDirectories(directoryInfo, searchPattern, searchOption));
        }

        /// <summary>
        /// Returns an enumerable collection of directory information that matches a specified search pattern and search subdirectory option.
        /// Will return an empty enumerable on exception. Quick and dirty but does what I need just now.
        /// </summary>
        /// <param name="directoryInfo">
        /// The <see cref="System.IO.DirectoryInfo"/> that this method extends.
        /// </param>
        /// <param name="searchPattern">
        /// The search string to match against the names of directories. This parameter can contain a combination of valid literal path 
        /// and wildcard (* and ?) characters (see Remarks), but doesn't support regular expressions. The default pattern is "*", which returns all files.
        /// </param>
        /// <param name="searchOption">
        /// One of the enumeration values that specifies whether the search operation should include only 
        /// the current directory or all subdirectories. The default value is TopDirectoryOnly.
        /// </param>
        /// <returns>
        /// An enumerable collection of directories that matches searchPattern and searchOption.
        /// </returns>
        public static IEnumerable<DirectoryInfo> SafeEnumerateDirectories(
            this DirectoryInfo directoryInfo,
            string searchPattern = "*",
            SearchOption searchOption = SearchOption.TopDirectoryOnly)
        {
            IEnumerable<DirectoryInfo> directories;

            try
            {
                directories = directoryInfo.EnumerateDirectories(searchPattern, searchOption);
            }
            catch
            {
                return Enumerable.Empty<DirectoryInfo>();
            }

            return directories;
        }
    }
}
