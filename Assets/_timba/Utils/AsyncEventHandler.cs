using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;

namespace Timba.Utils
{
    public class AsyncEventHandler<TEventArgs>
    {
        private readonly List<Func<TEventArgs, UniTask>> invocationList;
        private readonly object locker;

        public AsyncEventHandler()
        {
            invocationList = new List<Func<TEventArgs, UniTask>>();
            locker = new object();
        }

        public static AsyncEventHandler<TEventArgs> operator +(AsyncEventHandler<TEventArgs> e, Func<TEventArgs, UniTask> callback)
        {
            if (callback == null) throw new NullReferenceException("callback is null");

            //Note: Thread safety issue- if two threads register to the same event (on the first time, i.e when it is null)
            //they could get a different instance, so whoever was first will be overridden.
            //A solution for that would be to switch to a public constructor and use it, but then we'll 'lose' the similar syntax to c# events             
            if (e == null) e = new AsyncEventHandler<TEventArgs>();

            lock (e.locker)
            {
                e.invocationList.Add(callback);
            }
            return e;
        }

        public static AsyncEventHandler<TEventArgs> operator -(AsyncEventHandler<TEventArgs> e, Func<TEventArgs, UniTask> callback)
        {
            if (callback == null) throw new NullReferenceException("callback is null");
            if (e == null) return null;

            lock (e.locker)
            {
                e.invocationList.Remove(callback);
            }
            return e;
        }

        public async UniTask InvokeSingleAsync(TEventArgs eventArgs)
        {
            List<Func<TEventArgs, UniTask>> tmpInvocationList;
            lock (locker)
                tmpInvocationList = new List<Func<TEventArgs, UniTask>>(invocationList);

            foreach (var callback in tmpInvocationList)
            {
                //Assuming we want a serial invocation, for a parallel invocation we can use Task.WhenAll instead
                await callback(eventArgs);
            }
        }
        
        public async UniTask InvokeAllAtTimeAsync(TEventArgs eventArgs)
        {
            List<Func<TEventArgs, UniTask>> tmpInvocationList;
            lock (locker)
                tmpInvocationList = new List<Func<TEventArgs, UniTask>>(invocationList);

            await UniTask.WhenAll(tmpInvocationList.ConvertAll(callback => callback(eventArgs)));
        }
    }
    
    public class AsyncEventReturnHandler<TEventReturn>
    {
        private readonly List<Func<UniTask<TEventReturn>>> invocationList;
        private readonly object locker;

        public AsyncEventReturnHandler()
        {
            invocationList = new();
            locker = new object();
        }

        public static AsyncEventReturnHandler<TEventReturn> operator +(AsyncEventReturnHandler<TEventReturn> e, Func<UniTask<TEventReturn>> callback)
        {
            if (callback == null) throw new NullReferenceException("callback is null");

            //Note: Thread safety issue- if two threads register to the same event (on the first time, i.e when it is null)
            //they could get a different instance, so whoever was first will be overridden.
            //A solution for that would be to switch to a public constructor and use it, but then we'll 'lose' the similar syntax to c# events             
            if (e == null) e = new AsyncEventReturnHandler<TEventReturn>();

            lock (e.locker)
            {
                e.invocationList.Add(callback);
            }
            return e;
        }

        public static AsyncEventReturnHandler<TEventReturn> operator -(AsyncEventReturnHandler<TEventReturn> e, Func<UniTask<TEventReturn>> callback)
        {
            if (callback == null) throw new NullReferenceException("callback is null");
            if (e == null) return null;

            lock (e.locker)
            {
                e.invocationList.Remove(callback);
            }
            return e;
        }

        public async UniTask<IEnumerable<TEventReturn>> InvokeSingleAsync()
        {
            var tmpInvocationList = default(List<Func<UniTask<TEventReturn>>>);
            lock (locker)
                tmpInvocationList = new List<Func<UniTask<TEventReturn>>>(invocationList);

            var returnValues = default(List<TEventReturn>);
            foreach (var callback in tmpInvocationList)
            {
                //Assuming we want a serial invocation, for a parallel invocation we can use Task.WhenAll instead
                var returnValue = await callback();
                returnValues.Add(returnValue);
            }

            return returnValues;
        }
        
        public async UniTask<IEnumerable<TEventReturn>> InvokeAllAtTimeAsync()
        {
            var tmpInvocationList = default(List<Func<UniTask<TEventReturn>>>);
            lock (locker)
                tmpInvocationList = new List<Func<UniTask<TEventReturn>>>(invocationList);

            var returnValues = await UniTask.WhenAll(tmpInvocationList.ConvertAll(callback => callback()));
            return returnValues;
        }
    }

    public class AsyncEventReturnHandler<TEventReturn, TEventArgs>
    {
        private readonly List<Func<TEventArgs, UniTask<TEventReturn>>> invocationList;
        private readonly object locker;

        public AsyncEventReturnHandler()
        {
            invocationList = new();
            locker = new object();
        }

        public static AsyncEventReturnHandler<TEventReturn, TEventArgs> operator +(AsyncEventReturnHandler<TEventReturn, TEventArgs> e, Func<TEventArgs, UniTask<TEventReturn>> callback)
        {
            if (callback == null) throw new NullReferenceException("callback is null");

            //Note: Thread safety issue- if two threads register to the same event (on the first time, i.e when it is null)
            //they could get a different instance, so whoever was first will be overridden.
            //A solution for that would be to switch to a public constructor and use it, but then we'll 'lose' the similar syntax to c# events             
            if (e == null) e = new AsyncEventReturnHandler<TEventReturn, TEventArgs>();

            lock (e.locker)
            {
                e.invocationList.Add(callback);
            }
            return e;
        }

        public static AsyncEventReturnHandler<TEventReturn, TEventArgs> operator -(AsyncEventReturnHandler<TEventReturn, TEventArgs> e, Func<TEventArgs, UniTask<TEventReturn>> callback)
        {
            if (callback == null) throw new NullReferenceException("callback is null");
            if (e == null) return null;

            lock (e.locker)
            {
                e.invocationList.Remove(callback);
            }
            return e;
        }

        public async UniTask<IEnumerable<TEventReturn>> InvokeSingleAsync(TEventArgs eventArgs)
        {
            var tmpInvocationList = default(List<Func<TEventArgs, UniTask<TEventReturn>>>);
            lock (locker)
                tmpInvocationList = new List<Func<TEventArgs, UniTask<TEventReturn>>>(invocationList);

            var returnValues = default(List<TEventReturn>);
            foreach (var callback in tmpInvocationList)
            {
                //Assuming we want a serial invocation, for a parallel invocation we can use Task.WhenAll instead
                var returnValue = await callback(eventArgs);
                returnValues.Add(returnValue);
            }

            return returnValues;
        }

        public async UniTask<IEnumerable<TEventReturn>> InvokeAllAtTimeAsync(TEventArgs eventArgs)
        {
            var tmpInvocationList = default(List<Func<TEventArgs, UniTask<TEventReturn>>>);
            lock (locker)
                tmpInvocationList = new List<Func<TEventArgs, UniTask<TEventReturn>>>(invocationList);

            var returnValues = await UniTask.WhenAll(tmpInvocationList.ConvertAll(callback => callback(eventArgs)));
            return returnValues;
        }
    }

    public static class AsyncEventExtensions
    {
        public static AsyncEventHandler<TEventArgs> AddListener<TEventArgs>(this AsyncEventHandler<TEventArgs> e, Func<TEventArgs, UniTask> callback) =>
            e + callback;

        public static AsyncEventHandler<TEventArgs> RemoveListener<TEventArgs>(this AsyncEventHandler<TEventArgs> e, Func<TEventArgs, UniTask> callback) =>
            e - callback;
        
        public static AsyncEventReturnHandler<TEventReturn> AddListener<TEventReturn>(this AsyncEventReturnHandler<TEventReturn> e, Func<UniTask<TEventReturn>> callback) =>
            e + callback;

        public static AsyncEventReturnHandler<TEventReturn> RemoveListener<TEventReturn>(this AsyncEventReturnHandler<TEventReturn> e, Func<UniTask<TEventReturn>> callback) =>
            e - callback;
    }
}
