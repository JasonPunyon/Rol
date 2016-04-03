using System;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using StackExchange.Redis;

namespace Rol.Tests
{
    public class RolFixture
    {
        protected ConnectionMultiplexer Connection;
        protected Store Store;

        [TestFixtureSetUp]
        public void TestFixtureSetup()
        {
            Connection = ConnectionMultiplexer.Connect("localhost:6379,allowAdmin=true");
            Store = new Store(Connection);
        }

        [SetUp]
        public void Setup()
        {
            Connection.GetServer(Connection.GetEndPoints()[0]).FlushDatabase();   
        }
    }

    [TestFixture]
    public class Get : RolFixture
    {
        public interface IIntId
        {
            int Id { get; }
        }

        public interface IStringId
        {
            string Id { get; }
        }

        public interface IGuidId
        {
            Guid Id { get; }
        }

        [Test]
        public void InterfaceWithIntKeyCanGetGot()
        {
            var withIntId = Store.Get<IIntId>(3);
            Assert.AreEqual(3, withIntId.Id);
        }

        [Test]
        public void InterfaceWithIntKeyCanBeCreated()
        {
            var withIntId = Store.Create<IIntId>(3);
            Assert.AreEqual(3, withIntId.Id);
        }

        [Test]
        public async Task InterfaceWithIntKeyCanGetGotAsync()
        {
            var withIntId = await Store.GetAsync<IIntId>(3);
            Assert.AreEqual(3, withIntId.Id);
        }

        [Test]
        public void InterfaceWithStringKeyCanGetGot()
        {
            var withStringId = Store.Get<IStringId>("Hello");
            Assert.AreEqual("Hello", withStringId.Id);
        }

        [Test]
        public void InterfaceWithGuidKeyCanGetGot()
        {
            var id = Guid.NewGuid();
            var withGuidId = Store.Get<IGuidId>(id);
            Assert.AreEqual(id, withGuidId.Id);
        }

        [Test]
        public void CreatedIntegerIdsIncrease()
        {
            var first = Store.Create<IIntId>();
            var second = Store.Create<IIntId>();
            var third = Store.Create<IIntId>();            

            Assert.AreEqual(1, first.Id);
            Assert.AreEqual(2, second.Id);
            Assert.AreEqual(3, third.Id);

            Assert.IsTrue(Store.Enumerate<IIntId>().ToList().Select(o => o.Id).SequenceEqual(new[] { 1, 2, 3 }));
            Assert.AreEqual(3, Store.Enumerate<IIntId>().Count());
        }
    }

    [TestFixture]
    public class Properties : RolFixture
    {
        public interface IWithProperties
        {
            int Id { get; }
            bool BoolProp { get; set; }
            bool? NullableBoolProp { get; set; }
            int IntProp { get; set; }
            int? NullableIntProp { get; set; }
            string StringProp { get; set; }
            double DoubleProp { get; set; }
            double? NullableDoubleProp { get; set; }
            long LongProp { get; set; }
            long? NullableLongProp { get; set; }
            DateTime DateTimeProp { get; set; }
            IReferenceWithProperties ReferenceProp { get; set; }
            IRedisSet<int> IntegerSet { get; set; }
            IRedisList<int> IntegerList { get; set; } 
            IRedisHash<string, string> StringToStringHash { get; set; } 
            IRedisSortedSet<string> StringLengthSortedSet { get; set; } 
            IRedisSet<IReferenceWithProperties> ReferencesWithProperties { get; set; }
            IRedisSortedSet<IReferenceWithProperties> ReferenceSortedByNameLength { get; } 
            IRedisHyperLogLog<Guid> HyperLogLogOfGuids { get; set; }
            Async<int> IntPropAsync { get; set; }
            AnEnum AnEnumProperty { get; set; }
        }

        public enum AnEnum
        {
            ADefaultValue = 0,
            AnotherValue = 1
        }

        public interface IReferenceWithProperties
        {
            int Id { get; }
            string Name { get; set; }
        }

        [Test]
        public void SetAndGetBoolProperty()
        {
            var withProps = Store.Get<IWithProperties>(1);
            var val = new Random().NextDouble() > 0.5;

            withProps.BoolProp = val;
            Assert.AreEqual(val, withProps.BoolProp);
        }

        [Test]
        public void SetAndGetNullableBoolProperty()
        {
            var withProps = Store.Get<IWithProperties>(1);
            var val = new Random().NextDouble() > 0.5;

            withProps.NullableBoolProp = val;
            Assert.AreEqual(val, withProps.NullableBoolProp);

            withProps.NullableBoolProp = null;
            Assert.AreEqual(null, withProps.NullableBoolProp);
        }

        [Test]
        public void SetAndGetIntegerProperty()
        {
            var withProps = Store.Get<IWithProperties>(1);
            var val = new Random().Next();

            withProps.IntProp = val;
            Assert.AreEqual(val, withProps.IntProp);
        }

        [Test]
        public void SetAndGetNullableIntegerProperty()
        {
            var withProps = Store.Get<IWithProperties>(1);
            var val = new Random().Next();

            withProps.NullableIntProp = val;
            Assert.AreEqual(val, withProps.NullableIntProp);

            withProps.NullableIntProp = null;
            Assert.AreEqual(null, withProps.NullableIntProp);
        }

        [Test]
        public void SetAndGetDateTimeProperty()
        {
            var withProps = Store.Get<IWithProperties>(1);
            var val = DateTime.UtcNow.AddDays(new Random().Next(1, 100));

            withProps.DateTimeProp = val;
            Assert.AreEqual(val, withProps.DateTimeProp);
        }

        [Test]
        public void SetAndGetStringProperty()
        {
            var withProps = Store.Get<IWithProperties>(1);
            var val = "This is the greatest! " + new Random().Next();
            withProps.StringProp = val;
            Assert.AreEqual(val, withProps.StringProp);

            withProps.StringProp = null;
            Assert.AreEqual(null, withProps.StringProp);
        }

        [Test]
        public void SetAndGetLongProperty()
        {
            var withProps = Store.Get<IWithProperties>(1);
            var val = new Random().Next();
            withProps.LongProp = val;
            Assert.AreEqual(val, withProps.LongProp);
        }

        [Test]
        public void SetAndGetNullableLongProperty()
        {
            var withProps = Store.Get<IWithProperties>(1);
            var val = new Random().Next();
            withProps.NullableLongProp = val;
            Assert.AreEqual(val, withProps.NullableLongProp);

            withProps.NullableLongProp = null;
            Assert.AreEqual(null, withProps.NullableLongProp);
        }

        [Test]
        public void SetAndGetDoubleProperty()
        {
            var withProps = Store.Get<IWithProperties>(1);
            var val = new Random().NextDouble();
            withProps.DoubleProp = val;
            Assert.AreEqual(val, withProps.DoubleProp);
        }

        [Test]
        public void SetAndGetNullableDoubleProperty()
        {
            var withProps = Store.Get<IWithProperties>(1);
            var val = new Random().NextDouble();
            withProps.NullableDoubleProp = val;
            Assert.AreEqual(val, withProps.NullableDoubleProp);

            withProps.NullableDoubleProp = null;
            Assert.AreEqual(null, withProps.NullableDoubleProp);
        }

        [Test]
        public void SetAndGetReferenceProperty()
        {
            var withProps = Store.Get<IWithProperties>(1);
            var referenceA = Store.Get<IReferenceWithProperties>(1);
            referenceA.Name = "A";

            var referenceB = Store.Get<IReferenceWithProperties>(2);
            referenceB.Name = "B";

            Assert.AreEqual(null, withProps.ReferenceProp);

            withProps.ReferenceProp = referenceA;

            Assert.AreEqual("A", withProps.ReferenceProp.Name);

            withProps.ReferenceProp = referenceB;

            Assert.AreEqual("B", withProps.ReferenceProp.Name);

            withProps.ReferenceProp = null;
            Assert.AreEqual(null, withProps.ReferenceProp);
        }

        [Test]
        public void GetIntegerSet()
        {
            var withProps = Store.Get<IWithProperties>(1);
            var set = withProps.IntegerSet;
            Assert.AreEqual(0, set.Count);

            set.Add(3);

            Assert.AreEqual(1, set.Count);

            Assert.AreEqual(3, set.ToList()[0]);
        }

        [Test]
        public void GetIntegerList()
        {
            var withProps = Store.Get<IWithProperties>(1);
            var list = withProps.IntegerList;
            Assert.AreEqual(0, list.Count);

            list.PushHead(17);

            Assert.AreEqual(1, list.Count);
            Assert.AreEqual(17, list.PopHead());
        }

        [Test]
        public void GetHash()
        {
            var withProps = Store.Get<IWithProperties>(1);
            var hash = withProps.StringToStringHash;

            hash["Hello..."] = "World!";
            hash["I love..."] = "Hashes!";
        }

        [Test]
        public void GetSortedSet()
        {
            var withProps = Store.Get<IWithProperties>(1);
            var set = withProps.StringLengthSortedSet;

            set["hello"] = 5;
            set["goodbye"] = 7;
            set["1"] = 1;
            set["four"] = 4;

            var stuff = set.ToList();

            Assert.AreEqual("1", stuff[0]);
            Assert.AreEqual("four", stuff[1]);
            Assert.AreEqual("hello", stuff[2]);
            Assert.AreEqual("goodbye", stuff[3]);
        }

        [Test]
        public void SetAndSortedSetOfReferences()
        {
            var stuff = new[]
            {
                Store.Get<IReferenceWithProperties>(1),
                Store.Get<IReferenceWithProperties>(2),
                Store.Get<IReferenceWithProperties>(3),
            };

            stuff[0].Name = "Jason";
            stuff[1].Name = "WhatKindOfNameIsThis?";
            stuff[2].Name = "Izadora";

            var withProps = Store.Get<IWithProperties>(1);

            withProps.ReferencesWithProperties.Add(stuff[0]);
            withProps.ReferencesWithProperties.Add(stuff[1]);
            withProps.ReferencesWithProperties.Add(stuff[2]);

            withProps.ReferenceSortedByNameLength[stuff[0]] = stuff[0].Name.Length;
            withProps.ReferenceSortedByNameLength[stuff[1]] = stuff[1].Name.Length;
            withProps.ReferenceSortedByNameLength[stuff[2]] = stuff[2].Name.Length;

            var etcetera = withProps.ReferenceSortedByNameLength.ToList();
            Assert.AreEqual("Jason", etcetera[0].Name);
            Assert.AreEqual("Izadora", etcetera[1].Name);
            Assert.AreEqual("WhatKindOfNameIsThis?", etcetera[2].Name);
        }

        [Test]
        public void GetHyperLogLog()
        {
            var withProps = Store.Get<IWithProperties>(1);
            var hll = withProps.HyperLogLogOfGuids;

            Assert.AreEqual(0, withProps.HyperLogLogOfGuids.Count());
            foreach (var i in Enumerable.Range(1, 10000))
            {
                hll.Add(Guid.NewGuid());
            }
            Assert.AreNotEqual(0, hll.Count());
            Console.WriteLine(hll.Count());
        }

        [Test]
        public async Task AsyncProperties()
        {
            var withProps = Store.Get<IWithProperties>(1);
            await (withProps.IntPropAsync = 3);
            Assert.AreEqual(3, await withProps.IntPropAsync);
            Assert.AreEqual(3, withProps.IntProp);
        }

        [Test]
        public void EnumProperties()
        {
            var withProps = Store.Get<IWithProperties>(1);
            Assert.AreEqual(AnEnum.ADefaultValue, withProps.AnEnumProperty);
            withProps.AnEnumProperty = AnEnum.AnotherValue;
            Assert.AreEqual(AnEnum.AnotherValue, withProps.AnEnumProperty);
        }
    }

    [TestFixture]
    public class Inheritance : RolFixture
    {
        [ImplementInheritance]
        public interface IBase
        {
            int Id { get; }
        }

        public interface ISub : IBase
        {
            string SubProp { get; set; }
        }

        public interface ISubSub : ISub
        {
            string SubSubProp { get; set; }
        }

        [Test]
        public void BaseAndSubclassShareIdSpace()
        {
            var base1 = Store.Create<IBase>();
            var sub1 = Store.Create<ISub>();
            var base2 = Store.Create<IBase>();
            var sub2 = Store.Create<ISub>();
            var subsub1 = Store.Create<ISubSub>();

            Assert.AreEqual(1, base1.Id);
            Assert.AreEqual(2, sub1.Id);
            Assert.AreEqual(3, base2.Id);
            Assert.AreEqual(4, sub2.Id);
            Assert.AreEqual(5, subsub1.Id);
        }

        [Test]
        public void EnumeratingPolymorphicCollectionReturnsObjectsOfCorrectType()
        {
            var base1 = Store.Create<IBase>();
            var sub1 = Store.Create<ISub>();
            sub1.SubProp = "Yup Yup;";

            var base2 = Store.Create<IBase>();
            var sub2 = Store.Create<ISub>();
            var subsub1 = Store.Create<ISubSub>();

            subsub1.SubSubProp = "Yup";

            var stuff = Store.Enumerate<IBase>().ToList();

            base1 = (IBase) stuff[0];
            sub1 = (ISub)stuff[1];
            base2 = (IBase)stuff[2];
            sub2 = (ISub)stuff[3];
            subsub1 = (ISubSub) stuff[4];

            Assert.AreEqual("Yup", subsub1.SubSubProp);
            Assert.AreEqual("Yup Yup;", sub1.SubProp);
        }

        [Test]
        public void GettingPolymorphicValueReturnsObjectOfCorrectType()
        {
            var sub1 = Store.Create<ISub>();

            var base1 = Store.Get<IBase>(1);
            sub1 = (ISub) base1;
        }
    }

    [TestFixture]
    public class NakedRedisCollections : RolFixture
    {
        [Test]
        public void NakedSet()
        {
            var set = Store.Get<IRedisSet<int>>((RedisKey)"/helloworld");
            Assert.AreEqual(0, set.Count);

            set.Add(12345); //That's the same combination I have on my luggage!
            Assert.AreEqual(1, set.Count);
        }

        [Test]
        public void NakedHash()
        {
            var hash = Store.Get<IRedisHash<int, int>>((RedisKey) "/helloworld");
            hash[3] = 17;

            Assert.AreEqual(17, hash[3]);

            hash[3, When.NotExists] = 18;

            Assert.AreEqual(17, hash[3]);
        }

        [Test]
        public void NakedList()
        {
            var list = Store.Get<IRedisList<int>>((RedisKey) "/helloworld");
            Assert.AreEqual(0, list.Count);

            list.PushHead(48);
            Assert.AreEqual(48, list.PopHead());
        }

        [Test]
        public void NakedSortedSet()
        {
            var sortedSet = Store.Get<IRedisSortedSet<string>>((RedisKey) "/helloworld");
            Assert.AreEqual(0, sortedSet.WithRanksBetween(0, -1).Count());
            sortedSet["What up"] = 1.0;

            Assert.AreEqual(1.0, sortedSet["What up"]);
            Assert.AreEqual(1, sortedSet.WithScoresBetween(0, 100).Count());
        }

        [Test]
        public void NakedHyperLogLog()
        {
            var hyperLogLog = Store.Get<IRedisHyperLogLog<Guid>>((RedisKey) "/helloworld");
            Assert.AreEqual(0, hyperLogLog.Count());
            foreach (var i in Enumerable.Range(1, 10000))
            {
                hyperLogLog.Add(Guid.NewGuid());
            }
            Console.WriteLine(hyperLogLog.Count());
        }
    }
}