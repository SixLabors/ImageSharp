// <copyright file="IccClutProcessElement.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;

    /// <summary>
    /// A CLUT (color lookup table) element to process data
    /// </summary>
    internal sealed class IccClutProcessElement : IccMultiProcessElement, IEquatable<IccClutProcessElement>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="IccClutProcessElement"/> class.
        /// </summary>
        /// <param name="clutValue">The color lookup table of this element</param>
        public IccClutProcessElement(IccClut clutValue)
            : base(IccMultiProcessElementSignature.Clut, clutValue?.InputChannelCount ?? 1, clutValue?.OutputChannelCount ?? 1)
        {
            Guard.NotNull(clutValue, nameof(clutValue));
            this.ClutValue = clutValue;
        }

        /// <summary>
        /// Gets the color lookup table of this element
        /// </summary>
        public IccClut ClutValue { get; }

        /// <inheritdoc />
        public override bool Equals(IccMultiProcessElement other)
        {
            if (base.Equals(other) && other is IccClutProcessElement element)
            {
                return this.ClutValue.Equals(element.ClutValue);
            }

            return false;
        }

        /// <inheritdoc />
        public bool Equals(IccClutProcessElement other)
        {
            return this.Equals((IccMultiProcessElement)other);
        }
    }
}
