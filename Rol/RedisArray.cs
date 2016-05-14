using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Sigil;
using StackExchange.Redis;

namespace Rol
{
    public static class ToFixedWidthBytes<T>
    {
        public static Lazy<Func<T, byte[]>> Impl = new Lazy<Func<T, byte[]>>(Implement);

        private static Func<T, byte[]> Implement()
        {
            if (TypeModel<T>.Model.IsFixedWidth)
            {
                var il = Emit<Func<T, byte[]>>.NewDynamicMethod();
                var bitConverterConversion = typeof(BitConverter).GetMethods().SingleOrDefault(o => o.Name == "GetBytes" && o.GetParameters()[0].ParameterType == typeof(T));

                if (bitConverterConversion != null)
                {
                    il.LoadArgument(0);
                    il.Call(bitConverterConversion);
                    il.Return();
                    return il.CreateDelegate();
                }

                if (TypeModel<T>.Model.HasIdProperty && TypeModel<T>.Model.IdType == typeof (int))
                {
                    il.LoadArgument(0);
                    il.Call(TypeModel<T>.Model.RequestedType.GetMethod("get_Id"));
                    il.Call(typeof (BitConverter).GetMethod("GetBytes", new[] {typeof (int)}));
                    il.Return();
                    return il.CreateDelegate();
                }
            }

            throw new NotImplementedException();
        }
    }

    public static class FromFixedWidthBytes<T>
    {
        public static Lazy<Func<byte[], Store, T>> Impl = new Lazy<Func<byte[], Store, T>>(Implement);

        static Dictionary<Type, MethodInfo> BitConverterConversions = new Dictionary<Type, MethodInfo>
        {
            [typeof(int)] = typeof(BitConverter).GetMethod("ToInt32"),
            [typeof(double)] = typeof(BitConverter).GetMethod("ToDouble"),
            [typeof(short)] = typeof(BitConverter).GetMethod("ToInt16"),
            [typeof(long)] = typeof(BitConverter).GetMethod("ToInt64"),
            [typeof(float)] = typeof(BitConverter).GetMethod("ToSingle"),
            [typeof(ushort)] = typeof(BitConverter).GetMethod("ToUInt16"),
            [typeof(uint)] = typeof(BitConverter).GetMethod("ToUInt32"),
            [typeof(ulong)] = typeof(BitConverter).GetMethod("ToUInt64"),
            [typeof(char)] = typeof(BitConverter).GetMethod("ToChar"),
        };

        private static Func<byte[], Store, T> Implement()
        {
            if (TypeModel<T>.Model.IsFixedWidth)
            {
                if (typeof(T).In(BitConverterConversions.Select(o => o.Key).ToArray()))
                {
                    var il = Emit<Func<byte[], Store, T>>.NewDynamicMethod();
                    il.LoadArgument(0);
                    il.LoadConstant(0);
                    il.Call(BitConverterConversions[typeof(T)]);
                    il.Return();
                    return il.CreateDelegate();
                }

                if (TypeModel<T>.Model.HasIdProperty && TypeModel<T>.Model.IdType == typeof (int))
                {
                    var il = Emit<Func<byte[], Store, T>>.NewDynamicMethod();

                    il.LoadArgument(1);
                    il.LoadArgument(0);
                    il.LoadConstant(0);
                    il.Call(BitConverterConversions[typeof (int)]);
                    il.Box<int>();
                    il.Call(typeof (Store).GetMethod("Get").MakeGenericMethod(typeof (T)));
                    il.Return();
                    return il.CreateDelegate();
                }
            }

            throw new NotImplementedException();
        }
    }

    public interface IRedisArray<T>
    {
        RedisKey Id { get; }
        T Get(int index);
        Task<T> GetAsync(int index);
        void Set(int index, T val);
        Task SetAsync(int index, T val);
        T this[int index] { get; set; }
    }

    class RedisArray<T> : IRedisArray<T>
    {
        public RedisKey _id;
        public RedisKey Id => _id;
        public readonly Store Store;

        private const int MaxOffset = (64 * 1024) - 1; //64K Slabs, you want it small so the maximum allocation time in redis is low.
        private static int _elementsPerSlab = MaxOffset/TypeModel<T>.Model.FixedWidth;

        public RedisArray(RedisKey id, Store store)
        {
            _id = id;
            Store = store;
        }

        public RedisArray() { } 

        public T Get(int index)
        {
            var slab = index/_elementsPerSlab;
            var slabElementIndex = index%_elementsPerSlab;
            return FromFixedWidthBytes<T>.Impl.Value(Store.Connection.GetDatabase().StringGetRange(_id.Append($":{slab}"), slabElementIndex*TypeModel<T>.Model.FixedWidth, ((slabElementIndex + 1)*TypeModel<T>.Model.FixedWidth) - 1), Store);
        }

        public Task<T> GetAsync(int index)
        {
            var slab = index/_elementsPerSlab;
            var slabElementIndex = index%_elementsPerSlab;
            return Store.Connection.GetDatabase()
                .StringGetRangeAsync(_id.Append($":{slab}"), slabElementIndex*TypeModel<T>.Model.FixedWidth,(slabElementIndex + 1)*TypeModel<T>.Model.FixedWidth)
                .ContinueWith(o => FromFixedWidthBytes<T>.Impl.Value(o.Result, Store));
        }

        public void Set(int index, T val)
        {
            var slab = index / _elementsPerSlab;
            var slabElementIndex = index % _elementsPerSlab;
            Store.Connection.GetDatabase().StringSetRange(_id.Append($":{slab}"), slabElementIndex * TypeModel<T>.Model.FixedWidth, ToFixedWidthBytes<T>.Impl.Value(val));
        }

        public Task SetAsync(int index, T val)
        {
            var slab = index / _elementsPerSlab;
            var slabElementIndex = index % _elementsPerSlab;

            return Store.Connection.GetDatabase().StringSetRangeAsync(_id.Append($":{slab}"), slabElementIndex * TypeModel<T>.Model.FixedWidth, ToFixedWidthBytes<T>.Impl.Value(val));
        }

        public T this[int index]
        {
            get { return Get(index); }
            set { Set(index, value); }
        }
    }
}
