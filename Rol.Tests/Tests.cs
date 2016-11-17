using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
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
            Connection = ConnectionMultiplexer.Connect("localhost,allowAdmin=true,syncTimeout=100000");
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
        public interface IRedisKeyId
        {
            RedisKey Id { get; }
        }

        public interface IByteArrayId
        {
            byte[] Id { get; }
        }

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
        public void InterfaceWithRedisKeyIdCanGetGot()
        {
            var withRedisKeyId = Store.Get<IRedisKeyId>((RedisKey) "Key");
            Assert.AreEqual((RedisKey) "Key", withRedisKeyId.Id);
        }

        [Test]
        public void InterfaceWithByteArrayIdCanGetGot()
        {
            var key = new byte[] {1, 2, 3, 4};
            var withByteArrayId = Store.Get<IByteArrayId>(key);
            Assert.True(key.SequenceEqual(withByteArrayId.Id));
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
            var withIntId = Store.Create<IIntId>();
            Assert.AreEqual(1, withIntId.Id);
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
    public class Equality : RolFixture
    {
        public interface ISomeInterface
        {
            int Id { get; }
        }

        [Test]
        public void ObjectsWithTheSameTypeAndSameIdAreEqualNoMatterWhereTheyComeFrom()
        {
            var theObject = Store.Get<ISomeInterface>(1);

            //Get another one from the Store and they Should be equal...
            Assert.True(theObject == Store.Get<ISomeInterface>(1));

            //If you get it from a collection, they should be equal...
            var theSet = Store.Get<IRedisSet<ISomeInterface>>("TheSet");
            theSet.Add(theObject);

            Assert.True(theObject == theSet.First());
        }

        [Test]
        public void DictionariesWork()
        {
            var theObject = Store.Get<ISomeInterface>(1);
            var d = new Dictionary<ISomeInterface, ISomeInterface>();
            d[theObject] = theObject;
            Assert.AreEqual(theObject, d[theObject]);
        }

        [Test]
        [ExpectedException]
        public void DictionariesThrowOnMultipleAdd()
        {
            var theObject = Store.Get<ISomeInterface>(1);
            var d = new Dictionary<ISomeInterface, int>();
            d.Add(theObject, 1);
            d.Add(theObject, 2);
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
            IRedisArray<int> IntArray { get; set; } 
            IRedisArray<IReferenceWithProperties> RefArray { get; set; }

            [CompactStorage]
            int CompactInt { get; set; }
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

        [Test]
        public void IntRedisArrayProperty()
        {
            var withProps = Store.Get<IWithProperties>(1);
            var val = new Random().Next();
            withProps.IntArray[1] = val;
            Assert.AreEqual(val, withProps.IntArray[1]);
        }

        [Test]
        public void RefRedisArrayProperty()
        {
            var withProps = Store.Get<IWithProperties>(1);
            var theRef = Store.Get<IReferenceWithProperties>(34);

            withProps.RefArray[0] = theRef;
            Assert.AreEqual(theRef, withProps.RefArray[0]);
        }

        [Test]
        public void CompactIntProperty()
        {
            var withProps = Store.Get<IWithProperties>(1);
            var val = new Random().Next(10000);
            withProps.CompactInt = val;
            Assert.AreEqual(val, withProps.CompactInt);
        }
    }

    [TestFixture]
    public class NakedRedisCollections : RolFixture
    {
        [Test]
        public void NakedSet()
        {
            var set = Store.Get<IRedisSet<int>>("/helloworld");
            Assert.AreEqual(0, set.Count);

            set.Add(12345); //That's the same combination I have on my luggage!
            Assert.AreEqual(1, set.Count);
        }

        [Test]
        public void NakedHash()
        {
            var hash = Store.Get<IRedisHash<int, int>>("/helloworld");
            hash[3] = 17;

            Assert.AreEqual(17, hash[3]);

            hash[3, When.NotExists] = 18;

            Assert.AreEqual(17, hash[3]);
        }

        [Test]
        public void NakedList()
        {
            var list = Store.Get<IRedisList<int>>("/helloworld");
            Assert.AreEqual(0, list.Count);

            list.PushHead(48);
            Assert.AreEqual(48, list.PopHead());
        }

        [Test]
        public void NakedSortedSet()
        {
            var sortedSet = Store.Get<IRedisSortedSet<string>>("/helloworld");
            Assert.AreEqual(0, sortedSet.WithRanksBetween(0, -1).Count());
            sortedSet["What up"] = 1.0;

            Assert.AreEqual(1.0, sortedSet["What up"]);
            Assert.AreEqual(1, sortedSet.WithScoresBetween(0, 100).Count());
        }

        [Test]
        public void NakedHyperLogLog()
        {
            var hyperLogLog = Store.Get<IRedisHyperLogLog<Guid>>("/helloworld");
            Assert.AreEqual(0, hyperLogLog.Count());
            foreach (var i in Enumerable.Range(1, 10000))
            {
                hyperLogLog.Add(Guid.NewGuid());
            }
            Console.WriteLine(hyperLogLog.Count());
        }

        public interface IMemTest
        {
            int Id { get; }
            int AnId { get; set; }
        }

        [Test]
        public void NakedArray()
        {
            var intArray = Store.Get<IRedisArray<int>>("/int-array");
            var intVal = new Random().Next();
            intArray[0] = intVal;
            Assert.AreEqual(intVal, intArray[0]);

            var uintArray = Store.Get<IRedisArray<uint>>("/uint-array");
            var uintVal = (uint) new Random().Next();
            uintArray[0] = uintVal;
            Assert.AreEqual(uintVal, uintArray[0]);

            var shortArray = Store.Get<IRedisArray<short>>("/short-array");
            var shortVal = (short) new Random().Next(short.MaxValue);
            shortArray[0] = shortVal;
            Assert.AreEqual(shortVal, shortArray[0]);

            var uShortArray = Store.Get<IRedisArray<ushort>>("/ushort-array");
            var ushortVal = (ushort) new Random().Next(ushort.MaxValue);
            uShortArray[0] = ushortVal;
            Assert.AreEqual(ushortVal, uShortArray[0]);

            var longArray = Store.Get<IRedisArray<long>>("/long-array");
            var longVal = new Random().Next();
            longArray[0] = longVal;
            Assert.AreEqual(longVal, longArray[0]);

            var ulongArray = Store.Get<IRedisArray<ulong>>("/ulong-array");
            var ulongVal = (ulong)new Random().Next();
            ulongArray[0] = ulongVal;
            Assert.AreEqual(ulongVal, ulongArray[0]);

            var floatArray = Store.Get<IRedisArray<float>>("/float-array");
            var floatVal = (float) new Random().NextDouble();
            floatArray[0] = floatVal;
            Assert.AreEqual(floatVal, floatArray[0]);

            var doubleArray = Store.Get<IRedisArray<double>>("/double-array");
            var doubleVal = new Random().NextDouble();
            doubleArray[0] = doubleVal;
            Assert.AreEqual(doubleVal, doubleArray[0]);

            var charArray = Store.Get<IRedisArray<char>>("/char-array");
            char charVal = '☃';
            charArray[0] = charVal;
            Assert.AreEqual(charVal, charArray[0]);

            var memTestArray = Store.Get<IRedisArray<IMemTest>>("/imemtest-array");
            var memTestVal = Store.Get<IMemTest>(1);
            memTestArray[0] = memTestVal;
            Assert.AreEqual(memTestVal, memTestArray[0]);

            var dateTimeArray = Store.Get<IRedisArray<DateTime>>("/datetime-array");
            var dateTimeVal = DateTime.UtcNow.AddDays(new Random().Next(0, 1000));
            dateTimeArray[0] = dateTimeVal;
            Assert.AreEqual(dateTimeVal, dateTimeArray[0]);

            var guidArray = Store.Get<IRedisArray<Guid>>("/guid-array");
            var guidVal = Guid.NewGuid();
            guidArray[0] = guidVal;
            Assert.AreEqual(guidVal, guidArray[0]);
        }
    }

    [TestFixture]
    public class NameMapping : RolFixture
    {
        public interface IInterfaceThatIsOverlyDescribedByItsReallyRidiculouslyOverlyVerboseAndLongAndRedundantName
        {
            int Id { get; }
            Async<string> StringPropertyThatIsOverlyDescribedByItsReallyRidiculouslyOverlyVerboseAndLongAndRedundantName { get; set; }
        }

        [RolName("a")]
        public interface IInterfaceThatIsOverlyDescribedByItsReallyRidiculouslyOverlyVerboseAndLongAndRedundantNameButHasANameMapAttribute
        {
            int Id { get; }
            [RolName("b")]
            Async<string> StringPropertyThatIsOverlyDescribedByItsReallyRidiculouslyOverlyVerboseAndLongAndRedundantNameButHasANameMapAttribute { get; set; }
        }

        [Test]
        public void TestMemorySavings()
        {
            var createTasks = Enumerable.Range(1, 100000).Select(o =>Store.CreateAsync<IInterfaceThatIsOverlyDescribedByItsReallyRidiculouslyOverlyVerboseAndLongAndRedundantName>()).ToArray();
            Store.WaitAll(createTasks);

            var setTasks = createTasks.Select(o => o.Result).Select(o => o.StringPropertyThatIsOverlyDescribedByItsReallyRidiculouslyOverlyVerboseAndLongAndRedundantName = "Hello, world!").ToArray();
            Store.WaitAll(setTasks);

            Thread.Sleep(10000);

            var server = Store.Connection.GetServer(Store.Connection.GetEndPoints()[0]);

            var mem = int.Parse(server.Info("memory")[0].Single(o => o.Key == "used_memory").Value);

            Console.WriteLine($"Memory used with long names: {mem}");
            server.FlushAllDatabases();

            var createTasks2 = Enumerable.Range(1, 100000).Select(o =>Store.CreateAsync<IInterfaceThatIsOverlyDescribedByItsReallyRidiculouslyOverlyVerboseAndLongAndRedundantNameButHasANameMapAttribute>()).ToArray();
            Store.WaitAll(createTasks2);

            var setTasks2 = createTasks2.Select(o => o.Result).Select(o => o.StringPropertyThatIsOverlyDescribedByItsReallyRidiculouslyOverlyVerboseAndLongAndRedundantNameButHasANameMapAttribute = "Hello, world!").ToArray();
            Store.WaitAll(setTasks2);

            Thread.Sleep(10000);

            var mem2 = int.Parse(server.Info("memory")[0].Single(o => o.Key == "used_memory").Value);

            Console.WriteLine($"Memory used with short names: {mem2}");

            Assert.Less(mem2, mem);
        }
    }

    [TestFixture]
    public class CompactStorage : RolFixture
    {
        public interface IInterfaceThatIsOverlyDescribedByItsReallyRidiculouslyOverlyVerboseAndLongAndRedundantNameSparse
        {
            int Id { get; }
            Async<int> IntPropertyThatIsOverlyDescribedByItsReallyRidiculouslyOverlyVerboseAndLongAndRedundantNameButHasANameMapAttribute { get; set; }
        }

        public interface IInterfaceThatIsOverlyDescribedByItsReallyRidiculouslyOverlyVerboseAndLongAndRedundantNameCompact
        {
            int Id { get; }
            [CompactStorage] Async<int> IntPropertyThatIsOverlyDescribedByItsReallyRidiculouslyOverlyVerboseAndLongAndRedundantNameButHasANameMapAttribute { get; set; }
        }

        [Test]
        public void TestMemorySavings()
        {
            var createTasks = Enumerable.Range(1, 100000).Select(o => Store.CreateAsync<IInterfaceThatIsOverlyDescribedByItsReallyRidiculouslyOverlyVerboseAndLongAndRedundantNameSparse>()).ToArray();
            Store.WaitAll(createTasks);

            var setTasks = createTasks.Select(o => o.Result).Select(o => o.IntPropertyThatIsOverlyDescribedByItsReallyRidiculouslyOverlyVerboseAndLongAndRedundantNameButHasANameMapAttribute = 3456).ToArray();
            Store.WaitAll(setTasks);

            Thread.Sleep(10000);

            var server = Store.Connection.GetServer(Store.Connection.GetEndPoints()[0]);

            var mem = int.Parse(server.Info("memory")[0].Single(o => o.Key == "used_memory").Value);

            Console.WriteLine($"Memory used with long names: {mem}");
            server.FlushAllDatabases();

            var createTasks2 = Enumerable.Range(1, 100000).Select(o => Store.CreateAsync<IInterfaceThatIsOverlyDescribedByItsReallyRidiculouslyOverlyVerboseAndLongAndRedundantNameCompact>()).ToArray();
            Store.WaitAll(createTasks2);

            var setTasks2 = createTasks2.Select(o => o.Result).Select(o => o.IntPropertyThatIsOverlyDescribedByItsReallyRidiculouslyOverlyVerboseAndLongAndRedundantNameButHasANameMapAttribute = 3456).ToArray();
            Store.WaitAll(setTasks2);

            Thread.Sleep(10000);

            var mem2 = int.Parse(server.Info("memory")[0].Single(o => o.Key == "used_memory").Value);

            Console.WriteLine($"Memory used with compact storage: {mem2}");

            Assert.Less(mem2, mem);
        }
    }

    [TestFixture]
    public class RedisArray : RolFixture
    {
        [Test]
        public void GetRangeTest()
        {
            for (var i = 1; i < 100000; i += 1000)
            {
                var arr = Store.Get<IRedisArray<int>>(i.ToString());
                var data = Enumerable.Range(0, i).ToArray();

                arr.Set(data);
                var retrieved = arr.Get(0, i-1);
                Assert.True(data.SequenceEqual(retrieved));
            }
        }

        [Test]
        public async Task GetRangeTestAsync()
        {
            for (var i = 1; i < 100000; i += 1000)
            {
                var arr = Store.Get<IRedisArray<int>>(i.ToString());
                var data = Enumerable.Range(0, i).ToArray();

                await arr.SetAsync(data);
                var retrieved = await arr.GetAsync(0, i - 1);
                Assert.True(data.SequenceEqual(retrieved));
            }
        }

        [Test]
        public void TestPoco()
        {
            var arr = Store.Get<IRedisArray<IntClass>>("an-intclass-array");
            var val = new IntClass
            {
                FirstInt = 1,
                DateTime = DateTime.UtcNow,
            };
            arr.Set(0, val);
            arr.Set(1, val);

            var result = arr.Get(0, 1);


            Assert.AreEqual(val.FirstInt, result.First().FirstInt);
            Assert.AreEqual(val.DateTime, result.First().DateTime);
        }

        [Test]
        public async Task TestALotOfPoco()
        {
            var val = new IntClass
            {
                DateTime = DateTime.UtcNow.Date,
                FirstInt = 1,
            };

            var arr = Store.Get<IRedisArray<IntClass>>("an-intclass-array");

            var sw = Stopwatch.StartNew();

            var tasks = Enumerable.Range(0, 2).Select(o => arr.SetAsync(o, val));
            await Task.WhenAll(tasks);

            Console.WriteLine(sw.Elapsed);
        }

        [Test]
        public async Task MorePocos()
        {
            var val = new IntClass {DateTime = DateTime.UtcNow, FirstInt = 1};

            var r = new Random();
            var rands = Enumerable.Range(1, 1000001).Select(o => r.Next(1, 10)).ToArray();

            await Task.WhenAll(Enumerable.Range(1, 7000000).Select(o =>
            {
                var arr = Store.Get<IRedisArray<IntClass>>($"ic-a{o}");
                return arr.SetAsync(Enumerable.Repeat(val, 1).ToArray());
            }));
        }

        public class IntClass
        {
            public int FirstInt { get; set; }
            public DateTime DateTime { get; set; }
        }

        [Test]
        public void Append()
        {
            var arr = Store.Get<IRedisArray<IntClass>>("Key");
            arr.Append(new IntClass {DateTime = DateTime.UtcNow, FirstInt = 3});
            Assert.AreEqual(1, arr.Length);
            arr.Append(new IntClass { DateTime = DateTime.UtcNow, FirstInt = 4 });
            Assert.AreEqual(2, arr.Length);
        }

        [Test]
        public async Task AppendAsync()
        {
            var arr = Store.Get<IRedisArray<IntClass>>("Key");
            await arr.AppendAsync(new IntClass() {DateTime = DateTime.UtcNow, FirstInt = 1});
            Assert.AreEqual(1, await arr.LengthAsync);
            await arr.AppendAsync(new IntClass() {DateTime = DateTime.UtcNow, FirstInt = 2});
            Assert.AreEqual(2, await arr.LengthAsync);
        }

        public interface IInterfaceTypeProperty
        {
            int Id { get; }
        }

        public class PocoWithInterfaceTypeProperty
        {
            public DateTime Date { get; set; }
            public IInterfaceTypeProperty InterfaceTypeProperty { get; set; }
        }

        [Test]
        public void PocoWithInterfaceTypePropertyWorks()
        {
            var iprop = Store.Get<IInterfaceTypeProperty>(1);
            var arr = Store.Get<IRedisArray<PocoWithInterfaceTypeProperty>>("YouKnowIt");

            var now = DateTime.UtcNow;

            arr[0] = new PocoWithInterfaceTypeProperty {Date = now, InterfaceTypeProperty = iprop};
            var readValue = arr[0];
            Assert.AreEqual(now, readValue.Date);
            Assert.AreEqual(iprop, readValue.InterfaceTypeProperty);
        }

        [Test]
        public void Get()
        {
            var arr = Store.Get<IRedisArray<int>>("ILOVEKEYS");
            arr.Append(3);
            Assert.AreEqual(3, arr.Get()[0]);
            arr.Append(4);
            var gotten = arr.Get();
            Assert.AreEqual(3, gotten[0]);
            Assert.AreEqual(4, gotten[1]);
        }

        [Test]
        public async Task GetAsync()
        {
            var arr = Store.Get<IRedisArray<int>>("ILOVEKEYS");
            arr.Append(3);
            Assert.AreEqual(3, (await arr.GetAsync())[0]);
            arr.Append(4);
            var gotten = await(arr.GetAsync());
            Assert.AreEqual(3, gotten[0]);
            Assert.AreEqual(4, gotten[1]);
        }

        [Test]
        public void AppendMany()
        {
            var r = new Random();
            var theData = Enumerable.Range(1, 100).Select(o => r.Next()).ToArray();
            var arr = Store.Get<IRedisArray<int>>("ILOVEKEYS");
            arr.Append(theData);
            Assert.True(theData.SequenceEqual(arr.Get()));
        }

        [Test]
        public async Task AppendManyAsync()
        {
            var r = new Random();
            var theData = Enumerable.Range(1, 100).Select(o => r.Next()).ToArray();
            var arr = Store.Get<IRedisArray<int>>("ILOVEKEYS");
            await arr.AppendAsync(theData);
            Assert.True(theData.SequenceEqual(arr.Get()));
        }
    }

    [TestFixture]
    public class RedisSet : RolFixture
    {
        [Test]
        public async Task RemoveAll()
        {
            var set = Store.Get<IRedisSet<int>>("helloworld");
            set.Add(1);
            set.Add(2);
            set.Add(3);
            set.Add(4);
            Assert.AreEqual(4, set.Count);
            set.RemoveAll();
            Assert.AreEqual(0, set.Count);

            set.Add(1);
            set.Add(2);
            set.Add(3);
            set.Add(4);
            Assert.AreEqual(4, set.Count);
            await set.RemoveAllAsync();
            Assert.AreEqual(0, set.Count);
        }

        [Test]
        public void Intersect()
        {
            var set1 = Store.Get<IRedisSet<int>>("set1");
            var set2 = Store.Get<IRedisSet<int>>("set2");
            var set3 = Store.Get<IRedisSet<int>>("set3");

            set1.Add(1, 2, 3, 4, 5, 6);
            set2.Add(3, 4, 5, 6, 7, 8);

            Assert.True(new[] {3, 4, 5, 6}.SequenceEqual(set1.Intersect(set2).OrderBy(o => o)));

            set1.IntersectAndStore(set3, set2);
            Assert.True(new[] {3, 4, 5, 6}.SequenceEqual(set3.OrderBy(o => o)));
        }

        [Test]
        public void Union()
        {
            var set1 = Store.Get<IRedisSet<int>>("set1");
            var set2 = Store.Get<IRedisSet<int>>("set2");
            var set3 = Store.Get<IRedisSet<int>>("set3");

            set1.Add(1, 2, 3, 4, 5, 6);
            set2.Add(3, 4, 5, 6, 7, 8);

            Assert.True(new[] { 1, 2, 3, 4, 5, 6, 7, 8 }.SequenceEqual(set1.Union(set2).OrderBy(o => o)));

            set1.UnionAndStore(set3, set2);
            Assert.True(new[] { 1, 2, 3, 4, 5, 6, 7, 8 }.SequenceEqual(set3.OrderBy(o => o)));
        }

        [Test]
        public void Diff()
        {
            var set1 = Store.Get<IRedisSet<int>>("set1");
            var set2 = Store.Get<IRedisSet<int>>("set2");
            var set3 = Store.Get<IRedisSet<int>>("set3");

            set1.Add(1, 2, 3, 4, 5, 6);
            set2.Add(3, 4, 5, 6, 7, 8);

            Assert.True(new[] { 1, 2 }.SequenceEqual(set1.Difference(set2).OrderBy(o => o)));
            Assert.True(new[] { 7, 8 }.SequenceEqual(set2.Difference(set1).OrderBy(o => o)));

            set1.DifferenceAndStore(set3, set2);
            Assert.True(new[] { 1, 2 }.SequenceEqual(set3.OrderBy(o => o)));
        }

        [Test]
        public void SetOfSets()
        {
            var setOfSetsOfInt = Store.Get<IRedisSet<IRedisSet<int>>>("set-of-sets");
            var setOfInt = Store.Get<IRedisSet<int>>("set-of-int");

            setOfInt.Add(1, 2, 3, 4, 5, 6);

            setOfSetsOfInt.Add(setOfInt);
            Assert.True(setOfSetsOfInt.Contains(setOfInt));
            Assert.True(setOfSetsOfInt.SelectMany(o => o).OrderBy(o => o).SequenceEqual(new[] {1, 2, 3, 4, 5, 6}));
        }

        [Test]
        public void SetOfHashes()
        {
            var setOfHashes = Store.Get<IRedisSet<IRedisHash<int, int>>>("Yep");
            var hash1 = Store.Get<IRedisHash<int, int>>("Hash1");
            var hash2 = Store.Get<IRedisHash<int, int>>("Hash2");

            setOfHashes.Add(hash1);
            setOfHashes.Add(hash2);

            Assert.True(setOfHashes.Contains(hash1));
            Assert.True(setOfHashes.Contains(hash2));
        }
    }

    [TestFixture]
    public class RedisHash : RolFixture
    {
        [Test]
        public async void RemoveAll()
        {
            var hash = Store.Get<IRedisHash<int, int>>("Helloworld");
            hash[3] = 4;
            hash[4] = 5;
            hash[5] = 6;
            Assert.AreEqual(3, hash.Count());
            hash.RemoveAll();
            Assert.AreEqual(0, hash.Count());

            hash[3] = 4;
            hash[4] = 5;
            hash[5] = 6;
            Assert.AreEqual(3, hash.Count());
            await hash.RemoveAllAsync();
            Assert.AreEqual(0, hash.Count());
        }

        [Test]
        public async void MultiSet()
        {
            var hash = Store.Get<IRedisHash<int, int>>("helloworld");
            hash.Set(Enumerable.Range(0, 100).ToDictionary(o => o, o => (int) Math.Pow(o, 2)));
            for (var i = 0; i < 100; i++)
            {
                Assert.AreEqual((int) Math.Pow(i, 2), await hash.GetAsync(i));
            }

            hash.RemoveAll();

            await hash.SetAsync(Enumerable.Range(0, 100).ToDictionary(o => o, o => (int) Math.Pow(o, 2)));
            for (var i = 0; i < 100; i++)
            {
                Assert.AreEqual((int)Math.Pow(i, 2), await hash.GetAsync(i));
            }
        }

        [Test]
        public void HashOfSets()
        {
            var hash = Store.Get<IRedisHash<int, IRedisSet<int>>>("GotMeAHashOfSetsOfInt");
            var set = Store.Get<IRedisSet<int>>("GotMeASetOfInts");
            set.Add(1, 2, 3, 4, 5);

            var set2 = Store.Get<IRedisSet<int>>("GotMeASetOfInts2");
            set2.Add(4, 5, 6, 7, 8);

            hash[1] = set;
            hash[2] = set2;

            Assert.AreEqual(set, hash[1]);
            Assert.AreEqual(set2, hash[2]);

            Assert.True(hash.SelectMany(o => o.Value).OrderBy(o => o).SequenceEqual(new[] { 1, 2, 3, 4, 4, 5, 5, 6, 7, 8}));
        }
    }

    [TestFixture]
    public class RedisSortedSet : RolFixture
    {
        [Test]
        public async void Remove()
        {
            var sortedSet = Store.Get<IRedisSortedSet<int>>("helloworld");
            sortedSet[1] = 5;
            sortedSet[2] = 6;
            Assert.AreEqual(2, sortedSet.Count());
            sortedSet.Remove(1);
            Assert.AreEqual(1, sortedSet.Count());
            await sortedSet.RemoveAsync(2);
            Assert.AreEqual(0, sortedSet.Count());

            sortedSet[1] = 5;
            sortedSet[2] = 6;
            Assert.AreEqual(2, sortedSet.Count());
            sortedSet.RemoveAll();
            Assert.AreEqual(0, sortedSet.Count());

            sortedSet[1] = 5;
            sortedSet[2] = 6;
            Assert.AreEqual(2, sortedSet.Count());
            await sortedSet.RemoveAllAsync();
            Assert.AreEqual(0, sortedSet.Count());
        }
    }

    [TestFixture]
    public class Expiration : RolFixture
    {
        public interface IExpires
        {
            int Id { get; }
            RedisTTL TTL { get; }
            string Hello { get; set; }
        }

        [Test]
        public void ExpirationTests()
        {
            var expires = Store.Get<IExpires>(1);
            expires.Hello = "World";

            Assert.IsNull(expires.TTL.Get());
            Assert.IsNotNull(expires.Hello);

            expires.TTL.Set(DateTime.UtcNow);
            Assert.IsNull(expires.TTL.Get());
            Assert.IsNull(expires.Hello);

            expires.Hello = "World";
            expires.TTL.Set(DateTime.UtcNow.Date.AddDays(1));
            Assert.IsNotNull(expires.TTL.Get());
        }
    }

    [TestFixture]
    public class README : RolFixture
    {
        public interface IQuestion
        {
            int Id { get; }
            string Title { get; set; }
            string Body { get; set; }
            int Score { get; set; }
        }

        [Test]
        public void GetAndWorkWithQuestionFromStore()
        {
            var question = Store.Get<IQuestion>(42);
            Assert.AreEqual(42, question.Id); //The object has the id you provided.
            Assert.AreEqual(null, question.Title); //The object's properties have the default values for their types.
            Assert.AreEqual(null, question.Body);
            Assert.AreEqual(0, question.Score);

            //To write data to redis, just set the properties on the object.
            question.Title = "How do I X?";
            question.Body = "I'm trying to X. I've tried Y and Z but they're not X. How do I X?";

            //To read data from redis, just read the properties on the object.
            var title = question.Title;
            var body = question.Body;

            Assert.AreEqual("How do I X?", title);
            Assert.AreEqual("I'm trying to X. I've tried Y and Z but they're not X. How do I X?", body);
        }

        [Test]
        public void CreatedIntegerIdsIncrease()
        {
            var first = Store.Create<IQuestion>();
            var second = Store.Create<IQuestion>();
            var third = Store.Create<IQuestion>();

            Assert.AreEqual(1, first.Id);
            Assert.AreEqual(2, second.Id);
            Assert.AreEqual(3, third.Id);

            Assert.IsTrue(Store.Enumerate<IQuestion>().ToList().Select(o => o.Id).SequenceEqual(new[] { 1, 2, 3 }));
            Assert.AreEqual(3, Store.Enumerate<IQuestion>().Count());
        }
    }
}