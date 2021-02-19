// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Text.Json;
using System.Text.Json.Serialization;

[assembly: JsonSerializable(typeof(object[]))]
[assembly: JsonSerializable(typeof(string), CanBeDynamic = true)]
[assembly: JsonSerializable(typeof(int), CanBeDynamic = true)]
[assembly: JsonSerializable(typeof(bool), CanBeDynamic = true)]

namespace Microsoft.JSInterop
{
    /// <summary>
    /// Abstract base class for an in-process JavaScript runtime.
    /// </summary>
    public abstract class JSInProcessRuntime : JSRuntime, IJSInProcessRuntime
    {
        internal TValue Invoke<TValue>(string identifier, long targetInstanceId, params object?[]? args)
        {
            var options = JsonSerializerContext.GetOptions();

            var resultJson = InvokeJS(
                identifier,
                JsonSerializer.Serialize(args, JsonSerializerContext),
                JSCallResultTypeHelper.FromGeneric<TValue>(),
                targetInstanceId);

            // While the result of deserialization could be null, we're making a
            // quality of life decision and letting users explicitly determine if they expect
            // null by specifying TValue? as the expected return type.
            if (resultJson is null)
            {
                return default!;
            }

            return JsonSerializer.Deserialize<TValue>(resultJson, JsonSerializerContext)!;
        }

        /// <summary>
        /// Invokes the specified JavaScript function synchronously.
        /// </summary>
        /// <typeparam name="TValue">The JSON-serializable return type.</typeparam>
        /// <param name="identifier">An identifier for the function to invoke. For example, the value <c>"someScope.someFunction"</c> will invoke the function <c>window.someScope.someFunction</c>.</param>
        /// <param name="args">JSON-serializable arguments.</param>
        /// <returns>An instance of <typeparamref name="TValue"/> obtained by JSON-deserializing the return value.</returns>
        public TValue Invoke<TValue>(string identifier, params object?[]? args)
            => Invoke<TValue>(identifier, 0, args);

        /// <summary>
        /// Performs a synchronous function invocation.
        /// </summary>
        /// <param name="identifier">The identifier for the function to invoke.</param>
        /// <param name="argsJson">A JSON representation of the arguments.</param>
        /// <returns>A JSON representation of the result.</returns>
        protected virtual string? InvokeJS(string identifier, string? argsJson)
            => InvokeJS(identifier, argsJson, JSCallResultType.Default, 0);

        /// <summary>
        /// Performs a synchronous function invocation.
        /// </summary>
        /// <param name="identifier">The identifier for the function to invoke.</param>
        /// <param name="argsJson">A JSON representation of the arguments.</param>
        /// <param name="resultType">The type of result expected from the invocation.</param>
        /// <param name="targetInstanceId">The instance ID of the target JS object.</param>
        /// <returns>A JSON representation of the result.</returns>
        protected abstract string? InvokeJS(string identifier, string? argsJson, JSCallResultType resultType, long targetInstanceId);
    }
}
