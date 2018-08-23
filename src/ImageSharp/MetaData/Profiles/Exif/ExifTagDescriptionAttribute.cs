// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Reflection;

namespace SixLabors.ImageSharp.MetaData.Profiles.Exif
{
    /// <summary>
    /// Class that provides a description for an ExifTag value.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = true)]
    internal sealed class ExifTagDescriptionAttribute : Attribute
    {
        private readonly object value;
        private readonly string description;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExifTagDescriptionAttribute"/> class.
        /// </summary>
        /// <param name="value">The value of the exif tag.</param>
        /// <param name="description">The description for the value of the exif tag.</param>
        public ExifTagDescriptionAttribute(object value, string description)
        {
            this.value = value;
            this.description = description;
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
            FieldInfo field = tag.GetType().GetTypeInfo().GetDeclaredField(tag.ToString());

            if (field is null)
            {
                return null;
            }

            foreach (CustomAttributeData customAttribute in field.CustomAttributes)
            {
                object attributeValue = customAttribute.ConstructorArguments[0].Value;

                if (object.Equals(attributeValue, value))
                {
                    return (string)customAttribute.ConstructorArguments[1].Value;
                }
            }

            return null;
        }
    }
}
