// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.
#nullable disable

using System.Reflection;

namespace SixLabors.ImageSharp.Metadata.Profiles.Exif;

/// <summary>
/// Class that provides a description for an ExifTag value.
/// </summary>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = true)]
internal sealed class ExifTagDescriptionAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ExifTagDescriptionAttribute"/> class.
    /// </summary>
    /// <param name="value">The value of the exif tag.</param>
    /// <param name="description">The description for the value of the exif tag.</param>
    public ExifTagDescriptionAttribute(object value, string description)
    {
    }

    /// <summary>
    /// Gets the tag description from any custom attributes.
    /// </summary>
    /// <param name="tag">The tag.</param>
    /// <param name="value">The value.</param>
    /// <returns>
    /// The <see cref="string"/>.
    /// </returns>
    public static string GetDescription(ExifTag tag, object value)
    {
        var tagValue = (ExifTagValue)(ushort)tag;
        FieldInfo field = typeof(ExifTagValue).GetField(tagValue.ToString(), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

        if (field is null)
        {
            return null;
        }

        foreach (CustomAttributeData customAttribute in field.CustomAttributes)
        {
            object attributeValue = customAttribute.ConstructorArguments[0].Value;

            if (Equals(attributeValue, value))
            {
                return (string)customAttribute.ConstructorArguments[1].Value;
            }
        }

        return null;
    }
}
