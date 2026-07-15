namespace ArkCloud.Tests.Component.TestSupport;

/// <summary>
/// Stands in for ArkCloud.API so component tests exercise the real AuthApiClient /
/// JwtAuthenticationStateProvider stack without a real HTTP call. Tests reassign
/// <see cref="Responder"/> to control what the "server" returns.
/// </summary>
public class FakeHttpMessageHandler : HttpMessageHandler
{
    public Func<HttpRequestMessage, HttpResponseMessage> Responder { get; set; }
        = _ => new HttpResponseMessage(System.Net.HttpStatusCode.ServiceUnavailable);

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
        => Task.FromResult(Responder(request));
}
