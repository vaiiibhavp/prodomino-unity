using System;
using System.Threading.Tasks;

namespace Backend
{
    /// <summary>
    /// Utility class that provides reusable delegates for sync and async operations with input and output.
    /// </summary>
    public static class TaskUtils
    {
        /// <summary>
        /// Represents an asynchronous function that takes one input and returns a result.
        /// </summary>
        public delegate Task<TOut> AsyncFunc<TIn, TOut>(TIn input);

        /// <summary>
        /// Represents an asynchronous function that takes two inputs and returns a result.
        /// </summary>
        public delegate Task<TOut> AsyncFunc<TIn1, TIn2, TOut>(TIn1 input1, TIn2 input2);
        
        
        /// <summary>
        /// Represents an asynchronous function that takes two inputs and returns a result.
        /// </summary>
        public delegate Task<TOut> AsyncFunc<TIn1, TIn2, Tin3, TOut>(TIn1 input1, TIn2 input2, TIn2 input3);

        /// <summary>
        /// Represents an asynchronous operation that takes one input and returns no value.
        /// </summary>
        public delegate Task AsyncAction<TIn>(TIn input);

        /// <summary>
        /// Represents an asynchronous operation that takes two inputs and returns no value.
        /// </summary>
        public delegate Task AsyncAction<TIn1, TIn2>(TIn1 input1, TIn2 input2);
    }
}
