using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace BetterDiscordRichPresence.Services
{
    internal sealed class WidgetService : IDisposable
    {
        private readonly HttpClient httpClient = new();

        public async Task<WidgetUpdateResult> UpdateAsync(
            WidgetUpdateRequest widget,
            CancellationToken cancellationToken)
        {
            var endpoint =
                $"https://discord.com/api/v9/applications/{Uri.EscapeDataString(widget.ApplicationId)}/" +
                $"users/{Uri.EscapeDataString(widget.UserId)}/identities/0/profile";

            var payload = new
            {
                data = new
                {
                    @dynamic = new object[]
                    {
                        TextField("title", widget.Title),
                        TextField("description", widget.Description),
                        TextField("description2", widget.Description2),
                        TextField("description3", widget.Description3),
                        TextField("profile_title", widget.MiniProfileText),
                        ImageField("main_image", widget.MainImageUrl),
                        ImageField("profile_icon", widget.ProfileIconUrl),
                        TextField("stat1_title", widget.Stat1Value),
                        TextField("stat1_label", widget.Stat1Label),
                        TextField("stat2_title", widget.Stat2Value),
                        TextField("stat2_label", widget.Stat2Label),
                        TextField("stat3_title", widget.Stat3Value),
                        TextField("stat3_label", widget.Stat3Label),
                        TextField("stat4_title", widget.Stat4Value),
                        TextField("stat4_label", widget.Stat4Label),
                        TextField("stat5_title", widget.Stat5Value),
                        TextField("stat5_label", widget.Stat5Label),
                        TextField("stat6_title", widget.Stat6Value),
                        TextField("stat6_label", widget.Stat6Label),
                    },
                },
            };

            using var request = new HttpRequestMessage(HttpMethod.Patch, endpoint);
            request.Headers.TryAddWithoutValidation("Authorization", $"Bot {widget.BotToken}");
            request.Headers.TryAddWithoutValidation(
                "User-Agent",
                "DiscordBot (https://github.com/discord/discord-api-docs, 1.0.0)");
            request.Content = new StringContent(
                JsonConvert.SerializeObject(payload),
                Encoding.UTF8,
                "application/json");

            try
            {
                using var response = await httpClient.SendAsync(request, cancellationToken);
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

                if (response.IsSuccessStatusCode)
                    return new WidgetUpdateResult(true, "Widget updated successfully.");

                var detail = string.IsNullOrWhiteSpace(responseBody)
                    ? response.ReasonPhrase ?? "No response body"
                    : responseBody;

                return new WidgetUpdateResult(
                    false,
                    $"Discord returned HTTP {(int)response.StatusCode}: {detail}");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return new WidgetUpdateResult(false, "Widget update was cancelled.");
            }
            catch (Exception ex)
            {
                Plugin.Log.Error(ex, "Failed to update the Discord widget.");
                return new WidgetUpdateResult(false, $"Widget update failed: {ex.Message}");
            }
        }

        public void Dispose()
        {
            httpClient.Dispose();
        }

        private static object TextField(string name, string value)
            => new
            {
                type = 1,
                name,
                value,
            };

        private static object ImageField(string name, string url)
            => new
            {
                type = 3,
                name,
                value = new
                {
                    url,
                },
            };
    }

    internal sealed class WidgetUpdateRequest
    {
        public string ApplicationId { get; init; } = string.Empty;
        public string BotToken { get; init; } = string.Empty;
        public string UserId { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public string Description2 { get; init; } = string.Empty;
        public string Description3 { get; init; } = string.Empty;
        public string MiniProfileText { get; init; } = string.Empty;
        public string MainImageUrl { get; init; } = string.Empty;
        public string ProfileIconUrl { get; init; } = string.Empty;
        public string Stat1Value { get; init; } = string.Empty;
        public string Stat1Label { get; init; } = string.Empty;
        public string Stat2Value { get; init; } = string.Empty;
        public string Stat2Label { get; init; } = string.Empty;
        public string Stat3Value { get; init; } = string.Empty;
        public string Stat3Label { get; init; } = string.Empty;
        public string Stat4Value { get; init; } = string.Empty;
        public string Stat4Label { get; init; } = string.Empty;
        public string Stat5Value { get; init; } = string.Empty;
        public string Stat5Label { get; init; } = string.Empty;
        public string Stat6Value { get; init; } = string.Empty;
        public string Stat6Label { get; init; } = string.Empty;

        public static WidgetUpdateRequest FromConfiguration(Configuration configuration)
            => new()
            {
                ApplicationId = (configuration.WidgetApplicationId ?? string.Empty).Trim(),
                BotToken = (configuration.WidgetBotToken ?? string.Empty).Trim(),
                UserId = (configuration.WidgetUserId ?? string.Empty).Trim(),
                Title = configuration.WidgetTitle ?? string.Empty,
                Description = configuration.WidgetDescription ?? string.Empty,
                Description2 = configuration.WidgetDescription2 ?? string.Empty,
                Description3 = configuration.WidgetDescription3 ?? string.Empty,
                MiniProfileText = configuration.WidgetMiniProfileText ?? string.Empty,
                MainImageUrl = configuration.WidgetMainImageUrl ?? string.Empty,
                ProfileIconUrl = configuration.WidgetProfileIconUrl ?? string.Empty,
                Stat1Value = configuration.WidgetStat1Value ?? string.Empty,
                Stat1Label = configuration.WidgetStat1Label ?? string.Empty,
                Stat2Value = configuration.WidgetStat2Value ?? string.Empty,
                Stat2Label = configuration.WidgetStat2Label ?? string.Empty,
                Stat3Value = configuration.WidgetStat3Value ?? string.Empty,
                Stat3Label = configuration.WidgetStat3Label ?? string.Empty,
                Stat4Value = configuration.WidgetStat4Value ?? string.Empty,
                Stat4Label = configuration.WidgetStat4Label ?? string.Empty,
                Stat5Value = configuration.WidgetStat5Value ?? string.Empty,
                Stat5Label = configuration.WidgetStat5Label ?? string.Empty,
                Stat6Value = configuration.WidgetStat6Value ?? string.Empty,
                Stat6Label = configuration.WidgetStat6Label ?? string.Empty,
            };

        public WidgetUpdateRequest Transform(Func<string, string> transform)
            => new()
            {
                ApplicationId = transform(ApplicationId),
                BotToken = transform(BotToken),
                UserId = transform(UserId),
                Title = transform(Title),
                Description = transform(Description),
                Description2 = transform(Description2),
                Description3 = transform(Description3),
                MiniProfileText = transform(MiniProfileText),
                MainImageUrl = transform(MainImageUrl),
                ProfileIconUrl = transform(ProfileIconUrl),
                Stat1Value = transform(Stat1Value),
                Stat1Label = transform(Stat1Label),
                Stat2Value = transform(Stat2Value),
                Stat2Label = transform(Stat2Label),
                Stat3Value = transform(Stat3Value),
                Stat3Label = transform(Stat3Label),
                Stat4Value = transform(Stat4Value),
                Stat4Label = transform(Stat4Label),
                Stat5Value = transform(Stat5Value),
                Stat5Label = transform(Stat5Label),
                Stat6Value = transform(Stat6Value),
                Stat6Label = transform(Stat6Label),
            };
    }

    internal readonly record struct WidgetUpdateResult(bool Success, string Message);
}
