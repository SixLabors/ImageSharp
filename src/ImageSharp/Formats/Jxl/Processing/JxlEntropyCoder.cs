// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Fields;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing;

/// <summary>
/// Helper functions for use in entropy coding.
/// </summary>
internal static class JxlEntropyCoder
{
    /// <summary>
    /// Global DC threshold distributions
    /// </summary>
    public static readonly JxlU32Enc DcThresholdDistributions = new(
        JxlFieldExpressions.Bits(4),
        JxlFieldExpressions.BitsOffset(8, 16),
        JxlFieldExpressions.BitsOffset(16, 272),
        JxlFieldExpressions.BitsOffset(32, 65808));

    /// <summary>
    /// Global QF threshold distributions
    /// </summary>
    public static readonly JxlU32Enc QfThresholdDistributions = new(
        JxlFieldExpressions.Bits(2),
        JxlFieldExpressions.BitsOffset(3, 4),
        JxlFieldExpressions.BitsOffset(5, 12),
        JxlFieldExpressions.BitsOffset(8, 44));

    /// <summary>
    /// Predicts the entropy symbol using top and left neighboring pixels.
    /// </summary>
    /// <param name="rowTop">Above row; set to <see cref="Span{T}.Empty"/> if missing</param>
    /// <param name="row">Current row</param>
    /// <param name="x">The x coordinate offset</param>
    /// <param name="defaultValue">Default value that's used if both current and above row cannot be used</param>
    /// <returns>The predicted coefficient using neighboring pixels.</returns>
    public static int PredictFromTopAndLeft(
        ReadOnlySpan<int> rowTop,
        ReadOnlySpan<int> row,
        int x,
        int defaultValue)
    {
        if (x == 0)
        {
            return rowTop.Length == 0 ? defaultValue : rowTop[x];
        }

        if (rowTop.Length == 0)
        {
            return row[x - 1];
        }

        return (rowTop[x] + rowTop[x - 1] + 1) / 2;
    }

    public static bool DecodeBlockContextMap(Configuration configuration, JxlBitReader reader, ref JxlBlockContextMap contextMap)
    {
        List<int>[] dct = contextMap.DcThresholds;
        byte[] ctxMap = contextMap.ContextMap;

        bool isDefaultContextMap = reader.ReadBoolean();

        if (isDefaultContextMap)
        {
            contextMap = new();
            return true;
        }

        contextMap.DcContextCount = 1;

        for (int j = 0; j <= 2; j++)
        {
            int dcThresholdCount = (int)reader.ReadBits32(4u);

            dct[j] = new List<int>(dcThresholdCount);

            contextMap.DcContextCount = dcThresholdCount + 1;

            for (int i = 0; i < dcThresholdCount; i++)
            {
                dct[j][i] = JxlPackSigned.UnpackSigned(JxlU32Coder.Read(DcThresholdDistributions, reader));
            }
        }

        int qfThresholdCount = (int)reader.ReadBits32(4u);

        List<uint> qft = new(qfThresholdCount);

        for (int i = 0; i < qfThresholdCount; i++)
        {
            qft[i] = JxlU32Coder.Read(QfThresholdDistributions, reader) + 1;
        }

        contextMap.QfThresholds = qft;

        if (contextMap.DcContextCount * qft.Count > 64)
        {
            throw new InvalidOperationException("Invalid block context map. It is too large.");
        }

        Array.Resize(ref ctxMap, 3 * JxlForwardCoefficientOrder.OrderCount * contextMap.DcContextCount * qft.Count);

        contextMap.ContextMap = ctxMap;

        if (!JxlDecoderCore.DecodeContextMap(configuration, ref ctxMap, contextMap.ContextCount, reader))
        {
            return false;
        }

        if (contextMap.ContextCount > 16)
        {
            throw new InvalidOperationException("Too many distinct contexts in block context map");
        }

        return true;
    }
}
