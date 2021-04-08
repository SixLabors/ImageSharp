// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Reflection;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// Provides information about the .NET runtime installation.
    /// Many methods defer to <see cref="RuntimeInformation"/> when available.
    /// </summary>
    internal static class RuntimeEnvironment
    {
        private const string FrameworkName = ".NET";
        private static bool isNetCoreChecked;
        private static bool isNetCore;
        private static string frameworkDescription;

        /// <summary>
        /// Gets a value indicating whether the .NET installation is .NET Core
        /// </summary>
        public static bool IsNetCore
        {
            get
            {
                if (isNetCoreChecked)
                {
                    return isNetCore;
                }

                isNetCore = FrameworkDescription.StartsWith(".NET Core", StringComparison.OrdinalIgnoreCase);
                isNetCoreChecked = true;
                return isNetCore;
            }
        }

        /// <summary>
        /// Gets the name of the .NET installation on which an app is running.
        /// </summary>
        public static string FrameworkDescription
        {
            get
            {
                // Adapted from:
                // https://source.dot.net/#System.Runtime.InteropServices.RuntimeInformation/System/Runtime/InteropServices/RuntimeInformation/RuntimeInformation.cs
                if (frameworkDescription == null)
                {
                    string versionString = typeof(object).GetTypeInfo().Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

                    // Strip the git hash if there is one
                    if (versionString != null)
                    {
                        int plusIndex = versionString.IndexOf('+');
                        if (plusIndex != -1)
                        {
                            versionString = versionString.Substring(0, plusIndex);
                        }
                    }

                    frameworkDescription = !string.IsNullOrWhiteSpace(versionString) ? $"{FrameworkName} {versionString}" : FrameworkName;
                }

                return frameworkDescription;
            }
        }

        /// <summary>
        /// Indicates whether the current application is running on the specified platform.
        /// </summary>
        public static bool IsOSPlatform(OSPlatform osPlatform)
            => RuntimeInformation.IsOSPlatform(osPlatform);
    }
}
