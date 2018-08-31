// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Linq;

namespace SixLabors.ImageSharp.MetaData.Profiles.Icc
{
    /// <summary>
    /// This structure represents a color transform, containing
    /// a sequence of processing elements.
    /// </summary>
    internal sealed class IccMultiProcessElementsTagDataEntry : IccTagDataEntry, IEquatable<IccMultiProcessElementsTagDataEntry>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="IccMultiProcessElementsTagDataEntry"/> class.
        /// </summary>
        /// <param name="data">Processing elements</param>
        public IccMultiProcessElementsTagDataEntry(IccMultiProcessElement[] data)
            : this(data, IccProfileTag.Unknown)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IccMultiProcessElementsTagDataEntry"/> class.
        /// </summary>
        /// <param name="data">Processing elements</param>
        /// <param name="tagSignature">Tag Signature</param>
        public IccMultiProcessElementsTagDataEntry(IccMultiProcessElement[] data, IccProfileTag tagSignature)
            : base(IccTypeSignature.MultiProcessElements, tagSignature)
        {
            Guard.NotNull(data, nameof(data));
            Guard.IsTrue(data.Length > 0, nameof(data), $"{nameof(data)} must have at least one element");

            this.InputChannelCount = data[0].InputChannelCount;
            this.OutputChannelCount = data[0].OutputChannelCount;
            this.Data = data;

            bool channelsNotSame = data.Any(t => t.InputChannelCount != this.InputChannelCount || t.OutputChannelCount != this.OutputChannelCount);
            Guard.IsFalse(channelsNotSame, nameof(data), "The number of input and output channels are not the same for all elements");
        }

        /// <summary>
        /// Gets the number of input channels
        /// </summary>
        public int InputChannelCount { get; }

        /// <summary>
        /// Gets the number of output channels
        /// </summary>
        public int OutputChannelCount { get; }

        /// <summary>
        /// Gets the processing elements
        /// </summary>
        public IccMultiProcessElement[] Data { get; }

        /// <inheritdoc/>
        public override bool Equals(IccTagDataEntry other)
        {
            var entry = other as IccMultiProcessElementsTagDataEntry;
            return entry != null && this.Equals(entry);
        }

        /// <inheritdoc/>
        public bool Equals(IccMultiProcessElementsTagDataEntry other)
        {
            if (other is null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return base.Equals(other)
                && this.InputChannelCount == other.InputChannelCount
                && this.OutputChannelCount == other.OutputChannelCount
                && this.Data.SequenceEqual(other.Data);
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return obj is IccMultiProcessElementsTagDataEntry other && this.Equals(other);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = base.GetHashCode();
                hashCode = (hashCode * 397) ^ this.InputChannelCount;
                hashCode = (hashCode * 397) ^ this.OutputChannelCount;
                hashCode = (hashCode * 397) ^ (this.Data?.GetHashCode() ?? 0);
                return hashCode;
            }
        }
    }
}