// Copyright (c) Six Labors and contributors.
// Licensed under the GNU Affero General Public License, Version 3.

namespace SixLabors.ImageSharp.Formats.WebP
{
    internal enum HistoIx
    {
        HistoAlpha = 0,

        HistoAlphaPred,

        HistoGreen,

        HistoGreenPred,

        HistoRed,

        HistoRedPred,

        HistoBlue,

        HistoBluePred,

        HistoRedSubGreen,

        HistoRedPredSubGreen,

        HistoBlueSubGreen,

        HistoBluePredSubGreen,

        HistoPalette,

        HistoTotal, // Must be last.
    }
}
