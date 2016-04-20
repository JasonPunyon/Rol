using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Jil;
using Sigil;
using Sigil.NonGeneric;
using StackExchange.Redis;

namespace Rol
{
    public class Cache<T>
    {
        public static ConcurrentDictionary<object, T> Values;
        public static Lazy<Action<T>> Add = new Lazy<Action<T>>(ImplementAdd);

        private static Action<T> ImplementAdd()
        {
            var il = Emit<Action<T>>.NewDynamicMethod();

            var values = typeof (Cache<T>).GetField("Values");

            il.LoadField(values);
            il.LoadArgument(0);
            il.Call(TypeModel<T>.Model.IdDeclaringInterface.GetMethod("get_Id"));

            if (TypeModel<T>.Model.IdType.IsValueType)
            {
                il.Box(TypeModel<T>.Model.IdType);
            }

            il.LoadArgument(0);
            il.Call(typeof (ConcurrentDictionary<object, T>).GetMethod("set_Item"));
            il.Return();
            return il.CreateDelegate();
        }

        static Cache()
        {
            Values = new ConcurrentDictionary<object, T>();
        }
    }

    public class Store
    {
        public ConnectionMultiplexer Connection { get; set; }

        public Store(ConnectionMultiplexer connection)
        {
            Connection = connection;
        }

        public T Create<T>(object id = null)
        {
            if (id == null)
            {
                var result = Rol.Create<T>.Impl.Value(null, this);
                Cache<T>.Add.Value(result);
                return result;
            }
            
            return Cache<T>.Values.GetOrAdd(id, i => Rol.Create<T>.Impl.Value(i, this));
        }

        public Task<T> CreateAsync<T>(object id = null)
        {
            return Rol.CreateAsync<T>.Impl.Value(id, this);
        }

        public bool Exists<T>(object id)
        {
            return Rol.Exists<T>.Impl.Value(id, this);
        }

        public T Get<T>(object id)
        {
            return Cache<T>.Values.GetOrAdd(id, o => Construct<T>.Impl.Value(o, this));
        }

        public Task<T> GetAsync<T>(object id)
        {
            return Task.FromResult(Construct<T>.Impl.Value(id, this));
        }

        public IEnumerable<T> Enumerate<T>()
        {
            return Rol.Enumerate<T>.Impl.Value(this);
        }

        public void WaitAll<T>(Async<T>[] asyncs)
        {
            Connection.WaitAll(asyncs.Select(o => o.SetTask ?? o.Task).ToArray());
        }

        public void WaitAll(Task[] tasks)
        {
            Connection.WaitAll(tasks);
        }
    }

    internal static class Construct<T>
    {
        public static Lazy<Func<object, Store, T>> Impl = new Lazy<Func<object, Store, T>>(Implement);
        public static Func<object, Store, T> Implement()
        {
            var il = Emit<Func<object, Store, T>>.NewDynamicMethod();

            il.NewObject(TypeModel<T>.ImplementedType.Value);
            il.Duplicate();
            il.Duplicate();
            il.LoadArgument(0);

            if (TypeModel<T>.Model.IdType.IsValueType)
            {
                il.UnboxAny(TypeModel<T>.Model.IdType);
            }
            else
            {
                il.CastClass(TypeModel<T>.Model.IdType);
            }

            il.StoreField(TypeModel<T>.Model.IdField);
            il.LoadArgument(1);
            il.StoreField(TypeModel<T>.Model.StoreField);
            il.Return();
            return il.CreateDelegate();
        }
    }

    internal static class Create<T>
    {
        public static Lazy<Func<object, Store, T>> Impl = new Lazy<Func<object, Store, T>>(Implement);
        public static Func<object, Store, T> Implement()
        {
            if (TypeModel<T>.Model.IdType == typeof(int))
            {
                return RedisOperations.CreateWithIncrementingIntegerId<T>;
            }
            throw new NotImplementedException();
        }
    }

    internal static class CreateAsync<T>
    {
        public static Lazy<Func<object, Store, Task<T>>> Impl = new Lazy<Func<object, Store, Task<T>>>(Implement);
        public static Func<object, Store, Task<T>> Implement()
        {
            if (TypeModel<T>.Model.IdType == typeof (int))
            {
                return RedisOperations.CreateWithIncrementingIntegerIdAsync<T>;
            }
            throw new NotImplementedException();
        }
    }

    internal static class Exists<T>
    {
        public static Lazy<Func<object, Store, bool>> Impl = new Lazy<Func<object, Store, bool>>(Implement);

        private static Func<object, Store, bool> Implement()
        {
            var il = Emit<Func<object, Store, bool>>.NewDynamicMethod();

            il.LoadArgument(1);
            il.LoadConstant($"/{TypeModel<T>.Model.NameToUseInRedis}/{{0}}");
            il.LoadArgument(0);
            il.Call(MethodInfos.StringFormat);
            il.Call(MethodInfos.StringToRedisKey);
            il.Call(typeof (RedisOperations).GetMethod("Exists").MakeGenericMethod(typeof (T)));
            il.Return();

            return il.CreateDelegate();
        }
    }

    internal static class Get<T>
    {        
        public static Lazy<Func<object, Store, T>> Impl = new Lazy<Func<object, Store, T>>(Implement);
        public static Func<object, Store, T> Implement()
        {
            return Construct<T>.Impl.Value;
        }
    }

    internal static class GetAsync<T>
    {
        public static Lazy<Func<object, Store, Task<T>>> Impl = new Lazy<Func<object, Store, Task<T>>>(Implement);
        public static Func<object, Store, Task<T>> Implement()
        {
            throw new NotImplementedException();
        }
    }

    internal static class Enumerate<T>
    {
        public static Lazy<Func<Store, IEnumerable<T>>> Impl = new Lazy<Func<Store, IEnumerable<T>>>(Implement);
        private static Func<Store, IEnumerable<T>> Implement()
        {
            if (TypeModel<T>.Model.IdType == typeof (int))
            {
                return RedisOperations.EnumerateWithIntegerId<T>;
            }

            throw new InvalidOperationException($"You can only enumerate types with integer Ids. {typeof(T).Name} has a key of type {TypeModel<T>.Model.IdType}.");
        }
    }

    public static class TypeModel<T>
    {
        public static TypeModel Model;
        public static Lazy<Type> ImplementedType = new Lazy<Type>(ImplementType);

        static TypeModel()
        {
            Model = new TypeModel
            {
                RequestedType = typeof (T)
            };

            Model.IsInterface = Model.RequestedType.IsInterface;
            Model.AllInterfaces = Model.RequestedType.GetInterfaces().Concat(new[] {typeof (T)}).ToArray();

            Model.Properties = Model
                .AllInterfaces
                .SelectMany(o => o.GetProperties())
                .Select(o => new PropertyModel
                {
                    Name = o.Name,
                    Type = o.PropertyType,
                    DeclaringType = o.DeclaringType,
                    DeclaringTypeModel = Model,
                    NameToUseInRedis = o.GetCustomAttribute<RolNameAttribute>()?.Name ?? o.Name
                }).ToArray();

            var idProperty = Model.Properties.SingleOrDefault(o => o.Name == "Id");

            Model.HasIdProperty = idProperty != null;
            Model.IdDeclaringInterface = idProperty?.DeclaringType;
            Model.IdType = idProperty?.Type;
            Model.NameToUseInRedis = Model.IdDeclaringInterface?.GetCustomAttribute<RolNameAttribute>()?.Name ?? Model.RequestedType.Name;
        }

        static Type ImplementType()
        {
            if (Model.RequestedType.IsRedisSet() || Model.RequestedType.IsRedisHash() ||
                Model.RequestedType.IsRedisList() || Model.RequestedType.IsRedisSortedSet() || Model.RequestedType.IsRedisHyperLogLog())
            {
                var res = (Model.RequestedType.IsRedisSet() ? typeof (RedisSet<>)
                            : Model.RequestedType.IsRedisHash() ? typeof (RedisHash<,>)
                            : Model.RequestedType.IsRedisList() ? typeof (RedisList<>)
                            : Model.RequestedType.IsRedisSortedSet() ? typeof (RedisSortedSet<>)
                            : Model.RequestedType.IsRedisHyperLogLog() ? typeof(RedisHyperLogLog<>) : null)
                    .MakeGenericType(Model.RequestedType.GenericTypeArguments);

                Model.IdField = res.GetField("_id");
                Model.IdType = Model.IdField.FieldType;
                Model.IdProperty = res.GetProperty("Id");
                Model.StoreField = res.GetField("Store");

                return res;
            }

            var _tb = Assembly.mb.DefineType($"Rol.{Model.RequestedType.Name}", TypeAttributes.Public);
            foreach (var iface in Model.AllInterfaces)
            {
                _tb.AddInterfaceImplementation(iface);
            }

            Model.StoreField = _tb.DefineField("Store", typeof (Store), FieldAttributes.Public);
            Model.IdField = _tb.DefineField("_id", Model.IdType, FieldAttributes.Public);

            foreach (var property in Model.Properties)
            {
                property.ImplementProperty(_tb);
            }

            var result = _tb.CreateType();

            Model.StoreField = result.GetField("Store");
            Model.IdField = result.GetField("_id");
            Model.IdProperty = result.GetProperty("Id");

            return _tb.CreateType();
        }
    }

    class Assembly
    {
        public static AssemblyBuilder ab = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("StoreImplementations"), AssemblyBuilderAccess.RunAndSave);
        public static ModuleBuilder mb = ab.DefineDynamicModule("module", "StoreImplementations.dll");
        public static readonly MethodAttributes MethodAttributes = MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Final | MethodAttributes.SpecialName | MethodAttributes.NewSlot | MethodAttributes.HideBySig;

        public static void Dump()
        {
            ab.Save("StoreImplementations.dll");
        }
    }

    public class TypeModel
    {
        public Type RequestedType;
        public bool IsInterface;
        public bool HasIdProperty;
        public Type[] AllInterfaces;
        public PropertyModel[] Properties;
        public Type IdDeclaringInterface;
        public Type IdType;
        public FieldInfo IdField;
        public FieldInfo StoreField;
        public PropertyInfo IdProperty;
        public string NameToUseInRedis;
    }

    public class PropertyModel
    {
        public TypeModel DeclaringTypeModel { get; set; }
        public string Name { get; set; }
        public Type Type { get; set; }
        public Type DeclaringType { get; set; }
        public string NameToUseInRedis { get; set; }

        public static readonly MethodAttributes MethodAttributes = MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Final | MethodAttributes.SpecialName | MethodAttributes.NewSlot | MethodAttributes.HideBySig;

        public void ImplementProperty(TypeBuilder typeBuilder)
        {
            if (Name == "Id")
            {
                var idProperty = typeBuilder.DefineProperty("Id", PropertyAttributes.None, CallingConventions.HasThis, Type, Type.EmptyTypes);

                var getIl = Emit.BuildInstanceMethod(Type, Type.EmptyTypes, typeBuilder, "get_Id", MethodAttributes);
                getIl.LoadArgument(0);
                getIl.LoadField(DeclaringTypeModel.IdField);
                getIl.Return();

                idProperty.SetGetMethod(getIl.CreateMethod());
                return;
            }

            if (Type.IsRedisSet() || Type.IsRedisList() || Type.IsRedisHash() || Type.IsRedisSortedSet() || Type.IsRedisHyperLogLog())
            {
                var prop = typeBuilder.DefineProperty(Name, PropertyAttributes.None, CallingConventions.HasThis, Type, Type.EmptyTypes);
                var getIl = Emit.BuildInstanceMethod(Type, Type.EmptyTypes, typeBuilder, $"get_{Name}", MethodAttributes);

                var type = Type.IsRedisSet() ? typeof (RedisSet<>).MakeGenericType(Type.GenericTypeArguments[0])
                    : Type.IsRedisList() ? typeof (RedisList<>).MakeGenericType(Type.GenericTypeArguments[0])
                    : Type.IsRedisHash() ? typeof (RedisHash<,>).MakeGenericType(Type.GenericTypeArguments) 
                    : Type.IsRedisSortedSet() ? typeof(RedisSortedSet<>).MakeGenericType(Type.GenericTypeArguments)
                    : Type.IsRedisHyperLogLog() ? typeof(RedisHyperLogLog<>).MakeGenericType(Type.GenericTypeArguments)
                    : null;

                getIl.LoadConstant($"/{DeclaringTypeModel.NameToUseInRedis}/{{0}}/{NameToUseInRedis}");
                getIl.LoadArgument(0);
                getIl.LoadField(DeclaringTypeModel.IdField);

                if (DeclaringTypeModel.IdField.FieldType.IsValueType)
                {
                    getIl.Box(DeclaringTypeModel.IdField.FieldType);
                }

                getIl.Call(MethodInfos.StringFormat);
                getIl.Call(MethodInfos.StringToRedisKey);
                getIl.LoadArgument(0);
                getIl.LoadField(DeclaringTypeModel.StoreField);

                getIl.NewObject(type.GetConstructor(new[] {typeof (RedisKey), typeof (Store)}));
                getIl.Return();

                prop.SetGetMethod(getIl.CreateMethod());

                var setIl = Emit.BuildInstanceMethod(typeof (void), new[] { Type }, typeBuilder, $"set_{Name}", MethodAttributes);
                setIl.Return();
                prop.SetSetMethod(setIl.CreateMethod());
            }
            else if (Type.IsAsync())
            {
                var prop = typeBuilder.DefineProperty(Name, PropertyAttributes.None, CallingConventions.HasThis, Type, Type.EmptyTypes);
                var getIl = Emit.BuildInstanceMethod(Type, Type.EmptyTypes, typeBuilder, $"get_{Name}", MethodAttributes);
                var getMi = typeof (RedisOperations).GetMethod("GetAsyncProp").MakeGenericMethod(typeof (string), Type.GenericTypeArguments[0]);

                getIl.LoadArgument(0);
                getIl.LoadField(DeclaringTypeModel.StoreField);
                getIl.LoadConstant($"/{DeclaringTypeModel.NameToUseInRedis}/{{0}}");
                getIl.LoadArgument(0);
                getIl.LoadField(DeclaringTypeModel.IdField);

                if (DeclaringTypeModel.IdField.FieldType.IsValueType)
                {
                    getIl.Box(DeclaringTypeModel.IdField.FieldType);
                }

                getIl.Call(MethodInfos.StringFormat);
                getIl.Call(MethodInfos.StringToRedisKey);
                getIl.LoadConstant(NameToUseInRedis.Replace("Async", ""));
                getIl.Call(getMi);
                getIl.Return();

                prop.SetGetMethod(getIl.CreateMethod());

                var setMi = typeof (RedisOperations).GetMethod("SetAsyncProp").MakeGenericMethod(typeof (string), Type.GenericTypeArguments[0]);
                var setIl = Emit.BuildInstanceMethod(typeof(void), new[] { Type }, typeBuilder, $"set_{Name}", MethodAttributes);

                setIl.LoadArgument(0);
                setIl.LoadField(DeclaringTypeModel.StoreField);
                setIl.LoadConstant($"/{DeclaringTypeModel.NameToUseInRedis}/{{0}}");
                setIl.LoadArgument(0);
                setIl.LoadField(DeclaringTypeModel.IdField);

                if (DeclaringTypeModel.IdField.FieldType.IsValueType)
                {
                    setIl.Box(DeclaringTypeModel.IdField.FieldType);
                }

                setIl.Call(MethodInfos.StringFormat);
                setIl.Call(MethodInfos.StringToRedisKey);
                setIl.LoadConstant(NameToUseInRedis.Replace("Async", ""));
                setIl.LoadArgument(1);
                setIl.Call(setMi);
                setIl.Return();

                prop.SetSetMethod(setIl.CreateMethod());
            }
            else
            {
                var prop = typeBuilder.DefineProperty(Name, PropertyAttributes.None, CallingConventions.HasThis, Type, Type.EmptyTypes);

                var getIl = Emit.BuildInstanceMethod(Type, Type.EmptyTypes, typeBuilder, $"get_{Name}", MethodAttributes);

                getIl.LoadArgument(0);
                getIl.LoadField(DeclaringTypeModel.StoreField);

                getIl.LoadConstant($"/{DeclaringTypeModel.NameToUseInRedis}/{{0}}");
                getIl.LoadArgument(0);
                getIl.LoadField(DeclaringTypeModel.IdField);

                if (DeclaringTypeModel.IdField.FieldType.IsValueType)
                {
                    getIl.Box(DeclaringTypeModel.IdField.FieldType);
                }

                getIl.Call(MethodInfos.StringFormat);
                getIl.Call(MethodInfos.StringToRedisKey);

                getIl.LoadConstant(NameToUseInRedis);

                var mi = typeof (RedisOperations).GetMethod("GetHashValue").MakeGenericMethod(typeof (string), Type);
                getIl.Call(mi);

                getIl.Return();

                prop.SetGetMethod(getIl.CreateMethod());

                var setIl = Emit.BuildInstanceMethod(typeof (void), new [] { Type }, typeBuilder, $"set_{Name}", MethodAttributes);

                setIl.LoadArgument(0);
                setIl.LoadField(DeclaringTypeModel.StoreField);

                setIl.LoadConstant($"/{DeclaringTypeModel.NameToUseInRedis}/{{0}}");
                setIl.LoadArgument(0);
                setIl.LoadField(DeclaringTypeModel.IdField);

                if (DeclaringTypeModel.IdField.FieldType.IsValueType)
                {
                    setIl.Box(DeclaringTypeModel.IdField.FieldType);
                }

                setIl.Call(MethodInfos.StringFormat);
                setIl.Call(MethodInfos.StringToRedisKey);

                setIl.LoadConstant(NameToUseInRedis);
                setIl.LoadArgument(1);

                mi = typeof (RedisOperations).GetMethod("SetHashValue").MakeGenericMethod(typeof (string), Type);

                setIl.Call(mi);
                setIl.Return();

                prop.SetSetMethod(setIl.CreateMethod());
            }
        }
    }

    public static class ToRedisKey<T>
    {
        public static Lazy<Func<T, RedisKey>> Impl = new Lazy<Func<T, RedisKey>>(Implement);
        public static Func<T, RedisKey> Implement()
        {
            var il = Emit<Func<T, RedisKey>>.NewDynamicMethod();

            var implicitOrExplicitConversion = typeof(RedisKey).GetMethods().SingleOrDefault(o => o.Name.In("op_Implicit", "op_Explicit") && o.GetParameters()[0].ParameterType == typeof(T));

            if (implicitOrExplicitConversion != null)
            {
                il.LoadArgument(0);
                il.Call(implicitOrExplicitConversion);
                il.Return();
                return il.CreateDelegate();
            }

            if (TypeModel<T>.Model.IsInterface && TypeModel<T>.Model.HasIdProperty)
            {
                var typeModel = TypeModel<T>.Model;

                il.LoadConstant($"/{typeModel.IdDeclaringInterface.Name}/{{0}}");
                il.LoadArgument(0);
                il.CallVirtual(typeModel.IdDeclaringInterface.GetProperty("Id").GetGetMethod());

                if (typeModel.IdType.IsValueType)
                {
                    il.Box(typeModel.IdType);
                }

                il.Call(MethodInfos.StringFormat);
                il.Call(MethodInfos.StringToRedisKey);

                il.Return();
                return il.CreateDelegate();
            }

            return null;
        }
    }

    public static class ToRedisValue<T>
    {
        public static Lazy<Func<T, RedisValue>> Impl = new Lazy<Func<T, RedisValue>>(Implement);
        static Func<T, RedisValue> Implement()
        {
            var il = Emit<Func<T, RedisValue>>.NewDynamicMethod();
            var implicitOrExplicitConversion = typeof(RedisValue).GetMethods().SingleOrDefault(o => o.Name.In("op_Implicit", "op_Explicit") && o.GetParameters()[0].ParameterType == typeof(T));

            if (implicitOrExplicitConversion != null)
            {
                il.LoadArgument(0);
                il.Call(implicitOrExplicitConversion);
                il.Return();
                return il.CreateDelegate();
            }

            if (typeof(T).IsInterface && typeof(T).GetProperty("Id") != null)
            {
                var idProp = typeof(T).GetProperty("Id");
                var idConversion = typeof(RedisValue).GetMethods().SingleOrDefault(o => o.Name.In("op_Implicit", "op_Explicit") && o.GetParameters()[0].ParameterType == idProp.PropertyType);

                if (idConversion != null || idProp.PropertyType == typeof(RedisKey))
                {
                    var branch = il.DefineLabel();

                    il.LoadArgument(0);
                    il.LoadNull();
                    il.BranchIfEqual(branch);

                    il.LoadArgument(0);
                    il.CallVirtual(typeof(T).GetProperty("Id").GetGetMethod());
                    if (idConversion != null)
                    {
                        il.Call(idConversion);
                    }
                    else //idProp.PropertyType == typeof (RedisKey)
                    {
                        il.Call(typeof (RedisKey).GetMethods().Single(o => o.Name == "op_Implicit" && o.ReturnType == typeof (byte[])));
                        il.Call(typeof (RedisValue).GetMethods().Single(o =>o.Name == "op_Implicit" && o.GetParameters()[0].ParameterType == typeof (byte[])));
                    }
                    
                    il.Return();

                    il.MarkLabel(branch);
                    il.Call(typeof (RedisValue).GetMethod("get_Null", BindingFlags.Static | BindingFlags.Public));
                    il.Return();
                    return il.CreateDelegate();
                }
            }

            if (typeof(T) == typeof(DateTime))
            {
                il.LoadArgumentAddress(0);
                il.Call(typeof(DateTime).GetMethod("ToBinary"));
                il.Call(MethodInfos.LongToRedisValue);
                il.Return();
                return il.CreateDelegate();
            }

            if (typeof (T).IsEnum)
            {
                var underlyingType = Enum.GetUnderlyingType(typeof (T));
                var conversion = typeof(RedisValue).GetMethods().SingleOrDefault(o => o.Name.In("op_Implicit", "op_Explicit") && o.GetParameters()[0].ParameterType == underlyingType);

                if (conversion != null)
                {
                    il.LoadArgument(0);
                    il.Convert(Enum.GetUnderlyingType(typeof(T)));
                    il.Call(conversion);
                    il.Return();
                    return il.CreateDelegate();
                }
            }

            if (typeof (T) == typeof (RedisKey))
            {
                var redisKeyToByteArray = typeof (RedisKey).GetMethods().Single(o => o.Name == "op_Implicit" && o.ReturnType == typeof (byte[]));
                var byteArrayToRedisValue = typeof (RedisValue).GetMethods().Single(o => o.Name == "op_Implicit" && o.GetParameters()[0].ParameterType == typeof (byte[]));

                il.LoadArgument(0);
                il.Call(redisKeyToByteArray);
                il.Call(byteArrayToRedisValue);
                il.Return();

                return il.CreateDelegate();
            }

            if (typeof (T) == typeof (Guid))
            {
                var byteArrayToRedisValue = typeof(RedisValue).GetMethods().Single(o => o.Name == "op_Implicit" && o.GetParameters()[0].ParameterType == typeof(byte[]));

                il.LoadArgumentAddress(0);
                il.Call(typeof (Guid).GetMethod("ToByteArray"));
                il.Call(byteArrayToRedisValue);
                il.Return();
                return il.CreateDelegate();
            }

            //Otherwise, json serialize it.
            return t => (RedisValue) JSON.Serialize(t);
        }
    }

    public static class FromRedisKey<T>
    {
        public static Lazy<Func<RedisKey, T>> Impl = new Lazy<Func<RedisKey, T>>(Implement);
        private static Func<RedisKey, T> Implement()
        {
            var il = Emit<Func<RedisKey, T>>.NewDynamicMethod();

            il.LoadArgument(0);

            var implicitOrExplicitConversion = typeof(RedisKey).GetMethods().SingleOrDefault(o => o.Name.In("op_Implicit", "op_Explicit") && o.ReturnType == typeof(T));
            if (implicitOrExplicitConversion != null)
            {
                il.Call(implicitOrExplicitConversion);
            }

            il.Return();

            return il.CreateDelegate();
        }
    }

    public static class FromRedisValue<T>
    {
        public static Lazy<Func<RedisValue, Store, T>> Impl = new Lazy<Func<RedisValue, Store, T>>(Implement);

        public static Func<RedisValue, Store, T> Helper<TId>()
        {
            return (v, s) => v.IsNull ? default(T) : s.Get<T>(FromRedisValue<TId>.Impl.Value(v, s));
        }

        private static Func<RedisValue, Store, T> Implement()
        {
            var implicitOrExplicitConversion = typeof(RedisValue).GetMethods().SingleOrDefault(o => o.Name.In("op_Implicit", "op_Explicit") && o.ReturnType == typeof(T));

            if (implicitOrExplicitConversion != null)
            {
                var il = Emit<Func<RedisValue, Store, T>>.NewDynamicMethod();

                il.LoadArgument(0);
                il.Call(implicitOrExplicitConversion);
                il.Return();

                return il.CreateDelegate();
            }

            if (typeof(T).IsInterface && typeof(T).GetProperty("Id") != null)
            {
                var idProp = typeof(T).GetProperty("Id");
                var idConversion = typeof(RedisValue).GetMethods().SingleOrDefault(o => o.Name.In("op_Implicit", "op_Explicit") && o.ReturnType == idProp.PropertyType);

                if (idConversion != null || idProp.PropertyType == typeof(RedisKey))
                {
                    var method = typeof (FromRedisValue<T>).GetMethod("Helper").MakeGenericMethod(TypeModel<T>.Model.IdType);
                    return (Func<RedisValue, Store, T>)method.Invoke(null, new object[] {});
                }
            }

            if (typeof(T) == typeof(DateTime))
            {
                var il = Emit<Func<RedisValue, Store, T>>.NewDynamicMethod();

                il.LoadArgument(0);
                il.Call(MethodInfos.RedisValueToLong);
                il.Call(typeof(DateTime).GetMethod("FromBinary"));
                il.Return();
                return il.CreateDelegate();
            }

            if (typeof (T).IsEnum)
            {
                var underlyingType = Enum.GetUnderlyingType(typeof (T));
                var conversion = typeof(RedisValue).GetMethods().SingleOrDefault(o => o.Name.In("op_Implicit", "op_Explicit") && o.ReturnType == underlyingType);

                if (conversion != null)
                {
                    var il = Emit<Func<RedisValue, Store, T>>.NewDynamicMethod();

                    var branch = il.DefineLabel();

                    il.LoadArgumentAddress(0);
                    il.Call(typeof(RedisValue).GetMethod("get_IsNull"));
                    il.BranchIfTrue(branch);

                    il.LoadArgument(0);
                    il.Call(conversion);
                    il.Return();

                    il.MarkLabel(branch);
                    il.LoadConstant(0);
                    il.Return();

                    return il.CreateDelegate();
                }
            }

            if (typeof (T) == typeof (Guid))
            {
                var il = Emit<Func<RedisValue, Store, T>>.NewDynamicMethod();

                il.LoadArgument(0);
                il.Call(typeof (RedisValue).GetMethods().Single(o => o.Name == "op_Implicit" && o.ReturnType == typeof (byte[])));
                il.NewObject(typeof (Guid).GetConstructors().Single(o => o.GetParameters()[0].ParameterType == typeof (byte[])));
                il.Return();
                return il.CreateDelegate();
            }

            return (v, s) => JSON.Deserialize<T>(v);
        }
    }

    static class MethodInfos
    {
        public static MethodInfo StringFormat = typeof(string).GetMethod("Format", new[] { typeof(string), typeof(object) });
        public static MethodInfo StringToRedisKey = typeof(RedisKey).GetMethod("op_Implicit", new[] { typeof(string) });
        public static MethodInfo LongToRedisValue = typeof(RedisValue).GetMethod("op_Implicit", new[] { typeof(long) });
        public static MethodInfo RedisValueToLong = typeof(RedisValue).GetMethods().First(o => o.Name == "op_Explicit" && o.ReturnType == typeof(long));
    }

    static class Extensions
    {
        public static bool In<T>(this T t, params T[] source)
        {
            return source.Contains(t);
        }

        public static bool IsRedisSet(this Type t) => t.IsGenericType && t.GetGenericTypeDefinition() == typeof (IRedisSet<>);
        public static bool IsRedisList(this Type t) => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IRedisList<>);
        public static bool IsRedisHash(this Type t) => t.IsGenericType && t.GetGenericTypeDefinition() == typeof (IRedisHash<,>);
        public static bool IsRedisSortedSet(this Type t) => t.IsGenericType && t.GetGenericTypeDefinition() == typeof (IRedisSortedSet<>);

        public static bool IsRedisHyperLogLog(this Type t) => t.IsGenericType && t.GetGenericTypeDefinition() == typeof (IRedisHyperLogLog<>);
        public static bool IsAsync(this Type t) => t.IsGenericType && t.GetGenericTypeDefinition() == typeof (Async<>);
    }

    public static class PublicExtensions
    {
        public static TaskAwaiter<T> GetAwaiter<T>(this Async<T> toAwait)
        {
            return (toAwait.SetTask ?? toAwait.Task).GetAwaiter();
        }
    }
}