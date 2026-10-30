using Grpc.Core;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.IdentityModel.Tokens;
using NotificationService.Grpc;
using Serilog;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt__Key is required");
var grpcKey = builder.Configuration["Grpc:ApiKey"] ?? throw new InvalidOperationException("Grpc__ApiKey is required");
if (Encoding.UTF8.GetByteCount(jwtKey) < 32 || Encoding.UTF8.GetByteCount(grpcKey) < 32) throw new InvalidOperationException("Both keys must be at least 32 bytes");
builder.Host.UseSerilog((ctx, services, log) => {
    log.ReadFrom.Configuration(ctx.Configuration).Enrich.FromLogContext().WriteTo.Console();
    var seq = ctx.Configuration["Seq:Url"];
    if (!string.IsNullOrWhiteSpace(seq)) log.WriteTo.Seq(seq, apiKey: ctx.Configuration["Seq:ApiKey"]);
});
builder.WebHost.ConfigureKestrel(options => {
 options.ListenAnyIP(5000, o => o.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1);
 options.ListenAnyIP(5001, o => o.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2);
});
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o => {
 o.TokenValidationParameters = new TokenValidationParameters { ValidateIssuer = true, ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "NotificationIssuer", ValidateAudience = true, ValidAudience = builder.Configuration["Jwt:Audience"] ?? "NotificationClients", ValidateLifetime = true, ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)), ClockSkew = TimeSpan.Zero };
 o.Events = new JwtBearerEvents { OnMessageReceived = context => { var token = context.Request.Query["access_token"]; if (!string.IsNullOrEmpty(token) && context.HttpContext.Request.Path.StartsWithSegments("/hub")) context.Token = token; return Task.CompletedTask; } };
});
builder.Services.AddAuthorization(); builder.Services.AddSignalR(); builder.Services.AddGrpc();
var app = builder.Build(); app.UseSerilogRequestLogging(); app.UseAuthentication(); app.UseAuthorization();
app.MapGet("/", () => "Notification service is running");
app.MapHub<NotificationHub>("/hub"); app.MapGrpcService<NotificationGatewayService>();
app.Run();
[Authorize]
public class NotificationHub : Hub {
 public override async Task OnConnectedAsync() {
  var id = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
  if (string.IsNullOrWhiteSpace(id)) { Context.Abort(); return; }
  await Groups.AddToGroupAsync(Context.ConnectionId, id);
  await base.OnConnectedAsync();
 }
}
public class NotificationGatewayService(IHubContext<NotificationHub> hub, IConfiguration config, ILogger<NotificationGatewayService> logger) : NotificationGateway.NotificationGatewayBase {
 public override async Task<NotificationReply> Send(NotificationRequest request, ServerCallContext context) {
  var supplied = context.RequestHeaders.GetValue("x-api-key") ?? "";
  var expected = config["Grpc:ApiKey"]!;
  if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(supplied), Encoding.UTF8.GetBytes(expected))) throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid service credentials"));
  if (string.IsNullOrWhiteSpace(request.UserId) || string.IsNullOrWhiteSpace(request.Message) || request.Message.Length > 2000) throw new RpcException(new Status(StatusCode.InvalidArgument, "user_id and message (up to 2000 characters) are required"));
  await hub.Clients.Group(request.UserId).SendAsync("ReceiveNotification", request.Message, context.CancellationToken);
  logger.LogInformation("Notification dispatched to {UserId}", request.UserId);
  return new NotificationReply { Accepted = true };
 }
}
