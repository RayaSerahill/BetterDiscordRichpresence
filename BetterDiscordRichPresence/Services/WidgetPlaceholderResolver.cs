using System;
using System.Collections.Generic;

namespace BetterDiscordRichPresence.Services
{
    internal sealed class WidgetPlaceholderResolver
    {
        private readonly IReadOnlyDictionary<string, Func<WidgetPlaceholderContext, string>> placeholders =
            new Dictionary<string, Func<WidgetPlaceholderContext, string>>(StringComparer.Ordinal)
            {
                ["{FCName}"] = context => context.FreeCompanyName,
                ["{FCTag}"] = context => context.FreeCompanyTag,
            };

        public WidgetUpdateRequest Resolve(
            WidgetUpdateRequest template,
            WidgetPlaceholderContext context)
            => template.Transform(value => ResolveText(value, context));

        private string ResolveText(string value, WidgetPlaceholderContext context)
        {
            foreach (var placeholder in placeholders)
            {
                value = value.Replace(
                    placeholder.Key,
                    placeholder.Value(context),
                    StringComparison.Ordinal);
            }

            return value;
        }
    }

    internal readonly record struct WidgetPlaceholderContext(
        string FreeCompanyName,
        string FreeCompanyTag);
}
