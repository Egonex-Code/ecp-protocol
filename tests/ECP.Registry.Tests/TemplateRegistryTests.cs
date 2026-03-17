// Copyright (c) 2026 Egonex S.R.L.
// SPDX-License-Identifier: Apache-2.0
// Licensed under the Apache License, Version 2.0.
// See the LICENSE file in the project root for full license information.
using ECP.Registry.Templates;
using Xunit;

namespace ECP.Registry.Tests;

public class TemplateRegistryTests
{
    [Fact]
    public void ResolvesTemplatesForMultipleLanguages()
    {
        var registry = CreateRegistry();

        Assert.True(registry.TryResolve(1, 1, "it", out var it));
        Assert.True(registry.TryResolve(1, 1, "en", out var en));
        Assert.True(registry.TryResolve(1, 1, "es", out var es));
        Assert.True(registry.TryResolve(1, 1, "de", out var de));

        Assert.Contains("Incendio", it);
        Assert.Contains("Fire", en);
        Assert.Contains("Incendio", es);
        Assert.Contains("Feuer", de);
    }

    [Fact]
    public void FallbackWhenTemplateMissing()
    {
        var registry = CreateRegistry();

        var ok = registry.TryResolve(1, 1, "fr", out _);

        Assert.False(ok);
    }

    [Fact]
    public void HasTemplateChecksVersion()
    {
        var registry = CreateRegistry();

        Assert.True(registry.HasTemplate(1, 1));
        Assert.False(registry.HasTemplate(1, 2));
        Assert.False(registry.HasTemplate(2, 1));
    }

    [Fact]
    public void TemplateHashValidation()
    {
        var registry = CreateRegistry();
        var hash = registry.TemplateHash;

        Assert.True(registry.ValidateHash(hash));
        Assert.False(registry.ValidateHash((ushort)(hash + 1)));
    }

    private static TemplateRegistry CreateRegistry()
    {
        var registry = new TemplateRegistry(templateSetId: 1, templateVersion: 1);
        registry.AddTemplate("it", "Incendio al piano {floor}. Evacuare {building}.");
        registry.AddTemplate("en", "Fire on floor {floor}. Evacuate {building}.");
        registry.AddTemplate("es", "Incendio en el piso {floor}. Evacuar {building}.");
        registry.AddTemplate("de", "Feuer im Stockwerk {floor}. Evakuieren {building}.");
        return registry;
    }
}
