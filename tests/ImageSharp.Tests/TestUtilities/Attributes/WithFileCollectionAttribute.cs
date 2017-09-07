// <copyright file="WithFileCollectionAttribute.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;

    /// <summary>
    /// Triggers passing <see cref="TestImageProvider{TPixel}"/> instances which read an image for each file being enumerated by the (static) test class field/property defined by enumeratorMemberName
    /// See <a href="TestImageProvider{TPixel}"/> instances will be passed for each the pixel format defined by the pixelTypes parameter
    /// </summary>
    public class WithFileCollectionAttribute : ImageDataAttributeBase
    {
        private readonly string fileEnumeratorMemberName;

        /// <summary>
        /// Triggers passing <see cref="TestImageProvider{TPixel}"/> instances which read an image for each file being enumerated by the (static) test class field/property defined by enumeratorMemberName
        /// See <a href="TestImageProvider{TPixel}"/> instances will be passed for each the pixel format defined by the pixelTypes parameter
        /// </summary>
        /// <param name="fileEnumeratorMemberName">The name of the static test class field/property enumerating the files</param>
        /// <param name="pixelTypes">The requested pixel types</param>
        /// <param name="additionalParameters">Additional theory parameter values</param>
        public WithFileCollectionAttribute(
            string fileEnumeratorMemberName,
            PixelTypes pixelTypes,
            params object[] additionalParameters)
            : base(null, pixelTypes, additionalParameters)
        {
            this.fileEnumeratorMemberName = fileEnumeratorMemberName;
        }

        /// <summary>
        /// Triggers passing <see cref="TestImageProvider{TPixel}"/> instances which read an image for each file being enumerated by the (static) test class field/property defined by enumeratorMemberName
        /// See <a href="TestImageProvider{TPixel}"/> instances will be passed for each the pixel format defined by the pixelTypes parameter
        /// </summary>
        /// <param name="enumeratorMemberName">The name of the static test class field/property enumerating the files</param>
        /// <param name="memberName">The member name for enumerating method parameters</param>
        /// <param name="pixelTypes">The requested pixel types</param>
        /// <param name="additionalParameters">Additional theory parameter values</param>
        public WithFileCollectionAttribute(
            string enumeratorMemberName,
            string memberName,
            PixelTypes pixelTypes,
            params object[] additionalParameters)
            : base(memberName, pixelTypes, additionalParameters)
        {
            this.fileEnumeratorMemberName = enumeratorMemberName;
        }

        /// <summary>
        /// Generates the collection of method arguments from the given test.
        /// </summary>
        /// <param name="testMethod">The test method</param>
        /// <param name="factoryType">The test image provider factory type</param>
        /// <returns>The <see cref="IEnumerable{T}"/></returns>
        protected override IEnumerable<object[]> GetAllFactoryMethodArgs(MethodInfo testMethod, Type factoryType)
        {
            Func<object> accessor = this.GetPropertyAccessor(testMethod.DeclaringType);
            accessor = accessor ?? this.GetFieldAccessor(testMethod.DeclaringType);

            var files = (IEnumerable<string>)accessor();
            return files.Select(f => new object[] { f });
        }

        /// <inheritdoc/>
        protected override string GetFactoryMethodName(MethodInfo testMethod) => "File";

        /// <summary>
        /// Gets the field accessor for the given type.
        /// </summary>
        private Func<object> GetFieldAccessor(Type type)
        {
            FieldInfo fieldInfo = null;
            for (Type reflectionType = type;
                 reflectionType != null;
                 reflectionType = reflectionType.GetTypeInfo().BaseType)
            {
                fieldInfo = reflectionType.GetRuntimeField(this.fileEnumeratorMemberName);
                if (fieldInfo != null)
                {
                    break;
                }
            }

            if (fieldInfo == null || !fieldInfo.IsStatic)
            {
                return null;
            }

            return () => fieldInfo.GetValue(null);
        }

        /// <summary>
        /// Gets the property accessor for the given type.
        /// </summary>
        private Func<object> GetPropertyAccessor(Type type)
        {
            PropertyInfo propInfo = null;
            for (Type reflectionType = type;
                 reflectionType != null;
                 reflectionType = reflectionType.GetTypeInfo().BaseType)
            {
                propInfo = reflectionType.GetRuntimeProperty(this.fileEnumeratorMemberName);
                if (propInfo != null) break;
            }

            if (propInfo?.GetMethod == null || !propInfo.GetMethod.IsStatic)
            {
                return null;
            }

            return () => propInfo.GetValue(null, null);
        }
    }
}