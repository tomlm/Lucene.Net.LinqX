using System;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using Lucene.Net.Analysis;
using Lucene.Net.Documents;
using Lucene.Net.Linq.Util;
using Microsoft.Extensions.Logging;
using Version = Lucene.Net.Util.LuceneVersion;

namespace Lucene.Net.Linq.Mapping
{
    /// <summary>
    /// Thread-safe cache of document mappers keyed by runtime type.
    /// Provides polymorphic hydration, serialization, and modification
    /// detection without exposing generic type parameters to callers.
    /// </summary>
    internal class PolymorphicMapperRegistry
    {
        private static readonly ILogger Log = Logging.CreateLogger(typeof(PolymorphicMapperRegistry));

        private readonly ConcurrentDictionary<Type, MapperEntry> mappers = new ConcurrentDictionary<Type, MapperEntry>();
        private readonly Version version;
        private readonly Analyzer externalAnalyzer;

        public PolymorphicMapperRegistry(Version version, Analyzer externalAnalyzer)
        {
            this.version = version;
            this.externalAnalyzer = externalAnalyzer;
        }

        /// <summary>
        /// Creates an instance of <paramref name="actualType"/> and hydrates it from the document
        /// using a mapper built for that type.
        /// </summary>
        public object CreateAndHydrate(Type actualType, Document doc, IQueryExecutionContext context)
        {
            var entry = GetOrCreateEntry(actualType);
            var item = Activator.CreateInstance(actualType);
            entry.ToObject(doc, context, item);
            return item;
        }

        /// <summary>
        /// Serializes the source object to a Lucene document using a mapper
        /// built for the object's actual runtime type. Calls MapFieldsToDocument
        /// on the subtype mapper to avoid re-entering polymorphic dispatch.
        /// </summary>
        public void MapToDocument(object source, Document target)
        {
            var entry = GetOrCreateEntry(source.GetType());
            entry.MapFieldsToDocument(source, target);
        }

        /// <summary>
        /// Checks whether the item has been modified relative to the stored document,
        /// using a mapper built for the item's actual runtime type.
        /// </summary>
        public bool IsModified(object item, Document document)
        {
            var entry = GetOrCreateEntry(item.GetType());
            return entry.IsModified(item, document);
        }

        private MapperEntry GetOrCreateEntry(Type type)
        {
            return mappers.GetOrAdd(type, t =>
            {
                Log.LogDebug("Creating polymorphic mapper for type {Type}", t);
                return MapperEntry.Create(t, version, externalAnalyzer);
            });
        }

        /// <summary>
        /// Wraps a generic DocumentMapperBase&lt;T&gt; and exposes non-generic
        /// operations via compiled delegates for performance.
        /// </summary>
        private class MapperEntry
        {
            private readonly Action<Document, IQueryExecutionContext, object> toObject;
            private readonly Action<object, Document> mapFieldsToDocument;
            private readonly Func<object, Document, bool> isModified;

            private MapperEntry(
                Action<Document, IQueryExecutionContext, object> toObject,
                Action<object, Document> mapFieldsToDocument,
                Func<object, Document, bool> isModified)
            {
                this.toObject = toObject;
                this.mapFieldsToDocument = mapFieldsToDocument;
                this.isModified = isModified;
            }

            public void ToObject(Document source, IQueryExecutionContext context, object target)
                => toObject(source, context, target);

            public void MapFieldsToDocument(object source, Document target)
                => mapFieldsToDocument(source, target);

            public bool IsModified(object item, Document document)
                => isModified(item, document);

            public static MapperEntry Create(Type type, Version version, Analyzer externalAnalyzer)
            {
                var mapperType = typeof(ReflectionDocumentMapper<>).MakeGenericType(type);
                var mapper = Activator.CreateInstance(mapperType, version, externalAnalyzer);

                return new MapperEntry(
                    CompileToObject(mapperType, type, mapper),
                    CompileMapFieldsToDocument(mapperType, type, mapper),
                    CompileIsModified(mapperType, type, mapper));
            }

            private static Action<Document, IQueryExecutionContext, object> CompileToObject(
                Type mapperType, Type entityType, object mapper)
            {
                // Build: (doc, ctx, target) => mapper.ToObject(doc, ctx, (T)target)
                var docParam = Expression.Parameter(typeof(Document), "doc");
                var ctxParam = Expression.Parameter(typeof(IQueryExecutionContext), "ctx");
                var targetParam = Expression.Parameter(typeof(object), "target");

                var method = mapperType.GetMethod("ToObject", new[] { typeof(Document), typeof(IQueryExecutionContext), entityType });
                var call = Expression.Call(
                    Expression.Constant(mapper),
                    method,
                    docParam,
                    ctxParam,
                    Expression.Convert(targetParam, entityType));

                return Expression.Lambda<Action<Document, IQueryExecutionContext, object>>(
                    call, docParam, ctxParam, targetParam).Compile();
            }

            private static Action<object, Document> CompileMapFieldsToDocument(
                Type mapperType, Type entityType, object mapper)
            {
                // Build: (source, target) => mapper.MapFieldsToDocument((T)source, target)
                var sourceParam = Expression.Parameter(typeof(object), "source");
                var targetParam = Expression.Parameter(typeof(Document), "target");

                var method = mapperType.GetMethod("MapFieldsToDocument",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
                    null, new[] { entityType, typeof(Document) }, null);
                var call = Expression.Call(
                    Expression.Constant(mapper),
                    method,
                    Expression.Convert(sourceParam, entityType),
                    targetParam);

                return Expression.Lambda<Action<object, Document>>(
                    call, sourceParam, targetParam).Compile();
            }

            private static Func<object, Document, bool> CompileIsModified(
                Type mapperType, Type entityType, object mapper)
            {
                // Build: (item, doc) => mapper.IsModifiedCore((T)item, doc)
                // Use IsModifiedCore (not IsModified) to avoid re-entering polymorphic dispatch,
                // same pattern as MapFieldsToDocument vs ToDocument.
                var itemParam = Expression.Parameter(typeof(object), "item");
                var docParam = Expression.Parameter(typeof(Document), "doc");

                var method = mapperType.GetMethod("IsModifiedCore",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
                    null, new[] { entityType, typeof(Document) }, null);
                var call = Expression.Call(
                    Expression.Constant(mapper),
                    method,
                    Expression.Convert(itemParam, entityType),
                    docParam);

                return Expression.Lambda<Func<object, Document, bool>>(
                    call, itemParam, docParam).Compile();
            }
        }
    }
}
