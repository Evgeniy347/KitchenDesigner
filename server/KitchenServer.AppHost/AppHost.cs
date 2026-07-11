using Projects;

var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<KitchenServer_Web>("web");

builder.Build().Run();