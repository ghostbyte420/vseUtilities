using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.AddConsole(consoleLogOptions =>
{
    // Configure all logs to go to stderr since stdout is reserved for MCP protocol messages.
    consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
});

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithRequestFilters(f => f.AddCallToolFilter(vseAgentRules.Rules.RulesGate.Filter(
        "get_agent_rules", "get_standing_operating_procedure", "get_rules_gate_status")))
    .WithToolsFromAssembly();

await builder.Build().RunAsync();
