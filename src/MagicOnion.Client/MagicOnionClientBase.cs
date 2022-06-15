using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using MagicOnion.Client.Client.Internal;

namespace MagicOnion.Client
{
    public readonly struct MagicOnionClientOptions
    {
        public string Host { get; }
        public CallInvoker CallInvoker { get; }
        public IReadOnlyList<IClientFilter> Filters { get; }
        public CallOptions CallOptions { get; }

        public MagicOnionClientOptions(CallInvoker callInvoker, string host, CallOptions callOptions, IReadOnlyList<IClientFilter> filters)
        {
            Host = host;
            CallOptions = callOptions;
            CallInvoker = callInvoker ?? throw new ArgumentNullException(nameof(callInvoker));
            Filters = filters ?? Array.Empty<IClientFilter>();
        }

        public MagicOnionClientOptions WithCallOptions(CallOptions callOptions)
            => new MagicOnionClientOptions(CallInvoker, Host, callOptions, Filters);
        public MagicOnionClientOptions WithHost(string host)
            => new MagicOnionClientOptions(CallInvoker, host, CallOptions, Filters);
        public MagicOnionClientOptions WithFilters(IReadOnlyList<IClientFilter> filters)
            => new MagicOnionClientOptions(CallInvoker, Host, CallOptions, filters);
    }

    public class MagicOnionClientBase
    {
        protected internal MagicOnionClientOptions Options { get; }

        protected MagicOnionClientBase(MagicOnionClientOptions options)
        {
            if (options.CallOptions.Headers == null && options.Filters.Count != 0)
            {
                // always creating new Metadata is bad manner for performance
                options = options.WithCallOptions(options.CallOptions.WithHeaders(new Metadata()));
            }

            Options = options;
        }

        protected internal UnaryResult<TResponse> InvokeAsync<TRequest, TResponse>(string path, TRequest request, Func<RequestContext, ResponseContext> requestMethod)
        {
            var future = InvokeAsyncCore<TRequest, TResponse>(path, request, requestMethod);
            return new UnaryResult<TResponse>(future);
        }

        async Task<IResponseContext<TResponse>> InvokeAsyncCore<TRequest, TResponse>(string path, TRequest request, Func<RequestContext, ResponseContext> requestMethod)
        {
            var requestContext = new RequestContext<TRequest>(request, this, path, Options.CallOptions, typeof(TResponse), Options.Filters, requestMethod);
            var response = await InterceptInvokeHelper.InvokeWithFilter(requestContext);
            var result = response as IResponseContext<TResponse>;
            if (result != null)
            {
                return result;
            }
            else
            {
                throw new InvalidOperationException("ResponseContext is null.");
            }
        }

    }

    public abstract class MagicOnionClientBase<T> : MagicOnionClientBase
        where T : IService<T>
    {
        protected MagicOnionClientBase(MagicOnionClientOptions options)
            : base(options)
        {
        }

        protected abstract MagicOnionClientBase<T> Clone(MagicOnionClientOptions options);

        public virtual T WithCancellationToken(CancellationToken cancellationToken)
            => WithOptions(Options.CallOptions.WithCancellationToken(cancellationToken));

        public virtual T WithDeadline(DateTime deadline)
            => WithOptions(Options.CallOptions.WithDeadline(deadline));

        public virtual T WithHeaders(Metadata headers)
            => WithOptions(Options.CallOptions.WithHeaders(headers));

        public virtual T WithHost(string host)
            => (T)(object)Clone(Options.WithHost(host));

        public virtual T WithOptions(CallOptions options)
            => (T)(object)Clone(Options.WithCallOptions(options));
    }
}