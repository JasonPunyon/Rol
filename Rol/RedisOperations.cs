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
}