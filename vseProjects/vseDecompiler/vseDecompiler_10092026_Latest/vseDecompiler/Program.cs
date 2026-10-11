using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

// Configure all logs to go to stderr (stdout is used for the MCP protocol messages).
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

// Add the MCP services: the transport to use (stdio) and the tools to register.
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithRequestFilters(f => f.AddCallToolFilter(vseDecompiler.RulesGate.Filter()))
    .WithTools<DecompilerInfoTools>()
    .WithTools<InventoryTools>()
    .WithTools<DecompileTools>()
    .WithTools<BundleTools>();

await builder.Build().RunAsync();
