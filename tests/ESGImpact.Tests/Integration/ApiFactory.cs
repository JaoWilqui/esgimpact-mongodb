using ESGImpact.Api.Data;
using ESGImpact.Api.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Driver;

namespace ESGImpact.Tests.Integration;

/// <summary>
/// Sobe a API inteira em memória (WebApplicationFactory) apontando para um
/// MongoDB real, em um banco descartável criado só para a execução.
///
/// Onde está o MongoDB:
///   - no pipeline: container de serviço do GitHub Actions (localhost:27017),
///     informado pelas variáveis MongoDb__ConnectionString/Username/Password;
///   - localmente: o mesmo `docker compose up -d` do projeto.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public string DatabaseName { get; } = "esgimpact_tests_" + Guid.NewGuid().ToString("N")[..8];

    public ApiFactory()
    {
        // Variável de ambiente é lida já na criação do WebApplicationBuilder,
        // antes de qualquer código do Program.cs — o jeito mais confiável de
        // trocar o banco sem depender da ordem de configuração do host.
        Environment.SetEnvironmentVariable("MongoDb__Database", DatabaseName);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // "Testing" desliga o bootstrap automático do Program.cs;
        // a preparação do banco é feita explicitamente em InitializeAsync.
        builder.UseEnvironment("Testing");
    }

    public MongoContext Mongo => Services.GetRequiredService<MongoContext>();

    public async Task InitializeAsync()
    {
        var ctx = Mongo;

        await MongoBootstrapper.EnsureIndexesAsync(ctx, NullLogger.Instance);
        await MongoBootstrapper.EnsureDefaultAnalystAsync(ctx, NullLogger.Instance);

        await ctx.EmissionFactors.InsertManyAsync(new[]
        {
            new EmissionFactor { Code = "CARRO", Activity = "Carro", FactorKgCo2PerUnit = 0.21, Unit = "km", Category = "transporte", Pillar = "E" },
            new EmissionFactor { Code = "ONIBUS", Activity = "Ônibus", FactorKgCo2PerUnit = 0.08, Unit = "km", Category = "transporte", Pillar = "E" },
            new EmissionFactor { Code = "BICICLETA", Activity = "Bicicleta", FactorKgCo2PerUnit = 0.0, Unit = "km", Category = "transporte", Pillar = "E" }
        });

        await ctx.OffsetProjects.InsertOneAsync(new OffsetProject
        {
            Code = "PRJ-T01",
            Name = "Projeto de teste",
            Type = "reflorestamento",
            Status = "ativo",
            CapacityTonCo2Year = 100,
            Pricing = new List<OffsetPricing> { new() { Currency = "BRL", PricePerTon = 90.0 } }
        });
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        try
        {
            await Mongo.Database.Client.DropDatabaseAsync(DatabaseName);
        }
        finally
        {
            Environment.SetEnvironmentVariable("MongoDb__Database", null);
            await base.DisposeAsync();
        }
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "api";
}
