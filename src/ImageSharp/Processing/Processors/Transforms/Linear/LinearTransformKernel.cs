// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Processing.Processors.Transforms
{
    /// <summary>
    /// Represents a kernel buffer of sampling weights used for linear transforms.
    /// </summary>
    internal readonly ref struct LinearTransformKernel
    {
        [MethodImpl(InliningOptions.ShortMethod)]
        internal LinearTransformKernel(Span<float> values, int start, int end)
        {
            // By doing this we ensure that the kernal cannot be iterated
            // based upon the start and end values.
            if (end <= start)
            {
                start = 0;
                end = -1;
            }

            this.Start = start;
            this.End = end;
            this.Length = end - start + 1;
            this.Values = values.Slice(0, this.Length);
        }

        public static LinearTransformKernel Empty => default;

        public int Start
        {
            [MethodImpl(InliningOptions.ShortMethod)]
            get;
        }

        public int End
        {
            [MethodImpl(InliningOptions.ShortMethod)]
            get;
        }

        public int Length
        {
            [MethodImpl(InliningOptions.ShortMethod)]
            get;
        }

        public Span<float> Values
        {
            [MethodImpl(InliningOptions.ShortMethod)]
            get;
        }
    }
}
