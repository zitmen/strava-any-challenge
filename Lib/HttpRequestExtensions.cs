using Microsoft.AspNetCore.Http;

namespace Lib;

public static class HttpRequestExtensions
{
    public static async Task<HttpRequestMessage> CloneAsync(this HttpRequestMessage httpRequestMessage, CancellationToken cancellationToken)
    {
        HttpRequestMessage httpRequestMessageClone = new HttpRequestMessage(httpRequestMessage.Method, httpRequestMessage.RequestUri);

        if (httpRequestMessage.Content != null)
        {
            var ms = new MemoryStream();
            await httpRequestMessage.Content.CopyToAsync(ms, cancellationToken);
            ms.Position = 0;
            httpRequestMessageClone.Content = new StreamContent(ms);

            httpRequestMessage.Content.Headers?.ToList().ForEach(header => httpRequestMessageClone.Content.Headers.Add(header.Key, header.Value));
        }

        httpRequestMessageClone.Version = httpRequestMessage.Version;

        httpRequestMessage.Options.ToList().ForEach(props => httpRequestMessageClone.Options.Append(props));
        httpRequestMessage.Headers.ToList().ForEach(header => httpRequestMessageClone.Headers.TryAddWithoutValidation(header.Key, header.Value));

        return httpRequestMessageClone;
    }

    public static async Task<string> ReadRequestBody(this HttpRequest request)
    {
        using var sr = new StreamReader(request.Body);
        return await sr.ReadToEndAsync();
    }

    public static bool IsAuthorized(this HttpRequest request)
    {
        var auth = request.Query["x-custom-auth"];
        return string.Equals(auth, Environment.GetEnvironmentVariable("EventGridTriggerAuth"), StringComparison.OrdinalIgnoreCase);
    }
}
