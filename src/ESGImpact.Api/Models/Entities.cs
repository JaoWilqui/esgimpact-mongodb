using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ESGImpact.Api.Models;

/* =====================================================================
 * MODELOS DE DOMÍNIO — MongoDB
 * ---------------------------------------------------------------------
 * Todas as entidades declaram apenas o NÚCLEO estável do documento e
 * expõem `Extra` com [BsonExtraElements]. Isso é o que permite conciliar
 * uma API fortemente tipada em C# com o esquema flexível do MongoDB:
 * campos que existem em apenas alguns documentos (variants, gridMix,
 * iotDevice, socialImpact, checklist...) são lidos e regravados sem
 * perda e sem precisar de classe nem de migração.
 *
 * A convenção camelCase é registrada em Program.cs, de modo que
 * `FactorKgCo2PerUnit` (C#) <-> `factorKgCo2PerUnit` (BSON).
 * ===================================================================== */

/// <summary>Collection <c>emission_factors</c> — pilar Environmental.</summary>
public class EmissionFactor
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Activity { get; set; } = string.Empty;

    public double FactorKgCo2PerUnit { get; set; }

    public string Unit { get; set; } = string.Empty;

    public string? FieldLabel { get; set; }

    public string? Category { get; set; }

    public string? Pillar { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime? UpdatedAt { get; set; }

    [BsonExtraElements]
    public BsonDocument Extra { get; set; } = new();
}

/// <summary>Fotografia do fator no instante do cálculo (desnormalização controlada).</summary>
public class EmissionFactorSnapshot
{
    public string Code { get; set; } = string.Empty;
    public double FactorKgCo2PerUnit { get; set; }
    public string Unit { get; set; } = string.Empty;
    public DateTime CapturedAt { get; set; }
}

/// <summary>Collection <c>simulations</c> — pilar Environmental.</summary>
public class Simulation
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    public string ActivityCode { get; set; } = string.Empty;

    public string Activity { get; set; } = string.Empty;

    public double Value { get; set; }

    public string Unit { get; set; } = string.Empty;

    public double EstimatedKgCo2 { get; set; }

    public string Classification { get; set; } = string.Empty;

    public string? OwnerCode { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string? Source { get; set; }

    public EmissionFactorSnapshot? FactorSnapshot { get; set; }

    [BsonExtraElements]
    public BsonDocument Extra { get; set; } = new();
}

/// <summary>Credenciais embutidas no participante (substitui a tabela AppUser).</summary>
public class ParticipantCredentials
{
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string PasswordSalt { get; set; } = string.Empty;
}

/// <summary>
/// Collection <c>participants</c> — pilar Social. Documento POLIMÓRFICO:
/// <c>type</c> define a forma (cidadao | organizacao | orgao_publico).
/// </summary>
public class Participant
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Type { get; set; } = "cidadao";

    public string Name { get; set; } = string.Empty;

    public string? District { get; set; }

    public string? City { get; set; }

    public DateTime? JoinedAt { get; set; }

    public string Role { get; set; } = "User";

    public ParticipantCredentials? Credentials { get; set; }

    [BsonExtraElements]
    public BsonDocument Extra { get; set; } = new();
}

/// <summary>Preço do crédito de carbono por moeda (array embutido no projeto).</summary>
public class OffsetPricing
{
    public string Currency { get; set; } = "BRL";
    public double PricePerTon { get; set; }
}

/// <summary>Collection <c>offset_projects</c> — pilares Environmental e Social.</summary>
public class OffsetProject
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public string Status { get; set; } = "ativo";

    public double CapacityTonCo2Year { get; set; }

    public List<OffsetPricing> Pricing { get; set; } = new();

    [BsonExtraElements]
    public BsonDocument Extra { get; set; } = new();
}

/// <summary>
/// Collection <c>governance_audits</c> — pilar Governance. Coleção
/// append-only; cada <c>eventType</c> carrega um payload próprio, que
/// fica integralmente em <see cref="Extra"/>.
/// </summary>
public class GovernanceAudit
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    public string EventType { get; set; } = string.Empty;

    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

    [BsonExtraElements]
    public BsonDocument Extra { get; set; } = new();
}
