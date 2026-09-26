// Aspire orchestrates this service's dependencies for local development.
//
// The AppHost is the only project that knows Aspire exists. Its job is to translate each
// resource into the *same* explicit configuration keys the service already binds from
// appsettings.json — RabbitMQOptions, RedisOptions, DATABASE_CON, ApplicationOptions — so:
//
//   * Template.Api references no Aspire package and uses no service discovery;
//   * every value injected here can be set in production as a plain environment
//     variable or appsettings entry, with no Aspire involved;
//   * running the service directly (`dotnet run --project src/Template.Api`) still works,
//     falling back to the values in its own appsettings.json.
//
// Environment variables use `__` where configuration uses `:`.

var builder = DistributedApplication.CreateBuilder(args);

var rabbitmq = builder.AddRabbitMQ("rabbitmq")
    .WithDataVolume()
    .WithManagementPlugin();

var redis = builder.AddRedis("redis")
    .WithDataVolume();

// ApplicationOptions.LogUrl points Serilog's Seq sink somewhere real instead of at a
// port nothing is listening on.
var seq = builder.AddSeq("seq");

var api = builder.AddProject("api", "../Template.Api/Template.Api.csproj")
    // RabbitMQOptions is a decomposed host/port/credential set rather than a single
    // connection string, so each part is mapped on its own. Aspire generates the
    // credentials, so they are read off the resource instead of being hard-coded.
    .WithEnvironment("RabbitMQOptions__HostName", rabbitmq.Resource.Host)
    .WithEnvironment("RabbitMQOptions__Port", rabbitmq.Resource.Port)
    .WithEnvironment("RabbitMQOptions__UserName", rabbitmq.Resource.UserNameReference)
    .WithEnvironment("RabbitMQOptions__Password", rabbitmq.Resource.PasswordParameter)
    .WithEnvironment("RabbitMQOptions__VirtualHost", "/")
    .WithEnvironment("RedisOptions__ConnectionString", redis.Resource.ConnectionStringExpression)
    .WithEnvironment("RedisOptions__InstanceName", "Template.Api")
    .WithEnvironment("ApplicationOptions__LogUrl", seq.Resource.PrimaryEndpoint)
    // Wolverine connects to RabbitMQ while the host starts and fails the process if the
    // broker is not there yet, so waiting is what makes a cold start reliable.
    .WaitFor(rabbitmq)
    .WaitFor(redis);

// The database provider is chosen when the project is scaffolded. Only the matching block
// survives `dotnet new`; the template source keeps all four, the same way
// Template.Infrastructure references every EF Core provider. DatabaseKind is deliberately
// not injected here — the appsettings.json that ships with the chosen provider already
// sets it, and that stays the single source of truth.
//
// Each block names its database resource after its provider rather than sharing one name:
// Aspire rejects duplicate resource names, so a shared name would make the *template's own*
// AppHost throw on startup even though only one block ever reaches a scaffolded project.

//#if (useMssql)
var sqlServer = builder.AddSqlServer("sqlserver")
    .WithDataVolume();

var sqlServerDatabase = sqlServer.AddDatabase("sqlserverdb", "Template.Api");

api.WithEnvironment("DATABASE_CON", sqlServerDatabase.Resource.ConnectionStringExpression)
   .WaitFor(sqlServerDatabase);
//#endif

//#if (useSqlite)
// SQLite is a file, not a service: there is no container to orchestrate and no connection
// string to inject, so Template.Api keeps the DATABASE_CON from its own appsettings.json.
//#endif

//#if (usePostgres)
var postgres = builder.AddPostgres("postgres")
    .WithDataVolume();

var postgresDatabase = postgres.AddDatabase("postgresdb", "Template.Api");

api.WithEnvironment("DATABASE_CON", postgresDatabase.Resource.ConnectionStringExpression)
   .WaitFor(postgresDatabase);
//#endif

//#if (useMysql)
var mysql = builder.AddMySql("mysql")
    .WithDataVolume();

var mysqlDatabase = mysql.AddDatabase("mysqldb", "Template.Api");

api.WithEnvironment("DATABASE_CON", mysqlDatabase.Resource.ConnectionStringExpression)
   .WaitFor(mysqlDatabase);
//#endif

builder.Build().Run();
