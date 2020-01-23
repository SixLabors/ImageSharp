using System;

namespace SixLabors.ImageSharp.Formats.WebP.Filters
{
    class WebPFilterVertical : WebPFilterBase
    {
        public override void Unfilter(Span<byte> prevLine, int? prevLineOffsetNullable, Span<byte> input, int inputOffset, Span<byte> output, int outputOffset, int width)
        {
            if (prevLineOffsetNullable is int prevLineOffset)
            {
                for (int i = 0; i < width; i++)
                {
                    output[outputOffset + i] = (byte)(prevLine[prevLineOffset + i] + input[inputOffset + i]);
                }
            }
            else
            {
                this.UnfilterHorizontalOrVerticalCore(0, input, inputOffset, output, outputOffset, width);
            }
        }

        public override void Filter(
            Span<byte> input, int inputOffset,
            int width, int height, int stride,
            Span<byte> output, int outputOffset)
        {
            int row = 0;
            bool inverse = false;

            // TODO: DoVerticalFilter_C with parameters after stride and after height set to 0
            int startOffset = row * stride;
            int lastRow = row + height;
            SanityCheck(input, output, width, height, height, stride, row);
            inputOffset += startOffset;
            outputOffset += startOffset;
            Span<byte> preds;
            int predsOffset;

            if (inverse)
            {
                preds = output;
                predsOffset = outputOffset;
            }
            else
            {
                preds = input;
                predsOffset = inputOffset;
            }

            if (row == 0)
            {
                // very first top-left pixel is copied.
                output[0] = input[0];
                // rest of top scan-line is left-predicted:
                PredictLine(
                    input, inputOffset + 1,
                    preds, predsOffset,
                    output, outputOffset + 1,
                    width - 1, inverse);
                row = 1;
                inputOffset += stride;
                outputOffset += stride;
            }
            else
            {
                predsOffset -= stride;
            }

            // filter line-by-line
            while (row < lastRow)
            {
                PredictLine(
                    input, inputOffset,
                    preds, predsOffset,
                    output, outputOffset,
                    width, inverse);
                row++;
                predsOffset += stride;
                inputOffset += stride;
                outputOffset += stride;
            }
        }
    }
}