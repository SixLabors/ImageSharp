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
        public const string ImageSharpSolution = "ImageSharp.sln";

        public const string InputImagesRelativePath = @"tests\ImageSharp.Tests\TestImages\Formats";
        
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
        
        internal static string GetSolutionDirectoryFullPath()
        {
            string assemblyLocation = typeof(TestFile).GetTypeInfo().Assembly.Location;

            var assemblyFile = new FileInfo(assemblyLocation);

            DirectoryInfo directory = assemblyFile.Directory;

            while (!directory.EnumerateFiles(ImageSharpSolution).Any())
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
        /// Gets the correct path to the InputImages directory.
        /// </summary>
        /// <returns>
        /// The <see cref="string"/>.
        /// </returns>
        internal static string GetInputImagesDirectoryFullPath()
        {
            string soulitionDir = GetSolutionDirectoryFullPath();

            return Path.Combine(soulitionDir, InputImagesRelativePath);
        }
        
    }
}