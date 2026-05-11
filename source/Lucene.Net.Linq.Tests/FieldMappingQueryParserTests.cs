using System;
using System.Collections.Generic;
using Lucene.Net.Analysis.Core;
using Lucene.Net.Linq.Analysis;
using Lucene.Net.Linq.Mapping;
using Lucene.Net.QueryParsers.Classic;
using NSubstitute;
using NUnit.Framework;
using Version = Lucene.Net.Util.LuceneVersion;

namespace Lucene.Net.Linq.Tests.Mapping
{
    [TestFixture]
    public class NonGenericFieldMappingQueryParserTests
    {
        private IFieldMappingInfoProvider mappingInfo;
        private FieldMappingQueryParser parser;

        [SetUp]
        public void SetUp()
        {
            mappingInfo = Substitute.For<IFieldMappingInfoProvider>();
            var analyzer = new PerFieldAnalyzer(new KeywordAnalyzer());
            parser = new FieldMappingQueryParser(Version.LUCENE_48, "defaultField", analyzer, mappingInfo);
        }

        [Test]
        public void Constructor_SetsProperties()
        {
            Assert.That(parser.MatchVersion, Is.EqualTo(Version.LUCENE_48));
            Assert.That(parser.DefaultSearchProperty, Is.EqualTo("defaultField"));
            Assert.That(parser.MappingInfo, Is.SameAs(mappingInfo));
        }

        [Test]
        public void Field_ReturnsDefaultSearchProperty()
        {
            Assert.That(parser.Field, Is.EqualTo("defaultField"));
        }

        [Test]
        public void DefaultSearchProperty_CanBeChanged()
        {
            parser.DefaultSearchProperty = "otherField";
            Assert.That(parser.Field, Is.EqualTo("otherField"));
        }

        [Test]
        public void Parse_FieldQuery_DelegatesToMappingInfo()
        {
            var fieldMapping = new FakeFieldMappingInfo
            {
                FieldName = "Name",
                PropertyName = "Name",
                IsNumericField = false
            };
            mappingInfo.GetMappingInfo("Name").Returns(fieldMapping);

            var query = parser.Parse("Name:test");

            Assert.That(query, Is.Not.Null);
            mappingInfo.Received(1).GetMappingInfo("Name");
        }

        [Test]
        public void Parse_DefaultField_DelegatesToMappingInfo()
        {
            var fieldMapping = new FakeFieldMappingInfo
            {
                FieldName = "defaultField",
                PropertyName = "defaultField",
                IsNumericField = false
            };
            mappingInfo.GetMappingInfo("defaultField").Returns(fieldMapping);

            var query = parser.Parse("test");

            Assert.That(query, Is.Not.Null);
            mappingInfo.Received(1).GetMappingInfo("defaultField");
        }

        [Test]
        public void Parse_UnrecognizedField_ThrowsParseException()
        {
            mappingInfo.When(x => x.GetMappingInfo("Unknown")).Do(_ => throw new KeyNotFoundException());

            Assert.That(() => parser.Parse("Unknown:test"), Throws.TypeOf<ParseException>()
                .With.Message.Contains("Unrecognized field"));
        }

        [Test]
        public void Parse_RangeQuery_DelegatesToMappingInfo()
        {
            var fieldMapping = new FakeFieldMappingInfo
            {
                FieldName = "Age",
                PropertyName = "Age",
                IsNumericField = true
            };
            mappingInfo.GetMappingInfo("Age").Returns(fieldMapping);

            var query = parser.Parse("Age:[10 TO 20]");

            Assert.That(query, Is.Not.Null);
            mappingInfo.Received().GetMappingInfo("Age");
        }
    }

    [TestFixture]
    public class GenericFieldMappingQueryParserTests
    {
        private IDocumentMapper<Record> mapper;

        [SetUp]
        public void SetUp()
        {
            mapper = Substitute.For<IDocumentMapper<Record>>();
            mapper.Analyzer.Returns(new PerFieldAnalyzer(new KeywordAnalyzer()));
        }

        [Test]
        public void Constructor_WithMapper_SetsDocumentMapper()
        {
            var parser = new FieldMappingQueryParser<Record>(Version.LUCENE_48, "Name", mapper);

            Assert.That(parser.DocumentMapper, Is.SameAs(mapper));
        }

        [Test]
        public void Constructor_WithDefaultField_InheritsFromNonGeneric()
        {
            var parser = new FieldMappingQueryParser<Record>(Version.LUCENE_48, "Name", mapper);

            // FieldMappingQueryParser<T> inherits from FieldMappingQueryParser
            Assert.That(parser, Is.InstanceOf<FieldMappingQueryParser>());
            Assert.That(parser.DefaultSearchProperty, Is.EqualTo("Name"));
        }

        [Test]
        public void Constructor_WithoutDefaultField_UsesGenericDefault()
        {
            var parser = new FieldMappingQueryParser<Record>(Version.LUCENE_48, mapper);

            // When no default field specified, uses the type-specific sentinel
            Assert.That(parser, Is.InstanceOf<FieldMappingQueryParser>());
            Assert.That(parser.MappingInfo, Is.SameAs(mapper));
        }

        [Test]
        public void Parse_DelegatesToMapperGetMappingInfo()
        {
            var fieldMapping = new FakeFieldMappingInfo
            {
                FieldName = "Name",
                PropertyName = "Name",
                IsNumericField = false
            };
            mapper.GetMappingInfo("Name").Returns(fieldMapping);

            var parser = new FieldMappingQueryParser<Record>(Version.LUCENE_48, "Name", mapper);
            var query = parser.Parse("Name:hello");

            Assert.That(query, Is.Not.Null);
            mapper.Received(1).GetMappingInfo("Name");
        }
    }
}
