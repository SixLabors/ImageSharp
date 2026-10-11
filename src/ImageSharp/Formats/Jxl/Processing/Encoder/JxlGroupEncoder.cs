// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder;

internal static class JxlGroupEncoder
{
    private const double QuantNormalizer = 2.2942708343284721;

    private static ReadOnlySpan<double> Limit => [0.46, 0.46, 0.46, 0.46];

    private static ReadOnlySpan<double> Multipliers => [0.9999, 0.9999, 0.9999, 0.9999];

    private static readonly double[,] Mul1Data =
    {
        {
            0.22080615753848404,
            0.45797479824262011,
            0.29859235095977965,
        },
        {
            0.70109486510286834,
            0.16185281305512639,
            0.14387691730035473,
        },
        {
            0.114985964456218638,
            0.44656840441027695,
            0.10587658215149048,
        },
        {
            0.46849665264409396,
            0.41239077937781954,
            0.088667407767185444,
        },
    };

    private static readonly double[,] Mul2Data =
    {
        {
            0.27450281941822197,
            1.1255766549984996,
            0.98950459134128388,
        },
        {
            0.4652168675598285,
            0.40945807983455818,
            0.36581899811751367,
        },
        {
            0.28034972424715715,
            0.9182653201929738,
            1.5581531543057416,
        },
        {
            0.26873118114033728,
            0.68863712390392484,
            1.2082185408666786,
        },
    };

    private static readonly DenseMatrix<double> Mul1 = new(Mul1Data);
    private static readonly DenseMatrix<double> Mul2 = new(Mul2Data);

}
