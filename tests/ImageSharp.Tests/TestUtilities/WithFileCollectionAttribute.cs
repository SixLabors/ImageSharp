namespace ImageSharp.Tests.TestUtilities
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;

    public class WithFileCollectionAttribute : ImageDataAttributeBase
    {
        private readonly string enumeratorMemberName;
        

        public WithFileCollectionAttribute(string enumeratorMemberName, PixelTypes pixelTypes, params object[] additionalParameters)
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
            for (var reflectionType = type; reflectionType != null; reflectionType = reflectionType.GetTypeInfo().BaseType)
            {
                fieldInfo = reflectionType.GetRuntimeField(this.enumeratorMemberName);
                if (fieldInfo != null)
                    break;
            }

            if (fieldInfo == null || !fieldInfo.IsStatic)
                return null;

            return () => fieldInfo.GetValue(null);
        }

        /// <summary>
        /// Based on MemberData implementation
        /// </summary>
        private Func<object> GetPropertyAccessor(Type type)
        {
            PropertyInfo propInfo = null;
            for (var reflectionType = type; reflectionType != null; reflectionType = reflectionType.GetTypeInfo().BaseType)
            {
                propInfo = reflectionType.GetRuntimeProperty(this.enumeratorMemberName);
                if (propInfo != null)
                    break;
            }

            if (propInfo == null || propInfo.GetMethod == null || !propInfo.GetMethod.IsStatic)
                return null;

            return () => propInfo.GetValue(null, null);
        }
    }
}