// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;

namespace SixLabors.ImageSharp.Formats.WebP.Filters
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
}
