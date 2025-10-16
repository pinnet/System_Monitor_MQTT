using System_Monitor_MQTT;
using Microsoft.Extensions.Logging.Configuration;
using Microsoft.Extensions.Logging.EventLog;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "System Monitor MQTT Service";
});

#pragma warning disable CA1416 // Validate platform compatibility
LoggerProviderOptions.RegisterProviderOptions<
    EventLogSettings, EventLogLoggerProvider>(builder.Services);
#pragma warning restore CA1416 // Validate platform compatibility

builder.Services.AddSingleton<HWmonitorService>();
builder.Services.AddHostedService<WindowsBackgroundService>();

IHost host = builder.Build();
host.Run();
