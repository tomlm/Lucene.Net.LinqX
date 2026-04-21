using System;

namespace Lucene.Net.Linq.Mapping
{
    /// <summary>
    /// Marks a string property for automatic vector embedding.
    /// At index time the property value is embedded via the configured
    /// <c>IEmbeddingGenerator</c> and stored as a companion
    /// <c>BinaryDocValuesField</c> named <c>{FieldName}_vector</c>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class VectorFieldAttribute : Attribute
    {
        /// <summary>
        /// Number of nearest neighbors to return in KNN queries.
        /// Default: 10.
        /// </summary>
        public int K { get; set; } = 10;

        /// <summary>
        /// HNSW graph parameter: max edges per node. Default: 16.
        /// </summary>
        public int M { get; set; } = 16;

        /// <summary>
        /// HNSW graph parameter: search quality. Default: 50.
        /// </summary>
        public int EfSearch { get; set; } = 50;
    }
}
