using System.CodeDom;
using System.Collections.Generic;
using System.Threading.Tasks;
using StackExchange.Redis;

namespace Rol
{
    /// <summary>
    /// Convenience functions to help make the IL layer as thin as possible.
    /// </summary>
    public static class RedisOperations
    {
        public static T CreateWithIncrementingIntegerId<T>(object id, Store store)
        {
            var db = store.Connection.GetDatabase();
            id = id ?? (int)db.HashIncrement("TypeIds", TypeModel<T>.Model.RequestedType.Name);

            return Construct<T>.Impl.Value(id, store);
        }

        public static async Task<T> CreateWithIncrementingIntegerIdAsync<T>(object id, Store store)
        {
            var db = store.Connection.GetDatabase();
            id = id ?? (int) (await db.HashIncrementAsync("TypeIds", TypeModel<T>.Model.RequestedType.Name));

            return Construct<T>.Impl.Value(id, store);
        }

        public static IEnumerable<T> EnumerateWithIntegerId<T>(Store store)
        {
            var max = (int)store.Connection.GetDatabase().HashGet("TypeIds", TypeModel<T>.Model.RequestedType.Name);
            for (var i = 1; i <= max; i++)
            {
                yield return Construct<T>.Impl.Value(i, store);
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

        public static TElement GetCompactProp<TElement>(Store store, string arrayName, int index)
        {
            var arr = store.Get<IRedisArray<TElement>>(arrayName);
            return arr[index];
        }

        public static Async<TElement> GetCompactPropAsync<TElement>(Store store, string arrayName, int index)
        {
            return (Async<TElement>)store.Get<IRedisArray<TElement>>(arrayName).GetAsync(index);
        }

        public static void SetCompactProp<TElement>(Store store, string arrayName, int index, TElement val)
        {
            store.Get<IRedisArray<TElement>>(arrayName).Set(index, val);
        }

        public static void SetCompactPropAsync<TElement>(Store store, string arrayName, int index, Async<TElement> val)
        {
            val.SetTask = store.Get<IRedisArray<TElement>>(arrayName).SetAsync(index, val.SetValue).ContinueWith(o => val.SetValue);
        }
    }
}