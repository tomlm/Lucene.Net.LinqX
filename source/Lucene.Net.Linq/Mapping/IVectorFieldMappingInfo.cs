namespace Lucene.Net.Linq.Mapping
{
    /// <summary>
    /// Extends <see cref="IFieldMappingInfo"/> with HNSW vector index
    /// parameters used when building and querying the KNN graph.
    /// </summary>
    public interface IVectorFieldMappingInfo : IFieldMappingInfo
    {
        /// <summary>HNSW M parameter (max edges per node).</summary>
        int HnswM { get; }

        /// <summary>HNSW EfSearch parameter (search quality).</summary>
        int HnswEfSearch { get; }
    }
}
