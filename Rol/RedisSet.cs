using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using StackExchange.Redis;

namespace Rol
{
    public interface IRedisSet<T> : IEnumerable<T>
    {
        string Id { get; }
        bool Add(T value);
        long Add(params T[] values);
        Task<bool> AddAsync(T value);
        Task<long> AddAsync(params T[] values);
        int Count { get; }
        Task<int> CountAsync { get; }
        bool Contains(T value);
        Task<bool> ContainsAsync(T value);
        void Remove(T value);
        Task RemoveAsync(T value);
        void RemoveAll();
        Task RemoveAllAsync();
        Task<IEnumerable<T>> GetAllAsync();
        RedisTTL TTL { get; }
        T[] Intersect(IRedisSet<T> other);
        T[] Intersect(params IRedisSet<T>[] others);
        long IntersectAndStore(IRedisSet<T> destination, IRedisSet<T> other);
        long IntersectAndStore(IRedisSet<T> destination, params IRedisSet<T>[] other);
        T[] Union(IRedisSet<T> other);
        T[] Union(params IRedisSet<T>[] others);
        long UnionAndStore(IRedisSet<T> destination, IRedisSet<T> other);
        long UnionAndStore(IRedisSet<T> destination, params IRedisSet<T>[] other);
        T[] Difference(IRedisSet<T> other);
        T[] Difference(params IRedisSet<T>[] others);
        long DifferenceAndStore(IRedisSet<T> destination, IRedisSet<T> other);
        long DifferenceAndStore(IRedisSet<T> destination, params IRedisSet<T>[] others);
        Task<T[]> IntersectAsync(IRedisSet<T> other);
        Task<T[]> IntersectAsync(params IRedisSet<T>[] other);
        Task<long> IntersectAndStoreAsync(IRedisSet<T> destination, IRedisSet<T> other);
        Task<long> IntersectAndStoreAsync(IRedisSet<T> destination, params IRedisSet<T>[] others);
        Task<T[]> UnionAsync(IRedisSet<T> other);
        Task<T[]> UnionAsync(params IRedisSet<T>[] other);
        Task<long> UnionAndStoreAsync(IRedisSet<T> destination, IRedisSet<T> other);
        Task<long> UnionAndStoreAsync(IRedisSet<T> destination, params IRedisSet<T>[] others);
        Task<T[]> DifferenceAsync(IRedisSet<T> other);
        Task<T[]> DifferenceAsync(params IRedisSet<T>[] other);
        Task<long> DifferenceAndStoreAsync(IRedisSet<T> destination, IRedisSet<T> other);
        Task<long> DifferenceAndStoreAsync(IRedisSet<T> destination, params IRedisSet<T>[] others);
    }

    class RedisSet<T> : IRedisSet<T>
    {
        public string _id;
        public string Id
        {
            get
            {
                return _id;
            }
        }

        public readonly Store Store;

        public RedisSet(string id, Store store)
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

        public long Add(params T[] values)
        {
            if (values.Any())
            {
                return Store.Connection.GetDatabase().SetAdd(Id, values.Select(ToRedisValue<T>.Impl.Value).ToArray());
            }

            return 0;
        }

        public Task<bool> AddAsync(T value)
        {
            return Store.Connection.GetDatabase().SetAddAsync(Id, ToRedisValue<T>.Impl.Value(value));
        }

        public Task<long> AddAsync(params T[] values)
        {
            if (values.Any())
            {
                return Store.Connection.GetDatabase().SetAddAsync(Id, values.Select(ToRedisValue<T>.Impl.Value).ToArray());
            }

            return Task.FromResult(0L);
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

        public void RemoveAll()
        {
            Store.Connection.GetDatabase().KeyDelete(Id);
        }

        public Task RemoveAllAsync()
        {
            return Store.Connection.GetDatabase().KeyDeleteAsync(Id);
        }

        public async Task<IEnumerable<T>> GetAllAsync()
        {
            return (await Store.Connection.GetDatabase().SetMembersAsync(Id)).Select(o => FromRedisValue<T>.Impl.Value(o, Store));
        }

        public RedisTTL TTL => new RedisTTL(_id, Store);

        public T[] Intersect(IRedisSet<T> other)
        {
            return Store.Connection.GetDatabase().SetCombine(SetOperation.Intersect, Id, other.Id).Select(o => FromRedisValue<T>.Impl.Value(o, Store)).ToArray();
        }

        public T[] Intersect(params IRedisSet<T>[] others)
        {
            return Store.Connection.GetDatabase().SetCombine(SetOperation.Intersect, new [] { (RedisKey)Id }.Concat(others.Select(p => (RedisKey)p.Id)).ToArray()).Select(o => FromRedisValue<T>.Impl.Value(o, Store)).ToArray();
        }

        public long IntersectAndStore(IRedisSet<T> destination, IRedisSet<T> other)
        {
            return Store.Connection.GetDatabase().SetCombineAndStore(SetOperation.Intersect, destination.Id, Id, other.Id);
        }

        public long IntersectAndStore(IRedisSet<T> destination, params IRedisSet<T>[] others)
        {
            return Store.Connection.GetDatabase().SetCombineAndStore(SetOperation.Intersect, destination.Id, new[] { (RedisKey)Id }.Concat(others.Select(p => (RedisKey)p.Id)).ToArray());
        }

        public T[] Union(IRedisSet<T> other)
        {
            return Store.Connection.GetDatabase().SetCombine(SetOperation.Union, Id, other.Id).Select(o => FromRedisValue<T>.Impl.Value(o, Store)).ToArray();
        }

        public T[] Union(params IRedisSet<T>[] others)
        {
            return Store.Connection.GetDatabase().SetCombine(SetOperation.Union, new[] { (RedisKey)Id }.Concat(others.Select(p => (RedisKey)p.Id)).ToArray()).Select(o => FromRedisValue<T>.Impl.Value(o, Store)).ToArray();
        }

        public long UnionAndStore(IRedisSet<T> destination, IRedisSet<T> other)
        {
            return Store.Connection.GetDatabase().SetCombineAndStore(SetOperation.Union, destination.Id, Id, other.Id);
        }

        public long UnionAndStore(IRedisSet<T> destination, params IRedisSet<T>[] others)
        {
            return Store.Connection.GetDatabase().SetCombineAndStore(SetOperation.Union, destination.Id, new[] { (RedisKey)Id }.Concat(others.Select(p => (RedisKey)p.Id)).ToArray());
        }

        public T[] Difference(IRedisSet<T> other)
        {
            return Store.Connection.GetDatabase().SetCombine(SetOperation.Difference, Id, other.Id).Select(o => FromRedisValue<T>.Impl.Value(o, Store)).ToArray();
        }

        public T[] Difference(params IRedisSet<T>[] others)
        {
            return Store.Connection.GetDatabase().SetCombine(SetOperation.Difference, new[] { (RedisKey)Id }.Concat(others.Select(p => (RedisKey)p.Id)).ToArray()).Select(o => FromRedisValue<T>.Impl.Value(o, Store)).ToArray();
        }

        public long DifferenceAndStore(IRedisSet<T> destination, IRedisSet<T> other)
        {
            return Store.Connection.GetDatabase().SetCombineAndStore(SetOperation.Difference, destination.Id, Id, other.Id);
        }

        public long DifferenceAndStore(IRedisSet<T> destination, params IRedisSet<T>[] others)
        {
            return Store.Connection.GetDatabase().SetCombineAndStore(SetOperation.Difference, destination.Id, new[] { (RedisKey)Id }.Concat(others.Select(p => (RedisKey)p.Id)).ToArray());
        }

        public async Task<T[]> IntersectAsync(IRedisSet<T> other)
        {
            return (await Store.Connection.GetDatabase().SetCombineAsync(SetOperation.Intersect, Id, other.Id)).Select(o => FromRedisValue<T>.Impl.Value(o, Store)).ToArray();
        }

        public async Task<T[]> IntersectAsync(params IRedisSet<T>[] others)
        {
            return (await Store.Connection.GetDatabase().SetCombineAsync(SetOperation.Intersect, new[] { (RedisKey)Id }.Concat(others.Select(p => (RedisKey)p.Id)).ToArray())).Select(o => FromRedisValue<T>.Impl.Value(o, Store)).ToArray();
        }

        public Task<long> IntersectAndStoreAsync(IRedisSet<T> destination, IRedisSet<T> other)
        {
            return Store.Connection.GetDatabase().SetCombineAndStoreAsync(SetOperation.Intersect, destination.Id, Id, other.Id);
        }

        public Task<long> IntersectAndStoreAsync(IRedisSet<T> destination, params IRedisSet<T>[] others)
        {
            return Store.Connection.GetDatabase().SetCombineAndStoreAsync(SetOperation.Intersect, destination.Id, new[] { (RedisKey)Id }.Concat(others.Select(p => (RedisKey)p.Id)).ToArray());
        }

        public async Task<T[]> UnionAsync(IRedisSet<T> other)
        {
            return (await Store.Connection.GetDatabase().SetCombineAsync(SetOperation.Union, Id, other.Id)).Select(o => FromRedisValue<T>.Impl.Value(o, Store)).ToArray();
        }

        public async Task<T[]> UnionAsync(params IRedisSet<T>[] others)
        {
            return (await Store.Connection.GetDatabase().SetCombineAsync(SetOperation.Union, new[] { (RedisKey)Id }.Concat(others.Select(p => (RedisKey)p.Id)).ToArray())).Select(o => FromRedisValue<T>.Impl.Value(o, Store)).ToArray();
        }

        public Task<long> UnionAndStoreAsync(IRedisSet<T> destination, IRedisSet<T> other)
        {
            return Store.Connection.GetDatabase().SetCombineAndStoreAsync(SetOperation.Union, destination.Id, Id, other.Id);
        }

        public Task<long> UnionAndStoreAsync(IRedisSet<T> destination, params IRedisSet<T>[] others)
        {
            return Store.Connection.GetDatabase().SetCombineAndStoreAsync(SetOperation.Union, destination.Id, new[] { (RedisKey)Id }.Concat(others.Select(p => (RedisKey)p.Id)).ToArray());
        }

        public async Task<T[]> DifferenceAsync(IRedisSet<T> other)
        {
            return (await Store.Connection.GetDatabase().SetCombineAsync(SetOperation.Difference, Id, other.Id)).Select(o => FromRedisValue<T>.Impl.Value(o, Store)).ToArray();
        }

        public async Task<T[]> DifferenceAsync(params IRedisSet<T>[] others)
        {
            return (await Store.Connection.GetDatabase().SetCombineAsync(SetOperation.Difference, new[] { (RedisKey)Id }.Concat(others.Select(p => (RedisKey)p.Id)).ToArray())).Select(o => FromRedisValue<T>.Impl.Value(o, Store)).ToArray();
        }

        public Task<long> DifferenceAndStoreAsync(IRedisSet<T> destination, IRedisSet<T> other)
        {
            return Store.Connection.GetDatabase().SetCombineAndStoreAsync(SetOperation.Difference, destination.Id, Id, other.Id);
        }

        public Task<long> DifferenceAndStoreAsync(IRedisSet<T> destination, params IRedisSet<T>[] others)
        {
            return Store.Connection.GetDatabase().SetCombineAndStoreAsync(SetOperation.Intersect, destination.Id, new[] { (RedisKey)Id }.Concat(others.Select(p => (RedisKey)p.Id)).ToArray());
        }
    }
}