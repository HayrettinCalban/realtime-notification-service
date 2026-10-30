# Real-time notification service

SignalR sends messages to connected clients. A gRPC endpoint accepts service-to-service messages. JWT authentication binds each SignalR connection to its own user ID; the gRPC endpoint requires an API key. Serilog writes structured logs to the console and optionally a central Seq server.

## Run
Requires .NET 8 and an identity provider that issues HS256 JWTs. Configure secrets outside source control:
```bash
export Jwt__Key="replace-with-a-random-secret-of-at-least-32-bytes"
export Grpc__ApiKey="replace-with-another-random-secret-at-least-32-bytes"
# Optional: export Seq__Url="http://localhost:5341"
# Optional: export Seq__ApiKey="your-seq-api-key"
dotnet run --project NotificationService.csproj
```
The trusted issuer must issue a JWT with `sub` equal to the user ID, issuer `NotificationIssuer`, audience `NotificationClients`, a valid expiry and HS256 signature using `Jwt__Key`. Override issuer and audience using `Jwt__Issuer` and `Jwt__Audience`. A SignalR client connects to `http://localhost:5000/hub` using its JWT as bearer token or SignalR access token. It receives `ReceiveNotification`; groups are assigned from its validated identity.

Call `notifications.NotificationGateway/Send` on port 5001 with the `x-api-key` gRPC metadata header and the `user_id` and `message` fields. See `Protos/notifications.proto`. The response confirms dispatch, not receipt by a client.

## Deployment limitations
HTTP and gRPC are plaintext for local development. Put both behind TLS before sending tokens or keys over a network. Configure a separately deployed Seq server to centralize logs; without `Seq__Url` logs go only to the console. Delivery is ephemeral: offline users receive nothing and a multi-instance deployment needs a SignalR backplane or managed service. Add persistence, retries and key rotation to meet your deployment requirements. Never commit secrets.
