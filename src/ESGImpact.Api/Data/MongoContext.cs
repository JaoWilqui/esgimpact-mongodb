using ESGImpact.Api.Configuration;
using ESGImpact.Api.Models;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace ESGImpact.Api.Data;

/// <summary>
/// Ponto único de acesso ao banco. É o substituto direto do
/// <c>AppDbContext</c> (EF Core) do projeto relacional: em vez de
/// <c>DbSet&lt;T&gt;</c>, expõe <c>IMongoCollection&lt;T&gt;</c>.
/// Registrado como singleton — o <see cref="MongoClient"/> já mantém
/// o pool de conexões internamente e é thread-safe.
/// </summary>
public class MongoContext
{
    public IMongoDatabase Database { get; }

    public MongoContext(IOptions<MongoSettings> options)
    {
        var settings = options.Value;
        var client = new MongoClient(settings.ConnectionString);
        Database = client.GetDatabase(settings.Database);
    }

    // --- collections tipadas ------------------------------------------
    public IMongoCollection<EmissionFactor> EmissionFactors =>
        Database.GetCollection<EmissionFactor>(MongoSettings.Collections.EmissionFactors);

    public IMongoCollection<Simulation> Simulations =>
        Database.GetCollection<Simulation>(MongoSettings.Collections.Simulations);

    public IMongoCollection<Participant> Participants =>
        Database.GetCollection<Participant>(MongoSettings.Collections.Participants);

    public IMongoCollection<OffsetProject> OffsetProjects =>
        Database.GetCollection<OffsetProject>(MongoSettings.Collections.OffsetProjects);

    public IMongoCollection<GovernanceAudit> GovernanceAudits =>
        Database.GetCollection<GovernanceAudit>(MongoSettings.Collections.GovernanceAudits);

    // --- acesso "cru" (usado pelos endpoints genéricos de CRUD) -------
    public IMongoCollection<BsonDocument> Raw(string collectionName) =>
        Database.GetCollection<BsonDocument>(collectionName);
}
