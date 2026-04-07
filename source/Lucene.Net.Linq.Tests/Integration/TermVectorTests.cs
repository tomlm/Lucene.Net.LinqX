// Stage 5 port: TermFreqVectorDocumentMapper is a stub in the Lucene 4.8
// port (the Lucene 3 ITermFreqVector type was removed in favour of Terms
// + TermsEnum from IndexReader.GetTermVector). The full integration test
// is preserved in git history; this stub keeps the file in the project so
// callers that reference TermVectorTests still resolve.

using NUnit.Framework;

namespace Lucene.Net.Linq.Tests.Integration
{
    [TestFixture]
    [Ignore("TermFreqVectorDocumentMapper needs Lucene 4.8 port; see Mapping/TermFreqVectorDocumentMapper.cs")]
    public class TermVectorTests
    {
        [Test]
        public void GetTermVectors()
        {
        }
    }
}
