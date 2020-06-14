// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Metadata.Profiles.Exif
{
    internal abstract class ExifValue : IExifValue, IEquatable<ExifTag>
    {
        protected ExifValue(ExifTag tag) => this.Tag = tag;

        protected ExifValue(ExifTagValue tag) => this.Tag = new UnkownExifTag(tag);

        internal ExifValue(ExifValue other)
        {
            Guard.NotNull(other, nameof(other));

            this.DataType = other.DataType;
            this.IsArray = other.IsArray;
            this.Tag = other.Tag;

            if (!other.IsArray)
            {
                // All types are value types except for string which is immutable so safe to simply assign.
                this.TrySetValue(other.GetValue());
            }
            else
            {
                // All array types are value types so Clone() is sufficient here.
                var array = (Array)other.GetValue();
                this.TrySetValue(array.Clone());
            }
        }

        public virtual ExifDataType DataType { get; }

        public virtual bool IsArray { get; }

        public ExifTag Tag { get; }

        public static bool operator ==(ExifValue left, ExifTag right) => Equals(left, right);

        public static bool operator !=(ExifValue left, ExifTag right) => !Equals(left, right);

        public override bool Equals(object obj)
        {
            if (obj is null)
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj is ExifTag tag)
            {
                return this.Equals(tag);
            }

            if (obj is ExifValue value)
            {
                return this.Tag.Equals(value.Tag) && Equals(this.GetValue(), value.GetValue());
            }

            return false;
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        public bool Equals(ExifTag other) => this.Tag.Equals(other);

        [MethodImpl(InliningOptions.ShortMethod)]
        public override int GetHashCode() => HashCode.Combine(this.Tag, this.GetValue());

        public abstract object GetValue();

        public abstract bool TrySetValue(object value);

        public abstract IExifValue DeepClone();
    }
}
