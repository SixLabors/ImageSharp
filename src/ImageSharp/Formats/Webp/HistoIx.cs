// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Webp
{
    internal enum HistoIx : byte
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

        HistoTotal
    }
}
