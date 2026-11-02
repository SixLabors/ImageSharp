// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Modular.Encoding.ContextPrediction;

/// <summary>
/// Stores a node and its two children at the same time.
/// </summary>
internal struct JxlFlatDecisionNode
{
    public int Property0;
    public int SplitValue0;
    public JxlPredictor Predictor;
    public InlineArray2<int> SplitValues;
    public int Multiplier;
    public uint ChildID;
    public InlineArray2<short> Properties;
    public int PredictorOffset;
}
