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

                if (typeof (T) == typeof(DateTime))
                {
                    il.LoadArgumentAddress(0);
                    var dt = il.DeclareLocal(typeof (DateTime));
                    il.Call(typeof (DateTime).GetMethod("ToBinary"));
                    il.Call(typeof (BitConverter).GetMethod("GetBytes", new[] {typeof (long)}));
                    il.Return();
                    return il.CreateDelegate();
                }

                if (typeof (T) == typeof (Guid))
                {
                    il.LoadArgumentAddress(0);
                    il.Call(typeof (Guid).GetMethod("ToByteArray"));
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
        public static Lazy<Func<byte[], int, Store, T>> ImplOffset = new Lazy<Func<byte[], int, Store, T>>(ImplementOffset);

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


        private static Func<byte[], int, Store, T> ImplementOffset()
        {
            if (TypeModel<T>.Model.IsFixedWidth)
            {
                if (typeof(T).In(BitConverterConversions.Select(o => o.Key).ToArray()))
                {
                    var il = Emit<Func<byte[], int, Store, T>>.NewDynamicMethod();
                    il.LoadArgument(0);
                    il.LoadArgument(1);
                    il.Call(BitConverterConversions[typeof(T)]);
                    il.Return();
                    return il.CreateDelegate();
                }

                if (typeof(T) == typeof(DateTime))
                {
                    var il = Emit<Func<byte[], int, Store, T>>.NewDynamicMethod();
                    il.LoadArgument(0);
                    il.LoadArgument(1);
                    il.Call(BitConverterConversions[typeof(long)]);
                    il.Call(typeof(DateTime).GetMethod("FromBinary"));
                    il.Return();
                    return il.CreateDelegate();
                }

                if (typeof(T) == typeof(Guid))
                {
                    var il = Emit<Func<byte[], int, Store, T>>.NewDynamicMethod();
                    var theArray = il.DeclareLocal<byte[]>();
                    il.LoadArgument(0);
                    il.LoadArgument(1);
                    il.LoadLocal(theArray);
                    il.LoadConstant(0);
                    il.LoadConstant(16);
                    il.Call(typeof (Buffer).GetMethod("BlockCopy"));
                    il.NewObject(typeof(Guid), typeof(byte[]));
                    il.Return();
                    return il.CreateDelegate();
                }

                if (TypeModel<T>.Model.HasIdProperty && TypeModel<T>.Model.IdType == typeof(int))
                {
                    var il = Emit<Func<byte[], int, Store, T>>.NewDynamicMethod();

                    il.LoadArgument(2);
                    il.LoadArgument(0);
                    il.LoadArgument(1);
                    il.Call(BitConverterConversions[typeof(int)]);
                    il.Box<int>();
                    il.Call(typeof(Store).GetMethod("Get").MakeGenericMethod(typeof(T)));
                    il.Return();
                    return il.CreateDelegate();
                }
            }

            throw new NotImplementedException();
        }
        
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

                if (typeof (T) == typeof (DateTime))
                {
                    var il = Emit<Func<byte[], Store, T>>.NewDynamicMethod();
                    il.LoadArgument(0);
                    il.LoadConstant(0);
                    il.Call(BitConverterConversions[typeof (long)]);
                    il.Call(typeof (DateTime).GetMethod("FromBinary"));
                    il.Return();
                    return il.CreateDelegate();
                }

                if (typeof (T) == typeof (Guid))
                {
                    var il = Emit<Func<byte[], Store, T>>.NewDynamicMethod();
                    il.LoadArgument(0);
                    il.NewObject(typeof (Guid), typeof (byte[]));
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
        Task<T[]> GetAsync(int startIndex, int endIndex);
        void Set(int index, T val);
        void Set(T[] values);
        Task SetAsync(int index, T val);
        Task SetAsync(T[] values);
        T[] Get(int startIndex, int endIndex);
        T this[int index] { get; set; }
    }

    class RedisArray<T> : IRedisArray<T>
    {
        public RedisKey _id;
        public RedisKey Id => _id;
        public readonly Store Store;

        private const int MaxOffset = (64 * 1024); //64K Slabs, you want it small so the maximum allocation time in redis is low.
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

        public T[] Get(int startIndex, int endIndex)
        {
            var startSlab = startIndex / _elementsPerSlab;
            var slabs = (endIndex / _elementsPerSlab) - startSlab + 1;

            var numberOfElements = endIndex - startIndex + 1;
            var result = new T[numberOfElements];

            for (var i = startSlab; i < slabs; i++)
            {
                var slabStartIndex = i * _elementsPerSlab;
                var slabEndIndex = (i + 1) * _elementsPerSlab;

                var getStart = Math.Max(slabStartIndex, startIndex) * TypeModel<T>.Model.FixedWidth;
                var getEnd = Math.Min(slabEndIndex, endIndex + 1) * TypeModel<T>.Model.FixedWidth - 1;

                var bytes = (byte[])Store.Connection.GetDatabase().StringGetRange(_id.Append($":{i}"), getStart % 65536, getEnd % 65536);
                for (var j = 0; j < bytes.Length / TypeModel<T>.Model.FixedWidth; j += 1)
                {
                    result[j + i * _elementsPerSlab] = FromFixedWidthBytes<T>.ImplOffset.Value(bytes, j * TypeModel<T>.Model.FixedWidth, Store);
                }
            }

            return result;
        }

        public async Task<T[]> GetAsync(int startIndex, int endIndex)
        {
            var startSlab = startIndex / _elementsPerSlab;
            var slabs = (endIndex / _elementsPerSlab) - startSlab + 1;

            var numberOfElements = endIndex - startIndex + 1;
            var result = new T[numberOfElements];

            for (var i = startSlab; i < slabs; i++)
            {
                var slabStartIndex = i * _elementsPerSlab;
                var slabEndIndex = (i + 1) * _elementsPerSlab;

                var getStart = Math.Max(slabStartIndex, startIndex) * TypeModel<T>.Model.FixedWidth;
                var getEnd = Math.Min(slabEndIndex, endIndex + 1) * TypeModel<T>.Model.FixedWidth - 1;

                var bytes = (byte[])await Store.Connection.GetDatabase().StringGetRangeAsync(_id.Append($":{i}"), getStart % 65536, getEnd % 65536);
                for (var j = 0; j < bytes.Length / TypeModel<T>.Model.FixedWidth; j += 1)
                {
                    result[j + i * _elementsPerSlab] = FromFixedWidthBytes<T>.ImplOffset.Value(bytes, j * TypeModel<T>.Model.FixedWidth, Store);
                }
            }

            return result;
        }

        public void Set(int index, T val)
        {
            var slab = index / _elementsPerSlab;
            var slabElementIndex = index % _elementsPerSlab;
            Store.Connection.GetDatabase().StringSetRange(_id.Append($":{slab}"), slabElementIndex * TypeModel<T>.Model.FixedWidth, ToFixedWidthBytes<T>.Impl.Value(val));
        }

        public void Set(T[] values)
        {
            var slabs = values.Length/_elementsPerSlab + 1;

            for (var i = 0; i < slabs; i++)
            {
                var startValueIndex = i*_elementsPerSlab;
                var endValueIndex = startValueIndex + ((i == slabs - 1) ? (values.Length % _elementsPerSlab) : _elementsPerSlab);

                var destination = new byte[(endValueIndex - startValueIndex)*TypeModel<T>.Model.FixedWidth];

                for (var v = startValueIndex; v < endValueIndex; v++)
                {
                    Buffer.BlockCopy(ToFixedWidthBytes<T>.Impl.Value(values[v]), 0, destination, (v%_elementsPerSlab)*TypeModel<T>.Model.FixedWidth, TypeModel<T>.Model.FixedWidth);
                }

                Store.Connection.GetDatabase().StringSetRange(_id.Append($":{i}"), 0, destination);
            }
        }

        public Task SetAsync(int index, T val)
        {
            var slab = index / _elementsPerSlab;
            var slabElementIndex = index % _elementsPerSlab;

            return Store.Connection.GetDatabase().StringSetRangeAsync(_id.Append($":{slab}"), slabElementIndex * TypeModel<T>.Model.FixedWidth, ToFixedWidthBytes<T>.Impl.Value(val));
        }

        public async Task SetAsync(T[] values)
        {
            var slabs = values.Length / _elementsPerSlab + 1;

            for (var i = 0; i < slabs; i++)
            {
                var startValueIndex = i * _elementsPerSlab;
                var endValueIndex = startValueIndex + ((i == slabs - 1) ? (values.Length % _elementsPerSlab) : _elementsPerSlab);

                var destination = new byte[(endValueIndex - startValueIndex) * TypeModel<T>.Model.FixedWidth];

                for (var v = startValueIndex; v < endValueIndex; v++)
                {
                    Buffer.BlockCopy(ToFixedWidthBytes<T>.Impl.Value(values[v]), 0, destination, (v % _elementsPerSlab) * TypeModel<T>.Model.FixedWidth, TypeModel<T>.Model.FixedWidth);
                }

                await Store.Connection.GetDatabase().StringSetRangeAsync(_id.Append($":{i}"), 0, destination);
            }
        }

        public T this[int index]
        {
            get { return Get(index); }
            set { Set(index, value); }
        }
    }
}
