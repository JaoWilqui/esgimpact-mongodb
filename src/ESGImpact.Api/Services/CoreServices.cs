using ESGImpact.Api.Data;
using ESGImpact.Api.Exceptions;
using ESGImpact.Api.Models;
using ESGImpact.Api.Security;
using ESGImpact.Api.ViewModels;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace ESGImpact.Api.Services;

/* =====================================================================
 * SERVIÇOS DE NEGÓCIO
 * ---------------------------------------------------------------------
 * Mesma superfície de contrato do projeto relacional (as interfaces não
 * mudaram), porém a implementação passou de LINQ-to-EF para o driver do
 * MongoDB. Onde antes havia GROUP BY + JOIN, agora há Aggregation
 * Pipeline resolvido em uma única ida ao banco.
 * ===================================================================== */

// ---------------------------------------------------------------------
// Token JWT
// ---------------------------------------------------------------------
public interface ITokenService
{
    TokenResponse CreateToken(Participant participant);
}

public class TokenService : ITokenService
{
    private readonly JwtSettings _settings;

    public TokenService(IOptions<JwtSettings> settings) => _settings = settings.Value;

    public TokenResponse CreateToken(Participant participant)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Key));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiresAt = DateTime.UtcNow.AddMinutes(_settings.ExpiryMinutes);

        var username = participant.Credentials?.Username ?? participant.Code;

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, participant.Id ?? participant.Code),
            new Claim(JwtRegisteredClaimNames.UniqueName, username),
            new Claim(ClaimTypes.Name, username),
            new Claim(ClaimTypes.Role, participant.Role),
            new Claim("participantCode", participant.Code),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials);

        return new TokenResponse
        {
            AccessToken = new JwtSecurityTokenHandler().WriteToken(token),
            ExpiresInSeconds = _settings.ExpiryMinutes * 60,
            ExpiresAt = expiresAt,
            ParticipantCode = participant.Code,
            Role = participant.Role
        };
    }
}

// ---------------------------------------------------------------------
// Autenticação — as credenciais agora vivem EMBUTIDAS no participante
// ---------------------------------------------------------------------
public interface IAuthService
{
    Task<TokenResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);
}

public class AuthService : IAuthService
{
    private readonly MongoContext _ctx;
    private readonly ITokenService _tokenService;

    public AuthService(MongoContext ctx, ITokenService tokenService)
    {
        _ctx = ctx;
        _tokenService = tokenService;
    }

    public async Task<TokenResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var filter = Builders<Participant>.Filter.Eq("credentials.username", request.Username);
        var participant = await _ctx.Participants.Find(filter).FirstOrDefaultAsync(ct);

        if (participant?.Credentials is null ||
            !PasswordHasher.Verify(request.Password, participant.Credentials.PasswordHash, participant.Credentials.PasswordSalt))
        {
            throw new DomainValidationException("Usuário ou senha inválidos.");
        }

        return _tokenService.CreateToken(participant);
    }
}

// ---------------------------------------------------------------------
// Cálculo de impacto
// ---------------------------------------------------------------------
public interface IImpactCalculatorService
{
    Task<EmissionFactor> GetFactorAsync(string activityOrCode, CancellationToken ct = default);
    double CalculateEmission(EmissionFactor factor, double value);
    string Classify(double estimatedKgCo2);
}

public class ImpactCalculatorService : IImpactCalculatorService
{
    private readonly MongoContext _ctx;

    public ImpactCalculatorService(MongoContext ctx) => _ctx = ctx;

    public async Task<EmissionFactor> GetFactorAsync(string activityOrCode, CancellationToken ct = default)
    {
        var b = Builders<EmissionFactor>.Filter;
        var filter = b.And(
            b.Eq(x => x.IsActive, true),
            b.Or(b.Eq(x => x.Code, activityOrCode), b.Eq(x => x.Activity, activityOrCode)));

        var factor = await _ctx.EmissionFactors.Find(filter).FirstOrDefaultAsync(ct);

        if (factor is null)
            throw new NotFoundException($"Atividade '{activityOrCode}' não encontrada ou inativa.");

        return factor;
    }

    public double CalculateEmission(EmissionFactor factor, double value)
        => Math.Round(value * factor.FactorKgCo2PerUnit, 2);

    public string Classify(double estimatedKgCo2) => estimatedKgCo2 switch
    {
        <= 1 => "Baixo impacto",
        <= 10 => "Impacto moderado",
        <= 500 => "Alto impacto",
        _ => "Impacto crítico"
    };
}

// ---------------------------------------------------------------------
// Simulações
// ---------------------------------------------------------------------
public interface ISimulationService
{
    Task<SimulationResponse> CreateAsync(SimulationRequest request, CancellationToken ct = default);
    Task<PagedResult<SimulationResponse>> GetHistoryAsync(PaginationQuery query, string? ownerCode, string? activity, CancellationToken ct = default);
}

public class SimulationService : ISimulationService
{
    private readonly MongoContext _ctx;
    private readonly IImpactCalculatorService _calculator;
    private readonly IAuditService _audit;

    public SimulationService(MongoContext ctx, IImpactCalculatorService calculator, IAuditService audit)
    {
        _ctx = ctx;
        _calculator = calculator;
        _audit = audit;
    }

    public async Task<SimulationResponse> CreateAsync(SimulationRequest request, CancellationToken ct = default)
    {
        var factor = await _calculator.GetFactorAsync(request.Activity, ct);
        var emission = _calculator.CalculateEmission(factor, request.Value);
        var now = DateTime.UtcNow;

        var simulation = new Simulation
        {
            ActivityCode = factor.Code,
            Activity = factor.Activity,
            Value = request.Value,
            Unit = factor.Unit,
            EstimatedKgCo2 = emission,
            Classification = _calculator.Classify(emission),
            OwnerCode = request.OwnerCode,
            Source = string.IsNullOrWhiteSpace(request.Source) ? "api" : request.Source,
            CreatedAt = now,
            // DESNORMALIZAÇÃO CONTROLADA: o fator usado fica congelado no documento
            FactorSnapshot = new EmissionFactorSnapshot
            {
                Code = factor.Code,
                FactorKgCo2PerUnit = factor.FactorKgCo2PerUnit,
                Unit = factor.Unit,
                CapturedAt = now
            }
        };

        await _ctx.Simulations.InsertOneAsync(simulation, cancellationToken: ct);

        await _audit.LogAsync("simulation_created", "simulations", simulation.Id,
            new BsonDocument
            {
                { "activityCode", factor.Code },
                { "estimatedKgCo2", emission },
                { "ownerCode", request.OwnerCode is null ? (BsonValue)BsonNull.Value : new BsonString(request.OwnerCode) }
            }, ct);

        return ToResponse(simulation);
    }

    public async Task<PagedResult<SimulationResponse>> GetHistoryAsync(
        PaginationQuery query, string? ownerCode, string? activity, CancellationToken ct = default)
    {
        var b = Builders<Simulation>.Filter;
        var filter = b.Empty;

        if (!string.IsNullOrWhiteSpace(ownerCode))
            filter &= b.Eq(x => x.OwnerCode, ownerCode);

        if (!string.IsNullOrWhiteSpace(activity))
            filter &= b.Or(b.Eq(x => x.ActivityCode, activity), b.Eq(x => x.Activity, activity));

        var total = await _ctx.Simulations.CountDocumentsAsync(filter, cancellationToken: ct);

        var items = await _ctx.Simulations
            .Find(filter)
            .SortByDescending(x => x.CreatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Limit(query.PageSize)
            .ToListAsync(ct);

        return new PagedResult<SimulationResponse>
        {
            Items = items.Select(ToResponse).ToList(),
            Page = query.Page,
            PageSize = query.PageSize,
            TotalItems = total
        };
    }

    private static SimulationResponse ToResponse(Simulation s) => new()
    {
        Id = s.Id ?? string.Empty,
        ActivityCode = s.ActivityCode,
        Activity = s.Activity,
        Value = s.Value,
        Unit = s.Unit,
        EstimatedKgCo2 = s.EstimatedKgCo2,
        Classification = s.Classification,
        OwnerCode = s.OwnerCode,
        Source = s.Source,
        FactorUsed = s.FactorSnapshot?.FactorKgCo2PerUnit ?? 0,
        CreatedAt = s.CreatedAt
    };
}

// ---------------------------------------------------------------------
// Trilha de auditoria (pilar G) — toda escrita relevante vira evento
// ---------------------------------------------------------------------
public interface IAuditService
{
    Task LogAsync(string eventType, string collection, string? documentCode, BsonDocument? payload = null, CancellationToken ct = default);
}

public class AuditService : IAuditService
{
    private readonly MongoContext _ctx;
    private readonly IHttpContextAccessor _http;

    public AuditService(MongoContext ctx, IHttpContextAccessor http)
    {
        _ctx = ctx;
        _http = http;
    }

    public async Task LogAsync(string eventType, string collection, string? documentCode,
        BsonDocument? payload = null, CancellationToken ct = default)
    {
        var user = _http.HttpContext?.User;
        var actorCode = user?.FindFirst("participantCode")?.Value;
        var actorName = user?.Identity?.Name;
        var role = user?.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;

        var extra = new BsonDocument
        {
            {
                "actor", new BsonDocument
                {
                    { "participantCode", actorCode ?? "anonimo" },
                    { "name", actorName ?? "anonimo" },
                    { "role", role ?? "Anonymous" }
                }
            },
            {
                "resource", new BsonDocument
                {
                    { "collection", collection },
                    { "documentCode", documentCode ?? string.Empty }
                }
            },
            { "origin", new BsonDocument { { "channel", "rest-api" } } }
        };

        if (payload is not null)
            extra.Add("payload", payload);

        await _ctx.GovernanceAudits.InsertOneAsync(new GovernanceAudit
        {
            EventType = eventType,
            OccurredAt = DateTime.UtcNow,
            Extra = extra
        }, cancellationToken: ct);
    }
}
