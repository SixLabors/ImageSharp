﻿// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;

namespace SixLabors.ImageSharp.Primitives
{
    /// <summary>
    /// A <see langword="struct"/> that contains data about a set of bokeh blur kernels
    /// </summary>
    internal readonly struct BokehBlurKernelData
    {
        /// <summary>
        /// The kernel parameters to use for the current set of complex kernels
        /// </summary>
        public readonly Vector4[] Parameters;

        /// <summary>
        /// The scaling factor for the kernel values
        /// </summary>
        public readonly float Scale;

        /// <summary>
        /// The kernel components to apply the bokeh blur effect
        /// </summary>
        public readonly Complex64[][] Kernels;

        /// <summary>
        /// Initializes a new instance of the <see cref="BokehBlurKernelData"/> struct.
        /// </summary>
        /// <param name="parameters">The kernel parameters</param>
        /// <param name="scale">The kernel scale factor</param>
        /// <param name="kernels">The complex kernel components</param>
        public BokehBlurKernelData(Vector4[] parameters, float scale, Complex64[][] kernels)
        {
            this.Parameters = parameters;
            this.Scale = scale;
            this.Kernels = kernels;
        }
    }
}
