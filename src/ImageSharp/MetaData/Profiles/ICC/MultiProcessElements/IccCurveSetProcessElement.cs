// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Linq;

namespace SixLabors.ImageSharp.MetaData.Profiles.Icc
{
    /// <summary>
    /// A set of curves to process data
    /// </summary>
    internal sealed class IccCurveSetProcessElement : IccMultiProcessElement, IEquatable<IccCurveSetProcessElement>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="IccCurveSetProcessElement"/> class.
        /// </summary>
        /// <param name="curves">An array with one dimensional curves</param>
        public IccCurveSetProcessElement(IccOneDimensionalCurve[] curves)
            : base(IccMultiProcessElementSignature.CurveSet, curves?.Length ?? 1, curves?.Length ?? 1)
        {
            this.Curves = curves ?? throw new ArgumentNullException(nameof(curves));
        }

        /// <summary>
        /// Gets an array of one dimensional curves
        /// </summary>
        public IccOneDimensionalCurve[] Curves { get; }

        /// <inheritdoc />
        public override bool Equals(IccMultiProcessElement other)
        {
            if (base.Equals(other) && other is IccCurveSetProcessElement element)
            {
                return this.Curves.SequenceEqual(element.Curves);
            }

            return false;
        }

        /// <inheritdoc />
        public bool Equals(IccCurveSetProcessElement other)
        {
            return this.Equals((IccMultiProcessElement)other);
        }
    }
}