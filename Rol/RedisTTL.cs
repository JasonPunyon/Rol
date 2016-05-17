using System;
using System.Threading.Tasks;
using StackExchange.Redis;

namespace Rol
{
    public struct RedisTTL
    {
        private readonly RedisKey RedisKey;
        private readonly Store Store;

        public RedisTTL(RedisKey redisKey, Store store)
        {
            this.RedisKey = redisKey;
            this.Store = store;
        }

        public bool Set(TimeSpan timeSpan)
        {
            return Store.Connection.GetDatabase().KeyExpire(RedisKey, timeSpan);
        }

        public bool Set(DateTime dateTime)
        {
            return Store.Connection.GetDatabase().KeyExpire(RedisKey, dateTime);
        }

        public Task<bool> SetAsync(TimeSpan timeSpan)
        {
            return Store.Connection.GetDatabase().KeyExpireAsync(RedisKey, timeSpan);
        }

        public Task<bool> SetAsync(DateTime dateTime)
        {
            return Store.Connection.GetDatabase().KeyExpireAsync(RedisKey, dateTime);
        }

        public TimeSpan? Get()
        {
            return Store.Connection.GetDatabase().KeyTimeToLive(RedisKey);
        }

        public Task<TimeSpan?> GetAsync()
        {
            return Store.Connection.GetDatabase().KeyTimeToLiveAsync(RedisKey);
        }
    }
}