// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Tests
{
    using System;

    /// <summary>
    /// The output produced by this test class should be grouped into the specified subfolder.
    /// </summary>
    public class GroupOutputAttribute : Attribute
    {
        public GroupOutputAttribute(string subfolder)
        {
            this.Subfolder = subfolder;
        }

        public string Subfolder { get; }
    }
}
