// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;

namespace SixLabors.ImageSharp.Metadata.Profiles.Icc
{
    /// <summary>
    /// A placeholder <see cref="IccMultiProcessElement"/> (might be used for future ICC versions)
    /// </summary>
    internal sealed class IccEAcsProcessElement : IccMultiProcessElement, IEquatable<IccEAcsProcessElement>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="IccEAcsProcessElement"/> class.
        /// </summary>
        /// <param name="inChannelCount">Number of input channels</param>
        /// <param name="outChannelCount">Number of output channels</param>
        public IccEAcsProcessElement(int inChannelCount, int outChannelCount)
            : base(IccMultiProcessElementSignature.EAcs, inChannelCount, outChannelCount)
        {
        }

        /// <inheritdoc />
        public bool Equals(IccEAcsProcessElement other)
        {
            return base.Equals(other);
        }
    }
}
