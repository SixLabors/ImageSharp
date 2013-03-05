using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ImageProcessor.Web.Helpers
{
    using System.Linq.Expressions;
    using System.Reflection;

    public class ObjectFactory
    {
        public delegate T ObjectActivator<out T>(params object[] args);

        public static ObjectActivator<T> GetActivator<T>(ConstructorInfo ctor)
        {
            Type type = ctor.DeclaringType;
            ParameterInfo[] paramsInfo = ctor.GetParameters();

            // Create a single param of type object[]
            ParameterExpression param = Expression.Parameter(typeof(object[]), "args");

            Expression[] argsExp = new Expression[paramsInfo.Length];

            // Pick each arg from the params array 
            // and create a typed expression for them
            for (int i = 0; i < paramsInfo.Length; i++)
            {
                Expression index = Expression.Constant(i);
                Type paramType = paramsInfo[i].ParameterType;

                Expression paramAccessorExp = Expression.ArrayIndex(param, index);

                Expression paramCastExp = Expression.Convert(paramAccessorExp, paramType);

                argsExp[i] = paramCastExp;
            }

            // Make a NewExpression that calls the
            // ctor with the args we just created
            NewExpression newExp = Expression.New(ctor, argsExp);

            // Create a lambda with the New
            // Expression as body and our param object[] as arg
            LambdaExpression lambda = Expression.Lambda(typeof(ObjectActivator<T>), newExp, param);

            // Compile it
            ObjectActivator<T> compiled = (ObjectActivator<T>)lambda.Compile();
            return compiled;
        }
    }
}
