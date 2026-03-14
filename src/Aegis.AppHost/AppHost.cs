var builder = DistributedApplication.CreateBuilder(args);

var postgresUser = builder.AddParameter("postgres-username", "postgres", publishValueAsDefault: true, secret: false);
var postgresPassword = builder.AddParameter("postgres-password", "postgres", publishValueAsDefault: false, secret: true);

var postgres = builder.AddPostgres("postgres", postgresUser, postgresPassword)
    .WithImageTag("17")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithPgAdmin(pgadmin => pgadmin
        .WithHostPort(5050)
        .WithEnvironment("PGADMIN_DEFAULT_EMAIL", "admin@example.com")
        .WithEnvironment("PGADMIN_DEFAULT_PASSWORD", "admin")
        .WithEnvironment("PGADMIN_CONFIG_ENHANCED_COOKIE_PROTECTION", "False"), containerName: "pgadmin");

var universeDatabase = postgres.AddDatabase("universe");
var marketDataDatabase = postgres.AddDatabase("marketdata");

// AppHost keeps local verification self-contained by wiring both module databases and fake provider flags up front.
var backend = builder.AddProject<Projects.Aegis_Backend>("backend")
    .WithReference(universeDatabase)
    .WithReference(marketDataDatabase)
    .WithEnvironment("Cors__AllowedOrigins__0", "http://localhost:3001")
    .WithEnvironment("Alpaca__SymbolReference__UseFakeProvider", "true")
    .WithEnvironment("Alpaca__HistoricalData__UseFakeProvider", "true")
    .WithEnvironment("MarketData__Realtime__EnableProviderRuntime", "false")
    .WaitFor(universeDatabase)
    .WaitFor(marketDataDatabase)
    .WithExternalHttpEndpoints();

builder.AddNpmApp("web", "../Aegis.Web", "dev")
    .WithReference(backend)
    .WaitFor(backend)
    .WithEnvironment("NEXT_PUBLIC_BACKEND_URL", backend.GetEndpoint("http"))
    .WithHttpEndpoint(env: "PORT", port: 3001, isProxied: false);

builder.Build().Run();
