// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Modular.Encoding.ContextPrediction;

internal sealed class JxlMaTreeLookup(List<JxlFlatDecisionNode> nodes)
{
    public JxlMaTreeLookupResult Lookup(Span<int> properties)
    {
        uint pos = 0;
        while (true)
        {
            for (int i = 0; i < 2; i++)
            {
                JxlFlatDecisionNode node = nodes[(int)pos];
                if (node.Property0 < 0)
                {
                    return new(node.ChildID, node.Predictor, node.PredictorOffset, node.Multiplier);
                }

                bool p0 = properties[node.Property0] <= node.SplitValue0;
                uint off0 = properties[node.Properties[0]] <= node.SplitValues[0] ? 1u : 0u;
                uint off1 = 2u | (properties[node.Properties[1]] <= node.SplitValues[1] ? 1u : 0u);

                pos = node.ChildID + (p0 ? off1 : off0);
            }
        }
    }
}
