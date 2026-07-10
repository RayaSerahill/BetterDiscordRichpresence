using System;
using System.Collections.Generic;

namespace BetterDiscordRichPresence.Services
{
    internal sealed class WidgetPlaceholderResolver
    {
        private readonly IReadOnlyList<WidgetPlaceholderDefinition> placeholders =
            new[]
            {
                new WidgetPlaceholderDefinition("{FCName}", context => context.FreeCompanyName),
                new WidgetPlaceholderDefinition("{FCTag}", context => context.FreeCompanyTag),
                new WidgetPlaceholderDefinition("{TTProgress}", context => context.TripleTriadProgress),
                new WidgetPlaceholderDefinition("{MountsCollected}", context => context.MountsCollected),
                new WidgetPlaceholderDefinition("{MinionsCollected}", context => context.MinionsCollected),
                new WidgetPlaceholderDefinition("{CharacterName}", context => context.CharacterName),
                new WidgetPlaceholderDefinition("{CurrentWorld}", context => context.CurrentWorld),
                new WidgetPlaceholderDefinition("{HomeWorld}", context => context.HomeWorld),
                new WidgetPlaceholderDefinition("{Location}", context => context.Location),
                new WidgetPlaceholderDefinition("{Job}", context => context.Job),
                new WidgetPlaceholderDefinition("{JobAbbreviation}", context => context.JobAbbreviation),
                new WidgetPlaceholderDefinition("{Level}", context => context.Level),
                new WidgetPlaceholderDefinition("{PartySize}", context => context.PartySize),
            };

        public WidgetUpdateRequest Resolve(
            WidgetUpdateRequest template,
            WidgetPlaceholderContext context)
            => template.Transform(value => ResolveText(value, context));

        public IReadOnlyList<WidgetPlaceholderValue> GetValues(WidgetPlaceholderContext context)
        {
            var values = new List<WidgetPlaceholderValue>(placeholders.Count);
            foreach (var placeholder in placeholders)
            {
                values.Add(new WidgetPlaceholderValue(
                    placeholder.Token,
                    placeholder.GetValue(context)));
            }

            return values;
        }

        private string ResolveText(string value, WidgetPlaceholderContext context)
        {
            foreach (var placeholder in placeholders)
            {
                value = value.Replace(
                    placeholder.Token,
                    placeholder.GetValue(context),
                    StringComparison.Ordinal);
            }

            return value;
        }
    }

    internal readonly record struct WidgetPlaceholderDefinition(
        string Token,
        Func<WidgetPlaceholderContext, string> GetValue);

    internal readonly record struct WidgetPlaceholderValue(string Token, string Value);

    internal readonly record struct WidgetPlaceholderContext(
        string FreeCompanyName,
        string FreeCompanyTag,
        string TripleTriadProgress,
        string MountsCollected,
        string MinionsCollected,
        string CharacterName,
        string CurrentWorld,
        string HomeWorld,
        string Location,
        string Job,
        string JobAbbreviation,
        string Level,
        string PartySize);
}
