using System;
using System.Linq.Expressions;

namespace Lucene.Net.Linq.Clauses.Expressions
{
    /// <summary>
    /// Represents a vector similarity query in the LINQ expression tree.
    /// Carries all parameters needed to construct a <c>KnnVectorQuery</c>
    /// except the <c>IndexReader</c>, which is only available at execution time.
    /// </summary>
    internal class LuceneVectorQueryExpression : Expression
    {
        internal LuceneVectorQueryExpression(string fieldName, float[] queryVector, int k)
        {
            FieldName = fieldName;
            QueryVector = queryVector;
            K = k;
        }

        public override ExpressionType NodeType => ExpressionType.Extension;
        public override Type Type => typeof(bool);
        public override bool CanReduce => false;

        public string FieldName { get; }
        public float[] QueryVector { get; }
        public int K { get; }

        protected override Expression VisitChildren(ExpressionVisitor visitor)
        {
            return this;
        }
    }
}
