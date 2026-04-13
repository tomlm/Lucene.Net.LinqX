using System;
using Lucene.Net.Index;
using Lucene.Net.Search;

namespace Lucene.Net.Linq
{
    /// <summary>
    /// Holds configuration settings that specify how the library behaves.
    /// </summary>
    public class LuceneDataProviderSettings
    {
        public LuceneDataProviderSettings()
        {
            DeletionPolicy = new KeepOnlyLastCommitDeletionPolicy();
            RAMBufferSizeMB = IndexWriterConfig.DEFAULT_RAM_BUFFER_SIZE_MB;
        }

        /// <summary>
        /// Specifies the <see cref="IndexDeletionPolicy"/> of the <see cref="IndexWriter"/>.
        /// Default: <see cref="KeepOnlyLastCommitDeletionPolicy"/>.
        /// </summary>
        public IndexDeletionPolicy DeletionPolicy { get; set; }

        /// <summary>
        /// Specifies the RAM buffer size of the <see cref="IndexWriter"/> in megabytes.
        /// Default: <see cref="IndexWriterConfig.DEFAULT_RAM_BUFFER_SIZE_MB"/>.
        /// </summary>
        public double RAMBufferSizeMB { get; set; }

        /// <summary>
        /// A factory that produces a <see cref="MergePolicy"/> for use with
        /// <see cref="IndexWriter"/>. Default: <c>null</c>, which causes
        /// <see cref="IndexWriter"/> to use its default policy.
        /// </summary>
        /// <remarks>
        /// The Lucene 3.x version of this delegate took the in-progress
        /// <c>IndexWriter</c> as an argument so callers could inspect its
        /// state. Lucene 4.8 requires the merge policy to be set on the
        /// <see cref="IndexWriterConfig"/> before the writer is constructed,
        /// so the writer is no longer available to the factory.
        /// </remarks>
        public Func<MergePolicy> MergePolicyBuilder { get; set; }
    }
}
