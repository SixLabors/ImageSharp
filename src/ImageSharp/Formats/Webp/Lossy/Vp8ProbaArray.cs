// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Webp.Lossy
{
    /// <summary>
    /// Probabilities associated to one of the contexts.
    /// </summary>
    internal class Vp8ProbaArray
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Vp8ProbaArray"/> class.
        /// </summary>
        public Vp8ProbaArray() => this.Probabilities = new byte[WebpConstants.NumProbas];

        /// <summary>
        /// Gets the probabilities.
        /// </summary>
        public byte[] Probabilities { get; }
    }
}
