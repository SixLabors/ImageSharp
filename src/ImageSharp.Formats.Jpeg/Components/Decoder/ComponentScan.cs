// <copyright file="ComponentScan.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats.Jpg
{
    using System.Runtime.InteropServices;

    /// <summary>
    /// Represents a component scan
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct ComponentScan
    {
        /// <summary>
        /// Gets or sets the component index.
        /// </summary>
        public byte ComponentIndex;

        /// <summary>
        /// Gets or sets the DC table selector
        /// </summary>
        public byte DcTableSelector;

        /// <summary>
        /// Gets or sets the AC table selector
        /// </summary>
        public byte AcTableSelector;
    }
}