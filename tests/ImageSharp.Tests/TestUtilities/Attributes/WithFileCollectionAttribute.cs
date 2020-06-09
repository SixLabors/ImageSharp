// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace SixLabors.ImageSharp.Tests
{
    /// <summary>
    /// Triggers passing <see cref="TestImageProvider{TPixel}"/> instances which read an image for each file being enumerated by the (static) test class field/property defined by enumeratorMemberName
    /// <see cref="TestImageProvider{TPixel}"/> instances will be passed for each the pixel format defined by the pixelTypes parameter
    /// </summary>
    public class WithFileCollectionAttribute : ImageDataAttributeBase
    {
        private readonly string fileEnumeratorMemberName;

        /// <summary>
        /// Triggers passing <see cref="TestImageProvider{TPixel}"/> instances which read an image for each file being enumerated by the (static) test class field/property defined by enumeratorMemberName
        /// <see cref="TestImageProvider{TPixel}"/> instances will be passed for each the pixel format defined by the pixelTypes parameter
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
        /// <see cref="TestImageProvider{TPixel}"/> instances will be passed for each the pixel format defined by the pixelTypes parameter
        /// </summary>
        /// <param name="fileEnumeratorMemberName">The name of the static test class field/property enumerating the files</param>
        /// <param name="memberName">The member name for enumerating method parameters</param>
        /// <param name="pixelTypes">The requested pixel types</param>
        /// <param name="additionalParameters">Additional theory parameter values</param>
        public WithFileCollectionAttribute(
            string fileEnumeratorMemberName,
            string memberName,
            PixelTypes pixelTypes,
            params object[] additionalParameters)
            : base(memberName, pixelTypes, additionalParameters)
        {
            this.fileEnumeratorMemberName = fileEnumeratorMemberName;
        }

        /// <summary>
        /// Generates the collection of method arguments from the given test.
        /// </summary>
        /// <param name="testMethod">The test method</param>
        /// <param name="factoryType">The test image provider factory type</param>
        /// <returns>The <see cref="IEnumerable{T}"/></returns>
        protected override IEnumerable<object[]> GetAllFactoryMethodArgs(MethodInfo testMethod, Type factoryType)
        {
            Func<object> accessor = this.GetPropertyAccessor(testMethod.DeclaringType, this.fileEnumeratorMemberName);
            accessor = accessor ?? this.GetFieldAccessor(testMethod.DeclaringType, this.fileEnumeratorMemberName);

            var files = (IEnumerable<string>)accessor();
            return files.Select(f => new object[] { f });
        }

        /// <inheritdoc/>
        protected override string GetFactoryMethodName(MethodInfo testMethod) => "File";
    }
}