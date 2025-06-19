using System.Net.Http;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register HttpClient for proxying
builder.Services.AddHttpClient();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Hardcoded service URLs for demo (replace with env/config in prod)
const string ordersService = "http://localhost:5222";
const string paymentsService = "http://localhost:5075";

// Proxy /orders/* to OrdersService
app.MapMethods("/orders/{**catchall}", new[] { "GET", "POST", "PUT", "DELETE" }, async (HttpContext context, IHttpClientFactory httpClientFactory, string catchall) =>
{
    var client = httpClientFactory.CreateClient();
    var targetUri = $"{ordersService}/orders/{catchall}".TrimEnd('/');
    var request = context.Request;
    var proxiedRequest = new HttpRequestMessage(new HttpMethod(request.Method), targetUri);
    if (request.ContentLength > 0)
        proxiedRequest.Content = new StreamContent(request.Body);
    foreach (var header in request.Headers)
        proxiedRequest.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
    var response = await client.SendAsync(proxiedRequest, HttpCompletionOption.ResponseHeadersRead, context.RequestAborted);
    context.Response.StatusCode = (int)response.StatusCode;
    foreach (var header in response.Headers)
        context.Response.Headers[header.Key] = header.Value.ToArray();
    foreach (var header in response.Content.Headers)
        context.Response.Headers[header.Key] = header.Value.ToArray();
    context.Response.Headers.Remove("transfer-encoding");
    await response.Content.CopyToAsync(context.Response.Body);
});

// Proxy /accounts/* to PaymentsService
app.MapMethods("/accounts/{**catchall}", new[] { "GET", "POST", "PUT", "DELETE" }, async (HttpContext context, IHttpClientFactory httpClientFactory, string catchall) =>
{
    var client = httpClientFactory.CreateClient();
    var targetUri = $"{paymentsService}/accounts/{catchall}".TrimEnd('/');
    var request = context.Request;
    var proxiedRequest = new HttpRequestMessage(new HttpMethod(request.Method), targetUri);
    if (request.ContentLength > 0)
        proxiedRequest.Content = new StreamContent(request.Body);
    foreach (var header in request.Headers)
        proxiedRequest.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
    var response = await client.SendAsync(proxiedRequest, HttpCompletionOption.ResponseHeadersRead, context.RequestAborted);
    context.Response.StatusCode = (int)response.StatusCode;
    foreach (var header in response.Headers)
        context.Response.Headers[header.Key] = header.Value.ToArray();
    foreach (var header in response.Content.Headers)
        context.Response.Headers[header.Key] = header.Value.ToArray();
    context.Response.Headers.Remove("transfer-encoding");
    await response.Content.CopyToAsync(context.Response.Body);
});

app.Run();

public partial class Program { }
