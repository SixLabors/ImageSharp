// Copyright (c) Six Labors and contributors.
// Licensed under the GNU Affero General Public License, Version 3.

namespace SixLabors.ImageSharp.Formats.WebP.Lossy
{
    internal enum IntraPredictionMode
    {
        /// <summary>
        /// Predict DC using row above and column to the left.
        /// </summary>
        DcPrediction = 0,

        /// <summary>
        /// Propagate second differences a la "True Motion".
        /// </summary>
        TrueMotion = 1,

        /// <summary>
        /// Predict rows using row above.
        /// </summary>
        VPrediction = 2,

        /// <summary>
        /// Predict columns using column to the left.
        /// </summary>
        HPrediction = 3,
    }
}
