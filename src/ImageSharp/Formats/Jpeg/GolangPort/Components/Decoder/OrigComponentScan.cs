// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Formats.Jpeg.GolangPort.Components.Decoder
{
    /// <summary>
    /// Represents a component scan
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct OrigComponentScan
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