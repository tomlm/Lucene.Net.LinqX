#if NET10_0
using ElBruno.LocalEmbeddings;
using ElBruno.LocalEmbeddings.Options;
#endif
using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lucene.Net.Linq.Tests
{
    internal static class SearchFixture
    {
        internal static readonly IEmbeddingGenerator<string, Embedding<float>> Generator =
#if NET10_0
        new LocalEmbeddingGenerator(new LocalEmbeddingsOptions
        {
            ModelName = "SmartComponents/bge-micro-v2",
            PreferQuantized = true
        });
#else
        null;
#endif
    }
}
