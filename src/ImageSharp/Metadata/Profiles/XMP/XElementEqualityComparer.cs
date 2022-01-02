// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Xml.Linq;

namespace SixLabors.ImageSharp.Metadata.Profiles.Xmp
{
    /// <summary>
    /// Compare <see cref="XElement"/> objects for Name and Value equality.
    /// </summary>
    public class XElementEqualityComparer : IEqualityComparer<XElement>
    {
        /// <inheritdoc />
        public bool Equals([AllowNull] XElement x, [AllowNull] XElement y) => x.Name == y.Name && x.Value == y.Value;

        /// <inheritdoc />
        public int GetHashCode([DisallowNull] XElement obj) => obj.GetHashCode();
    }
}
