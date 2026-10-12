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
    .WithRequestFilters(f => f.AddCallToolFilter(vseUOAware.RulesGate.Filter()))
    .WithTools<RandomNumberTools>()
    .WithTools<FileTools>()
    .WithTools<MapTools>()
    .WithTools<MapAnalysisTools>()
    .WithTools<MapIntegrityTools>()
    .WithTools<MapDiffTools>()
    .WithTools<MapStaticsTools>()
    .WithTools<MapExportTools>()
    .WithTools<WorldMapExportTools>()
    .WithTools<MapPlacementTools>()
    .WithTools<SnippetTools>()
    .WithTools<VisualTools>()
    .WithTools<SemanticTools>()
    .WithTools<VisualRenderTools>()
    .WithTools<SoundTools>()
    .WithTools<AnimationTools>()
    .WithTools<LightTools>()
    .WithTools<AnimDataTools>()
    .WithTools<BinaryAnalysisTools>()
    .WithTools<MapEditorTools>()
    .WithTools<TransitionFitTools>()
    .WithTools<MapCreatorXmlTools>()
    .WithTools<MissingTransitionTools>()
    .WithTools<TransitionRepairTools>()
    .WithTools<TerrainEditTools>()
    .WithTools<TransitionCreateTools>()
    .WithTools<TransitionAuditTools>()
    .WithTools<CommonEditTools>()
    .WithTools<MapCreatorXmlGuideTools>()
    .WithTools<SourceAnalysisTools>()
    .WithTools<SourceBitmapTools>()
    .WithTools<WaterTools>()
    .WithTools<TileRoleTools>();

await builder.Build().RunAsync();
