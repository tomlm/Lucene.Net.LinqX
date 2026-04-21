using System;
using System.Reflection;
using System.Threading.Tasks;
using Lucene.Net.Analysis;
using Lucene.Net.Documents;
using Lucene.Net.Linq.Search;
using Lucene.Net.Search;
using Lucene.Net.Util;
using Microsoft.Extensions.AI;

namespace Lucene.Net.Linq.Mapping
{
    /// <summary>
    /// Wraps an inner <see cref="ReflectionFieldMapper{T}"/> for a string property
    /// and adds a companion <c>BinaryDocValuesField</c> that stores the vector embedding.
    /// The vector field is named <c>{FieldName}_vector</c>.
    /// </summary>
    public class VectorFieldMapper<T> : IFieldMapper<T>
    {
        private readonly ReflectionFieldMapper<T> inner;
        private readonly IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator;
        private readonly int k;
        private readonly int m;
        private readonly int efSearch;

        public VectorFieldMapper(
            ReflectionFieldMapper<T> inner,
            PropertyInfo propertyInfo,
            VectorFieldAttribute vectorAttr,
            IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator)
        {
            this.inner = inner;
            this.embeddingGenerator = embeddingGenerator;
            this.k = vectorAttr.K;
            this.m = vectorAttr.M;
            this.efSearch = vectorAttr.EfSearch;
        }

        /// <summary>
        /// The Lucene field name for the vector BinaryDocValuesField.
        /// </summary>
        public string VectorFieldName => inner.FieldName + "_vector";

        /// <summary>
        /// Default K for KNN queries on this field.
        /// </summary>
        public int K => k;

        /// <summary>
        /// HNSW M parameter.
        /// </summary>
        public int HnswM => m;

        /// <summary>
        /// HNSW EfSearch parameter.
        /// </summary>
        public int HnswEfSearch => efSearch;

        // --- IFieldMappingInfo delegation to inner ---
        public string FieldName => inner.FieldName;
        public string PropertyName => inner.PropertyName;
        public Analyzer Analyzer => inner.Analyzer;
        public IndexMode IndexMode => inner.IndexMode;

        public string ConvertToQueryExpression(object value) => inner.ConvertToQueryExpression(value);
        public string EscapeSpecialCharacters(string str) => inner.EscapeSpecialCharacters(str);
        public Query CreateQuery(string pattern) => inner.CreateQuery(pattern);
        public Query CreateRangeQuery(object lowerBound, object upperBound, RangeType lowerRange, RangeType upperRange)
            => inner.CreateRangeQuery(lowerBound, upperBound, lowerRange, upperRange);
        public SortField CreateSortField(bool reverse) => inner.CreateSortField(reverse);

        public object GetPropertyValue(T source) => inner.GetPropertyValue(source);

        public void CopyFromDocument(Document source, IQueryExecutionContext context, T target)
        {
            // Read the string field as normal; the vector is not mapped to any CLR property.
            inner.CopyFromDocument(source, context, target);
        }

        public void CopyToDocument(T source, Document target)
        {
            // Write the string field as normal.
            inner.CopyToDocument(source, target);

            // Generate embedding and write the vector field.
            var textValue = inner.GetPropertyValue(source) as string;
            if (textValue != null && embeddingGenerator != null)
            {
                var vector = GenerateEmbedding(textValue);
                if (vector != null)
                {
                    target.RemoveFields(VectorFieldName);
                    var bytes = VectorToBytes(vector);
                    target.Add(new BinaryDocValuesField(VectorFieldName, new BytesRef(bytes)));
                    // Also store as StoredField so it can be retrieved from Document.
                    target.Add(new StoredField(VectorFieldName, bytes));
                }
            }
        }

        private float[] GenerateEmbedding(string text)
        {
            try
            {
                var result = Task.Run(() => embeddingGenerator.GenerateAsync(new[] { text })).GetAwaiter().GetResult();
                if (result != null && result.Count > 0)
                {
                    return result[0].Vector.ToArray();
                }
            }
            catch
            {
                // If embedding generation fails, the document is still indexed without the vector.
            }
            return null;
        }

        private static byte[] VectorToBytes(float[] vector)
        {
            var bytes = new byte[vector.Length * sizeof(float)];
            Buffer.BlockCopy(vector, 0, bytes, 0, bytes.Length);
            return bytes;
        }
    }
}
