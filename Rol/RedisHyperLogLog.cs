using System.Linq;
using System.Threading.Tasks;
using StackExchange.Redis;

namespace Rol
{
    public interface IRedisHyperLogLog<T>
    {
        string Id { get; }
        bool Add(T element);
        long Count();
        void Merge(params IRedisHyperLogLog<T>[] otherHyperLogLogs);
        RedisTTL TTL { get; }
    }

    internal class RedisHyperLogLog<T> : IRedisHyperLogLog<T>
    {
        public string _id;
        public string Id { get { return _id; } }
        public Store Store;

        public RedisHyperLogLog() { }

        public RedisHyperLogLog(string id, Store store)
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
            Store.Connection.GetDatabase().HyperLogLogMerge(_id, otherHyperLogLogs.Select(p => (RedisKey)p.Id).ToArray());
        }

        public Task MergeAsync(params IRedisHyperLogLog<T>[] otherHyperLogLogs)
        {
            return Store.Connection.GetDatabase().HyperLogLogMergeAsync(_id, otherHyperLogLogs.Select(p => (RedisKey)p.Id).ToArray());
        }

        public RedisTTL TTL => new RedisTTL(_id, Store);
    }
}