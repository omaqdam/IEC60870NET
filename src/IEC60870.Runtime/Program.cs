using IEC60870.App.Codecs;
using IEC60870.Core.Abstractions;
using IEC60870.Core.Util;
using IEC60870.Runtime.Configuration;
using IEC60870.Runtime.Services;
using IEC60870.Security;
using IEC60870.Transport104.States;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

builder.Logging.AddConsole();

builder.Services.Configure<RuntimeOptions>(builder.Configuration.GetSection(RuntimeOptions.SectionName));

builder.Services.AddSingleton<IAsduSerializer, AsduCodecRegistry>();
builder.Services.AddSingleton<ISystemClock, SystemClock>();

builder.Services.AddSingleton(provider =>
{
    var runtime = provider.GetRequiredService<IOptions<RuntimeOptions>>().Value;
    return new ApciOptions
    {
        T1 = TimeSpan.FromMilliseconds(runtime.Transport104.T1Milliseconds),
        T2 = TimeSpan.FromMilliseconds(runtime.Transport104.T2Milliseconds),
        T3 = TimeSpan.FromMilliseconds(runtime.Transport104.T3Milliseconds),
        K = runtime.Transport104.KWindow,
        W = runtime.Transport104.WWindow
    };
});

builder.Services.AddSingleton(provider =>
{
    var runtime = provider.GetRequiredService<IOptions<RuntimeOptions>>().Value;
    return new TlsClientOptions
    {
        Enabled = runtime.Security?.EnableTls ?? false,
        TargetHost = runtime.Security?.TargetHost ?? string.Empty
    };
});

builder.Services.AddSingleton(provider =>
{
    var serializer = provider.GetRequiredService<IAsduSerializer>();
    var clock = provider.GetRequiredService<ISystemClock>();
    var apciOptions = provider.GetRequiredService<ApciOptions>();
    var tlsOptions = provider.GetRequiredService<TlsClientOptions>();
    var loggerFactory = provider.GetService<ILoggerFactory>();
    return new Transport104Client(serializer, clock, apciOptions, tlsOptions, loggerFactory);
});

builder.Services.AddHostedService<Worker>();

await builder.Build().RunAsync();
