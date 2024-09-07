// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Reflection;

namespace SixLabors.ImageSharp.Tests;

/// <summary>
/// A test image file.
/// </summary>
public static class TestFontUtilities
{
    /// <summary>
    /// The formats directory.
    /// </summary>
    private static readonly string FormatsDirectory = GetFontsDirectory();

    /// <summary>
    /// Gets the full qualified path to the file.
    /// </summary>
    /// <param name="file">
    /// The file path.
    /// </param>
    /// <returns>
    /// The <see cref="string"/>.
    /// </returns>
    public static string GetPath(string file)
    {
        return Path.Combine(FormatsDirectory, file);
    }

    /// <summary>
    /// Gets the correct path to the formats directory.
    /// </summary>
    /// <returns>
    /// The <see cref="string"/>.
    /// </returns>
    private static string GetFontsDirectory()
    {
        List<string> directories =
        [
            "TestFonts/", // Here for code coverage tests.
            "tests/ImageSharp.Tests/TestFonts/", // from travis/build script
            "../../../../../ImageSharp.Tests/TestFonts/", // from Sandbox46
            "../../../../TestFonts/"
        ];

        directories = directories.SelectMany(x => new[]
                                 {
                                     Path.GetFullPath(x)
                                 }).ToList();

        AddFormatsDirectoryFromTestAssemblyPath(directories);

        string directory = directories.FirstOrDefault(Directory.Exists);

        if (directory != null)
        {
            return directory;
        }

        throw new($"Unable to find Fonts directory at any of these locations [{string.Join(", ", directories)}]");
    }

    /// <summary>
    /// The path returned by Path.GetFullPath(x) can be relative to dotnet framework directory
    /// in certain scenarios like dotTrace test profiling.
    /// This method calculates and adds the format directory based on the ImageSharp.Tests assembly location.
    /// </summary>
    /// <param name="directories">The directories list</param>
    private static void AddFormatsDirectoryFromTestAssemblyPath(List<string> directories)
    {
        string assemblyLocation = typeof(TestFile).GetTypeInfo().Assembly.Location;
        assemblyLocation = Path.GetDirectoryName(assemblyLocation);

        if (assemblyLocation != null)
        {
            string dirFromAssemblyLocation = Path.Combine(assemblyLocation, "../../../TestFonts/");
            dirFromAssemblyLocation = Path.GetFullPath(dirFromAssemblyLocation);
            directories.Add(dirFromAssemblyLocation);
        }
    }
}
