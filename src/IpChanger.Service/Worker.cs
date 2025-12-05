using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;
using IpChanger.Common;

namespace IpChanger.Service;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private const string PipeName = "IpChangerPipe";

    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("IpChanger Service starting...");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Create pipe with security settings to allow clients to connect
                var pipeSecurity = new PipeSecurity();
                var id = new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null);
                pipeSecurity.AddAccessRule(new PipeAccessRule(id, PipeAccessRights.ReadWrite, AccessControlType.Allow));

                // Don't use 'await using' here - let ProcessConnectionAsync own the disposal
                var serverStream = NamedPipeServerStreamAcl.Create(
                    PipeName,
                    PipeDirection.InOut,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous,
                    0,
                    0,
                    pipeSecurity);

                _logger.LogInformation("Waiting for connection...");
                await serverStream.WaitForConnectionAsync(stoppingToken);

                _logger.LogInformation("Client connected.");

                // Handle connection in a background task to allow accepting new connections immediately
                // ProcessConnectionAsync will dispose the stream when done
                _ = ProcessConnectionAsync(serverStream, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in connection listener loop.");
                await Task.Delay(1000, stoppingToken);
            }
        }
    }

    private async Task ProcessConnectionAsync(NamedPipeServerStream serverStream, CancellationToken stoppingToken)
    {
        await using (serverStream)
        {
            try
            {
                using var reader = new StreamReader(serverStream);
                using var writer = new StreamWriter(serverStream) { AutoFlush = true };

                var line = await reader.ReadLineAsync(stoppingToken);
                if (line != null)
                {
                    IpConfigResponse response;
                    try
                    {
                        var request = JsonSerializer.Deserialize<IpConfigRequest>(line);
                        if (request != null)
                        {
                            // _logger.LogInformation($"Processing request for Adapter: {request.AdapterId}");
                            response = IpHelper.ApplyConfig(request);
                        }
                        else
                        {
                            response = new IpConfigResponse { Success = false, Message = "Invalid request format." };
                        }
                    }
                    catch (Exception ex)
                    {
                        response = new IpConfigResponse { Success = false, Message = $"Error: {ex.Message}" };
                    }

                    await writer.WriteLineAsync(JsonSerializer.Serialize(response));
                    await writer.FlushAsync();
                    // Give client time to read before closing pipe
                    await Task.Delay(100, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                // Log error but don't crash the service
                // _logger.LogError(ex, "Error processing client request.");
            }
        }
    }

}
