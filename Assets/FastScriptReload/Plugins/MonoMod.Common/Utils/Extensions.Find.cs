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
        /// Find a method for a given ID.
        /// </summary>
        /// <param name="type">The type to search in.</param>
        /// <param name="id">The method ID.</param>
        /// <param name="simple">Whether to perform a simple search pass as well or not.</param>
        /// <returns>The first matching method or null.</returns>
        public static MethodDefinition FindMethod(this TypeDefinition type, string id, bool simple = true) {
            if (simple && !id.Contains(" ", StringComparison.Ordinal)) {
                // First simple pass: With type name (just "Namespace.Type::MethodName")
                foreach (MethodDefinition method in type.Methods)
                    if (method.GetID(simple: true) == id)
                        return method;
                // Second simple pass: Without type name (basically name only)
                foreach (MethodDefinition method in type.Methods)
                    if (method.GetID(withType: false, simple: true) == id)
                        return method;
            }

            // First pass: With type name (f.e. global searches)
            foreach (MethodDefinition method in type.Methods)
                if (method.GetID() == id)
                    return method;
            // Second pass: Without type name (f.e. LinkTo)
            foreach (MethodDefinition method in type.Methods)
                if (method.GetID(withType: false) == id)
                    return method;

            return null;
        }
        /// <summary>
        /// Find a method for a given ID recursively (including the passed type's base types).
        /// </summary>
        /// <param name="type">The type to search in.</param>
        /// <param name="id">The method ID.</param>
        /// <param name="simple">Whether to perform a simple search pass as well or not.</param>
        /// <returns>The first matching method or null.</returns>
        public static MethodDefinition FindMethodDeep(this TypeDefinition type, string id, bool simple = true) {
            return type.FindMethod(id, simple) ?? type.BaseType?.Resolve()?.FindMethodDeep(id, simple);
        }

        /// <summary>
        /// Find a method for a given ID.
        /// </summary>
        /// <param name="type">The type to search in.</param>
        /// <param name="id">The method ID.</param>
        /// <param name="simple">Whether to perform a simple search pass as well or not.</param>
        /// <returns>The first matching method or null.</returns>
        public static MethodInfo FindMethod(this Type type, string id, bool simple = true) {
            MethodInfo[] methods = type.GetMethods(
                BindingFlags.Instance | BindingFlags.Static |
                BindingFlags.Public | BindingFlags.NonPublic
            );

            if (simple && !id.Contains(" ", StringComparison.Ordinal)) {
                // First simple pass: With type name (just "Namespace.Type::MethodName")
                foreach (MethodInfo method in methods)
                    if (method.GetID(simple: true) == id)
                        return method;
                // Second simple pass: Without type name (basically name only)
                foreach (MethodInfo method in methods)
                    if (method.GetID(withType: false, simple: true) == id)
                        return method;
            }

            // First pass: With type name (f.e. global searches)
            foreach (MethodInfo method in methods)
                if (method.GetID() == id)
                    return method;
            // Second pass: Without type name (f.e. LinkTo)
            foreach (MethodInfo method in methods)
                if (method.GetID(withType: false) == id)
                    return method;

            return null;
        }
        /// <summary>
        /// Find a method for a given ID recursively (including the passed type's base types).
        /// </summary>
        /// <param name="type">The type to search in.</param>
        /// <param name="id">The method ID.</param>
        /// <param name="simple">Whether to perform a simple search pass as well or not.</param>
        /// <returns>The first matching method or null.</returns>
        public static MethodInfo FindMethodDeep(this Type type, string id, bool simple = true) {
            return type.FindMethod(id, simple) ?? type.BaseType?.FindMethodDeep(id, simple);
        }

        /// <summary>
        /// Find a property for a given name.
        /// </summary>
        /// <param name="type">The type to search in.</param>
        /// <param name="name">The property name.</param>
        /// <returns>The first matching property or null.</returns>
        public static PropertyDefinition FindProperty(this TypeDefinition type, string name) {
            foreach (PropertyDefinition prop in type.Properties)
                if (prop.Name == name)
                    return prop;
            return null;
        }
        /// <summary>
        /// Find a property for a given name recursively (including the passed type's base types).
        /// </summary>
        /// <param name="type">The type to search in.</param>
        /// <param name="name">The property name.</param>
        /// <returns>The first matching property or null.</returns>
        public static PropertyDefinition FindPropertyDeep(this TypeDefinition type, string name) {
            return type.FindProperty(name) ?? type.BaseType?.Resolve()?.FindPropertyDeep(name);
        }

        /// <summary>
        /// Find a field for a given name.
        /// </summary>
        /// <param name="type">The type to search in.</param>
        /// <param name="name">The field name.</param>
        /// <returns>The first matching field or null.</returns>
        public static FieldDefinition FindField(this TypeDefinition type, string name) {
            foreach (FieldDefinition field in type.Fields)
                if (field.Name == name)
                    return field;
            return null;
        }
        /// <summary>
        /// Find a field for a given name recursively (including the passed type's base types).
        /// </summary>
        /// <param name="type">The type to search in.</param>
        /// <param name="name">The field name.</param>
        /// <returns>The first matching field or null.</returns>
        public static FieldDefinition FindFieldDeep(this TypeDefinition type, string name) {
            return type.FindField(name) ?? type.BaseType?.Resolve()?.FindFieldDeep(name);
        }

        /// <summary>
        /// Find an event for a given name.
        /// </summary>
        /// <param name="type">The type to search in.</param>
        /// <param name="name">The event name.</param>
        /// <returns>The first matching event or null.</returns>
        public static EventDefinition FindEvent(this TypeDefinition type, string name) {
            foreach (EventDefinition eventDef in type.Events)
                if (eventDef.Name == name)
                    return eventDef;
            return null;
        }
        /// <summary>
        /// Find an event for a given name recursively (including the passed type's base types).
        /// </summary>
        /// <param name="type">The type to search in.</param>
        /// <param name="name">The event name.</param>
        /// <returns>The first matching event or null.</returns>
        public static EventDefinition FindEventDeep(this TypeDefinition type, string name) {
            return type.FindEvent(name) ?? type.BaseType?.Resolve()?.FindEventDeep(name);
        }

    }
}
