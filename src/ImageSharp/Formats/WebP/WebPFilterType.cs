// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;

namespace SixLabors.ImageSharp.Formats.WebP
{
    // TODO from dsp.h
    // public enum WebPFilterType
    // {
    //     None = 0,
    //     Horizontal,
    //     Vertical,
    //     Gradient,
    //     Last = Gradient + 1, // end marker
    //     Best, // meta types
    //     Fast
    // }

    abstract class WebPFilterBase
    {
        /// <summary>
        /// </summary>
        /// <param name="prevLine"></param>
        /// <param name="prevLineOffset">nullable as prevLine is nullable in the original but Span'T can't be null.</param>
        /// <param name="preds"></param>
        /// <param name="predsOffset"></param>
        /// <param name="currentLine"></param>
        /// <param name="currentLineOffset"></param>
        /// <param name="width"></param>
        public abstract void Unfilter(
            Span<byte> prevLine,
            int? prevLineOffset,
            Span<byte> preds,
            int predsOffset,
            Span<byte> currentLine,
            int currentLineOffset,
            int width);

        public abstract void Filter(
            Span<byte> input, int inputOffset,
            int width, int height, int stride,
            Span<byte> output, int outputOffset);

        protected static void SanityCheck(
            Span<byte> input, Span<byte> output, int width, int numRows, int height, int stride, int row)
        {
            Debug.Assert(input != null);
            Debug.Assert(output != null);
            Debug.Assert(width > 0);
            Debug.Assert(height > 0);
            Debug.Assert(stride > width);
            Debug.Assert(row >= 0);
            Debug.Assert(height > 0);
            Debug.Assert(row + numRows <= height);
        }

        protected static void PredictLine(
            Span<byte> src, int srcOffset,
            Span<byte> pred, int predOffset,
            Span<byte> dst, int dstOffset,
            int length, bool inverse)
        {
            if (inverse)
            {
                for (int i = 0; i < length; i++)
                {
                    dst[i] = (byte)(src[i] + pred[i]);
                }
            }
            else
            {
                for (int i = 0; i < length; i++)
                {
                    dst[i] = (byte)(src[i] - pred[i]);
                }
            }
        }

        protected void UnfilterHorizontalOrVerticalCore(
            byte pred,
            Span<byte> input, int inputOffset,
            Span<byte> output, int outputOffset,
            int width)
        {
            for (int i = 0; i < width; i++)
            {
                output[outputOffset + i] = (byte)(pred + input[inputOffset + i]);
                pred = output[i];
            }
        }
    }

    // TODO: check if this is a filter or just a placeholder from the C implementation details
    class WebPFilterNone : WebPFilterBase
    {
        public override void Unfilter(Span<byte> prevLine, int? prevLineOffset, Span<byte> input, int inputOffset, Span<byte> output, int outputOffset, int width)
        { }

        public override void Filter(Span<byte> input, int inputOffset, int width, int height, int stride, Span<byte> output, int outputOffset)
        { }
    }

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
