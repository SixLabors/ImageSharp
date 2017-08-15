namespace ImageSharp.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Security;

    public static class TestEnvironment
    {
        private const string ImageSharpSolutionFileName = "ImageSharp.sln";

        private const string InputImagesRelativePath = @"tests\Images\Input";

        private const string ActualOutputDirectoryRelativePath = @"tests\Images\ActualOutput";

        private const string ReferenceOutputDirectoryRelativePath = @"tests\Images\External\ReferenceOutput";

        private static Lazy<string> solutionDirectoryFullPath = new Lazy<string>(GetSolutionDirectoryFullPathImpl);

        private static Lazy<bool> runsOnCi = new Lazy<bool>(
            () =>
                {
                    bool isCi;
                    return bool.TryParse(Environment.GetEnvironmentVariable("CI"), out isCi) && isCi;
                });

        // ReSharper disable once InconsistentNaming
        /// <summary>
        /// Gets a value indicating whether test execution runs on CI.
        /// </summary>
        internal static bool RunsOnCI => runsOnCi.Value;

        internal static string SolutionDirectoryFullPath => solutionDirectoryFullPath.Value;

        private static string GetSolutionDirectoryFullPathImpl()
        {
            string assemblyLocation = typeof(TestFile).GetTypeInfo().Assembly.Location;

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
       
        /// <summary>
        /// Gets the correct full path to the Input Images directory.
        /// </summary>
        internal static string InputImagesDirectoryFullPath => Path.Combine(SolutionDirectoryFullPath, InputImagesRelativePath);

        /// <summary>
        /// Gets the correct full path to the Actual Output directory. (To be written to by the test cases.)
        /// </summary>
        internal static string ActualOutputDirectoryFullPath => Path.Combine(SolutionDirectoryFullPath, ActualOutputDirectoryRelativePath);

        /// <summary>
        /// Gets the correct full path to the Expected Output directory. (To compare the test results to.)
        /// </summary>
        internal static string ReferenceOutputDirectoryFullPath => Path.Combine(SolutionDirectoryFullPath, ReferenceOutputDirectoryRelativePath);

        internal static string GetReferenceOutputFileName(string actualOutputFileName) =>
            actualOutputFileName.Replace("ActualOutput", @"External\ReferenceOutput");
    }
}