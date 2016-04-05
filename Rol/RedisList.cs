using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using StackExchange.Redis;

namespace Rol
{
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
}