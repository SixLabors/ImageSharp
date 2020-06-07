// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;

namespace SixLabors.ImageSharp.Metadata.Profiles.Icc
{
    /// <summary>
    /// A placeholder <see cref="IccMultiProcessElement"/> (might be used for future ICC versions)
    /// </summary>
    internal sealed class IccBAcsProcessElement : IccMultiProcessElement, IEquatable<IccBAcsProcessElement>
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

        /// <inheritdoc />
        public bool Equals(IccBAcsProcessElement other)
        {
            return base.Equals(other);
        }
    }
}
