﻿using System;
using System.IO;
using System.Linq;
using System.Text;
#if UNITY_EDITOR
using UnityEditor;
#endif


namespace UniJSON
{
    public static partial class GenericExpressionCallFactory
    {
#if UNITY_EDITOR && VRM_DEVELOP
        const int ARGS = 6;
        const string GENERATE_PATH = "Assets/VRM/UniJSON/Scripts/GenericCallUtility/GenericExpressionCallFactory.g.cs";

        static System.Collections.Generic.IEnumerable<string> GetArgs(string prefix, int n)
        {
            for (int i = 0; i < n; ++i)
            {
                yield return prefix + i;
            }
        }

        [MenuItem(VRM.VRMVersion.MENU + "/Generate GenericExpressionCallFactory")]
        static void Generate()
        {
            var sb = new StringBuilder();
            using (var w = new StringWriter(sb))
            {
                w.WriteLine(@"
using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;


namespace UniJSON
{
    public static partial class GenericExpressionCallFactory
    {
");
                // Create
                for (int i = 1; i <= ARGS; ++i)
                {
                    var g = String.Join(", ", GetArgs("A", i).ToArray());
                    var a = String.Join(", ", GetArgs("a", i).ToArray());

                    var source = @"
#if UNITY_5
        public static Delegate Create<S, $0>(MethodInfo m)
#else
        public static Action<S, $0> Create<S, $0>(MethodInfo m)
#endif
        {
            var self = Expression.Parameter(m.DeclaringType, m.Name);
            var args = m.GetParameters().Select(x => Expression.Parameter(x.ParameterType, x.Name)).ToArray();
            var call = Expression.Call(self, m, args);
            return 
#if UNITY_5
#else
                (Action<S, $0>)
#endif
                Expression.Lambda(call, new[] { self }.Concat(args).ToArray()).Compile();
        }
".Replace("$0", g).Replace("$1", a);

                    w.WriteLine(source);
                }

                // CreateWithThis
                for (int i = 1; i <= ARGS; ++i)
                {
                    var g = String.Join(", ", GetArgs("A", i).ToArray());

                    var source = @"
#if UNITY_5
        public static Delegate CreateWithThis<S, $0>(MethodInfo m, S instance)
#else
        public static Action<$0> CreateWithThis<S, $0>(MethodInfo m, S instance)
#endif
        {
            if (m.IsStatic)
            {
                if (instance != null)
                {
                    throw new ArgumentException();
                }
            }
            else
            {
                if (instance == null)
                {
                    throw new ArgumentNullException();
                }
            }

            var self = Expression.Constant(instance, typeof(S)); // thisを定数化
            var args = m.GetParameters().Select(x => Expression.Parameter(x.ParameterType, x.Name)).ToArray();
            MethodCallExpression call;
            if (m.IsStatic)
            {
                call = Expression.Call(m, args);
            }
            else
            {
                call = Expression.Call(self, m, args);
            }
            return 
#if UNITY_5
#else
                (Action<$0>)
#endif
                Expression.Lambda(call, args).Compile();
        }
".Replace("$0", g);

                    w.WriteLine(source);
                }

                w.WriteLine(@"
    }
}
");
            }

            var path = UniGLTF.UnityPath.FromUnityPath(GENERATE_PATH);
            File.WriteAllText(path.FullPath, sb.ToString().Replace("\r\n", "\n"));
            path.ImportAsset();
        }
#endif
    }
}