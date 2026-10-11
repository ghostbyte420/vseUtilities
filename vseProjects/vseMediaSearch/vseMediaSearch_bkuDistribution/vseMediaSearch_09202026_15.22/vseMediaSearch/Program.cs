using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using vseMediaSearch.Services;
using vseMediaSearch.Tools;

var builder = Host.CreateApplicationBuilder(args);

// Configure all logs to go to stderr (stdout is used for the MCP protocol messages).
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

// Add the MCP services: the transport to use (stdio) and the tools to register.
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<ArticleSearchTools>()
    .WithTools<VideoLearningTools>()
    .WithTools<KnowledgeSearchTools>()
    .WithTools<DiagnosticsTools>();

DiagnosticLog.Write("server.start", $"assembly={typeof(DiagnosticsTools).Assembly.Location} build={typeof(DiagnosticsTools).Module.ModuleVersionId}");
try
{
    await builder.Build().RunAsync();
}
finally
{
    DiagnosticLog.Write("server.stop");
}
