// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;

namespace SixLabors.ImageSharp.Metadata.Profiles.Icc
{
    /// <summary>
    /// Represents the ICC profile version number.
    /// </summary>
    public readonly struct IccVersion : IEquatable<IccVersion>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="IccVersion"/> struct.
        /// </summary>
        /// <param name="major">The major version number.</param>
        /// <param name="minor">The minor version number.</param>
        /// <param name="patch">The patch version number.</param>
        public IccVersion(int major, int minor, int patch)
        {
            this.Major = major;
            this.Minor = minor;
            this.Patch = patch;
        }

        /// <summary>
        /// Gets the major version number.
        /// </summary>
        public int Major { get; }

        /// <summary>
        /// Gets the minor version number.
        /// </summary>
        public int Minor { get; }

        /// <summary>
        /// Gets the patch number.
        /// </summary>
        public int Patch { get; }

        /// <inheritdoc/>
        public bool Equals(IccVersion other) =>
            this.Major == other.Major &&
            this.Minor == other.Minor &&
            this.Patch == other.Patch;

        /// <inheritdoc/>
        public override string ToString()
        {
            return string.Join(".", this.Major, this.Minor, this.Patch);
        }
    }
}
