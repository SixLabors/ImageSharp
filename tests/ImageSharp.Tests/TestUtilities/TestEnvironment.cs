// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Tests
{
    public static partial class TestEnvironment
    {
        private const string ImageSharpSolutionFileName = "ImageSharp.sln";

        private const string InputImagesRelativePath = @"tests\Images\Input";

        private const string ActualOutputDirectoryRelativePath = @"tests\Images\ActualOutput";

        private const string ReferenceOutputDirectoryRelativePath = @"tests\Images\External\ReferenceOutput";

        private const string ToolsDirectoryRelativePath = @"tests\Images\External\tools";

        private static Lazy<string> solutionDirectoryFullPath = new Lazy<string>(GetSolutionDirectoryFullPathImpl);

        private static Lazy<bool> runsOnCi = new Lazy<bool>(
            () =>
                {
                    bool isCi;
                    return Boolean.TryParse(Environment.GetEnvironmentVariable("CI"), out isCi) && isCi;
                });

        // ReSharper disable once InconsistentNaming
        /// <summary>
        /// Gets a value indicating whether test execution runs on CI.
        /// </summary>
        internal static bool RunsOnCI => runsOnCi.Value;

        internal static string SolutionDirectoryFullPath => solutionDirectoryFullPath.Value;

        private static string GetSolutionDirectoryFullPathImpl()
        {
            string assemblyLocation = typeof(TestEnvironment).GetTypeInfo().Assembly.Location;

            var assemblyFile = new FileInfo(assemblyLocation);

            DirectoryInfo directory = assemblyFile.Directory;

            while (!directory.EnumerateFiles(ImageSharpSolutionFileName).Any())
            {
                try
                {
                    directory = directory.Parent;
                }
                catch (Exception ex)
                {
                    throw new Exception(
                        $"Unable to find ImageSharp solution directory from {assemblyLocation} because of {ex.GetType().Name}!",
                        ex);
                }
                if (directory == null)
                {
                    throw new Exception($"Unable to find ImageSharp solution directory from {assemblyLocation}!");
                }
            }

            return directory.FullName;
        }

        private static string GetFullPath(string relativePath) => 
            Path.Combine(SolutionDirectoryFullPath, relativePath)
            .Replace('\\', Path.DirectorySeparatorChar);

        /// <summary>
        /// Gets the correct full path to the Input Images directory.
        /// </summary>
        internal static string InputImagesDirectoryFullPath => GetFullPath(InputImagesRelativePath);
        
        /// <summary>
        /// Gets the correct full path to the Actual Output directory. (To be written to by the test cases.)
        /// </summary>
        internal static string ActualOutputDirectoryFullPath => GetFullPath(ActualOutputDirectoryRelativePath);

        /// <summary>
        /// Gets the correct full path to the Expected Output directory. (To compare the test results to.)
        /// </summary>
        internal static string ReferenceOutputDirectoryFullPath => GetFullPath(ReferenceOutputDirectoryRelativePath);

        internal static string ToolsDirectoryFullPath => GetFullPath(ToolsDirectoryRelativePath);

        internal static string GetReferenceOutputFileName(string actualOutputFileName) =>
            actualOutputFileName.Replace("ActualOutput", @"External\ReferenceOutput").Replace('\\', Path.DirectorySeparatorChar);

        internal static bool IsLinux => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

        internal static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        /// <summary>
        /// Creates the image output directory.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <param name="pathParts">The path parts.</param>
        /// <returns>
        /// The <see cref="string"/>.
        /// </returns>
        internal static string CreateOutputDirectory(string path, params string[] pathParts)
        {
            path = Path.Combine(TestEnvironment.ActualOutputDirectoryFullPath, path);

            if (pathParts != null && pathParts.Length > 0)
            {
                path = Path.Combine(path, Path.Combine(pathParts));
            }

            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            return path;
        }
    }
}