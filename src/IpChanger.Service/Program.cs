using IpChanger.Service;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "IpChangerService";
});

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
