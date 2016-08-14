// <copyright file="ExifTag.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore
{
    using System;
    using System.Linq;
    using System.Reflection;

    /// <summary>
    /// Class that provides a description for an ExifTag value.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = true)]
    internal sealed class ExifTagDescriptionAttribute : Attribute
    {
        private object value;
        private string description;

        ///<summary>
        /// Initializes a new instance of the ExifTagDescriptionAttribute class.
        ///</summary>
        /// <param name="value">The value of the exif tag.</param>
        /// <param name="description">The description for the value of the exif tag.</param>
        public ExifTagDescriptionAttribute(object value, string description)
        {
            this.value = value;
            this.description = description;
        }

        public static string GetDescription(ExifTag tag, object value)
        {
            FieldInfo field = tag.GetType().GetTypeInfo().GetDeclaredField(tag.ToString());
            if (field == null)
              return null;

            foreach (CustomAttributeData customAttribute in field.CustomAttributes)
            {
                object attributeValue = customAttribute.ConstructorArguments[0].Value;

                if (Equals(attributeValue, value))
                    return (string)customAttribute.ConstructorArguments[1].Value;
            }

            return null;
        }
    }
}
