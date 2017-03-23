// <copyright file="IccBAcsProcessElement.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    /// <summary>
    /// A placeholder <see cref="IccMultiProcessElement"/> (might be used for future ICC versions)
    /// </summary>
    internal sealed class IccBAcsProcessElement : IccMultiProcessElement
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="IccBAcsProcessElement"/> class.
        /// </summary>
        /// <param name="inChannelCount">Number of input channels</param>
        /// <param name="outChannelCount">Number of output channels</param>
        public IccBAcsProcessElement(int inChannelCount, int outChannelCount)
            : base(IccMultiProcessElementSignature.BAcs, inChannelCount, outChannelCount)
        {
        }
    }
}
