using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Core;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Linq.Analysis;
using Lucene.Net.Linq.Util;
using Lucene.Net.QueryParsers.Classic;
using Lucene.Net.Search;
using Microsoft.Extensions.Logging;
using Version = Lucene.Net.Util.LuceneVersion;

namespace Lucene.Net.Linq.Mapping
{
    public abstract class DocumentMapperBase<T> : IDocumentMapper<T>, IDocumentKeyConverter, IDocumentModificationDetector<T>
    {
        private static readonly ILogger Log = Logging.CreateLogger(typeof(DocumentMapperBase<>));

        protected readonly Analyzer externalAnalyzer;
        protected PerFieldAnalyzer analyzer;
        protected readonly Version version;
        protected readonly IDictionary<string, IFieldMapper<T>> fieldMap = new Dictionary<string, IFieldMapper<T>>(StringComparer.Ordinal);
        protected readonly List<IFieldMapper<T>> keyFields = new List<IFieldMapper<T>>();

        /// <summary>
        /// Registry for creating and hydrating polymorphic subtypes.
        /// When set, <see cref="ToDocument"/> writes _type_/_types_ fields
        /// and subtypes are instantiated via the registry.
        /// </summary>
        internal PolymorphicMapperRegistry PolymorphicMapperRegistry { get; set; }

        /// <summary>
        /// Constructs an instance that will create an <see cref="Analyzer"/>
        /// using metadata on public properties on the type <typeparamref name="T"/>.
        /// </summary>
        /// <param name="version">Version compatibility for analyzers and indexers.</param>
        protected DocumentMapperBase(Version version)
            : this(version, null)
        {
        }

        /// <summary>
        /// Constructs an instance with an externall supplied analyzer
        /// and the compatibility version of the index.
        /// </summary>
        /// <param name="version">Version compatibility for analyzers and indexers.</param>
        /// <param name="externalAnalyzer"></param>
        protected DocumentMapperBase(Version version, Analyzer externalAnalyzer)
        {
            this.version = version;
            this.externalAnalyzer = externalAnalyzer;
            this.analyzer = new PerFieldAnalyzer(new KeywordAnalyzer());
        }

        public Type MappedType => typeof(T);

        public virtual PerFieldAnalyzer Analyzer
        {
            get { return analyzer; }
        }

        public virtual IEnumerable<string> AllProperties
        {
            get { return fieldMap.Values.Select(m => m.PropertyName); }
        }

        public IEnumerable<string> IndexedProperties
        {
            get { return fieldMap.Values.Where(m => m.IndexMode != IndexMode.NotIndexed).Select(m => m.PropertyName); }
        }

        public virtual IEnumerable<string> KeyProperties
        {
            get { return keyFields.Select(k => k.PropertyName); }
        }

        protected virtual bool EnableScoreTracking
        {
            get { return fieldMap.Values.Any(m => m is ReflectionScoreMapper<T>); }
        }

        public virtual IFieldMappingInfo GetMappingInfo(string propertyName)
        {
            return fieldMap[propertyName];
        }

        public virtual void ToObject(Document source, IQueryExecutionContext context, T target)
        {
            foreach (var mapping in fieldMap)
            {
                mapping.Value.CopyFromDocument(source, context, target);
            }
        }

        public virtual void ToDocument(T source, Document target)
        {
            var actualType = source.GetType();

            // If the source is a subtype and we have a registry, delegate to the
            // subtype's mapper so that subtype-specific properties are captured.
            if (PolymorphicMapperRegistry != null && actualType != typeof(T) && typeof(T).IsAssignableFrom(actualType))
            {
                PolymorphicMapperRegistry.MapToDocument(source, target);
                return;
            }

            MapFieldsToDocument(source, target);
        }

        /// <summary>
        /// Copies all mapped fields from source to target, then writes
        /// the polymorphic _type_/_types_ internal fields.
        /// Called directly by <see cref="PolymorphicMapperRegistry"/> to
        /// avoid re-entering polymorphic dispatch.
        /// </summary>
        internal void MapFieldsToDocument(T source, Document target)
        {
            foreach (var mapping in fieldMap)
            {
                mapping.Value.CopyToDocument(source, target);
            }

            var actualType = source.GetType();
            target.Add(new StringField(TypeHierarchyHelper.TypeFieldName,
                TypeHierarchyHelper.GetTypeIdentifier(actualType), Field.Store.YES));

            foreach (var type in TypeHierarchyHelper.GetTypeHierarchy(actualType))
            {
                target.Add(new StringField(TypeHierarchyHelper.TypeHierarchyFieldName,
                    TypeHierarchyHelper.GetTypeFilterValue(type), Field.Store.YES));
            }
        }

        public virtual IDocumentKey ToKey(T source)
        {
            var keyValues = keyFields.ToDictionary(f => (IFieldMappingInfo)f, f => f.GetPropertyValue(source));

            ValidateKey(keyValues);

            return new DocumentKey(keyValues);
        }

        public virtual IDocumentKey ToKey(Document document)
        {
            var keyValues = keyFields.ToDictionary(f => (IFieldMappingInfo)f, f => GetFieldValue(f, document));

            ValidateKey(keyValues);

            return new DocumentKey(keyValues);
        }

        private object GetFieldValue(IFieldMappingInfo fieldMapper, Document document)
        {
            var fieldConverter = fieldMapper as IDocumentFieldConverter;

            if (fieldConverter == null)
            {
                throw new NotSupportedException(
                    string.Format("The field mapping of type {0} for field {1} must implement {2}.",
                    fieldMapper.GetType(), fieldMapper.FieldName, typeof(IDocumentFieldConverter)));
            }

            return fieldConverter.GetFieldValue(document);
        }

        protected virtual void ValidateKey(Dictionary<IFieldMappingInfo, object> keyValues)
        {
            var nulls = keyValues.Where(kv => kv.Value == null).ToArray();

            if (!nulls.Any()) return;

            var message = string.Format("Cannot create key for document of type '{0}' with null value(s) for properties {1} which are marked as Key=true.",
                                        typeof(T),
                                        string.Join(", ", nulls.Select(n => n.Key.PropertyName)));

            throw new InvalidOperationException(message);
        }

        public virtual void PrepareSearchSettings(IQueryExecutionContext context)
        {
            // In Lucene 4.8 IndexSearcher.SetDefaultFieldSortScoring is gone;
            // score tracking on a Sort happens via the Search overload that
            // takes doDocScores: true. LuceneQueryExecutorBase passes
            // doDocScores when EnableScoreTracking is true (see executor).
        }

        public Query CreateMultiFieldQuery(string pattern)
        {
            // TODO: pattern should be analyzed/converted on per-field basis.
            var parser = new MultiFieldQueryParser(version, fieldMap.Keys.ToArray(), externalAnalyzer ?? (Analyzer)analyzer);
            return parser.Parse(pattern);
        }

        public virtual bool IsModified(T item, Document document)
        {
            // If the item is a subtype, delegate to the subtype's mapper
            // so that subtype-specific fields are included in the comparison.
            var actualType = item.GetType();
            if (PolymorphicMapperRegistry != null && actualType != typeof(T) && typeof(T).IsAssignableFrom(actualType))
            {
                return PolymorphicMapperRegistry.IsModified(item, document);
            }

            return IsModifiedCore(item, document);
        }

        /// <summary>
        /// Compares the item's field values against the stored document
        /// using this mapper's field map. Does not perform polymorphic dispatch.
        /// </summary>
        internal bool IsModifiedCore(T item, Document document)
        {
            foreach (var field in fieldMap.Values)
            {
                if (field is ReflectionScoreMapper<T>)
                {
                    continue;
                }

                var val1 = field.GetPropertyValue(item);
                var val2 = GetFieldValue(field, document);

                if (!ValuesEqual(val1, val2))
                {
                    return true;
                }
            }

            return false;
        }

        public virtual bool Equals(T item1, T item2)
        {
            foreach (var field in fieldMap.Values)
            {
                var val1 = field.GetPropertyValue(item1);
                var val2 = field.GetPropertyValue(item2);

                if (!ValuesEqual(val1, val2))
                {
                    return false;
                }
            }

            return true;
        }

        protected internal virtual bool ValuesEqual(object val1, object val2)
        {
            if (val1 is IEnumerable && val2 is IEnumerable)
            {
                return ((IEnumerable) val1).Cast<object>().SequenceEqual(((IEnumerable) val2).Cast<object>());
            }

            return Equals(val1, val2);
        }

        public void AddField(IFieldMapper<T> fieldMapper)
        {
            fieldMap.Add(fieldMapper.PropertyName, fieldMapper);
            if (!string.IsNullOrWhiteSpace(fieldMapper.FieldName) && fieldMapper.Analyzer != null)
            {
                Analyzer.AddAnalyzer(fieldMapper.FieldName, fieldMapper.Analyzer);
            }
        }

        public void AddKeyField(IFieldMapper<T> fieldMapper)
        {
            AddField(fieldMapper);
            keyFields.Add(fieldMapper);
        }

    }
}
