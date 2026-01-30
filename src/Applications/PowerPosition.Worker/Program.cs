using PowerPosition.Worker;
using PowerPosition.Worker.Services;
using Axpo;
using System.Reflection;
using System.Diagnostics.CodeAnalysis;

namespace PowerPosition.Worker;

[ExcludeFromCodeCoverage]
public class Program
{
    public static void Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        builder.Services.AddSingleton<Axpo.PowerService>();

        builder.Services.AddSingleton<IPowerService>(sp =>
            new ResilientPowerService(
                sp.GetRequiredService<Axpo.PowerService>(),
                sp.GetRequiredService<ILogger<ResilientPowerService>>()
            ));
        builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));

        builder.Services.AddHostedService<Worker>();

        var host = builder.Build();
        host.Run();
    }
}
