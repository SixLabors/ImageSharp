// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Webp.Lossy
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
