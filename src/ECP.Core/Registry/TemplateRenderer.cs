// Copyright (c) 2026 Egonex S.R.L.
// SPDX-License-Identifier: Apache-2.0
// Licensed under the Apache License, Version 2.0.
// See the LICENSE file in the project root for full license information.
using System.Globalization;
using System.Text;

namespace ECP.Core.Registry;

/// <summary>
/// Renders templates using typed parameters.
/// </summary>
public static class TemplateRenderer
{
    /// <summary>
    /// Renders a template by replacing placeholders with parameter values.
    /// </summary>
    public static string Render(string template, params TemplateParameter[] parameters)
    {
        return Render(template, (IReadOnlyList<TemplateParameter>)parameters);
    }

    /// <summary>
    /// Renders a template by replacing placeholders with parameter values.
    /// </summary>
    public static string Render(string template, IReadOnlyList<TemplateParameter> parameters)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(parameters);

        if (parameters.Count == 0 || template.Length == 0)
        {
            return template;
        }

        var map = new Dictionary<string, TemplateParameter>(StringComparer.OrdinalIgnoreCase);
        foreach (var parameter in parameters)
        {
            map[parameter.Name] = parameter;
        }

        var builder = new StringBuilder(template.Length + 16);
        for (var i = 0; i < template.Length; i++)
        {
            var ch = template[i];
            if (ch != '{')
            {
                builder.Append(ch);
                continue;
            }

            var end = template.IndexOf('}', i + 1);
            if (end <= i + 1)
            {
                builder.Append(ch);
                continue;
            }

            var name = template.Substring(i + 1, end - i - 1);
            if (!map.TryGetValue(name, out var parameter))
            {
                builder.Append(template, i, end - i + 1);
                i = end;
                continue;
            }

            builder.Append(FormatValue(parameter));
            i = end;
        }

        return builder.ToString();
    }

    private static string FormatValue(TemplateParameter parameter)
    {
        if (parameter.Value is null)
        {
            return string.Empty;
        }

        return parameter.Type switch
        {
            TemplateParamType.Number => FormatInvariant(parameter.Value),
            TemplateParamType.GeoZone => FormatInvariant(parameter.Value),
            TemplateParamType.Floor => FormatInvariant(parameter.Value),
            TemplateParamType.Stairway => FormatInvariant(parameter.Value),
            TemplateParamType.DictionaryRef => FormatInvariant(parameter.Value),
            TemplateParamType.Gate => FormatInvariant(parameter.Value),
            TemplateParamType.Sector => FormatInvariant(parameter.Value),
            _ => FormatInvariant(parameter.Value)
        };
    }

    private static string FormatInvariant(object value)
    {
        return value is IFormattable formattable
            ? formattable.ToString(null, CultureInfo.InvariantCulture)
            : value.ToString() ?? string.Empty;
    }
}
