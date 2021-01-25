// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Linq;

using SixLabors.ImageSharp.Metadata.Profiles.Exif;

namespace SixLabors.ImageSharp.Formats.Experimental.Tiff
{
    /// <summary>
    /// The tiff metadata extensions
    /// </summary>
    internal static class TiffFrameMetadataExtensions
    {
        public static T[] GetArray<T>(this TiffFrameMetadata meta, ExifTag tag, bool optional = false)
            where T : struct
        {
            if (meta.TryGetArray(tag, out T[] result))
            {
                return result;
            }

            if (!optional)
            {
                TiffThrowHelper.ThrowTagNotFound(nameof(tag));
            }

            return null;
        }

        public static bool TryGetArray<T>(this TiffFrameMetadata meta, ExifTag tag, out T[] result)
            where T : struct
        {
            IExifValue obj = meta.ExifProfile.GetValueInternal(tag);
            if (obj != null)
            {
                DebugGuard.IsTrue(obj.IsArray, "Expected array entry");
                object value = obj.GetValue();
                result = (T[])value;
                return true;
            }

            result = null;
            return false;
        }

        public static TEnum[] GetEnumArray<TEnum, TTagValue>(this TiffFrameMetadata meta, ExifTag tag, bool optional = false)
          where TEnum : struct
          where TTagValue : struct
        {
            if (meta.TryGetArray(tag, out TTagValue[] result))
            {
                return result.Select(a => (TEnum)(object)a).ToArray();
            }

            if (!optional)
            {
                TiffThrowHelper.ThrowTagNotFound(nameof(tag));
            }

            return null;
        }

        public static string GetString(this TiffFrameMetadata meta, ExifTag tag)
        {
            IExifValue obj = meta.ExifProfile.GetValueInternal(tag);
            if (obj != null)
            {
                DebugGuard.IsTrue(obj.DataType == ExifDataType.Ascii, "Expected string entry");
                object value = obj.GetValue();
                DebugGuard.IsTrue(value is string, "Expected string entry");
                return (string)value;
            }

            return null;
        }

        public static void Set(this TiffFrameMetadata meta, ExifTag tag, object value) =>
            meta.ExifProfile.SetValueInternal(tag, value);

        public static TEnum? GetSingleEnumNullable<TEnum, TTagValue>(this TiffFrameMetadata meta, ExifTag tag)
          where TEnum : struct
          where TTagValue : struct
        {
            if (!meta.TryGetSingle(tag, out TTagValue value))
            {
                return null;
            }

            return (TEnum)(object)value;
        }

        public static TEnum GetSingleEnum<TEnum, TTagValue>(this TiffFrameMetadata meta, ExifTag tag, TEnum? defaultValue = null)
            where TEnum : struct
            where TTagValue : struct
        => meta.GetSingleEnumNullable<TEnum, TTagValue>(tag) ?? (defaultValue != null ? defaultValue.Value : throw TiffThrowHelper.TagNotFound(nameof(tag)));

        public static T GetSingle<T>(this TiffFrameMetadata meta, ExifTag tag)
            where T : struct
        {
            if (meta.TryGetSingle(tag, out T result))
            {
                return result;
            }

            throw TiffThrowHelper.TagNotFound(nameof(tag));
        }

        public static bool TryGetSingle<T>(this TiffFrameMetadata meta, ExifTag tag, out T result)
            where T : struct
        {
            IExifValue obj = meta.ExifProfile.GetValueInternal(tag);
            if (obj != null)
            {
                DebugGuard.IsTrue(!obj.IsArray, "Expected non array entry");
                object value = obj.GetValue();
                result = (T)value;
                return true;
            }

            result = default;
            return false;
        }

        public static bool Remove(this TiffFrameMetadata meta, ExifTag tag)
        {
            IExifValue obj = meta.ExifProfile.GetValueInternal(tag);
            if (obj != null)
            {
                return meta.ExifProfile.RemoveValue(obj.Tag);
            }

            return false;
        }
    }
}
