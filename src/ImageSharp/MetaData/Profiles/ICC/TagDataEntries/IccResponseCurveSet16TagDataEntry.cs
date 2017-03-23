// <copyright file="IccResponseCurveSet16TagDataEntry.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System.Linq;

    /// <summary>
    /// The purpose of this tag type is to provide a mechanism to relate physical
    /// colorant amounts with the normalized device codes produced by lut8Type, lut16Type,
    /// lutAToBType, lutBToAType or multiProcessElementsType tags so that corrections can
    /// be made for variation in the device without having to produce a new profile.
    /// </summary>
    internal sealed class IccResponseCurveSet16TagDataEntry : IccTagDataEntry
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="IccResponseCurveSet16TagDataEntry"/> class.
        /// </summary>
        /// <param name="curves">The Curves</param>
        public IccResponseCurveSet16TagDataEntry(IccResponseCurve[] curves)
            : this(curves, IccProfileTag.Unknown)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IccResponseCurveSet16TagDataEntry"/> class.
        /// </summary>
        /// <param name="curves">The Curves</param>
        /// <param name="tagSignature">Tag Signature</param>
        public IccResponseCurveSet16TagDataEntry(IccResponseCurve[] curves, IccProfileTag tagSignature)
            : base(IccTypeSignature.ResponseCurveSet16, tagSignature)
        {
            Guard.NotNull(curves, nameof(curves));
            Guard.IsTrue(curves.Length < 1, nameof(curves), $"{nameof(curves)} needs at least one element");

            this.Curves = curves;
            this.ChannelCount = (ushort)curves[0].ResponseArrays.Length;

            Guard.IsTrue(curves.Any(t => t.ResponseArrays.Length != this.ChannelCount), nameof(curves), "All curves need to have the same number of channels");
        }

        /// <summary>
        /// Gets the number of channels
        /// </summary>
        public ushort ChannelCount { get; }

        /// <summary>
        /// Gets the curves
        /// </summary>
        public IccResponseCurve[] Curves { get; }

        /// <inheritdoc />
        public override bool Equals(IccTagDataEntry other)
        {
            if (base.Equals(other) && other is IccResponseCurveSet16TagDataEntry entry)
            {
                return this.ChannelCount == entry.ChannelCount
                    && this.Curves.SequenceEqual(entry.Curves);
            }

            return false;
        }
    }
}
