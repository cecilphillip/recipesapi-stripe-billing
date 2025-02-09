var builder = DistributedApplication.CreateBuilder(args);

var postgresUser = builder.AddParameter("postgresUser", secret: true);
var postgresPassword = builder.AddParameter("postgresPassword", secret: true);

var postgresServer = builder.AddPostgres("postgres", postgresUser, postgresPassword, 5432)
    .WithDataBindMount(".temp/postgres/data")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithPgWeb();

var recipesDb= postgresServer.AddDatabase("recipesDb");

var redis = builder.AddRedis("redisCache")
    .WithDataBindMount(".temp/redis/data")
    .WithRedisInsight()
    .WithLifetime(ContainerLifetime.Persistent);

builder.AddProject<Projects.Recipes_Api>("recipes-api")
    .WaitFor(recipesDb)
    .WithReference(recipesDb)
    .WaitFor(redis)
    .WithReference(redis);


builder.Build().Run();