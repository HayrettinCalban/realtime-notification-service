using Grpc.Core;
using Microsoft.AspNetCore.SignalR;
using NotificationService.Grpc;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog((ctx, services, log) => log.ReadFrom.Configuration(ctx.Configuration).Enrich.FromLogContext().WriteTo.Console());
builder.WebHost.ConfigureKestrel(options => {
 options.ListenAnyIP(5000, o => o.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1);
 options.ListenAnyIP(5001, o => o.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2);
});
builder.Services.AddSignalR(); builder.Services.AddGrpc();
var app = builder.Build(); app.UseSerilogRequestLogging();
app.MapGet("/", () => "Notification service is running. SignalR: /hub; gRPC: port 5001.");
app.MapHub<NotificationHub>("/hub"); app.MapGrpcService<NotificationGatewayService>();
app.Run();
public class NotificationHub : Hub {
 // Demo only: clients choose a group. Real applications must bind groups to authenticated identities.
 public Task Join(string userId) => Groups.AddToGroupAsync(Context.ConnectionId, userId);
}
public class NotificationGatewayService(IHubContext<NotificationHub> hub, ILogger<NotificationGatewayService> logger) : NotificationGateway.NotificationGatewayBase {
 public override async Task<NotificationReply> Send(NotificationRequest request, ServerCallContext context) {
  if (string.IsNullOrWhiteSpace(request.UserId) || string.IsNullOrWhiteSpace(request.Message)) throw new RpcException(new Status(StatusCode.InvalidArgument, "user_id and message are required"));
  await hub.Clients.Group(request.UserId).SendAsync("ReceiveNotification", request.Message, context.CancellationToken);
  logger.LogInformation("Notification sent to group {UserId}", request.UserId);
  return new NotificationReply { Accepted = true };
 }
}
