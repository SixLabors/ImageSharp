// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.Webp
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
