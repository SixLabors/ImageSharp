// <copyright file="ImageDataAttributeBase.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>
namespace ImageSharp.Tests.TestUtilities
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;

    using Xunit.Sdk;

    /// <summary>
    /// Base class for Theory Data attributes which pass an instance of <see cref="TestImageFactory{TColor,TPacked}"/> to the test cases.
    /// </summary>
    public abstract class ImageDataAttributeBase : DataAttribute
    {
        protected readonly object[] AdditionalParameters;

        protected readonly PixelTypes PixelTypes;

        protected ImageDataAttributeBase(PixelTypes pixelTypes, object[] additionalParameters)
        {
            this.PixelTypes = pixelTypes;
            this.AdditionalParameters = additionalParameters;
        }

        public override IEnumerable<object[]> GetData(MethodInfo testMethod)
        {
            var type = testMethod.GetParameters().First().ParameterType;
            if (!typeof(ITestImageFactory).IsAssignableFrom(type))
            {
                yield return this.AdditionalParameters;
            }
            else
            {
                foreach (var pixelType in this.PixelTypes.ToTypes())
                {
                    var packedType = TestUtilityExtensions.GetPackedType(pixelType);
                    Type[] args = { pixelType, packedType };
                    var factoryType = typeof(TestImageFactory<,>).MakeGenericType(args);

                    foreach (object[] originalFacoryMethodArgs in this.GetAllFactoryMethodArgs(testMethod, factoryType))
                    {
                        var actualFactoryMethodArgs = new object[originalFacoryMethodArgs.Length + 1];
                        Array.Copy(originalFacoryMethodArgs, actualFactoryMethodArgs, originalFacoryMethodArgs.Length);
                        actualFactoryMethodArgs[actualFactoryMethodArgs.Length - 1] = testMethod;

                        var factory = factoryType.GetMethod(this.GetFactoryMethodName(testMethod))
                            .Invoke(null, actualFactoryMethodArgs);

                        object[] result = new object[this.AdditionalParameters.Length + 1];
                        result[0] = factory;
                        Array.Copy(this.AdditionalParameters, 0, result, 1, this.AdditionalParameters.Length);
                        yield return result;
                    }
                }
            }
        }

        protected virtual IEnumerable<object[]> GetAllFactoryMethodArgs(MethodInfo testMethod, Type factoryType)
        {
            var args = this.GetFactoryMethodArgs(testMethod, factoryType);
            return Enumerable.Repeat(args, 1);
        }

        protected virtual object[] GetFactoryMethodArgs(MethodInfo testMethod, Type factoryType)
        {
            throw new InvalidOperationException("Semi-abstract method");
        }

        protected abstract string GetFactoryMethodName(MethodInfo testMethod);
    }
}