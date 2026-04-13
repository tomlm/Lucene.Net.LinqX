using System;
using System.Collections.Generic;
using Lucene.Net.Linq.Util;
using Microsoft.Extensions.Logging;

namespace Lucene.Net.Linq.Mapping
{
    internal static class TypeHierarchyHelper
    {
        internal const string TypeFieldName = "_type_";
        internal const string TypeHierarchyFieldName = "_types_";

        private static readonly ILogger Log = Logging.CreateLogger(typeof(TypeHierarchyHelper));

        /// <summary>
        /// Walks the BaseType chain from the given type up to (but not including) object.
        /// </summary>
        internal static IEnumerable<Type> GetTypeHierarchy(Type type)
        {
            for (var current = type; current != null && current != typeof(object); current = current.BaseType)
            {
                yield return current;
            }
        }

        /// <summary>
        /// Returns a version-resilient type identifier for the _type_ field.
        /// Uses "FullName, AssemblyName" (without version/culture/token)
        /// so that assembly version bumps don't break type resolution.
        /// </summary>
        internal static string GetTypeIdentifier(Type type)
        {
            return type.FullName + ", " + type.Assembly.GetName().Name;
        }

        /// <summary>
        /// Returns the full name used for _types field values (for filtering).
        /// </summary>
        internal static string GetTypeFilterValue(Type type)
        {
            return type.FullName;
        }

        /// <summary>
        /// Reads the _type_ field from a document and resolves it to a Type.
        /// Returns null if the field is absent or the type cannot be resolved.
        /// </summary>
        internal static Type ReadActualType(Lucene.Net.Documents.Document source)
        {
            var typeString = source.Get(TypeFieldName);
            if (typeString == null) return null;
            return ResolveType(typeString);
        }

        /// <summary>
        /// Resolves an assembly-qualified type name back to a Type. Returns null on failure.
        /// </summary>
        internal static Type ResolveType(string assemblyQualifiedName)
        {
            if (string.IsNullOrEmpty(assemblyQualifiedName))
                return null;

            try
            {
                return Type.GetType(assemblyQualifiedName, throwOnError: false);
            }
            catch (Exception ex)
            {
                Log.LogWarning(ex, "Failed to resolve type '{TypeName}'", assemblyQualifiedName);
                return null;
            }
        }
    }
}
