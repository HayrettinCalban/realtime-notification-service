# Real-time notification service

ASP.NET Core SignalR delivers notifications to connected clients; a gRPC endpoint accepts messages from another service. Serilog emits structured logs to the console.

## Run
Requires .NET 8. `dotnet run --project NotificationService.csproj`. HTTP/SignalR listens on localhost:5000, gRPC on localhost:5001 (plaintext HTTP/2 for local development). The contract is in `Protos/notifications.proto`. Connect a SignalR client to `http://localhost:5000/hub`, invoke `Join` with a user ID, and subscribe to `ReceiveNotification`. Use a generated gRPC client or grpcurl with the proto to call `notifications.NotificationGateway/Send`, providing `user_id` and `message`.

Example with grpcurl installed:
```bash
grpcurl -plaintext -import-path Protos -proto notifications.proto -d '{"userId":"alice","message":"Hello"}' localhost:5001 notifications.NotificationGateway/Send
```

## Limitations
Demo only: no authentication or authorization is implemented. Any client can join any group and any gRPC caller can send messages; do not expose this service publicly. Add TLS, authenticated identity and service credentials before real use. Delivery is ephemeral and only reaches currently connected clients; there is no persistence, retry, backplane, or central log collector. Console logs can be collected by a platform, but are not themselves a centralized logging deployment.
