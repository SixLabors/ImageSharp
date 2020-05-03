// Copyright (c) Six Labors and contributors.
// Licensed under the GNU Affero General Public License, Version 3.

namespace SixLabors.ImageSharp.Formats.WebP.Lossy
{
    /// <summary>
    /// Probabilities associated to one of the contexts.
    /// </summary>
    internal class Vp8ProbaArray
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Vp8ProbaArray"/> class.
        /// </summary>
        public Vp8ProbaArray()
        {
            this.Probabilities = new byte[WebPConstants.NumProbas];
        }

        /// <summary>
        /// Gets the probabilities.
        /// </summary>
        public byte[] Probabilities { get; }
    }
}
