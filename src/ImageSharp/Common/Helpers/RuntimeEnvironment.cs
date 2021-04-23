// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// Provides information about the .NET runtime installation.
    /// Many methods defer to <see cref="RuntimeInformation"/> when available.
    /// </summary>
    internal static class RuntimeEnvironment
    {
        private static readonly Lazy<bool> IsNetCoreLazy = new Lazy<bool>(() => FrameworkDescription.StartsWith(".NET Core", StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// Gets a value indicating whether the .NET installation is .NET Core 3.1 or lower.
        /// </summary>
        public static bool IsNetCore => IsNetCoreLazy.Value;

        /// <summary>
        /// Gets the name of the .NET installation on which an app is running.
        /// </summary>
        public static string FrameworkDescription => RuntimeInformation.FrameworkDescription;

        /// <summary>
        /// Indicates whether the current application is running on the specified platform.
        /// </summary>
        public static bool IsOSPlatform(OSPlatform osPlatform) => RuntimeInformation.IsOSPlatform(osPlatform);
    }
}
