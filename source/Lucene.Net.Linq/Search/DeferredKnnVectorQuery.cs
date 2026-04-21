using Lucene.Net.Index;
using Lucene.Net.Search;
#if NET10_0
using Iciclecreek.Lucene.Net.Vector;
#endif

namespace Lucene.Net.Linq.Search
{
    /// <summary>
    /// A placeholder <see cref="Query"/> that carries all parameters needed to
    /// construct a <c>KnnVectorQuery</c> but defers actual construction until
    /// an <see cref="IndexReader"/> is available at execution time.
    /// </summary>
    internal class DeferredKnnVectorQuery : Query
    {
        public DeferredKnnVectorQuery(string field, float[] queryVector, int k, int m, int efSearch)
        {
            Field = field;
            QueryVector = queryVector;
            K = k;
            M = m;
            EfSearch = efSearch;
        }

        public string Field { get; }
        public float[] QueryVector { get; }
        public int K { get; }
        public int M { get; }
        public int EfSearch { get; }

        /// <summary>
        /// Resolves this deferred query into a real KNN vector query using the
        /// provided <paramref name="reader"/>.
        /// </summary>
        public Query Resolve(IndexReader reader)
        {
#if NET10_0
            var options = new VectorIndexOptions
            {
                M = M,
                EfSearch = EfSearch,
            };
            return new KnnVectorQuery(Field, QueryVector, K, reader, options);
#else
            throw new System.NotSupportedException(
                "Vector similarity search requires net10.0 or later. " +
                "Reference the Iciclecreek.Lucene.Net.Vector package and target net10.0.");
#endif
        }

        public override string ToString(string field)
        {
            return $"DeferredKnnVectorQuery(field={Field}, k={K})";
        }

        public override bool Equals(object obj)
        {
            if (obj is DeferredKnnVectorQuery other)
            {
                return Field == other.Field && K == other.K;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return Field.GetHashCode() ^ K;
        }
    }
}
