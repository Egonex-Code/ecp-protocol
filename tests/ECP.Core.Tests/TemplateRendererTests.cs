// Copyright (c) 2026 Egonex S.R.L.
// SPDX-License-Identifier: Apache-2.0
// Licensed under the Apache License, Version 2.0.
// See the LICENSE file in the project root for full license information.
using ECP.Core.Registry;
using Xunit;

namespace ECP.Core.Tests;

public class TemplateRendererTests
{
    [Fact]
    public void RenderReplacesNamedParameters()
    {
        var template = "Fire on floor {floor}. Evacuate {building}.";

        var result = TemplateRenderer.Render(
            template,
            new TemplateParameter("floor", TemplateParamType.Floor, 3),
            new TemplateParameter("building", TemplateParamType.DictionaryRef, "A"));

        Assert.Equal("Fire on floor 3. Evacuate A.", result);
    }

    [Fact]
    public void RenderKeepsUnknownPlaceholders()
    {
        var template = "Fire on floor {floor} in {building}.";

        var result = TemplateRenderer.Render(
            template,
            new TemplateParameter("floor", TemplateParamType.Floor, 3));

        Assert.Equal("Fire on floor 3 in {building}.", result);
    }
}
