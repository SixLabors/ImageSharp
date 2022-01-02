// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using System.Xml.Linq;

namespace SixLabors.ImageSharp.Metadata.Profiles.Xmp
{
    /// <summary>
    /// Compare <see cref="XElement"/> objects for Name and Value equality.
    /// </summary>
    public class XElementEqualityComparer : IEqualityComparer<XElement>
    {
        /// <inheritdoc />
        public bool Equals(XElement x, XElement y) => x.Name == y.Name && x.Value == y.Value;

        /// <inheritdoc />
        public int GetHashCode(XElement obj) => obj.GetHashCode();
    }
}
