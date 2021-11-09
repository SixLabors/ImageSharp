// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;

namespace SixLabors.ImageSharp.Formats.Tiff.Compression.Decompressors
{
    [DebuggerDisplay("Type = {Type}")]
    internal readonly struct CcittTwoDimensionalCode
    {
        private readonly ushort value;

        /// <summary>
        /// Initializes a new instance of the <see cref="CcittTwoDimensionalCode"/> struct.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <param name="bitsRequired">The bits required.</param>
        /// <param name="extensionBits">The extension bits.</param>
        public CcittTwoDimensionalCode(CcittTwoDimensionalCodeType type, int bitsRequired, int extensionBits = 0)
            => this.value = (ushort)((byte)type | ((bitsRequired & 0b1111) << 8) | ((extensionBits & 0b111) << 11));

        /// <summary>
        /// Gets the code type.
        /// </summary>
        public CcittTwoDimensionalCodeType Type => (CcittTwoDimensionalCodeType)(this.value & 0b11111111);
    }
}
