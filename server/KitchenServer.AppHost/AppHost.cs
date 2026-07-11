using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithPgAdmin()
    .WithLifetime(ContainerLifetime.Persistent)
    .AddDatabase("kitchendb");

builder.AddProject<KitchenServer_Web>("web")
    .WithReference(postgres)
    .WaitFor(postgres);

builder.Build().Run();
