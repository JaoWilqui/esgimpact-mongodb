using ESGImpact.Api.Configuration;
using ESGImpact.Api.Models;
using ESGImpact.Api.Security;
using MongoDB.Bson;
using MongoDB.Driver;

namespace ESGImpact.Api.Data;

/// <summary>
/// Substitui o par "migrations + DbSeeder" do EF Core.
/// No MongoDB não há DDL de tabelas, mas há duas coisas que precisam ser
/// garantidas na subida da aplicação e que são idempotentes:
///   1) os ÍNDICES (inclusive os únicos e o geoespacial 2dsphere);
///   2) a existência do usuário analista padrão, caso a base esteja vazia
///      (quando o banco sobe pelo docker-compose, os scripts de
///      mongo/init já fizeram a carga completa).
/// </summary>
public static class MongoBootstrapper
{
    public const string DefaultUsername = "analista";
    public const string DefaultPassword = "Esg@2026";

    public static async Task EnsureIndexesAsync(MongoContext ctx, ILogger logger, CancellationToken ct = default)
    {
        // emission_factors
        await ctx.EmissionFactors.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<EmissionFactor>(
                Builders<EmissionFactor>.IndexKeys.Ascending(x => x.Code),
                new CreateIndexOptions { Unique = true, Name = "ux_factor_code" }),
            new CreateIndexModel<EmissionFactor>(
                Builders<EmissionFactor>.IndexKeys.Ascending(x => x.Category).Ascending(x => x.IsActive),
                new CreateIndexOptions { Name = "ix_factor_category_active" })
        }, ct);

        // simulations
        await ctx.Simulations.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<Simulation>(
                Builders<Simulation>.IndexKeys.Descending(x => x.CreatedAt),
                new CreateIndexOptions { Name = "ix_sim_createdAt" }),
            new CreateIndexModel<Simulation>(
                Builders<Simulation>.IndexKeys.Ascending(x => x.OwnerCode).Descending(x => x.CreatedAt),
                new CreateIndexOptions { Name = "ix_sim_owner_createdAt" }),
            new CreateIndexModel<Simulation>(
                Builders<Simulation>.IndexKeys.Ascending(x => x.ActivityCode),
                new CreateIndexOptions { Name = "ix_sim_activity" }),
            new CreateIndexModel<Simulation>(
                Builders<Simulation>.IndexKeys.Geo2DSphere("location"),
                new CreateIndexOptions { Name = "ix_sim_geo", Sparse = true })
        }, ct);

        // participants
        await ctx.Participants.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<Participant>(
                Builders<Participant>.IndexKeys.Ascending(x => x.Code),
                new CreateIndexOptions { Unique = true, Name = "ux_participant_code" }),
            new CreateIndexModel<Participant>(
                Builders<Participant>.IndexKeys.Ascending("credentials.username"),
                new CreateIndexOptions { Unique = true, Sparse = true, Name = "ux_participant_username" }),
            new CreateIndexModel<Participant>(
                Builders<Participant>.IndexKeys.Ascending(x => x.Type).Ascending(x => x.District),
                new CreateIndexOptions { Name = "ix_participant_type_district" })
        }, ct);

        // offset_projects
        await ctx.OffsetProjects.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<OffsetProject>(
                Builders<OffsetProject>.IndexKeys.Ascending(x => x.Code),
                new CreateIndexOptions { Unique = true, Name = "ux_project_code" }),
            new CreateIndexModel<OffsetProject>(
                Builders<OffsetProject>.IndexKeys.Ascending(x => x.Status).Ascending(x => x.Type),
                new CreateIndexOptions { Name = "ix_project_status_type" }),
            new CreateIndexModel<OffsetProject>(
                Builders<OffsetProject>.IndexKeys.Ascending("pricing.currency"),
                new CreateIndexOptions { Name = "ix_project_currency" })
        }, ct);

        // governance_audits
        await ctx.GovernanceAudits.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<GovernanceAudit>(
                Builders<GovernanceAudit>.IndexKeys.Descending(x => x.OccurredAt),
                new CreateIndexOptions { Name = "ix_audit_occurredAt" }),
            new CreateIndexModel<GovernanceAudit>(
                Builders<GovernanceAudit>.IndexKeys.Ascending(x => x.EventType).Descending(x => x.OccurredAt),
                new CreateIndexOptions { Name = "ix_audit_event" }),
            new CreateIndexModel<GovernanceAudit>(
                Builders<GovernanceAudit>.IndexKeys
                    .Ascending("resource.collection")
                    .Ascending("resource.documentCode"),
                new CreateIndexOptions { Name = "ix_audit_resource" })
        }, ct);

        logger.LogInformation("Índices do MongoDB verificados/criados.");
    }

    /// <summary>Cria o analista padrão apenas se ninguém com esse login existir.</summary>
    public static async Task EnsureDefaultAnalystAsync(MongoContext ctx, ILogger logger, CancellationToken ct = default)
    {
        var filter = Builders<Participant>.Filter.Eq("credentials.username", DefaultUsername);
        var exists = await ctx.Participants.Find(filter).AnyAsync(ct);
        if (exists)
        {
            logger.LogInformation("Analista padrão já existe — nenhuma carga necessária.");
            return;
        }

        var (hash, salt) = PasswordHasher.Hash(DefaultPassword);
        await ctx.Participants.InsertOneAsync(new Participant
        {
            Code = "ANL-001",
            Type = "cidadao",
            Name = "Ana Lúcia Ferreira",
            District = "Sé",
            City = "São Paulo",
            JoinedAt = DateTime.UtcNow,
            Role = "Analyst",
            Credentials = new ParticipantCredentials
            {
                Username = DefaultUsername,
                PasswordHash = hash,
                PasswordSalt = salt
            },
            Extra = new BsonDocument
            {
                { "engagement", new BsonDocument { { "level", "ouro" }, { "points", 0 }, { "streakDays", 0 } } },
                { "consent", new BsonDocument { { "lgpdAccepted", true }, { "shareAnonymizedData", true } } }
            }
        }, cancellationToken: ct);

        logger.LogInformation("Analista padrão criado (login: {User}).", DefaultUsername);
    }

    /// <summary>Loga a contagem de documentos das 5 collections (diagnóstico de subida).</summary>
    public static async Task LogCountsAsync(MongoContext ctx, ILogger logger, CancellationToken ct = default)
    {
        foreach (var name in MongoSettings.Collections.All)
        {
            var total = await ctx.Raw(name).CountDocumentsAsync(new BsonDocument(), cancellationToken: ct);
            logger.LogInformation("Collection {Collection}: {Total} documento(s).", name, total);
        }
    }
}
