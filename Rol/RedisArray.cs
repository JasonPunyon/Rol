using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Sigil;
using StackExchange.Redis;

namespace Rol
{
    public static class ReadFromArrayOffset<T>
    {
        public static Lazy<Func<byte[], int, Store, T>> Impl = new Lazy<Func<byte[], int, Store, T>>(Implement);

        private static Func<byte[], int, Store, T> Implement()
        {
            var il = Emit<Func<byte[], int, Store, T>>.NewDynamicMethod();

            if (typeof (T) == typeof (int))
            {
                il.LoadArgument(0);
                il.LoadArgument(1);
                il.Call(typeof (BitConverter).GetMethod("ToInt32"));
            }
            else if (typeof (T) == typeof (long))
            {
                il.LoadArgument(0);
                il.LoadArgument(1);
                il.Call(typeof (BitConverter).GetMethod("ToInt64"));
            }
            else if (typeof(T) == typeof(DateTime))
            {
                il.LoadArgument(0);
                il.LoadArgument(1);
                il.Call(typeof (BitConverter).GetMethod("ToInt64"));
                il.Call(typeof (DateTime).GetMethod("FromBinary"));
            }
            else if (typeof (T) == typeof (byte))
            {
                il.LoadArgument(0);
                il.LoadArgument(1);
                il.LoadElement<byte>();
            }
            else if (typeof (T) == typeof (bool))
            {
                il.LoadArgument(0);
                il.LoadArgument(1);
                il.LoadElement<byte>();
                il.LoadConstant(0);
                il.UnsignedCompareGreaterThan();
            }
            else if (TypeModel<T>.Model.IsInterface && TypeModel<T>.Model.IsFixedWidth)
            {
                var idType = TypeModel<T>.Model.IdType;

                var rfao = typeof (ReadFromArrayOffset<>).MakeGenericType(idType);
                var lazy = rfao.GetField("Impl");
                var value = lazy.FieldType.GetProperty("Value");
                var invoke = lazy.FieldType.GetGenericArguments().First().GetMethod("Invoke");

                var thisIl = Emit<Func<byte[], int, Store, T>>.NewDynamicMethod();

                thisIl.LoadArgument(2);
                thisIl.LoadField(lazy);
                thisIl.CallVirtual(value.GetGetMethod());

                thisIl.LoadArgument(0);
                thisIl.LoadArgument(1);
                thisIl.LoadArgument(2);
                thisIl.Call(invoke);

                if (idType.IsValueType)
                {
                    thisIl.Box(idType);
                }

                var storeGet = typeof (Store).GetMethod("Get").MakeGenericMethod(typeof (T));
                thisIl.Call(storeGet);
                thisIl.Return();
                return thisIl.CreateDelegate();
            }
            else
            {
                throw new NotImplementedException();
            }

            il.Return();
            return il.CreateDelegate();
        }
    }

    public static class WriteToArrayOffset<T>
    {
        public static Lazy<Action<T, byte[], int>> Impl = new Lazy<Action<T, byte[], int>>(Implement);

        private static Action<T, byte[], int> Implement()
        {
            var il = Emit<Action<T, byte[], int>>.NewDynamicMethod();
            il.LoadArgument(0);
            il.LoadArgument(1);
            il.LoadArgument(2);

            if (typeof (T) == typeof (int))
            {
                il.Call(typeof (WriteToArrayOffset<int>).GetMethod("WriteInt"));
            }
            else if (typeof (T) == typeof (long))
            {
                il.Call(typeof (WriteToArrayOffset<long>).GetMethod("WriteLong"));
            }
            else if (typeof (T) == typeof (DateTime))
            {
                il.Call(typeof (WriteToArrayOffset<DateTime>).GetMethod("WriteDateTime"));
            }
            else if (typeof (T) == typeof (byte))
            {
                il.Call(typeof (WriteToArrayOffset<byte>).GetMethod("WriteByte"));
            }
            else if (typeof (T) == typeof (bool))
            {
                il.Call(typeof (WriteToArrayOffset<bool>).GetMethod("WriteBool"));
            }
            else if (TypeModel<T>.Model.IsInterface && TypeModel<T>.Model.IsFixedWidth)
            {
                var idType = TypeModel<T>.Model.IdType;

                var wtao = typeof (WriteToArrayOffset<>).MakeGenericType(idType);
                var lazy = wtao.GetField("Impl");
                var value = lazy.FieldType.GetProperty("Value");
                var invoke = lazy.FieldType.GetGenericArguments().First().GetMethod("Invoke");

                var thisIl = Emit<Action<T, byte[], int>>.NewDynamicMethod();

                var idPropGet = typeof(T).GetProperty("Id").GetGetMethod();

                thisIl.LoadField(lazy);
                thisIl.CallVirtual(value.GetGetMethod());

                thisIl.LoadArgument(0);
                thisIl.CallVirtual(idPropGet);
                thisIl.LoadArgument(1);
                thisIl.LoadArgument(2);

                thisIl.Call(invoke);
                thisIl.Return();
                return thisIl.CreateDelegate();
            }
            else
            {
                throw new NotImplementedException();
            }

            il.Return();

            return il.CreateDelegate();
        }

        public static unsafe void WriteInt(int value, byte[] buffer, int offset)
        {
            fixed (byte* pBuffer = buffer)
            {
                var ps = pBuffer + offset;

                *ps = (byte) value;
                ps++;
                *ps = (byte) (value >> 8);
                ps++;
                *ps = (byte) (value >> 16);
                ps++;
                *ps = (byte)(value >> 24);
            }
        }

        public static void WriteDateTime(DateTime value, byte[] buffer, int offset)
        {
            WriteLong(value.ToBinary(), buffer, offset);
        }

        public static unsafe void WriteLong(long value, byte[] buffer, int offset)
        {
            fixed (byte* pBuffer = buffer)
            {
                var ps = pBuffer + offset;
                *ps = (byte) value;
                ps++;
                *ps = (byte) (value >> 8);
                ps++;
                *ps = (byte) (value >> 16);
                ps++;
                *ps = (byte) (value >> 24);
                ps++;
                *ps = (byte)(value >> 32);
                ps++;
                *ps = (byte)(value >> 40);
                ps++;
                *ps = (byte)(value >> 48);
                ps++;
                *ps = (byte)(value >> 56);
            }
        }

        public static unsafe void WriteByte(byte value, byte[] buffer, int offset)
        {
            fixed (byte* pBuffer = buffer)
            {
                var ps = pBuffer + offset;
                *ps = value;
            }
        }

        public static unsafe void WriteBool(bool value, byte[] buffer, int offset)
        {
            fixed (byte* pBuffer = buffer)
            {
                var ps = pBuffer + offset;
                *ps = (byte) (value ? 1 : 0);
            }
        }
    }

    public static class ToFixedWidthBytes<T>
    {
        public static Lazy<Func<T, byte[]>> Impl = new Lazy<Func<T, byte[]>>(Implement);

        private static Func<T, byte[]> Implement()
        {
            if (TypeModel<T>.Model.IsFixedWidth)
            {
                var il = Emit<Func<T, byte[]>>.NewDynamicMethod();
                var bitConverterConversion = typeof(BitConverter).GetMethods().SingleOrDefault(o => o.Name == "GetBytes" && o.GetParameters()[0].ParameterType == typeof(T));
                
                //Anything convertible via BitConverter
                if (bitConverterConversion != null)
                {
                    il.LoadArgument(0);
                    il.Call(bitConverterConversion);
                    il.Return();
                    return il.CreateDelegate();
                }

                //DateTime
                if (typeof (T) == typeof(DateTime))
                {
                    il.LoadArgumentAddress(0);
                    il.Call(typeof (DateTime).GetMethod("ToBinary"));
                    il.Call(typeof (BitConverter).GetMethod("GetBytes", new[] {typeof (long)}));
                    il.Return();
                    return il.CreateDelegate();
                }

                //Guids
                if (typeof (T) == typeof (Guid))
                {
                    il.LoadArgumentAddress(0);
                    il.Call(typeof (Guid).GetMethod("ToByteArray"));
                    il.Return();
                    return il.CreateDelegate();
                }

                //Interface types.
                if (TypeModel<T>.Model.HasIdProperty && TypeModel<T>.Model.IdType == typeof (int))
                {
                    il.LoadArgument(0);
                    il.Call(TypeModel<T>.Model.RequestedType.GetMethod("get_Id"));
                    il.Call(typeof (BitConverter).GetMethod("GetBytes", new[] {typeof (int)}));
                    il.Return();
                    return il.CreateDelegate();
                }

                //Fixed Width Pocos
                if (!TypeModel<T>.Model.IsInterface && !typeof(T).IsValueType)
                {
                    var result = il.DeclareLocal<byte[]>();
                    il.LoadConstant(TypeModel<T>.Model.FixedWidth);
                    il.NewArray<byte>();
                    il.StoreLocal(result);

                    var offset = 0;
                    foreach (var property in typeof(T).GetProperties().OrderBy(o => o.Name))
                    {
                        var writer = typeof (WriteToArrayOffset<>).MakeGenericType(property.PropertyType);
                        var lazy = writer.GetField("Impl");
                        var value = lazy.FieldType.GetProperty("Value");
                        var invoke = lazy.FieldType.GetGenericArguments().First().GetMethod("Invoke");

                        il.LoadField(lazy);
                        il.CallVirtual(value.GetGetMethod());

                        il.LoadArgument(0);
                        il.CallVirtual(property.GetGetMethod());

                        il.LoadLocal(result);
                        il.LoadConstant(offset);

                        il.Call(invoke);
                        offset += TypeModel.Widths.ContainsKey(property.PropertyType) ? TypeModel.Widths[property.PropertyType] : 4;
                    }

                    il.LoadLocal(result);
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

                //Pocos
                if (!TypeModel<T>.Model.IsInterface && !typeof(T).IsValueType)
                {
                    var il = Emit<Func<byte[], int, Store, T>>.NewDynamicMethod();
                    var result = il.DeclareLocal<T>();

                    il.NewObject<T>();
                    il.StoreLocal(result);

                    var offset = 0;
                    foreach (var property in typeof(T).GetProperties().OrderBy(o => o.Name))
                    {
                        var reader = typeof(ReadFromArrayOffset<>).MakeGenericType(property.PropertyType);
                        var lazy = reader.GetField("Impl");
                        var value = lazy.FieldType.GetProperty("Value");
                        var invoke = lazy.FieldType.GetGenericArguments().First().GetMethod("Invoke");

                        il.LoadLocal(result);
                        il.LoadField(lazy);
                        il.CallVirtual(value.GetGetMethod());

                        il.LoadArgument(0);
                        il.LoadConstant(offset);
                        il.LoadArgument(1);
                        
                        il.Add();

                        il.LoadArgument(2);

                        il.Call(invoke);
                        il.CallVirtual(property.GetSetMethod());
                        offset += TypeModel.Widths.ContainsKey(property.PropertyType) ? TypeModel.Widths[property.PropertyType] : 4;
                    }

                    il.LoadLocal(result);
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

                //Fixed Width Pocos
                if (!TypeModel<T>.Model.IsInterface && !typeof (T).IsValueType)
                {
                    var il = Emit<Func<byte[], Store, T>>.NewDynamicMethod();
                    var result = il.DeclareLocal<T>();

                    il.NewObject<T>();
                    il.StoreLocal(result);

                    var offset = 0;
                    foreach (var property in typeof (T).GetProperties().OrderBy(o => o.Name))
                    {
                        var reader = typeof (ReadFromArrayOffset<>).MakeGenericType(property.PropertyType);
                        var lazy = reader.GetField("Impl");
                        var value = lazy.FieldType.GetProperty("Value");
                        var invoke = lazy.FieldType.GetGenericArguments().First().GetMethod("Invoke");

                        il.LoadLocal(result);
                        il.LoadField(lazy);
                        il.CallVirtual(value.GetGetMethod());

                        il.LoadArgument(0);
                        il.LoadConstant(offset);
                        il.LoadArgument(1);
                        

                        il.Call(invoke);
                        il.CallVirtual(property.GetSetMethod());
                        offset += TypeModel.Widths.ContainsKey(property.PropertyType) ? TypeModel.Widths[property.PropertyType] : 4;
                    }

                    il.LoadLocal(result);
                    il.Return();

                    return il.CreateDelegate();
                }
            }

            throw new NotImplementedException();
        }
    }

    public interface IRedisArray<T>
    {
        string Id { get; }
        T Get(int index);
        Task<T> GetAsync(int index);
        Task<T[]> GetAsync(int startIndex, int endIndex);
        void Set(int index, T val);
        void Set(T[] values);
        Task SetAsync(int index, T val);
        Task SetAsync(T[] values);
        T[] Get(int startIndex, int endIndex);
        T this[int index] { get; set; }
        RedisTTL TTL { get; }
        long Length { get; }
        Task<long> LengthAsync { get; }
        long Append(T val);
        Task<long> AppendAsync(T val);
        T[] Get();
        Task<T[]> GetAsync();
    }

    class RedisArray<T> : IRedisArray<T>
    {
        public string _id;
        public string Id => _id;
        public readonly Store Store;

        public RedisArray(string id, Store store)
        {
            _id = id;
            Store = store;
        }

        public RedisArray() { } 

        public T Get(int index)
        {
            var bytes = Store.Connection.GetDatabase()
                .StringGetRange(_id, index*TypeModel<T>.Model.FixedWidth,
                    ((index + 1)*TypeModel<T>.Model.FixedWidth) - 1);
            return FromFixedWidthBytes<T>.Impl.Value(bytes, Store);
        }

        public Task<T> GetAsync(int index)
        {
            return Store.Connection.GetDatabase()
                .StringGetRangeAsync(_id, index*TypeModel<T>.Model.FixedWidth,(index + 1)*TypeModel<T>.Model.FixedWidth)
                .ContinueWith(o => FromFixedWidthBytes<T>.Impl.Value(o.Result, Store));
        }

        public T[] Get(int startIndex, int endIndex)
        {
            var numberOfElements = endIndex - startIndex + 1;
            var result = new T[numberOfElements];

            var getStart = startIndex * TypeModel<T>.Model.FixedWidth;
            var getEnd = (endIndex + 1) * TypeModel<T>.Model.FixedWidth - 1;

            var bytes = (byte[])Store.Connection.GetDatabase().StringGetRange(_id, getStart, getEnd);
            for (var j = 0; j < bytes.Length / TypeModel<T>.Model.FixedWidth; j += 1)
            {
                result[j] = FromFixedWidthBytes<T>.ImplOffset.Value(bytes, j * TypeModel<T>.Model.FixedWidth, Store);
            }

            return result;
        }

        public async Task<T[]> GetAsync(int startIndex, int endIndex)
        {
            var numberOfElements = endIndex - startIndex + 1;
            var result = new T[numberOfElements];

            var getStart = startIndex * TypeModel<T>.Model.FixedWidth;
            var getEnd = (endIndex + 1) * TypeModel<T>.Model.FixedWidth - 1;

            var bytes = (byte[])(await Store.Connection.GetDatabase().StringGetRangeAsync(_id, getStart, getEnd));
            for (var j = 0; j < bytes.Length / TypeModel<T>.Model.FixedWidth; j += 1)
            {
                result[j] = FromFixedWidthBytes<T>.ImplOffset.Value(bytes, j * TypeModel<T>.Model.FixedWidth, Store);
            }

            return result;
        }

        public void Set(int index, T val)
        {
            Store.Connection.GetDatabase().StringSetRange(_id, index * TypeModel<T>.Model.FixedWidth, ToFixedWidthBytes<T>.Impl.Value(val));
        }

        public void Set(T[] values)
        {
            var destination = new byte[(values.Length)*TypeModel<T>.Model.FixedWidth];

            for (var v = 0; v < values.Length; v++)
            {
                Buffer.BlockCopy(ToFixedWidthBytes<T>.Impl.Value(values[v]), 0, destination, v*TypeModel<T>.Model.FixedWidth, TypeModel<T>.Model.FixedWidth);
            }

            Store.Connection.GetDatabase().StringSetRange(_id, 0, destination);
        }

        public Task SetAsync(int index, T val)
        {
            return Store.Connection.GetDatabase().StringSetRangeAsync(_id, index* TypeModel<T>.Model.FixedWidth, ToFixedWidthBytes<T>.Impl.Value(val));
        }

        public async Task SetAsync(T[] values)
        {
            var destination = new byte[values.Length * TypeModel<T>.Model.FixedWidth];

            for (var v = 0; v < values.Length; v++)
            {
                Buffer.BlockCopy(ToFixedWidthBytes<T>.Impl.Value(values[v]), 0, destination, v * TypeModel<T>.Model.FixedWidth, TypeModel<T>.Model.FixedWidth);
            }

            await Store.Connection.GetDatabase().StringSetRangeAsync(_id, 0, destination);
        }

        public T this[int index]
        {
            get { return Get(index); }
            set { Set(index, value); }
        }

        public RedisTTL TTL => new RedisTTL(_id, Store);

        public long Length => Store.Connection.GetDatabase()
            .StringLength(_id) / TypeModel<T>.Model.FixedWidth;

        public Task<long> LengthAsync => Store.Connection.GetDatabase()
            .StringLengthAsync(_id)
            .ContinueWith(o => o.Result / TypeModel<T>.Model.FixedWidth);

        public long Append(T val) => Store.Connection.GetDatabase()
            .StringAppend(_id, ToFixedWidthBytes<T>.Impl.Value(val)) / TypeModel<T>.Model.FixedWidth;

        public Task<long> AppendAsync(T val) => Store.Connection.GetDatabase()
            .StringAppendAsync(_id, ToFixedWidthBytes<T>.Impl.Value(val))
            .ContinueWith(o => o.Result/TypeModel<T>.Model.FixedWidth);

        public T[] Get()
        {
            var bytes = (byte[])Store.Connection.GetDatabase().StringGet(_id);
            var result = new T[bytes.Length/TypeModel<T>.Model.FixedWidth];

            for (var j = 0; j < bytes.Length / TypeModel<T>.Model.FixedWidth; j += 1)
            {
                result[j] = FromFixedWidthBytes<T>.ImplOffset.Value(bytes, j * TypeModel<T>.Model.FixedWidth, Store);
            }

            return result;
        }

        public async Task<T[]> GetAsync()
        {
            var bytes = (byte[])(await Store.Connection.GetDatabase().StringGetAsync(_id));
            var result = new T[bytes.Length / TypeModel<T>.Model.FixedWidth];

            for (var j = 0; j < bytes.Length / TypeModel<T>.Model.FixedWidth; j += 1)
            {
                result[j] = FromFixedWidthBytes<T>.ImplOffset.Value(bytes, j * TypeModel<T>.Model.FixedWidth, Store);
            }

            return result;
        }
    }
}