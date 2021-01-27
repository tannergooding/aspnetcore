// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Connections.Experimental;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging;

namespace Microsoft.AspNetCore.Server.Kestrel.Core.Internal
{
    internal class LoggingConnectionMiddleware
    {
        private readonly ConnectionDelegate? _next;
        private readonly MultiplexedConnectionDelegate? _multiplexedNext;
        private readonly ILogger _logger;

        public LoggingConnectionMiddleware(ConnectionDelegate? next, MultiplexedConnectionDelegate? multiplexedNext, ILogger logger)
        {
            _next = next;
            _multiplexedNext = multiplexedNext;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task OnConnectionAsync(ConnectionContext context)
        {
            Debug.Assert(_next != null);

            var oldTransport = context.Transport;

            try
            {
                await using (var loggingDuplexPipe = new LoggingDuplexPipe(context.Transport, _logger))
                {
                    context.Transport = loggingDuplexPipe;

                    await _next(context);
                }
            }
            finally
            {
                context.Transport = oldTransport;
            }
        }

        public async Task OnConnectionAsync(MultiplexedConnectionContext context)
        {
            Debug.Assert(_multiplexedNext != null);

            await _multiplexedNext(new LoggingMultiplexedConnectionContext(context, _logger));
        }

        private class LoggingMultiplexedConnectionContext : MultiplexedConnectionContext
        {
            private readonly MultiplexedConnectionContext _inner;
            private readonly ILogger _logger;

            public LoggingMultiplexedConnectionContext(MultiplexedConnectionContext inner, ILogger logger)
            {
                _inner = inner;
                _logger = logger;
            }

            public override string ConnectionId { get => _inner.ConnectionId; set => _inner.ConnectionId = value; }
            public override IFeatureCollection Features => _inner.Features;
            public override IDictionary<object, object?> Items { get => _inner.Items; set => _inner.Items = value; }

            public override void Abort()
            {
                _inner.Abort();
            }

            public override void Abort(ConnectionAbortedException abortReason)
            {
                _inner.Abort(abortReason);
            }

            public override async ValueTask<ConnectionContext?> AcceptAsync(CancellationToken cancellationToken = default)
            {
                var context = await _inner.AcceptAsync(cancellationToken);
                if (context != null)
                {
                    context.Transport = new LoggingDuplexPipe(context.Transport, _logger);
                }
                return context;
            }

            public override async ValueTask<ConnectionContext> ConnectAsync(IFeatureCollection? features = null, CancellationToken cancellationToken = default)
            {
                var context = await _inner.ConnectAsync(features, cancellationToken);

                context.Transport = new LoggingDuplexPipe(context.Transport, _logger);

                return context;
            }
        }

        private class LoggingDuplexPipe : DuplexPipeStreamAdapter<LoggingStream>
        {
            public LoggingDuplexPipe(IDuplexPipe transport, ILogger logger) :
                base(transport, stream => new LoggingStream(stream, logger))
            {
            }
        }
    }
}
