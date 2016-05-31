using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using StackExchange.Redis;

namespace Rol
{
    public interface IRedisSortedSet<TKey> : IEnumerable<TKey>
    {
        RedisKey Id { get; }
        double this[TKey key] { get; set; }
        long Count();
        Task<long> CountAsync();
        IEnumerable<KeyValuePair<TKey, double>> IncludeScores();
        IEnumerable<TKey> WithScoresBetween(double min, double max);
        long CountWithScoresBetween(double min, double max);
        Task<long> CountWithScoresBetweenAsync(double min, double max);
        IEnumerable<KeyValuePair<TKey, double>> WithRanksBetweenIncludeScores(long min, long max);
        IEnumerable<TKey> WithRanksBetween(long min, long max);
        Task<bool> SetAsync(TKey key, double score);
        void Remove(TKey key);
        Task RemoveAsync(TKey key);
        void RemoveAll();
        Task RemoveAllAsync();
        RedisTTL TTL { get; }
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

        public void Remove(TKey key)
        {
            Store.Connection.GetDatabase().SortedSetRemove(_id, ToRedisValue<TKey>.Impl.Value(key));
        }

        public Task RemoveAsync(TKey key)
        {
            return Store.Connection.GetDatabase().SortedSetRemoveAsync(_id, ToRedisValue<TKey>.Impl.Value(key));
        }

        public void RemoveAll()
        {
            Store.Connection.GetDatabase().KeyDelete(_id);
        }

        public Task RemoveAllAsync()
        {
            return Store.Connection.GetDatabase().KeyDeleteAsync(_id);
        }

        public RedisTTL TTL => new RedisTTL(_id, Store);
    }
}