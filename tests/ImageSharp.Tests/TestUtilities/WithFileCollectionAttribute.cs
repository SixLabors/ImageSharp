// <copyright file="WithFileCollectionAttribute.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>
namespace ImageSharp.Tests.TestUtilities
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;

    /// <summary>
    /// Triggers passing <see cref="TestImageFactory{TColor}"/> instances which read an image for each file being enumerated by the (static) test class field/property defined by enumeratorMemberName
    /// <see cref="TestImageFactory{TColor}"/> instances will be passed for each the pixel format defined by the pixelTypes parameter
    /// </summary>
    public class WithFileCollectionAttribute : ImageDataAttributeBase
    {
        private readonly string enumeratorMemberName;

        /// <summary>
        /// Triggers passing <see cref="TestImageFactory{TColor}"/> instances which read an image for each file being enumerated by the (static) test class field/property defined by enumeratorMemberName
        /// <see cref="TestImageFactory{TColor}"/> instances will be passed for each the pixel format defined by the pixelTypes parameter
        /// </summary>
        /// <param name="enumeratorMemberName">The name of the static test class field/property enumerating the files</param>
        /// <param name="pixelTypes">The requested pixel types</param>
        /// <param name="additionalParameters">Additional theory parameter values</param>
        public WithFileCollectionAttribute(
            string enumeratorMemberName,
            PixelTypes pixelTypes,
            params object[] additionalParameters)
            : base(pixelTypes, additionalParameters)
        {
            this.enumeratorMemberName = enumeratorMemberName;
        }

        protected override IEnumerable<object[]> GetAllFactoryMethodArgs(MethodInfo testMethod, Type factoryType)
        {
            var accessor = this.GetPropertyAccessor(testMethod.DeclaringType);

            accessor = accessor ?? this.GetFieldAccessor(testMethod.DeclaringType);

            IEnumerable<string> files = (IEnumerable<string>)accessor();
            return files.Select(f => new object[] { f });
        }

        protected override string GetFactoryMethodName(MethodInfo testMethod) => "File";

        /// <summary>
        /// Based on MemberData implementation
        /// </summary>
        private Func<object> GetFieldAccessor(Type type)
        {
            FieldInfo fieldInfo = null;
            for (var reflectionType = type;
                 reflectionType != null;
                 reflectionType = reflectionType.GetTypeInfo().BaseType)
            {
                fieldInfo = reflectionType.GetRuntimeField(this.enumeratorMemberName);
                if (fieldInfo != null) break;
            }

            if (fieldInfo == null || !fieldInfo.IsStatic) return null;

            return () => fieldInfo.GetValue(null);
        }

        /// <summary>
        /// Based on MemberData implementation
        /// </summary>
        private Func<object> GetPropertyAccessor(Type type)
        {
            PropertyInfo propInfo = null;
            for (var reflectionType = type;
                 reflectionType != null;
                 reflectionType = reflectionType.GetTypeInfo().BaseType)
            {
                propInfo = reflectionType.GetRuntimeProperty(this.enumeratorMemberName);
                if (propInfo != null) break;
            }

            if (propInfo == null || propInfo.GetMethod == null || !propInfo.GetMethod.IsStatic) return null;

            return () => propInfo.GetValue(null, null);
        }
    }
}