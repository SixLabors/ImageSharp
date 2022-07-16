// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Advanced
{
    /// <summary>
    /// This is necessary to avoid being excluded from compilation in environments that do AOT builds, such as Unity's IL2CPP and Xamarin.
    /// The only thing that matters is the class name.
    /// There is no need to use or inherit from the PreserveAttribute class in each environment.
    /// </summary>
    internal sealed class PreserveAttribute : System.Attribute
    {
    }
}
