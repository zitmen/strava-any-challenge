using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Newtonsoft.Json;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;

namespace Sync.StravaSync
{
    public sealed class WebhooksVerification : IDisposable
    {
        private readonly IMongoClient _mongoClient;
        private readonly HttpClient _httpClient;
        private readonly ILogger _logger;

        public WebhooksVerification(IMongoClient mongoClient, IHttpClientFactory httpClientFactory, ILogger logger)
        {
            _mongoClient = mongoClient;
            _httpClient = httpClientFactory.CreateClient();
            _logger = logger;
        }

        public void Dispose()
        {
            _httpClient.Dispose();
        }

        [FunctionName(nameof(VerifyWebhook))]
        public HttpResponseMessage VerifyWebhook(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "Webhook")] HttpRequest request)
        {
            var subscriptionToken = Environment.GetEnvironmentVariable("StravaWebhookSubscriptionToken");
            if (request.Query["hub.mode"] != "subscribe" || request.Query["hub.verify_token"] != subscriptionToken)
                return new HttpResponseMessage(HttpStatusCode.BadRequest);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    JsonConvert.SerializeObject(new StravaWebhookResponse(request.Query["hub.challenge"])),
                    Encoding.UTF8, "application/json")
            };
        }

        internal static bool TryValidateWebhookSubscriptionRequest(IHeaderDictionary requestHeaders, string requestBody, [NotNullWhen(returnValue: true)] out IActionResult? response)
        {
            if (!string.Equals(requestHeaders["aeg-event-type"], "SubscriptionValidation", StringComparison.OrdinalIgnoreCase))
            {
                response = null;
                return false;
            }

            var @event = JsonConvert.DeserializeObject<EventGridWebhook<EventGridSubscriptionValidation>[]>(requestBody)!.Single();
            if (!string.Equals(@event.eventType, "Microsoft.EventGrid.SubscriptionValidationEvent", StringComparison.OrdinalIgnoreCase))
            {
                response = new BadRequestResult();
                return true;
            }

            response = new OkObjectResult(new { validationResponse = @event.data.validationCode });
            return true;
        }
    }

    public class StravaWebhookResponse
    {
        [JsonProperty(PropertyName = "hub.challenge")]
        public string HubChallenge { get; }

        public StravaWebhookResponse(string hubChallenge)
        {
            HubChallenge = hubChallenge;
        }
    }

    public record EventGridWebhook<T>(
        string id,
        string topic,
        string subject,
        T data,
        string eventType,
        DateTime eventTime,
        string metadataVersion,
        string dataVersion);

    public record EventGridSubscriptionValidation(string validationCode);
}
