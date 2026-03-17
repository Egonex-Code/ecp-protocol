// Copyright (c) 2026 Egonex S.R.L.
// SPDX-License-Identifier: Apache-2.0
// Licensed under the Apache License, Version 2.0.
// See the LICENSE file in the project root for full license information.
using ECP.Transport.Abstractions;

namespace ECP.Transport.Abstractions.Tests;

public class IEcpTransportTests
{
    [Fact]
    public void InterfaceExtendsIAsyncDisposable()
    {
        var interfaces = typeof(IEcpTransport).GetInterfaces();
        Assert.Contains(typeof(IAsyncDisposable), interfaces);
    }
}
