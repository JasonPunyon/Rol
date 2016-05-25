using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using StackExchange.Redis;

namespace Rol
{
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
        Task<bool> ContainsAsync(TKey key);
        void Remove(TKey key);
        Task RemoveAsync(TKey key);
        Task<IEnumerable<KeyValuePair<TKey, TValue>>> GetAllAsync();
        void RemoveAll();
        Task RemoveAllAsync();
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

        public Task<bool> ContainsAsync(TKey key)
        {
            return Store.Connection.GetDatabase().HashExistsAsync(_id, ToRedisValue<TKey>.Impl.Value(key));
        }

        public void Remove(TKey key)
        {
            Store.Connection.GetDatabase().HashDelete(_id, ToRedisValue<TKey>.Impl.Value(key));
        }

        public Task RemoveAsync(TKey key)
        {
            return Store.Connection.GetDatabase().HashDeleteAsync(_id, ToRedisValue<TKey>.Impl.Value(key));
        }

        public async Task<IEnumerable<KeyValuePair<TKey, TValue>>> GetAllAsync()
        {
            return
                (await Store.Connection.GetDatabase().HashGetAllAsync(_id)).Select(o => new KeyValuePair<TKey, TValue>(
                    FromRedisValue<TKey>.Impl.Value(o.Name, Store),
                    FromRedisValue<TValue>.Impl.Value(o.Value, Store))
                    );
        }

        public void RemoveAll()
        {
            Store.Connection.GetDatabase().KeyDelete(_id);
        }

        public Task RemoveAllAsync()
        {
            return Store.Connection.GetDatabase().KeyDeleteAsync(_id);
        }
    }
}