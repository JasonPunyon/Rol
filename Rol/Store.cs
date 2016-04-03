using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Jil;
using Sigil;
using Sigil.NonGeneric;
using StackExchange.Redis;

namespace Rol
{
    public class Store
    {
        public ConnectionMultiplexer Connection { get; set; }
        public Store(ConnectionMultiplexer connection)
        {
            Connection = connection;
        }
        public T Create<T>(object id = null)
        {
            return Rol.Create<T>.Impl.Value(id, this);
        }
        public Task<T> CreateAsync<T>(object id = null)
        {
            return Rol.CreateAsync<T>.Impl.Value(id, this);
        }

        public bool Exists<T>(object id)
        {
            return Rol.Exists<T>.Impl.Value(id, this);
        }

        public T Get<T>(object id)
        {
            return ConstructSubTyped<T>.Impl.Value(id, this);
        }

        public Task<T> GetAsync<T>(object id)
        {
            return ConstructSubTyped<T>.AsyncImpl.Value(id, this);
        }

        public IEnumerable<T> Enumerate<T>()
        {
            return Rol.Enumerate<T>.Impl.Value(this);
        }

        public void WaitAll<T>(Async<T>[] asyncs)
        {
            Connection.WaitAll(asyncs.Select(o => o.SetTask ?? o.Task).ToArray());
        }

        public void WaitAll(Task[] tasks)
        {
            Connection.WaitAll(tasks);
        }
    }

    internal static class Construct<T>
    {
        public static Lazy<Func<object, Store, T>> Impl = new Lazy<Func<object, Store, T>>(Implement);
        public static Func<object, Store, T> Implement()
        {
            var il = Emit<Func<object, Store, T>>.NewDynamicMethod();

            il.NewObject(TypeModel<T>.ImplementedType.Value);
            il.Duplicate();
            il.Duplicate();
            il.LoadArgument(0);

            if (TypeModel<T>.Model.IdType.IsValueType)
            {
                il.UnboxAny(TypeModel<T>.Model.IdType);
            }
            else
            {
                il.CastClass(TypeModel<T>.Model.IdType);
            }

            il.StoreField(TypeModel<T>.Model.IdField);
            il.LoadArgument(1);
            il.StoreField(TypeModel<T>.Model.StoreField);
            il.Return();
            return il.CreateDelegate();
        }
    }

    internal static class ConstructSubTyped<T>
    {
        public static Lazy<Func<object, Store, T>> Impl = new Lazy<Func<object, Store, T>>(Implement);
        public static Lazy<Func<object, Store, Task<T>>> AsyncImpl = new Lazy<Func<object, Store, Task<T>>>(ImplementAsync);

        private static Func<object, Store, Task<T>> ImplementAsync()
        {
            var il = Emit<Func<object, Store, Task<T>>>.NewDynamicMethod();

            if (typeof (T).IsRedisSet() || typeof (T).IsRedisList() || typeof (T).IsRedisHash() ||
                typeof (T).IsRedisSortedSet() || typeof(T).IsRedisHyperLogLog())
            {
                il.LoadNull();
            }
            else if (TypeModel<T>.Model.ImplementInheritance)
            {
                il.LoadArgument(1);
                il.LoadConstant($"/{TypeModel<T>.Model.IdDeclaringInterface.Name}/{{0}}");
                il.LoadArgument(0);
                il.Call(MethodInfos.StringFormat);
                il.Call(MethodInfos.StringToRedisKey);
                il.LoadConstant("@@type");
                var ghvAsync = typeof (RedisOperations).GetMethod("GetHashValueAsync")
                    .MakeGenericMethod(typeof (string), typeof (string));

                il.Call(ghvAsync);
            }
            else
            {
                il.LoadNull();
            }

            il.LoadArgument(0);
            il.LoadArgument(1);

            il.Call(typeof(ConstructSubTyped<T>).GetMethod("AsyncContinuation"));

            il.Return();

            return il.CreateDelegate();
        }

        private static Func<object, Store, T> Implement()
        {
            var il = Emit<Func<object, Store, T>>.NewDynamicMethod();

            if (typeof (T).IsRedisSet() || typeof(T).IsRedisList() || typeof(T).IsRedisHash() || typeof(T).IsRedisSortedSet() || typeof(T).IsRedisHyperLogLog())
            {
                il.LoadNull();
            }
            else if (TypeModel<T>.Model.ImplementInheritance)
            {
                il.LoadArgument(1);
                il.LoadConstant($"/{TypeModel<T>.Model.IdDeclaringInterface.Name}/{{0}}");
                il.LoadArgument(0);
                il.Call(MethodInfos.StringFormat);
                il.Call(MethodInfos.StringToRedisKey);
                il.LoadConstant("@@type");
                var ghv = typeof (RedisOperations).GetMethod("GetHashValue")
                    .MakeGenericMethod(typeof (string), typeof (string));

                il.Call(ghv);
            }
            else
            {
                il.LoadNull();
            }

            il.Call(typeof (ConstructSubTyped<T>).GetMethod("GetFunc"));

            il.LoadArgument(0);
            il.LoadArgument(1);
            il.Call(typeof (Func<object, Store, T>).GetMethod("Invoke"));
            il.Return();

            return il.CreateDelegate();
        }

        static readonly ConcurrentDictionary<string, Func<object, Store, T>> funcs = new ConcurrentDictionary<string, Func<object, Store, T>>();
        public static Func<object, Store, T> GetFunc(string name)
        {
            name = name ?? typeof (T).AssemblyQualifiedName;
            return funcs.GetOrAdd(name, t =>
            {
                var type = Type.GetType(t);
                var constructor = typeof(Construct<>).MakeGenericType(type);
                var impl = constructor.GetField("Impl");
                var funcType = typeof(Func<,,>).MakeGenericType(typeof(object), typeof(Store), type);
                var invoke = funcType.GetMethod("Invoke");
                var value = typeof(Lazy<>).MakeGenericType(funcType).GetProperty("Value").GetGetMethod();

                var il = Emit<Func<object, Store, T>>.NewDynamicMethod();
                il.LoadField(impl);
                il.CallVirtual(value);

                il.LoadArgument(0);
                il.LoadArgument(1);
                il.CallVirtual(invoke);
                il.Return();

                return il.CreateDelegate();
            });
        }

        public static Task<T> AsyncContinuation(Task<string> getTypeTask, object id, Store store)
        {
            return (getTypeTask ?? Task.FromResult<string>(null)).ContinueWith(name => GetFunc(name.Result)(id, store));
        }
    }

    internal static class Create<T>
    {
        public static Lazy<Func<object, Store, T>> Impl = new Lazy<Func<object, Store, T>>(Implement);
        public static Func<object, Store, T> Implement()
        {
            if (TypeModel<T>.Model.IdType == typeof(int))
            {
                return RedisOperations.CreateWithIncrementingIntegerId<T>;
            }
            throw new NotImplementedException();
        }
    }

    internal static class CreateAsync<T>
    {
        public static Lazy<Func<object, Store, Task<T>>> Impl = new Lazy<Func<object, Store, Task<T>>>(Implement);
        public static Func<object, Store, Task<T>> Implement()
        {
            if (TypeModel<T>.Model.IdType == typeof (int))
            {
                return RedisOperations.CreateWithIncrementingIntegerIdAsync<T>;
            }
            throw new NotImplementedException();
        }
    }

    internal static class Exists<T>
    {
        public static Lazy<Func<object, Store, bool>> Impl = new Lazy<Func<object, Store, bool>>(Implement);

        private static Func<object, Store, bool> Implement()
        {
            var il = Emit<Func<object, Store, bool>>.NewDynamicMethod();

            il.LoadArgument(1);
            il.LoadConstant($"/{TypeModel<T>.Model.IdDeclaringInterface.Name}/{{0}}");
            il.LoadArgument(0);
            il.Call(MethodInfos.StringFormat);
            il.Call(MethodInfos.StringToRedisKey);
            il.Call(typeof (RedisOperations).GetMethod("Exists").MakeGenericMethod(typeof (T)));
            il.Return();

            return il.CreateDelegate();
        }
    }

    internal static class Get<T>
    {        
        public static Lazy<Func<object, Store, T>> Impl = new Lazy<Func<object, Store, T>>(Implement);
        public static Func<object, Store, T> Implement()
        {
            return ConstructSubTyped<T>.Impl.Value;
        }
    }

    internal static class GetAsync<T>
    {
        public static Lazy<Func<object, Store, Task<T>>> Impl = new Lazy<Func<object, Store, Task<T>>>(Implement);
        public static Func<object, Store, Task<T>> Implement()
        {
            throw new NotImplementedException();
        }
    }

    internal static class Enumerate<T>
    {
        public static Lazy<Func<Store, IEnumerable<T>>> Impl = new Lazy<Func<Store, IEnumerable<T>>>(Implement);
        private static Func<Store, IEnumerable<T>> Implement()
        {
            if (TypeModel<T>.Model.IdType == typeof (int))
            {
                return RedisOperations.EnumerateWithIntegerId<T>;
            }

            throw new InvalidOperationException($"You can only enumerate types with integer Ids. {typeof(T).Name} has a key of type {TypeModel<T>.Model.IdType}.");
        }
    }

    public class Async<T>
    {
        //So, either it's on it's way in, and it has the value to set...
        internal T SetValue;
        internal Task<T> SetTask;

        //Or it's on it's way out 
        internal Task<T> Task;

        public ConfiguredTaskAwaitable<T> ConfigureAwait(bool continueOnCapturedContext)
        {
            return (SetTask ?? Task).ConfigureAwait(continueOnCapturedContext);
        }

        public static implicit operator T(Async<T> source)
        {
            return (source.SetTask ?? source.Task).Result;
        }

        public static implicit operator Async<T>(T source)
        {
            return new Async<T> { SetValue = source };
        }

        public static implicit operator Task(Async<T> source)
        {
            return (source.SetTask ?? source.Task);
        }

        public static explicit operator Task<T>(Async<T> source)
        {
            return source.Task;
        }

        public static explicit operator Async<T>(Task<T> source)
        {
            return new Async<T> { Task = source };
        }
    }

    public interface IRedisSet<T> : IEnumerable<T>
    {
        RedisKey Id { get; }
        bool Add(T value);
        Task<bool> AddAsync(T value);
        int Count { get; }
        Task<int> CountAsync { get; }
        bool Contains(T value);
        Task<bool> ContainsAsync(T value);
        void Remove(T value);
        Task RemoveAsync(T value);
    }

    class RedisSet<T> : IRedisSet<T>
    {
        public RedisKey _id;
        public RedisKey Id
        {
            get
            {
                return _id;
            }
        }

        public readonly Store Store;

        public RedisSet(RedisKey id, Store store)
        {
            _id = id;
            Store = store;
        }

        public RedisSet()
        {
            
        }

        public IEnumerator<T> GetEnumerator()
        {
            return
                Store.Connection.GetDatabase()
                    .SetMembers(Id)
                    .Select(o => FromRedisValue<T>.Impl.Value(o, Store))
                    .GetEnumerator();
        }
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
        public bool Add(T value)
        {
            return Store.Connection.GetDatabase().SetAdd(Id, ToRedisValue<T>.Impl.Value(value));
        }
        public Task<bool> AddAsync(T value)
        {
            return Store.Connection.GetDatabase().SetAddAsync(Id, ToRedisValue<T>.Impl.Value(value));
        }
        public int Count => (int)Store.Connection.GetDatabase().SetLength(Id);
        public Task<int> CountAsync => (Store.Connection.GetDatabase().SetLengthAsync(Id).ContinueWith(t => (int) t.Result));
        public bool Contains(T value)
        {
            return Store.Connection.GetDatabase().SetContains(Id, ToRedisValue<T>.Impl.Value(value));
        }
        public Task<bool> ContainsAsync(T value)
        {
            return Store.Connection.GetDatabase().SetContainsAsync(Id, ToRedisValue<T>.Impl.Value(value));
        }
        public void Remove(T value)
        {
            Store.Connection.GetDatabase().SetRemove(Id, ToRedisValue<T>.Impl.Value(value));
        }
        public Task RemoveAsync(T value)
        {
            return Store.Connection.GetDatabase().SetRemoveAsync(Id, ToRedisValue<T>.Impl.Value(value));
        }
    }

    public interface IRedisList<T> : IEnumerable<T>
    {
        RedisKey Id { get; }
        int Count { get; }
        T GetByIndex(int index);
        Task<T> GetByIndexAsync(int index);
        T Head();
        Task<T> HeadAsync();
        void PushHead(T item);
        T Tail();
        Task<T> TailAsync();
        void PushTail(T item);
        Task PushHeadAsync(T item);
        Task PushTailAsync(T item);
        T PopHead();
        T PopTail();
        Task<T> PopHeadAsync();
        Task<T> PopTailAsync();
    }

    class RedisList<T> : IRedisList<T>
    {
        public RedisKey _id;
        public RedisKey Id { get { return _id; } }

        public Store Store;

        public RedisList(RedisKey id, Store store)
        {
            _id = id;
            Store = store;
        }
        public RedisList() { } 

        public int Count => (int)Store.Connection.GetDatabase().ListLength(_id);
        public T GetByIndex(int index)
        {
            return FromRedisValue<T>.Impl.Value(Store.Connection.GetDatabase().ListGetByIndex(_id, index), Store);
        }

        public Task<T> GetByIndexAsync(int index)
        {
            return Store.Connection.GetDatabase().ListGetByIndexAsync(_id, index).ContinueWith(o => FromRedisValue<T>.Impl.Value(o.Result, Store));
        }

        public T Head()
        {
            return GetByIndex(0);
        }

        public Task<T> HeadAsync()
        {
            return GetByIndexAsync(0);
        }

        public void PushHead(T item)
        {
            Store.Connection.GetDatabase().ListLeftPush(_id, ToRedisValue<T>.Impl.Value(item));
        }

        public T Tail()
        {
            return GetByIndex(-1);
        }

        public Task<T> TailAsync()
        {
            return GetByIndexAsync(-1);
        }

        public void PushTail(T item)
        {
            Store.Connection.GetDatabase().ListRightPush(_id, ToRedisValue<T>.Impl.Value(item));
        }

        public Task PushHeadAsync(T item)
        {
            return Store.Connection.GetDatabase().ListLeftPushAsync(_id, ToRedisValue<T>.Impl.Value(item));
        }

        public Task PushTailAsync(T item)
        {
            return Store.Connection.GetDatabase().ListRightPushAsync(_id, ToRedisValue<T>.Impl.Value(item));
        }

        public T PopHead()
        {
            return FromRedisValue<T>.Impl.Value(Store.Connection.GetDatabase().ListLeftPop(_id), Store);
        }
        public T PopTail()
        {
            return FromRedisValue<T>.Impl.Value(Store.Connection.GetDatabase().ListRightPop(_id), Store);
        }

        public Task<T> PopHeadAsync()
        {
            return Store.Connection.GetDatabase().ListLeftPopAsync(_id).ContinueWith(o => FromRedisValue<T>.Impl.Value(o.Result, Store));
        }

        public Task<T> PopTailAsync()
        {
            return Store.Connection.GetDatabase().ListRightPopAsync(_id).ContinueWith(o => FromRedisValue<T>.Impl.Value(o.Result, Store));
        }

        public IEnumerator<T> GetEnumerator()
        {
            return
                Store.Connection.GetDatabase()
                    .ListRange(_id)
                    .Select(o => FromRedisValue<T>.Impl.Value(o, Store))
                    .GetEnumerator();
        }
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    public interface IRedisHash<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>>
    {
        RedisKey Id { get; }
        TValue this[TKey key, When when = When.Always] { get; set; }
        IEnumerable<TKey> Keys { get; }
        Task<IEnumerable<TKey>> KeysAsync { get; }
        Task<TValue> GetAsync(TKey key);
        bool Set(TKey key, TValue value, When when = When.Always);
        Task<bool> SetAsync(TKey key, TValue value, When when = When.Always);
        bool Contains(TKey key);
        void Remove(TKey key);
        Task RemoveAsync(TKey key);
    }

    class RedisHash<TKey, TValue> :  IRedisHash<TKey, TValue>
    {
        public RedisKey _id;

        public RedisKey Id
        {
            get { return _id; }
        }

        public readonly Store Store;

        public RedisHash(RedisKey id, Store store)
        {
            _id = id;
            Store = store;
        }

        public RedisHash() { } 

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            return
                Store.Connection.GetDatabase()
                    .HashGetAll(_id)
                    .Select(o => new KeyValuePair<TKey, TValue>(
                        FromRedisValue<TKey>.Impl.Value(o.Name, Store),
                        FromRedisValue<TValue>.Impl.Value(o.Value, Store))
                    ).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public TValue this[TKey key, When when = When.Always]
        {
            get
            {
                return FromRedisValue<TValue>.Impl.Value(Store.Connection.GetDatabase().HashGet(_id, ToRedisValue<TKey>.Impl.Value(key)), Store);
            }
            set
            {
                Store.Connection.GetDatabase().HashSet(_id, ToRedisValue<TKey>.Impl.Value(key), ToRedisValue<TValue>.Impl.Value(value), when);
            }
        }

        public IEnumerable<TKey> Keys
        {
            get { return Store.Connection.GetDatabase().HashKeys(_id).Select(o => FromRedisValue<TKey>.Impl.Value(o, Store)); }
        }

        public Task<IEnumerable<TKey>> KeysAsync
        {
            get
            {
                return
                    Store.Connection.GetDatabase()
                        .HashKeysAsync(_id)
                        .ContinueWith(o => o.Result.Select(p => FromRedisValue<TKey>.Impl.Value(p, Store)));
            }
        }

        public Task<TValue> GetAsync(TKey key)
        {
            return RedisOperations.GetHashValueAsync<TKey, TValue>(Store, _id, key);
        }

        public bool Set(TKey key, TValue value, When when = When.Always)
        {
            return Store.Connection.GetDatabase().HashSet(_id, ToRedisValue<TKey>.Impl.Value(key), ToRedisValue<TValue>.Impl.Value(value));
        }

        public Task<bool> SetAsync(TKey key, TValue value, When when = When.Always)
        {
            return Store.Connection.GetDatabase().HashSetAsync(_id, ToRedisValue<TKey>.Impl.Value(key), ToRedisValue<TValue>.Impl.Value(value));
        }

        public bool Contains(TKey key)
        {
            return Store.Connection.GetDatabase().HashExists(_id, ToRedisValue<TKey>.Impl.Value(key));
        }

        public void Remove(TKey key)
        {
            Store.Connection.GetDatabase().HashDelete(_id, ToRedisValue<TKey>.Impl.Value(key));
        }

        public Task RemoveAsync(TKey key)
        {
            return Store.Connection.GetDatabase().HashDeleteAsync(_id, ToRedisValue<TKey>.Impl.Value(key));
        }
    }

    public interface IRedisSortedSet<TKey> : IEnumerable<TKey>
    {
        RedisKey Id { get; }
        double this[TKey key] { get; set; }
        long Count();
        Task<long> CountAsync();
        IEnumerable<KeyValuePair<TKey, double>> IncludeScores();
        IEnumerable<TKey> WithScoresBetween(double min, double max);
        long CountWithScoresBetween(double min, double max);
        IEnumerable<KeyValuePair<TKey, double>> WithRanksBetweenIncludeScores(long min, long max);
        IEnumerable<TKey> WithRanksBetween(long min, long max);
        Task<bool> SetAsync(TKey key, double score);

    }

    class RedisSortedSet<TKey> : IRedisSortedSet<TKey>
    {
        public RedisKey _id;
        public RedisKey Id { get { return _id; } }
        public Store Store;

        public RedisSortedSet(RedisKey id, Store store)
        {
            _id = id;
            Store = store;
        }
        public RedisSortedSet() {  }

        public double this[TKey key]
        {
            get { return Store.Connection.GetDatabase().SortedSetScore(_id, ToRedisValue<TKey>.Impl.Value(key)).Value; }
            set { Store.Connection.GetDatabase().SortedSetAdd(_id, ToRedisValue<TKey>.Impl.Value(key), value); }
        }

        IEnumerator<TKey> IEnumerable<TKey>.GetEnumerator()
        {
            return Store
                .Connection
                .GetDatabase()
                .SortedSetRangeByRank(_id)
                .Select(o => FromRedisValue<TKey>.Impl.Value(o, Store))
                .GetEnumerator();
        }

        public IEnumerator GetEnumerator()
        {
            return ((IEnumerable<TKey>)this).GetEnumerator();
        }

        public long Count()
        {
            return Store.Connection.GetDatabase().SortedSetLength(_id);
        }

        public Task<long> CountAsync()
        {
            return Store.Connection.GetDatabase().SortedSetLengthAsync(_id);
        }

        public IEnumerable<KeyValuePair<TKey, double>> IncludeScores()
        {
            return WithRanksBetweenIncludeScores(0, -1);
        }

        public IEnumerable<TKey> WithScoresBetween(double min, double max)
        {
            return Store.Connection.GetDatabase().SortedSetRangeByScore(_id, min, max).Select(o => FromRedisValue<TKey>.Impl.Value(o, Store));
        }

        public long CountWithScoresBetween(double min, double max)
        {
            return Store.Connection.GetDatabase().SortedSetLength(_id, min, max);
        }

        public Task<long> CountWithScoresBetweenAsync(double min, double max)
        {
            return Store.Connection.GetDatabase().SortedSetLengthAsync(_id, min, max);
        }

        public IEnumerable<KeyValuePair<TKey, double>> WithRanksBetweenIncludeScores(long min, long max)
        {
            return
                Store.Connection.GetDatabase()
                    .SortedSetRangeByRankWithScores(_id, min, max)
                    .Select(o => new KeyValuePair<TKey, double>(FromRedisValue<TKey>.Impl.Value(o.Element, Store), o.Score));
        }

        public IEnumerable<TKey> WithRanksBetween(long min, long max)
        {
            return Store.Connection.GetDatabase().SortedSetRangeByRank(_id, min, max).Select(o => FromRedisValue<TKey>.Impl.Value(o, Store));
        }

        public Task<bool> SetAsync(TKey key, double score)
        {
            return Store.Connection.GetDatabase().SortedSetAddAsync(_id, ToRedisValue<TKey>.Impl.Value(key), score);
        }
    }

    public interface IRedisHyperLogLog<T>
    {
        RedisKey Id { get; }
        bool Add(T element);
        long Count();
        void Merge(params IRedisHyperLogLog<T>[] otherHyperLogLogs);
    }

    internal class RedisHyperLogLog<T> : IRedisHyperLogLog<T>
    {
        public RedisKey _id;
        public RedisKey Id { get { return _id; } }
        public Store Store;

        public RedisHyperLogLog() { }

        public RedisHyperLogLog(RedisKey id, Store store)
        {
            _id = id;
            Store = store;
        }

        public bool Add(T element)
        {
            return Store.Connection.GetDatabase().HyperLogLogAdd(_id, ToRedisValue<T>.Impl.Value(element));
        }

        public Task<bool> AddAsync(T element)
        {
            return Store.Connection.GetDatabase().HyperLogLogAddAsync(_id, ToRedisValue<T>.Impl.Value(element));
        }

        public long Count()
        {
            return Store.Connection.GetDatabase().HyperLogLogLength(_id);
        }

        public Task<long> CountAsync()
        {
            return Store.Connection.GetDatabase().HyperLogLogLengthAsync(_id);
        }

        public void Merge(params IRedisHyperLogLog<T>[] otherHyperLogLogs)
        {
            Store.Connection.GetDatabase().HyperLogLogMerge(_id, otherHyperLogLogs.Select(p => p.Id).ToArray());
        }

        public Task MergeAsync(params IRedisHyperLogLog<T>[] otherHyperLogLogs)
        {
            return Store.Connection.GetDatabase().HyperLogLogMergeAsync(_id, otherHyperLogLogs.Select(p => p.Id).ToArray());
        }
    }

    public class ImplementInheritanceAttribute : Attribute
    {
        
    }

    public static class TypeModel<T>
    {
        public static TypeModel Model;
        public static Lazy<Type> ImplementedType = new Lazy<Type>(ImplementType);

        static TypeModel()
        {
            Model = new TypeModel
            {
                RequestedType = typeof (T)
            };

            Model.IsInterface = Model.RequestedType.IsInterface;
            Model.AllInterfaces = Model.RequestedType.GetInterfaces().Concat(new[] {typeof (T)}).ToArray();

            Model.Properties = Model
                .AllInterfaces
                .SelectMany(o => o.GetProperties())
                .Select(o => new PropertyModel
                {
                    Name = o.Name,
                    Type = o.PropertyType,
                    DeclaringType = o.DeclaringType,
                    DeclaringTypeModel = Model
                }).ToArray();

            var idProperty = Model.Properties.SingleOrDefault(o => o.Name == "Id");

            Model.HasIdProperty = idProperty != null;
            Model.IdDeclaringInterface = idProperty?.DeclaringType;
            Model.IdType = idProperty?.Type;
            Model.ImplementInheritance = Model.IdDeclaringInterface?.GetCustomAttribute<ImplementInheritanceAttribute>() != null;
        }

        static Type ImplementType()
        {
            if (Model.RequestedType.IsRedisSet() || Model.RequestedType.IsRedisHash() ||
                Model.RequestedType.IsRedisList() || Model.RequestedType.IsRedisSortedSet() || Model.RequestedType.IsRedisHyperLogLog())
            {
                var res = (Model.RequestedType.IsRedisSet() ? typeof (RedisSet<>)
                            : Model.RequestedType.IsRedisHash() ? typeof (RedisHash<,>)
                            : Model.RequestedType.IsRedisList() ? typeof (RedisList<>)
                            : Model.RequestedType.IsRedisSortedSet() ? typeof (RedisSortedSet<>)
                            : Model.RequestedType.IsRedisHyperLogLog() ? typeof(RedisHyperLogLog<>) : null)
                    .MakeGenericType(Model.RequestedType.GenericTypeArguments);

                Model.IdField = res.GetField("_id");
                Model.IdType = Model.IdField.FieldType;
                Model.IdProperty = res.GetProperty("Id");
                Model.StoreField = res.GetField("Store");

                return res;
            }

            var _tb = Assembly.mb.DefineType($"Rol.{Model.RequestedType.Name}", TypeAttributes.Public);
            foreach (var iface in Model.AllInterfaces)
            {
                _tb.AddInterfaceImplementation(iface);
            }

            Model.StoreField = _tb.DefineField("Store", typeof (Store), FieldAttributes.Public);
            Model.IdField = _tb.DefineField("_id", Model.IdType, FieldAttributes.Public);

            foreach (var property in Model.Properties)
            {
                property.ImplementProperty(_tb);
            }

            var result = _tb.CreateType();

            Model.StoreField = result.GetField("Store");
            Model.IdField = result.GetField("_id");
            Model.IdProperty = result.GetProperty("Id");

            return _tb.CreateType();
        }
    }

    class Assembly
    {
        public static AssemblyBuilder ab = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("StoreImplementations"), AssemblyBuilderAccess.RunAndSave);
        public static ModuleBuilder mb = ab.DefineDynamicModule("module", "StoreImplementations.dll");
        public static readonly MethodAttributes MethodAttributes = MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Final | MethodAttributes.SpecialName | MethodAttributes.NewSlot | MethodAttributes.HideBySig;

        public static void Dump()
        {
            ab.Save("StoreImplementations.dll");
        }
    }

    public class TypeModel
    {
        public Type RequestedType;
        public bool IsInterface;
        public bool HasIdProperty;
        public Type[] AllInterfaces;
        public PropertyModel[] Properties;
        public Type IdDeclaringInterface;
        public Type IdType;
        public FieldInfo IdField;
        public FieldInfo StoreField;
        public PropertyInfo IdProperty;
        public bool ImplementInheritance;
    }

    public class PropertyModel
    {
        public TypeModel DeclaringTypeModel { get; set; }
        public string Name { get; set; }
        public Type Type { get; set; }
        public Type DeclaringType { get; set; }
        public static readonly MethodAttributes MethodAttributes = MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Final | MethodAttributes.SpecialName | MethodAttributes.NewSlot | MethodAttributes.HideBySig;

        public void ImplementProperty(TypeBuilder typeBuilder)
        {
            if (Name == "Id")
            {
                var idProperty = typeBuilder.DefineProperty("Id", PropertyAttributes.None, CallingConventions.HasThis, Type, Type.EmptyTypes);

                var getIl = Emit.BuildInstanceMethod(Type, Type.EmptyTypes, typeBuilder, "get_Id", MethodAttributes);
                getIl.LoadArgument(0);
                getIl.LoadField(DeclaringTypeModel.IdField);
                getIl.Return();

                idProperty.SetGetMethod(getIl.CreateMethod());
                return;
            }

            if (Type.IsRedisSet() || Type.IsRedisList() || Type.IsRedisHash() || Type.IsRedisSortedSet() || Type.IsRedisHyperLogLog())
            {
                var prop = typeBuilder.DefineProperty(Name, PropertyAttributes.None, CallingConventions.HasThis, Type, Type.EmptyTypes);
                var getIl = Emit.BuildInstanceMethod(Type, Type.EmptyTypes, typeBuilder, $"get_{Name}", MethodAttributes);

                var type = Type.IsRedisSet() ? typeof (RedisSet<>).MakeGenericType(Type.GenericTypeArguments[0])
                    : Type.IsRedisList() ? typeof (RedisList<>).MakeGenericType(Type.GenericTypeArguments[0])
                    : Type.IsRedisHash() ? typeof (RedisHash<,>).MakeGenericType(Type.GenericTypeArguments) 
                    : Type.IsRedisSortedSet() ? typeof(RedisSortedSet<>).MakeGenericType(Type.GenericTypeArguments)
                    : Type.IsRedisHyperLogLog() ? typeof(RedisHyperLogLog<>).MakeGenericType(Type.GenericTypeArguments)
                    : null;

                getIl.LoadConstant($"/{DeclaringTypeModel.RequestedType.Name}/{{0}}/{Name}");
                getIl.LoadArgument(0);
                getIl.LoadField(DeclaringTypeModel.IdField);

                if (DeclaringTypeModel.IdField.FieldType.IsValueType)
                {
                    getIl.Box(DeclaringTypeModel.IdField.FieldType);
                }

                getIl.Call(MethodInfos.StringFormat);
                getIl.Call(MethodInfos.StringToRedisKey);
                getIl.LoadArgument(0);
                getIl.LoadField(DeclaringTypeModel.StoreField);

                getIl.NewObject(type.GetConstructor(new[] {typeof (RedisKey), typeof (Store)}));
                getIl.Return();

                prop.SetGetMethod(getIl.CreateMethod());

                var setIl = Emit.BuildInstanceMethod(typeof (void), new[] { Type }, typeBuilder, $"set_{Name}", MethodAttributes);
                setIl.Return();
                prop.SetSetMethod(setIl.CreateMethod());
            }
            else if (Type.IsAsync())
            {
                var prop = typeBuilder.DefineProperty(Name, PropertyAttributes.None, CallingConventions.HasThis, Type, Type.EmptyTypes);
                var getIl = Emit.BuildInstanceMethod(Type, Type.EmptyTypes, typeBuilder, $"get_{Name}", MethodAttributes);
                var getMi = typeof (RedisOperations).GetMethod("GetAsyncProp").MakeGenericMethod(typeof (string), Type.GenericTypeArguments[0]);

                getIl.LoadArgument(0);
                getIl.LoadField(DeclaringTypeModel.StoreField);
                getIl.LoadConstant($"/{DeclaringTypeModel.IdDeclaringInterface.Name}/{{0}}");
                getIl.LoadArgument(0);
                getIl.LoadField(DeclaringTypeModel.IdField);

                if (DeclaringTypeModel.IdField.FieldType.IsValueType)
                {
                    getIl.Box(DeclaringTypeModel.IdField.FieldType);
                }

                getIl.Call(MethodInfos.StringFormat);
                getIl.Call(MethodInfos.StringToRedisKey);
                getIl.LoadConstant(Name.Replace("Async", ""));
                getIl.Call(getMi);
                getIl.Return();

                prop.SetGetMethod(getIl.CreateMethod());

                var setMi = typeof (RedisOperations).GetMethod("SetAsyncProp").MakeGenericMethod(typeof (string), Type.GenericTypeArguments[0]);
                var setIl = Emit.BuildInstanceMethod(typeof(void), new[] { Type }, typeBuilder, $"set_{Name}", MethodAttributes);

                setIl.LoadArgument(0);
                setIl.LoadField(DeclaringTypeModel.StoreField);
                setIl.LoadConstant($"/{DeclaringTypeModel.IdDeclaringInterface.Name}/{{0}}");
                setIl.LoadArgument(0);
                setIl.LoadField(DeclaringTypeModel.IdField);

                if (DeclaringTypeModel.IdField.FieldType.IsValueType)
                {
                    setIl.Box(DeclaringTypeModel.IdField.FieldType);
                }

                setIl.Call(MethodInfos.StringFormat);
                setIl.Call(MethodInfos.StringToRedisKey);
                setIl.LoadConstant(Name.Replace("Async", ""));
                setIl.LoadArgument(1);
                setIl.Call(setMi);
                setIl.Return();

                prop.SetSetMethod(setIl.CreateMethod());
            }
            else
            {
                var prop = typeBuilder.DefineProperty(Name, PropertyAttributes.None, CallingConventions.HasThis, Type, Type.EmptyTypes);

                var getIl = Emit.BuildInstanceMethod(Type, Type.EmptyTypes, typeBuilder, $"get_{Name}", MethodAttributes);

                getIl.LoadArgument(0);
                getIl.LoadField(DeclaringTypeModel.StoreField);

                getIl.LoadConstant($"/{DeclaringTypeModel.IdDeclaringInterface.Name}/{{0}}");
                getIl.LoadArgument(0);
                getIl.LoadField(DeclaringTypeModel.IdField);

                if (DeclaringTypeModel.IdField.FieldType.IsValueType)
                {
                    getIl.Box(DeclaringTypeModel.IdField.FieldType);
                }

                getIl.Call(MethodInfos.StringFormat);
                getIl.Call(MethodInfos.StringToRedisKey);

                getIl.LoadConstant(Name);

                var mi = typeof (RedisOperations).GetMethod("GetHashValue").MakeGenericMethod(typeof (string), Type);
                getIl.Call(mi);

                getIl.Return();

                prop.SetGetMethod(getIl.CreateMethod());

                var setIl = Emit.BuildInstanceMethod(typeof (void), new [] { Type }, typeBuilder, $"set_{Name}", MethodAttributes);

                setIl.LoadArgument(0);
                setIl.LoadField(DeclaringTypeModel.StoreField);

                setIl.LoadConstant($"/{DeclaringTypeModel.IdDeclaringInterface.Name}/{{0}}");
                setIl.LoadArgument(0);
                setIl.LoadField(DeclaringTypeModel.IdField);

                if (DeclaringTypeModel.IdField.FieldType.IsValueType)
                {
                    setIl.Box(DeclaringTypeModel.IdField.FieldType);
                }

                setIl.Call(MethodInfos.StringFormat);
                setIl.Call(MethodInfos.StringToRedisKey);

                setIl.LoadConstant(Name);
                setIl.LoadArgument(1);

                mi = typeof (RedisOperations).GetMethod("SetHashValue").MakeGenericMethod(typeof (string), Type);

                setIl.Call(mi);
                setIl.Return();

                prop.SetSetMethod(setIl.CreateMethod());
            }
        }
    }

    /// <summary>
    /// Convenience functions to help make the IL layer as thin as possible.
    /// </summary>
    public static class RedisOperations
    {
        public static T CreateWithIncrementingIntegerId<T>(object id, Store store)
        {
            var db = store.Connection.GetDatabase();
            id = id ?? (int)db.HashIncrement("TypeIds", TypeModel<T>.Model.IdDeclaringInterface.Name);
            
            //Set the @@type Field on the hash for subinterfaces...
            var result = Construct<T>.Impl.Value(id, store);

            if (TypeModel<T>.Model.RequestedType != TypeModel<T>.Model.IdDeclaringInterface)
            {
                db.HashSet(ToRedisKey<T>.Impl.Value(result), "@@type", TypeModel<T>.Model.RequestedType.AssemblyQualifiedName);
            }

            return result;
        }

        public static async Task<T> CreateWithIncrementingIntegerIdAsync<T>(object id, Store store)
        {
            var db = store.Connection.GetDatabase();
            id = id ?? (int) (await db.HashIncrementAsync("TypeIds", TypeModel<T>.Model.IdDeclaringInterface.Name));

            var result = Construct<T>.Impl.Value(id, store);
            if (TypeModel<T>.Model.RequestedType != TypeModel<T>.Model.IdDeclaringInterface)
            {
                await db.HashSetAsync(ToRedisKey<T>.Impl.Value(result), "@@type", TypeModel<T>.Model.RequestedType.AssemblyQualifiedName);
            }
            return result;
        }

        public static IEnumerable<T> EnumerateWithIntegerId<T>(Store store)
        {
            var max = (int)store.Connection.GetDatabase().HashGet("TypeIds", TypeModel<T>.Model.IdDeclaringInterface.Name);
            for (var i = 1; i <= max; i++)
            {
                yield return ConstructSubTyped<T>.Impl.Value(i, store);
            }
        }

        public static bool Exists<T>(Store s, RedisKey key)
        {
            return s.Connection.GetDatabase().KeyExists(key);
        }

        //Hashes / Object Properties
        public static void SetHashValue<TKey, TValue>(Store store, RedisKey hashName, TKey field, TValue value)
        {
            store.Connection.GetDatabase().HashSet(hashName, ToRedisValue<TKey>.Impl.Value(field), ToRedisValue<TValue>.Impl.Value(value));
        }

        public static TValue GetHashValue<TKey, TValue>(Store store, RedisKey hashName, TKey field)
        {
            var resultFromRedis = store.Connection.GetDatabase().HashGet(hashName, ToRedisValue<TKey>.Impl.Value(field));
            return FromRedisValue<TValue>.Impl.Value(resultFromRedis, store);
        }

        public static Task<TValue> GetHashValueAsync<TKey, TValue>(Store store, RedisKey hashName, TKey field)
        {
            return store
                .Connection
                .GetDatabase()
                .HashGetAsync(hashName, ToRedisValue<TKey>.Impl.Value(field))
                .ContinueWith(tval => FromRedisValue<TValue>.Impl.Value(tval.Result, store));
        }

        public static Async<TValue> GetAsyncProp<TKey ,TValue>(Store store, RedisKey hashName, TKey field)
        {
            var result = new Async<TValue>();

            result.Task = store.Connection.GetDatabase().HashGetAsync(hashName, ToRedisValue<TKey>.Impl.Value(field))
                .ContinueWith(t => FromRedisValue<TValue>.Impl.Value(t.Result, store));

            return result;
        }

        public static void SetAsyncProp<TKey, TValue>(Store store, RedisKey hashName, TKey field, Async<TValue> value)
        {
            value.SetTask = store.Connection.GetDatabase()
                .HashSetAsync(hashName, ToRedisValue<TKey>.Impl.Value(field), ToRedisValue<TValue>.Impl.Value(value.SetValue))
                .ContinueWith(o => value.SetValue);
        }
    }

    public static class ToRedisKey<T>
    {
        public static Lazy<Func<T, RedisKey>> Impl = new Lazy<Func<T, RedisKey>>(Implement);
        public static Func<T, RedisKey> Implement()
        {
            var il = Emit<Func<T, RedisKey>>.NewDynamicMethod();

            var implicitOrExplicitConversion = typeof(RedisKey).GetMethods().SingleOrDefault(o => o.Name.In("op_Implicit", "op_Explicit") && o.GetParameters()[0].ParameterType == typeof(T));

            if (implicitOrExplicitConversion != null)
            {
                il.LoadArgument(0);
                il.Call(implicitOrExplicitConversion);
                il.Return();
                return il.CreateDelegate();
            }

            if (TypeModel<T>.Model.IsInterface && TypeModel<T>.Model.HasIdProperty)
            {
                var typeModel = TypeModel<T>.Model;

                il.LoadConstant($"/{typeModel.IdDeclaringInterface.Name}/{{0}}");
                il.LoadArgument(0);
                il.CallVirtual(typeModel.IdDeclaringInterface.GetProperty("Id").GetGetMethod());

                if (typeModel.IdType.IsValueType)
                {
                    il.Box(typeModel.IdType);
                }

                il.Call(MethodInfos.StringFormat);
                il.Call(MethodInfos.StringToRedisKey);

                il.Return();
                return il.CreateDelegate();
            }

            return null;
        }
    }

    public static class ToRedisValue<T>
    {
        public static Lazy<Func<T, RedisValue>> Impl = new Lazy<Func<T, RedisValue>>(Implement);
        static Func<T, RedisValue> Implement()
        {
            var il = Emit<Func<T, RedisValue>>.NewDynamicMethod();
            var implicitOrExplicitConversion = typeof(RedisValue).GetMethods().SingleOrDefault(o => o.Name.In("op_Implicit", "op_Explicit") && o.GetParameters()[0].ParameterType == typeof(T));

            if (implicitOrExplicitConversion != null)
            {
                il.LoadArgument(0);
                il.Call(implicitOrExplicitConversion);
                il.Return();
                return il.CreateDelegate();
            }

            if (typeof(T).IsInterface && typeof(T).GetProperty("Id") != null)
            {
                var idProp = typeof(T).GetProperty("Id");
                var idConversion = typeof(RedisValue).GetMethods().SingleOrDefault(o => o.Name.In("op_Implicit", "op_Explicit") && o.GetParameters()[0].ParameterType == idProp.PropertyType);

                if (idConversion != null)
                {
                    var branch = il.DefineLabel();

                    il.LoadArgument(0);
                    il.LoadNull();
                    il.BranchIfEqual(branch);

                    il.LoadArgument(0);
                    il.CallVirtual(typeof(T).GetProperty("Id").GetGetMethod());
                    il.Call(idConversion);
                    il.Return();

                    il.MarkLabel(branch);
                    il.Call(typeof (RedisValue).GetMethod("get_Null", BindingFlags.Static | BindingFlags.Public));
                    il.Return();
                    return il.CreateDelegate();
                }
            }

            if (typeof(T) == typeof(DateTime))
            {
                il.LoadArgumentAddress(0);
                il.Call(typeof(DateTime).GetMethod("ToBinary"));
                il.Call(MethodInfos.LongToRedisValue);
                il.Return();
                return il.CreateDelegate();
            }

            if (typeof (T).IsEnum)
            {
                var underlyingType = Enum.GetUnderlyingType(typeof (T));
                var conversion = typeof(RedisValue).GetMethods().SingleOrDefault(o => o.Name.In("op_Implicit", "op_Explicit") && o.GetParameters()[0].ParameterType == underlyingType);

                if (conversion != null)
                {
                    il.LoadArgument(0);
                    il.Convert(Enum.GetUnderlyingType(typeof(T)));
                    il.Call(conversion);
                    il.Return();
                    return il.CreateDelegate();
                }
            }

            //Otherwise, json serialize it.
            return t => (RedisValue) JSON.Serialize(t);
        }
    }

    public static class FromRedisKey<T>
    {
        public static Lazy<Func<RedisKey, T>> Impl = new Lazy<Func<RedisKey, T>>(Implement);
        private static Func<RedisKey, T> Implement()
        {
            var il = Emit<Func<RedisKey, T>>.NewDynamicMethod();

            il.LoadArgument(0);

            var implicitOrExplicitConversion = typeof(RedisKey).GetMethods().SingleOrDefault(o => o.Name.In("op_Implicit", "op_Explicit") && o.ReturnType == typeof(T));
            if (implicitOrExplicitConversion != null)
            {
                il.Call(implicitOrExplicitConversion);
            }

            il.Return();

            return il.CreateDelegate();
        }
    }

    public static class FromRedisValue<T>
    {
        public static Lazy<Func<RedisValue, Store, T>> Impl = new Lazy<Func<RedisValue, Store, T>>(Implement);
        private static Func<RedisValue, Store, T> Implement()
        {
            var implicitOrExplicitConversion = typeof(RedisValue).GetMethods().SingleOrDefault(o => o.Name.In("op_Implicit", "op_Explicit") && o.ReturnType == typeof(T));

            if (implicitOrExplicitConversion != null)
            {
                var il = Emit<Func<RedisValue, Store, T>>.NewDynamicMethod();

                il.LoadArgument(0);
                il.Call(implicitOrExplicitConversion);
                il.Return();

                return il.CreateDelegate();
            }

            if (typeof(T).IsInterface && typeof(T).GetProperty("Id") != null)
            {
                var idProp = typeof(T).GetProperty("Id");
                var idConversion = typeof(RedisValue).GetMethods().SingleOrDefault(o => o.Name.In("op_Implicit", "op_Explicit") && o.ReturnType == idProp.PropertyType);

                if (idConversion != null)
                {
                    var il = Emit<Func<RedisValue, Store, T>>.NewDynamicMethod();

                    var branch = il.DefineLabel();

                    il.LoadArgumentAddress(0);
                    il.Call(typeof(RedisValue).GetMethod("get_IsNull"));
                    il.BranchIfTrue(branch);

                    var implField = typeof(ConstructSubTyped<T>).GetField("Impl");
                    var value = typeof (Lazy<Func<object, Store, T>>).GetProperty("Value").GetGetMethod();
                    var invoke = typeof (Func<object, Store, T>).GetMethod("Invoke");


                    il.LoadField(implField);
                    il.Call(value);

                    il.LoadArgument(0);
                    il.Call(idConversion);
                    if (TypeModel<T>.Model.IdType.IsValueType)
                    {
                        il.Box(TypeModel<T>.Model.IdType);
                    }

                    il.LoadArgument(1);
                    il.Call(invoke);

                    il.Return();

                    il.MarkLabel(branch);
                    il.LoadNull();
                    il.Return();

                    return il.CreateDelegate();
                }
            }

            if (typeof(T) == typeof(DateTime))
            {
                var il = Emit<Func<RedisValue, Store, T>>.NewDynamicMethod();

                il.LoadArgument(0);
                il.Call(MethodInfos.RedisValueToLong);
                il.Call(typeof(DateTime).GetMethod("FromBinary"));
                il.Return();
                return il.CreateDelegate();
            }

            if (typeof (T).IsEnum)
            {
                var underlyingType = Enum.GetUnderlyingType(typeof (T));
                var conversion = typeof(RedisValue).GetMethods().SingleOrDefault(o => o.Name.In("op_Implicit", "op_Explicit") && o.ReturnType == underlyingType);

                if (conversion != null)
                {
                    var il = Emit<Func<RedisValue, Store, T>>.NewDynamicMethod();

                    var branch = il.DefineLabel();

                    il.LoadArgumentAddress(0);
                    il.Call(typeof(RedisValue).GetMethod("get_IsNull"));
                    il.BranchIfTrue(branch);

                    il.LoadArgument(0);
                    il.Call(conversion);
                    il.Return();

                    il.MarkLabel(branch);
                    il.LoadConstant(0);
                    il.Return();

                    return il.CreateDelegate();
                }
            }

            return (v, s) => JSON.Deserialize<T>(v);
        }
    }

    static class MethodInfos
    {
        public static MethodInfo StringFormat = typeof(string).GetMethod("Format", new[] { typeof(string), typeof(object) });
        public static MethodInfo StringToRedisKey = typeof(RedisKey).GetMethod("op_Implicit", new[] { typeof(string) });
        public static MethodInfo LongToRedisValue = typeof(RedisValue).GetMethod("op_Implicit", new[] { typeof(long) });
        public static MethodInfo RedisValueToLong = typeof(RedisValue).GetMethods().First(o => o.Name == "op_Explicit" && o.ReturnType == typeof(long));
    }

    static class Extensions
    {
        public static bool In<T>(this T t, params T[] source)
        {
            return source.Contains(t);
        }

        public static bool IsRedisSet(this Type t) => t.IsGenericType && t.GetGenericTypeDefinition() == typeof (IRedisSet<>);
        public static bool IsRedisList(this Type t) => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IRedisList<>);
        public static bool IsRedisHash(this Type t) => t.IsGenericType && t.GetGenericTypeDefinition() == typeof (IRedisHash<,>);
        public static bool IsRedisSortedSet(this Type t) => t.IsGenericType && t.GetGenericTypeDefinition() == typeof (IRedisSortedSet<>);

        public static bool IsRedisHyperLogLog(this Type t) => t.IsGenericType && t.GetGenericTypeDefinition() == typeof (IRedisHyperLogLog<>);
        public static bool IsAsync(this Type t) => t.IsGenericType && t.GetGenericTypeDefinition() == typeof (Async<>);
    }

    public static class PublicExtensions
    {
        public static TaskAwaiter<T> GetAwaiter<T>(this Async<T> toAwait)
        {
            return (toAwait.SetTask ?? toAwait.Task).GetAwaiter();
        }
    }
}