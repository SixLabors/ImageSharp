// <copyright file="ImageDataAttributeBase.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;

    using Xunit.Sdk;

    /// <summary>
    /// Base class for Theory Data attributes which pass an instance of <see cref="TestImageProvider{TPixel}"/> to the test case.
    /// </summary>
    public abstract class ImageDataAttributeBase : DataAttribute
    {
        protected readonly object[] AdditionalParameters;

        protected readonly PixelTypes PixelTypes;

        protected ImageDataAttributeBase(string memberName, PixelTypes pixelTypes, object[] additionalParameters)
        {
            this.PixelTypes = pixelTypes;
            this.AdditionalParameters = additionalParameters;
            this.MemberName = memberName;

        }

        public string MemberName { get; private set; }

        public Type MemberType { get; set; }

        public override IEnumerable<object[]> GetData(MethodInfo testMethod)
        {
            IEnumerable<object[]> addedRows = Enumerable.Empty<object[]>();
            if (!string.IsNullOrWhiteSpace(this.MemberName))
            {
                Type type = this.MemberType ?? testMethod.DeclaringType;
                Func<object> accessor = GetPropertyAccessor(type) ?? GetFieldAccessor(type);// ?? GetMethodAccessor(type);

                if (accessor != null)
                {
                    object obj = accessor();
                    if (obj is IEnumerable<object> memberItems)
                    {
                        addedRows = memberItems.Select(x => x as object[]);
                        if (addedRows.Any(x => x == null))
                        {
                            throw new ArgumentException($"Property {MemberName} on {MemberType ?? testMethod.DeclaringType} yielded an item that is not an object[]");
                        }
                    }
                }
            }

            if (!addedRows.Any())
            {
                addedRows = new[] { new object[0] };
            }

            bool firstIsprovider = FirstIsProvider(testMethod);
            IEnumerable<object[]> dataItems = Enumerable.Empty<object[]>();
            if (firstIsprovider)
            {
                return InnerGetData(testMethod, addedRows);
            }
            else
            {
                return addedRows.Select(x => x.Concat(this.AdditionalParameters).ToArray());
            }
        }

        private bool FirstIsProvider(MethodInfo testMethod)
        {
            TypeInfo dataType = testMethod.GetParameters().First().ParameterType.GetTypeInfo();
            return dataType.IsGenericType && dataType.GetGenericTypeDefinition() == typeof(TestImageProvider<>);
        }

        private IEnumerable<object[]> InnerGetData(MethodInfo testMethod, IEnumerable<object[]> memberData)
        {
            foreach (KeyValuePair<PixelTypes, Type> kv in this.PixelTypes.ExpandAllTypes())
            {
                Type factoryType = typeof(TestImageProvider<>).MakeGenericType(kv.Value);

                foreach (object[] originalFacoryMethodArgs in this.GetAllFactoryMethodArgs(testMethod, factoryType))
                {
                    foreach (object[] row in memberData)
                    {
                        object[] actualFactoryMethodArgs = new object[originalFacoryMethodArgs.Length + 2];
                        Array.Copy(originalFacoryMethodArgs, actualFactoryMethodArgs, originalFacoryMethodArgs.Length);
                        actualFactoryMethodArgs[actualFactoryMethodArgs.Length - 2] = testMethod;
                        actualFactoryMethodArgs[actualFactoryMethodArgs.Length - 1] = kv.Key;

                        object factory = factoryType.GetMethod(this.GetFactoryMethodName(testMethod))
                            .Invoke(null, actualFactoryMethodArgs);

                        object[] result = new object[this.AdditionalParameters.Length + 1 + row.Length];
                        result[0] = factory;
                        Array.Copy(row, 0, result, 1, row.Length);
                        Array.Copy(this.AdditionalParameters, 0, result, 1 + row.Length, this.AdditionalParameters.Length);
                        yield return result;
                    }
                }
            }
        }

        protected virtual IEnumerable<object[]> GetAllFactoryMethodArgs(MethodInfo testMethod, Type factoryType)
        {
            object[] args = this.GetFactoryMethodArgs(testMethod, factoryType);
            return Enumerable.Repeat(args, 1);
        }

        protected virtual object[] GetFactoryMethodArgs(MethodInfo testMethod, Type factoryType)
        {
            throw new InvalidOperationException("Semi-abstract method");
        }

        protected abstract string GetFactoryMethodName(MethodInfo testMethod);

        Func<object> GetFieldAccessor(Type type)
        {
            FieldInfo fieldInfo = null;
            for (Type reflectionType = type; reflectionType != null; reflectionType = reflectionType.GetTypeInfo().BaseType)
            {
                fieldInfo = reflectionType.GetRuntimeField(MemberName);
                if (fieldInfo != null)
                    break;
            }

            if (fieldInfo == null || !fieldInfo.IsStatic)
                return null;

            return () => fieldInfo.GetValue(null);
        }

        //Func<object> GetMethodAccessor(Type type)
        //{
        //    MethodInfo methodInfo = null;
        //    var parameterTypes = Parameters == null ? new Type[0] : Parameters.Select(p => p?.GetType()).ToArray();
        //    for (var reflectionType = type; reflectionType != null; reflectionType = reflectionType.GetTypeInfo().BaseType)
        //    {
        //        methodInfo = reflectionType.GetRuntimeMethods()
        //                                   .FirstOrDefault(m => m.Name == MemberName && ParameterTypesCompatible(m.GetParameters(), parameterTypes));
        //        if (methodInfo != null)
        //            break;
        //    }

        //    if (methodInfo == null || !methodInfo.IsStatic)
        //        return null;

        //    return () => methodInfo.Invoke(null, Parameters);
        //}

        Func<object> GetPropertyAccessor(Type type)
        {
            PropertyInfo propInfo = null;
            for (Type reflectionType = type; reflectionType != null; reflectionType = reflectionType.GetTypeInfo().BaseType)
            {
                propInfo = reflectionType.GetRuntimeProperty(MemberName);
                if (propInfo != null)
                    break;
            }

            if (propInfo == null || propInfo.GetMethod == null || !propInfo.GetMethod.IsStatic)
                return null;

            return () => propInfo.GetValue(null, null);
        }

    }
}