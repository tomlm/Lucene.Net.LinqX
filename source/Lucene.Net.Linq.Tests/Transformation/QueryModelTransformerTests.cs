using System.Linq.Expressions;
using Lucene.Net.Linq.Transformation;
using NSubstitute;
using NUnit.Framework;
using Remotion.Linq;
using Remotion.Linq.Clauses;
using Remotion.Linq.Parsing;

namespace Lucene.Net.Linq.Tests.Transformation
{
    [TestFixture]
    public class QueryModelTransformerTests
    {
        private static readonly ConstantExpression constantExpression = Expression.Constant(true);
        private static readonly WhereClause whereClause = new WhereClause(constantExpression);
        private ExpressionTreeVisitor visitor1;
        private ExpressionTreeVisitor visitor2;
        private QueryModelTransformer transformer;
        private readonly QueryModel queryModel = new QueryModel(new MainFromClause("i", typeof(Record), Expression.Constant("r")), new SelectClause(Expression.Constant("a")));

        [SetUp]
        public void SetUp()
        {
            visitor1 = Substitute.For<ExpressionTreeVisitor>();
            visitor2 = Substitute.For<ExpressionTreeVisitor>();
            var visitors = new[] { visitor1, visitor2 };
            transformer = new QueryModelTransformer(visitors, visitors);

            visitor1.VisitExpression(whereClause.Predicate).Returns(whereClause.Predicate);
            visitor2.VisitExpression(whereClause.Predicate).Returns(whereClause.Predicate);
        }

        [Test]
        public void VisitsWhereClause()
        {
            transformer.VisitWhereClause(whereClause, queryModel, 0);

            Received.InOrder(() =>
            {
                visitor1.VisitExpression(whereClause.Predicate);
                visitor2.VisitExpression(whereClause.Predicate);
            });
        }

        [Test]
        public void VisitsOrderByClause()
        {
            var orderByClause = new OrderByClause();
            orderByClause.Orderings.Add(new Ordering(constantExpression, OrderingDirection.Asc));

            transformer.VisitOrderByClause(orderByClause, queryModel, 0);

            visitor1.Received().VisitExpression(constantExpression);
            visitor2.Received().VisitExpression(constantExpression);
        }
    }
}
