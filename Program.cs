namespace OrchestrationInputBindingRepro;

using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask;
using Microsoft.Azure.Functions.Worker.Extensions.DurableTask;

public static class Program
{
    public static void Main()
    {
        var host = new HostBuilder()
            .ConfigureFunctionsWebApplication(
                // breaks orchestration input binding
                builder => new DurableTaskExtensionStartup().Configure(builder) 
            )
            .ConfigureServices(services =>
            {
                services.AddHttpClient();

                services.Configure<KestrelServerOptions>(options =>
                {
                    options.AllowSynchronousIO = true;
                });
            })
            .Build();

        host.Run();
    }
}

public static class MyFunction
{
    [Function(nameof(MyFunction))]
    public static async Task<List<string>> RunOrchestrator(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var outputs = new List<string>
        {
            // Replace name and input with values relevant for your Durable Functions Activity
            await context.CallActivityAsync<string>(nameof(SayHello), "Tokyo"),
            await context.CallActivityAsync<string>(nameof(SayHello), "Seattle"),
            await context.CallActivityAsync<string>(nameof(SayHello), "London")
        };

        return outputs;
    }

    [Function(nameof(SayHello))]
    public static string SayHello([ActivityTrigger] string name, FunctionContext executionContext)
    {
        return $"Hello {name}!";
    }

    [Function("MyFunction_HttpStart")]
    public static async Task<HttpResponseData> HttpStart(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req,
        [DurableClient] DurableTaskClient client,
        FunctionContext executionContext)
    {
        // Function input comes from the request content.
        string instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
            nameof(MyFunction));

        return client.CreateCheckStatusResponse(req, instanceId);
    }
}