// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics.CodeAnalysis;
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
    /// <param name="description">The description.</param>
    /// <returns>
    /// True when description was found
    /// </returns>
    public static bool TryGetDescription(ExifTag tag, object? value, [NotNullWhen(true)] out string? description)
    {
        ExifTagValue tagValue = (ExifTagValue)(ushort)tag;
        FieldInfo? field = typeof(ExifTagValue).GetField(tagValue.ToString(), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

        description = null;

        if (field is null)
        {
            return false;
        }

        foreach (CustomAttributeData customAttribute in field.CustomAttributes)
        {
            object? attributeValue = customAttribute.ConstructorArguments[0].Value;

            if (Equals(attributeValue, value))
            {
                description = (string?)customAttribute.ConstructorArguments[1].Value;

                return description is not null;
            }
        }

        return false;
    }
}
