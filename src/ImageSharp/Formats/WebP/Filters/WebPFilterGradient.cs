using System;

namespace SixLabors.ImageSharp.Formats.WebP.Filters
{
    class WebPFilterGradient : WebPFilterBase
    {

        public override void Unfilter(
            Span<byte> prevLine,
            int? prevLineOffsetNullable,
            Span<byte> input,
            int inputOffset,
            Span<byte> output,
            int outputOffset,
            int width)
        {
            if (prevLineOffsetNullable is int prevLineOffset)
            {
                byte top = prevLine[prevLineOffset];
                byte topLeft = top;
                byte left = top;
                for (int i = 0; i < width; i++)
                {
                    top = prevLine[prevLineOffset + i]; // need to read this first in case prev==out
                    left = (byte)(input[inputOffset + i] + GradientPredictor(left, top, topLeft));
                    topLeft = top;
                    output[outputOffset + i] = left;
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
            // calling (input, width, height, stride, 0, height, 0, output
            int row = 0;
            int numRows = height;
            bool inverse = false;

            int startOffset = row * stride;
            int lastRow = row + numRows;
            SanityCheck(input, output, width, numRows, height, stride, row);
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
                output[outputOffset] = input[inputOffset];
                PredictLine(
                    input, inputOffset+1,
                    preds, predsOffset,
                    output, outputOffset+1,
                    width-1,
                    inverse);
            }

            while (row < lastRow)
            {
                PredictLine(
                    input, inputOffset,
                    preds, predsOffset-stride,
                    output, outputOffset,
                    1, inverse);

                for (int w = 1; w < width; w++)
                {
                    int pred = GradientPredictor(preds[w - 1], preds[w - stride], preds[w - stride - 1]);
                    int signedPred = inverse ? pred : -pred;
                    output[outputOffset + w] = (byte)(input[inputOffset + w] + signedPred);
                }

                row++;
                predsOffset += stride;
                inputOffset += stride;
                outputOffset += stride;
            }
        }

        private static int GradientPredictor(byte a, byte b, byte c)
        {
            int g = a + b + c;
            return (g & ~0xff) == 0 ? g : (g < 0) ? 0 : 255;
        }
    }
}