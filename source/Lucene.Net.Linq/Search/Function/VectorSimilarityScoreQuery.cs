using System;
using System.Numerics;
using Lucene.Net.Index;
using Lucene.Net.Queries;
using Lucene.Net.Search;
using Lucene.Net.Util;

namespace Lucene.Net.Linq.Search.Function
{
    /// <summary>
    /// A <see cref="CustomScoreQuery"/> that ranks documents by cosine
    /// similarity between a query vector and a stored vector field.
    /// The sub-query acts as a filter (e.g. keyword predicates); only
    /// matching documents have their vectors read and scored.
    /// Uses SIMD acceleration via <see cref="Vector{T}"/> when available.
    /// </summary>
    internal class VectorSimilarityScoreQuery : CustomScoreQuery
    {
        private readonly string vectorFieldName;
        private readonly float[] queryVector;

        public VectorSimilarityScoreQuery(Query filterQuery, string vectorFieldName, float[] queryVector)
            : base(filterQuery)
        {
            this.vectorFieldName = vectorFieldName;
            this.queryVector = queryVector;
        }

        protected override CustomScoreProvider GetCustomScoreProvider(AtomicReaderContext context)
        {
            return new VectorSimilarityScoreProvider(context, vectorFieldName, queryVector);
        }

        public override string ToString(string field)
        {
            return $"VectorSimilarityScore({vectorFieldName}, subQuery={SubQuery})";
        }

        private class VectorSimilarityScoreProvider : CustomScoreProvider
        {
            private readonly AtomicReaderContext context;
            private readonly string vectorFieldName;
            private readonly float[] queryVector;
            private readonly float queryNorm;

            public VectorSimilarityScoreProvider(
                AtomicReaderContext context, string vectorFieldName, float[] queryVector)
                : base(context)
            {
                this.context = context;
                this.vectorFieldName = vectorFieldName;
                this.queryVector = queryVector;
                this.queryNorm = Norm(queryVector);
            }

            public override float CustomScore(int doc, float subQueryScore, float valSrcScore)
            {
                var document = context.AtomicReader.Document(doc);
                var storedBytes = document.GetBinaryValue(vectorFieldName);

                if (storedBytes == null || storedBytes.Length == 0)
                    return 0f;

                var docVector = BytesToVector(storedBytes);
                var similarity = CosineSimilarity(queryVector, docVector, queryNorm);

                // Scale from [-1,1] to [0,1] so Lucene sorting works naturally
                return (similarity + 1f) / 2f;
            }

            internal static float CosineSimilarity(float[] a, float[] b, float aNorm)
            {
                if (a.Length != b.Length) return 0f;

                float dot, bNormSq;

                if (Vector.IsHardwareAccelerated && a.Length >= Vector<float>.Count)
                {
                    DotAndNormSimd(a, b, out dot, out bNormSq);
                }
                else
                {
                    DotAndNormScalar(a, b, out dot, out bNormSq);
                }

                var bNorm = (float)Math.Sqrt(bNormSq);
                if (aNorm == 0f || bNorm == 0f) return 0f;
                return dot / (aNorm * bNorm);
            }

            private static void DotAndNormSimd(float[] a, float[] b, out float dot, out float bNormSq)
            {
                var vDot = Vector<float>.Zero;
                var vBNorm = Vector<float>.Zero;
                int simdLength = Vector<float>.Count;
                int i = 0;

                for (; i <= a.Length - simdLength; i += simdLength)
                {
                    var va = new Vector<float>(a, i);
                    var vb = new Vector<float>(b, i);
                    vDot += va * vb;
                    vBNorm += vb * vb;
                }

                // Reduce SIMD accumulators
                dot = 0f;
                bNormSq = 0f;
                for (int j = 0; j < simdLength; j++)
                {
                    dot += vDot[j];
                    bNormSq += vBNorm[j];
                }

                // Handle remaining elements
                for (; i < a.Length; i++)
                {
                    dot += a[i] * b[i];
                    bNormSq += b[i] * b[i];
                }
            }

            private static void DotAndNormScalar(float[] a, float[] b, out float dot, out float bNormSq)
            {
                dot = 0f;
                bNormSq = 0f;
                for (int i = 0; i < a.Length; i++)
                {
                    dot += a[i] * b[i];
                    bNormSq += b[i] * b[i];
                }
            }

            internal static float Norm(float[] v)
            {
                float sum;

                if (Vector.IsHardwareAccelerated && v.Length >= Vector<float>.Count)
                {
                    var vSum = Vector<float>.Zero;
                    int simdLength = Vector<float>.Count;
                    int i = 0;

                    for (; i <= v.Length - simdLength; i += simdLength)
                    {
                        var vv = new Vector<float>(v, i);
                        vSum += vv * vv;
                    }

                    sum = 0f;
                    for (int j = 0; j < simdLength; j++)
                        sum += vSum[j];

                    for (; i < v.Length; i++)
                        sum += v[i] * v[i];
                }
                else
                {
                    sum = 0f;
                    for (int i = 0; i < v.Length; i++)
                        sum += v[i] * v[i];
                }

                return (float)Math.Sqrt(sum);
            }

            private static float[] BytesToVector(BytesRef bytes)
            {
                int floatCount = bytes.Length / sizeof(float);
                var vector = new float[floatCount];
                Buffer.BlockCopy(bytes.Bytes, bytes.Offset, vector, 0, floatCount * sizeof(float));
                return vector;
            }
        }
    }
}
