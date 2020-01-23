using System;

namespace SixLabors.ImageSharp.Formats.WebP.Filters
{
    class WebPFilterHorizontal : WebPFilterBase
    {
        public override void Unfilter(
            Span<byte> prevLine, int? prevLineOffsetNullable,
            Span<byte> input,  int inputOffset,
            Span<byte> output, int outputOffset,
            int width)
        {
            byte pred = prevLineOffsetNullable is int prevLineOffset
                            ? prevLine[prevLineOffset]
                            : (byte)0;

            this.UnfilterHorizontalOrVerticalCore(
                pred,
                input, inputOffset,
                output, outputOffset,
                width);
        }

        public override void Filter(
            Span<byte> input, int inputOffset,
            int width, int height, int stride,
            Span<byte> output, int outputOffset)
        {
            int numRows = height;
            int row = 0;

            const bool inverse = false;

            int startOffset = row * stride;
            int lastRow = row + height;
            SanityCheck(input, output,  width, height, numRows, stride, row);
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
                // leftmost pixel is the same as Input for topmost scanline
                output[0] = input[0];
                PredictLine(
                    input, inputOffset + 1,
                    preds, predsOffset,
                    output, outputOffset + 1,
                    width - 1, inverse);

                row = 1;
                predsOffset += stride;
                inputOffset += stride;
                outputOffset += stride;
            }

            // filter line by line
            while (row < lastRow)
            {
                PredictLine(
                    input, inputOffset,
                    preds, predsOffset - stride,
                    output, 0,
                    1, inverse);
                PredictLine(
                    input, inputOffset,
                    preds, predsOffset,
                    output,outputOffset + 1,
                    width - 1, inverse);

                row++;
                predsOffset += stride;
                inputOffset += stride;
                outputOffset += stride;
            }
        }
    }
}