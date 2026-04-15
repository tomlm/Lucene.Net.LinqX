using System.Linq;
using Lucene.Net.Analysis;
using Lucene.Net.Linq.Analysis;
using Lucene.Net.Linq.Mapping;
using NUnit.Framework;

namespace Lucene.Net.Linq.Tests.Integration
{
    [TestFixture]
    public class PolymorphicTypeTests : IntegrationTestBase
    {
        protected override Analyzer GetAnalyzer(Net.Util.LuceneVersion version)
        {
            return new CaseInsensitiveKeywordAnalyzer();
        }

        // Type hierarchy: Animal (base) -> Dog -> GuideDog
        public class Animal
        {
            [Field(Key = true)]
            public string Id { get; set; }

            [Field]
            public string Name { get; set; }
        }

        public class Dog : Animal
        {
            [Field]
            public string Breed { get; set; }
        }

        public class GuideDog : Dog
        {
            [Field]
            public string Handler { get; set; }
        }

        public class Cat : Animal
        {
            [Field]
            public bool Indoor { get; set; }
        }

        [Test]
        public void QueryBaseType_ReturnsAllSubtypes()
        {
            using (var session = provider.OpenSession<Animal>())
            {
                session.Add(new Dog { Id = "1", Name = "Rex", Breed = "Shepherd" });
                session.Add(new Cat { Id = "2", Name = "Whiskers", Indoor = true });
                session.Add(new GuideDog { Id = "3", Name = "Buddy", Breed = "Labrador", Handler = "John" });
                session.Commit();
            }

            var results = provider.AsQueryable<Animal>().ToList();

            Assert.That(results.Count, Is.EqualTo(3));
        }

        [Test]
        public void QueryBaseType_InstantiatesActualTypes()
        {
            using (var session = provider.OpenSession<Animal>())
            {
                session.Add(new Dog { Id = "1", Name = "Rex", Breed = "Shepherd" });
                session.Add(new Cat { Id = "2", Name = "Whiskers", Indoor = true });
                session.Commit();
            }

            var results = provider.AsQueryable<Animal>().OrderBy(a => a.Id).ToList();

            Assert.That(results[0], Is.TypeOf<Dog>());
            Assert.That(results[1], Is.TypeOf<Cat>());
        }

        [Test]
        public void QueryBaseType_HydratesSubtypeProperties()
        {
            using (var session = provider.OpenSession<Animal>())
            {
                session.Add(new Dog { Id = "1", Name = "Rex", Breed = "Shepherd" });
                session.Commit();
            }

            var results = provider.AsQueryable<Animal>().ToList();
            var dog = results.Single() as Dog;

            Assert.That(dog, Is.Not.Null);
            Assert.That(dog.Name, Is.EqualTo("Rex"));
            Assert.That(dog.Breed, Is.EqualTo("Shepherd"));
        }

        [Test]
        public void QueryMiddleType_ReturnsOnlyMatchingSubtypes()
        {
            using (var session = provider.OpenSession<Animal>())
            {
                session.Add(new Dog { Id = "1", Name = "Rex", Breed = "Shepherd" });
                session.Add(new Cat { Id = "2", Name = "Whiskers", Indoor = true });
                session.Add(new GuideDog { Id = "3", Name = "Buddy", Breed = "Labrador", Handler = "John" });
                session.Commit();
            }

            // Querying for Dog should return Dog and GuideDog, but not Cat
            var results = provider.AsQueryable<Dog>().OrderBy(d => d.Id).ToList();

            Assert.That(results.Count, Is.EqualTo(2));
            Assert.That(results[0], Is.TypeOf<Dog>());
            Assert.That(results[0].Name, Is.EqualTo("Rex"));
            Assert.That(results[1], Is.TypeOf<GuideDog>());
            Assert.That(results[1].Name, Is.EqualTo("Buddy"));
        }

        [Test]
        public void QueryLeafType_ReturnsOnlyExactType()
        {
            using (var session = provider.OpenSession<Animal>())
            {
                session.Add(new Dog { Id = "1", Name = "Rex", Breed = "Shepherd" });
                session.Add(new GuideDog { Id = "2", Name = "Buddy", Breed = "Labrador", Handler = "John" });
                session.Commit();
            }

            var results = provider.AsQueryable<GuideDog>().ToList();

            Assert.That(results.Count, Is.EqualTo(1));
            Assert.That(results[0].Handler, Is.EqualTo("John"));
        }

        [Test]
        public void QueryMiddleType_HydratesGrandchildProperties()
        {
            using (var session = provider.OpenSession<Animal>())
            {
                session.Add(new GuideDog { Id = "1", Name = "Buddy", Breed = "Labrador", Handler = "John" });
                session.Commit();
            }

            var results = provider.AsQueryable<Dog>().ToList();
            var guideDog = results.Single() as GuideDog;

            Assert.That(guideDog, Is.Not.Null);
            Assert.That(guideDog.Name, Is.EqualTo("Buddy"));
            Assert.That(guideDog.Breed, Is.EqualTo("Labrador"));
            Assert.That(guideDog.Handler, Is.EqualTo("John"));
        }

        [Test]
        public void SessionWithSubtype_CanAddAndQueryBack()
        {
            using (var session = provider.OpenSession<Dog>())
            {
                session.Add(new Dog { Id = "1", Name = "Rex", Breed = "Shepherd" });
                session.Add(new GuideDog { Id = "2", Name = "Buddy", Breed = "Labrador", Handler = "John" });
                session.Commit();
            }

            var results = provider.AsQueryable<Dog>().OrderBy(d => d.Id).ToList();

            Assert.That(results.Count, Is.EqualTo(2));
            Assert.That(results[0], Is.TypeOf<Dog>());
            Assert.That(results[1], Is.TypeOf<GuideDog>());
            Assert.That(((GuideDog)results[1]).Handler, Is.EqualTo("John"));
        }

        [Test]
        public void Session_DirtyTracking_DetectsSubtypeFieldChanges()
        {
            using (var session = provider.OpenSession<Animal>())
            {
                session.Add(new Dog { Id = "1", Name = "Rex", Breed = "Shepherd" });
                session.Commit();
            }

            // Modify a subtype-specific field via a base-type session
            using (var session = provider.OpenSession<Animal>())
            {
                var animal = session.Query().Single();
                var dog = (Dog)animal;
                dog.Breed = "Labrador";
            }

            // Verify the change was flushed
            var result = provider.AsQueryable<Animal>().Single() as Dog;
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Breed, Is.EqualTo("Labrador"));
        }

        [Test]
        public void Session_DirtyTracking_DetectsBaseFieldChangeOnSubtype()
        {
            using (var session = provider.OpenSession<Animal>())
            {
                session.Add(new Dog { Id = "1", Name = "Rex", Breed = "Shepherd" });
                session.Commit();
            }

            using (var session = provider.OpenSession<Animal>())
            {
                var dog = (Dog)session.Query().Single();
                dog.Name = "Max";
            }

            var result = provider.AsQueryable<Animal>().Single() as Dog;
            Assert.That(result.Name, Is.EqualTo("Max"));
            Assert.That(result.Breed, Is.EqualTo("Shepherd"));
        }

        [Test]
        public void Session_TrackedSubtype_ReturnsSameInstance()
        {
            using (var session = provider.OpenSession<Animal>())
            {
                session.Add(new Dog { Id = "1", Name = "Rex", Breed = "Shepherd" });
                session.Commit();
            }

            using (var session = provider.OpenSession<Animal>())
            {
                var first = session.Query().Single();
                var second = session.Query().Single();

                Assert.That(second, Is.SameAs(first), "Session should return same tracked instance");
                Assert.That(first, Is.TypeOf<Dog>());
            }
        }

        [Test]
        public void Session_DeleteSubtype_RemovesFromIndex()
        {
            using (var session = provider.OpenSession<Animal>())
            {
                session.Add(new Dog { Id = "1", Name = "Rex", Breed = "Shepherd" });
                session.Add(new Cat { Id = "2", Name = "Whiskers", Indoor = true });
                session.Commit();
            }

            using (var session = provider.OpenSession<Animal>())
            {
                var dog = session.Query().OrderBy(a => a.Id).First();
                session.Delete(dog);
                session.Commit();
            }

            var results = provider.AsQueryable<Animal>().ToList();
            Assert.That(results.Count, Is.EqualTo(1));
            Assert.That(results[0], Is.TypeOf<Cat>());
        }

        [Test]
        public void Session_AddMixedSubtypes_CommitsAll()
        {
            using (var session = provider.OpenSession<Animal>())
            {
                session.Add(new Dog { Id = "1", Name = "Rex", Breed = "Shepherd" });
                session.Add(new Cat { Id = "2", Name = "Whiskers", Indoor = true });
                session.Add(new GuideDog { Id = "3", Name = "Buddy", Breed = "Labrador", Handler = "John" });
                session.Commit();

                // Query within same session after commit + reload
                var results = session.Query().OrderBy(a => a.Id).ToList();
                Assert.That(results.Count, Is.EqualTo(3));
                Assert.That(results[0], Is.TypeOf<Dog>());
                Assert.That(results[1], Is.TypeOf<Cat>());
                Assert.That(results[2], Is.TypeOf<GuideDog>());
            }
        }

        [Test]
        public void Session_QueryOmitsDeletedSubtype()
        {
            using (var session = provider.OpenSession<Animal>())
            {
                session.Add(new Dog { Id = "1", Name = "Rex", Breed = "Shepherd" });
                session.Add(new Cat { Id = "2", Name = "Whiskers", Indoor = true });
                session.Commit();
            }

            using (var session = provider.OpenSession<Animal>())
            {
                var dog = session.Query().OrderBy(a => a.Id).First();
                session.Delete(dog);

                // Within the same session, deleted item should be omitted
                var results = session.Query().ToList();
                Assert.That(results.Count, Is.EqualTo(1));
                Assert.That(results[0], Is.TypeOf<Cat>());
            }
        }
        [Test]
        public void Session_SameKey_DifferentSubtypes_LastWriteWins()
        {
            // Same key across different subtypes = same entity. Last write wins.
            using (var session = provider.OpenSession<Animal>())
            {
                session.Add(new Dog { Id = "1", Name = "Rex", Breed = "Shepherd" });
                session.Add(new Cat { Id = "1", Name = "Whiskers", Indoor = true });
                session.Commit();
            }

            var results = provider.AsQueryable<Animal>().ToList();
            Assert.That(results.Count, Is.EqualTo(1));
            // The Dog was added first, so the Cat (same key) replaces it
            Assert.That(results[0], Is.TypeOf<Cat>());
        }

        [Test]
        public void ScalarCount_RespectsTypeHierarchy()
        {
            using (var session = provider.OpenSession<Animal>())
            {
                session.Add(new Dog { Id = "1", Name = "Rex", Breed = "Shepherd" });
                session.Add(new Cat { Id = "2", Name = "Whiskers", Indoor = true });
                session.Add(new GuideDog { Id = "3", Name = "Buddy", Breed = "Labrador", Handler = "John" });
                session.Commit();
            }

            Assert.That(provider.AsQueryable<Animal>().Count(), Is.EqualTo(3));
            Assert.That(provider.AsQueryable<Dog>().Count(), Is.EqualTo(2));
            Assert.That(provider.AsQueryable<GuideDog>().Count(), Is.EqualTo(1));
            Assert.That(provider.AsQueryable<Cat>().Count(), Is.EqualTo(1));
        }

        [Test]
        public void ScalarAny_RespectsTypeHierarchy()
        {
            using (var session = provider.OpenSession<Animal>())
            {
                session.Add(new Dog { Id = "1", Name = "Rex", Breed = "Shepherd" });
                session.Commit();
            }

            Assert.That(provider.AsQueryable<Animal>().Any(), Is.True);
            Assert.That(provider.AsQueryable<Dog>().Any(), Is.True);
            Assert.That(provider.AsQueryable<Cat>().Any(), Is.False);
        }
    }
}
