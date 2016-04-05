using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Rol
{
    public class Async<T>
    {
        //So, either it's on it's way in, and it has the value to set...
        internal T SetValue;
        internal Task<T> SetTask;

        //Or it's on it's way out 
        internal Task<T> Task;

        public ConfiguredTaskAwaitable<T> ConfigureAwait(bool continueOnCapturedContext)
        {
            return (SetTask ?? Task).ConfigureAwait(continueOnCapturedContext);
        }

        public static implicit operator T(Async<T> source)
        {
            return (source.SetTask ?? source.Task).Result;
        }

        public static implicit operator Async<T>(T source)
        {
            return new Async<T> { SetValue = source };
        }

        public static implicit operator Task(Async<T> source)
        {
            return (source.SetTask ?? source.Task);
        }

        public static explicit operator Task<T>(Async<T> source)
        {
            return source.Task;
        }

        public static explicit operator Async<T>(Task<T> source)
        {
            return new Async<T> { Task = source };
        }
    }
}