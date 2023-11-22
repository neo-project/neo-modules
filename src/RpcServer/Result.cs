using System;
namespace Neo.Plugins
{
    public static class Result
    {
        /// <summary>
        /// Checks the execution result of a function and throws an exception if it is null or throw an exception.
        /// </summary>
        /// <param name="function">The function to execute</param>
        /// <param name="err">The rpc error</param>
        /// <typeparam name="T">The return type</typeparam>
        /// <returns>The execution result</returns>
        /// <exception cref="RpcException">The Rpc exception</exception>
        public static T Ok_Or<T>(this Func<T> function, RpcError err, bool withData = false)
        {
            try
            {
                var result = function();
                if (result == null) throw new RpcException(err);
                return result;
            }
            catch (Exception ex)
            {
                if (withData)
                    throw new RpcException(err.WithData(ex.GetBaseException().Message));
                throw new RpcException(err);
            }
        }

        /// <summary>
        /// The execution result is true or throws an exception or null.
        /// </summary>
        /// <param name="function"></param>
        /// <param name="err"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        /// <exception cref="RpcException"></exception>
        public static T True_Or<T>(this Func<T> function, RpcError err)
        {
            try
            {
                var result = function();
                if (result == null || !result.Equals(true)) throw new RpcException(err);
                return result;
            }
            catch
            {
                throw new RpcException(err);
            }
        }
    }
}
