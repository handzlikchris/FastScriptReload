using System;
using System.Reflection;
using SRE = System.Reflection.Emit;
using CIL = Mono.Cecil.Cil;
using System.Linq.Expressions;
using MonoMod.Utils;
using System.Collections.Generic;
using Mono.Cecil.Cil;
using Mono.Cecil;
using System.Text;
using Mono.Collections.Generic;
using System.Runtime.CompilerServices;

namespace MonoMod.Utils {
#if !MONOMOD_INTERNAL
    public
#endif
    static partial class Extensions {

        /// <summary>
        /// Get a reference ID that is similar to the full name, but consistent between System.Reflection and Mono.Cecil.
        /// </summary>
        /// <param name="method">The method to get the ID for.</param>
        /// <param name="name">The name to use instead of the reference's own name.</param>
        /// <param name="type">The ID to use instead of the reference's declaring type ID.</param>
        /// <param name="withType">Whether the type ID should be included or not. System.Reflection avoids it by default.</param>
        /// <param name="simple">Whether the ID should be "simple" (name only).</param>
        /// <returns>The ID.</returns>
        public static string GetID(this MethodReference method, string name = null, string type = null, bool withType = true, bool simple = false) {
            StringBuilder builder = new StringBuilder();

            if (simple) {
                if (withType && (type != null || method.DeclaringType != null))
                    builder.Append(type ?? method.DeclaringType.GetPatchFullName()).Append("::");
                builder.Append(name ?? method.Name);
                return builder.ToString();
            }

            builder
                .Append(method.ReturnType.GetPatchFullName())
                .Append(" ");

            if (withType && (type != null || method.DeclaringType != null))
                builder.Append(type ?? method.DeclaringType.GetPatchFullName()).Append("::");

            builder
                .Append(name ?? method.Name);

            if (method is GenericInstanceMethod gim && gim.GenericArguments.Count != 0) {
                builder.Append("<");
                Collection<TypeReference> arguments = gim.GenericArguments;
                for (int i = 0; i < arguments.Count; i++) {
                    if (i > 0)
                        builder.Append(",");
                    builder.Append(arguments[i].GetPatchFullName());
                }
                builder.Append(">");

            } else if (method.GenericParameters.Count != 0) {
                builder.Append("<");
                Collection<GenericParameter> arguments = method.GenericParameters;
                for (int i = 0; i < arguments.Count; i++) {
                    if (i > 0)
                        builder.Append(",");
                    builder.Append(arguments[i].Name);
                }
                builder.Append(">");
            }

            builder.Append("(");

            if (method.HasParameters) {
                Collection<ParameterDefinition> parameters = method.Parameters;
                for (int i = 0; i < parameters.Count; i++) {
                    ParameterDefinition parameter = parameters[i];
                    if (i > 0)
                        builder.Append(",");

                    if (parameter.ParameterType.IsSentinel)
                        builder.Append("...,");

                    builder.Append(parameter.ParameterType.GetPatchFullName());
                }
            }

            builder.Append(")");

            return builder.ToString();
        }

        /// <summary>
        /// Get a reference ID that is similar to the full name, but consistent between System.Reflection and Mono.Cecil.
        /// </summary>
        /// <param name="method">The call site to get the ID for.</param>
        /// <returns>The ID.</returns>
        public static string GetID(this Mono.Cecil.CallSite method) {
            StringBuilder builder = new StringBuilder();

            builder
                .Append(method.ReturnType.GetPatchFullName())
                .Append(" ");

            builder.Append("(");

            if (method.HasParameters) {
                Collection<ParameterDefinition> parameters = method.Parameters;
                for (int i = 0; i < parameters.Count; i++) {
                    ParameterDefinition parameter = parameters[i];
                    if (i > 0)
                        builder.Append(",");

                    if (parameter.ParameterType.IsSentinel)
                        builder.Append("...,");

                    builder.Append(parameter.ParameterType.GetPatchFullName());
                }
            }

            builder.Append(")");

            return builder.ToString();
        }

        private static readonly Type t_ParamArrayAttribute = typeof(ParamArrayAttribute);
        /// <summary>
        /// Get a reference ID that is similar to the full name, but consistent between System.Reflection and Mono.Cecil.
        /// </summary>
        /// <param name="method">The method to get the ID for.</param>
        /// <param name="name">The name to use instead of the reference's own name.</param>
        /// <param name="type">The ID to use instead of the reference's declaring type ID.</param>
        /// <param name="withType">Whether the type ID should be included or not. System.Reflection avoids it by default.</param>
        /// <param name="proxyMethod">Whether the method is regarded as a proxy method or not. Setting this paramater to true will skip the first parameter.</param>
        /// <param name="simple">Whether the ID should be "simple" (name only).</param>
        /// <returns>The ID.</returns>
        public static string GetID(this System.Reflection.MethodBase method, string name = null, string type = null, bool withType = true, bool proxyMethod = false, bool simple = false) {
            while (method is System.Reflection.MethodInfo && method.IsGenericMethod && !method.IsGenericMethodDefinition)
                method = ((System.Reflection.MethodInfo) method).GetGenericMethodDefinition();

            StringBuilder builder = new StringBuilder();

            if (simple) {
                if (withType && (type != null || method.DeclaringType != null))
                    builder.Append(type ?? method.DeclaringType.FullName).Append("::");
                builder.Append(name ?? method.Name);
                return builder.ToString();
            }

            builder
                .Append((method as System.Reflection.MethodInfo)?.ReturnType?.FullName ?? "System.Void")
                .Append(" ");

            if (withType && (type != null || method.DeclaringType != null))
                builder.Append(type ?? method.DeclaringType.FullName.Replace("+", "/", StringComparison.Ordinal)).Append("::");

            builder
                .Append(name ?? method.Name);

            if (method.ContainsGenericParameters) {
                builder.Append("<");
                Type[] arguments = method.GetGenericArguments();
                for (int i = 0; i < arguments.Length; i++) {
                    if (i > 0)
                        builder.Append(",");
                    builder.Append(arguments[i].Name);
                }
                builder.Append(">");
            }

            builder.Append("(");

            System.Reflection.ParameterInfo[] parameters = method.GetParameters();
            for (int i = proxyMethod ? 1 : 0; i < parameters.Length; i++) {
                System.Reflection.ParameterInfo parameter = parameters[i];
                if (i > (proxyMethod ? 1 : 0))
                    builder.Append(",");

                bool defined;
                try {
#if NETSTANDARD
                    defined = System.Reflection.CustomAttributeExtensions.IsDefined(parameter, t_ParamArrayAttribute, false);
#else
                    defined = parameter.GetCustomAttributes(t_ParamArrayAttribute, false).Length != 0;
#endif
                } catch (NotSupportedException) {
                    // Newer versions of Mono are stupidly strict and like to throw a NotSupportedException on DynamicMethod args.
                    defined = false;
                }
                if (defined)
                    builder.Append("...,");

                builder.Append(parameter.ParameterType.FullName);
            }

            builder.Append(")");

            return builder.ToString();
        }

    }
}
