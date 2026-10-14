using System.Text;
using ESGImpact.Api.Configuration;
using ESGImpact.Api.Data;
using ESGImpact.Api.Middleware;
using ESGImpact.Api.Repositories;
using ESGImpact.Api.Security;
using ESGImpact.Api.Services;
using ESGImpact.Api.Validators;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Conventions;

var builder = WebApplication.CreateBuilder(args);

/* =====================================================================
 * 1) CONVENÇÕES DE SERIALIZAÇÃO BSON
 * ---------------------------------------------------------------------
 * Precisa vir ANTES de qualquer uso das classes de modelo.
 *  - CamelCaseElementName: PascalCase (C#) <-> camelCase (MongoDB)
 *  - IgnoreIfNull:         não grava campos nulos (documentos enxutos)
 *  - EnumRepresentation:   enums gravados como string, legíveis no shell
 * ===================================================================== */
var conventions = new ConventionPack
{
    new CamelCaseElementNameConvention(),
    new IgnoreIfNullConvention(true),
    new EnumRepresentationConvention(BsonType.String)
};
ConventionRegistry.Register("esgimpact-conventions", conventions, _ => true);

/* =====================================================================
 * 2) CONFIGURAÇÃO E ACESSO A DADOS
 * ===================================================================== */
builder.Services.Configure<MongoSettings>(builder.Configuration.GetSection(MongoSettings.SectionName));
builder.Services.AddSingleton<MongoContext>();
builder.Services.AddScoped<IDocumentRepository, DocumentRepository>();

/* =====================================================================
 * 3) AUTENTICAÇÃO JWT
 * ===================================================================== */
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection(JwtSettings.SectionName));
var jwtSettings = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>() ?? new JwtSettings();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Key)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });
builder.Services.AddAuthorization();

/* =====================================================================
 * 4) SERVIÇOS DE DOMÍNIO
 * ===================================================================== */
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IImpactCalculatorService, ImpactCalculatorService>();
builder.Services.AddScoped<ISimulationService, SimulationService>();
builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();
builder.Services.AddScoped<IRecommendationService, RecommendationService>();
builder.Services.AddScoped<IOffsetService, OffsetService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddSingleton<ITokenService, TokenService>();

/* =====================================================================
 * 5) MVC, VALIDAÇÃO E SWAGGER
 * ===================================================================== */
builder.Services.AddControllers();
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<SimulationRequestValidator>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "ESGImpact API — MongoDB",
        Version = "v1",
        Description = "Backend ESG para cidades inteligentes migrado de SQL Server/EF Core para MongoDB. " +
                      "Expõe CRUD completo das 5 collections (emission_factors, simulations, participants, " +
                      "offset_projects, governance_audits) e os endpoints analíticos do domínio."
    });

    var scheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Informe o token JWT (apenas o valor, sem o prefixo 'Bearer ').",
        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
    };
    options.AddSecurityDefinition("Bearer", scheme);
    options.AddSecurityRequirement(new OpenApiSecurityRequirement { [scheme] = Array.Empty<string>() });

    var xml = Path.Combine(AppContext.BaseDirectory, "ESGImpact.Api.xml");
    if (File.Exists(xml)) options.IncludeXmlComments(xml);
});

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseSwagger();
app.UseSwaggerUI(o => o.DocumentTitle = "ESGImpact API — MongoDB");

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

/* =====================================================================
 * 6) DIAGNÓSTICO
 * ===================================================================== */
var startedAt = DateTime.UtcNow;

app.MapGet("/api/health", async (MongoContext ctx, CancellationToken ct) =>
{
    var ping = await ctx.Database.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1), cancellationToken: ct);
    return Results.Ok(new { status = "ok", mongo = ping["ok"].ToDouble() == 1.0 ? "up" : "down" });
});

// Identifica o ambiente e a versão em execução — usado pelos smoke tests
// do pipeline para confirmar que o deploy certo chegou ao ambiente certo.
app.MapGet("/api/info", (IWebHostEnvironment env, MongoContext ctx) => Results.Ok(new
{
    application = "ESGImpact API",
    environment = env.EnvironmentName,
    version = builder.Configuration["APP_VERSION"] ?? "local",
    commit = builder.Configuration["GIT_SHA"] ?? "local",
    database = ctx.Database.DatabaseNamespace.DatabaseName,
    startedAt,
    uptimeSeconds = (long)(DateTime.UtcNow - startedAt).TotalSeconds
}));

app.MapGet("/api/collections/stats", async (MongoContext ctx, CancellationToken ct) =>
{
    var stats = new Dictionary<string, long>();
    foreach (var name in MongoSettings.Collections.All)
        stats[name] = await ctx.Raw(name).CountDocumentsAsync(new BsonDocument(), cancellationToken: ct);

    return Results.Ok(new { database = ctx.Database.DatabaseNamespace.DatabaseName, collections = stats });
});

/* =====================================================================
 * 7) BOOTSTRAP DO BANCO (equivalente a Database.Migrate() + seeder)
 * ===================================================================== */
if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var ctx = scope.ServiceProvider.GetRequiredService<MongoContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("MongoBootstrapper");

    try
    {
        await MongoBootstrapper.EnsureIndexesAsync(ctx, logger);
        await MongoBootstrapper.EnsureDefaultAnalystAsync(ctx, logger);
        await MongoBootstrapper.LogCountsAsync(ctx, logger);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Falha ao preparar o MongoDB. A API subirá, mas verifique a conexão.");
    }
}

app.Run();

public partial class Program { }
