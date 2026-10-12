// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Processing.Modular.Encoding.ContextPrediction;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Modular.Encoding;

internal struct JxlPropertyDecisionNode
{
    public int SplitValue;
    public int Property;
    public int LeftChild;
    public int RightChild;
    public JxlPredictor Predictor;
    public long PredictorOffset;
    public int Multiplier;

    public JxlPropertyDecisionNode(int property, int splitValue, int leftChild, int rightChild, JxlPredictor predictor, long predictorOffset, int multiplier)
    {
        this.SplitValue = splitValue;
        this.Property = property;
        this.LeftChild = leftChild;
        this.RightChild = rightChild;
        this.Predictor = predictor;
        this.PredictorOffset = predictorOffset;
        this.Multiplier = multiplier;
    }

    public JxlPropertyDecisionNode()
        : this(0, -1, 0, 0, JxlPredictor.Zero, 0, 1)
    {
    }

    public static JxlPropertyDecisionNode Leaf(JxlPredictor predictor, long offset = 0, int multiplier = 1)
        => new(-1, 0, 0, 0, predictor, offset, multiplier);

    public static JxlPropertyDecisionNode Split(int p, int splitValue, int leftChild, int rightChild = -1)
    {
        if (rightChild == -1)
        {
            rightChild = leftChild + 1;
        }

        return new(p, splitValue, leftChild, rightChild, JxlPredictor.Zero, 0, 1);
    }
}
