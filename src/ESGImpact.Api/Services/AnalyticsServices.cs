using ESGImpact.Api.Data;
using ESGImpact.Api.Exceptions;
using ESGImpact.Api.Models;
using ESGImpact.Api.ViewModels;
using MongoDB.Bson;
using MongoDB.Driver;

namespace ESGImpact.Api.Services;

/* =====================================================================
 * ANALYTICS / RECOMENDAÇÕES / COMPENSAÇÃO
 * ---------------------------------------------------------------------
 * Os três serviços foram reescritos sobre o Aggregation Framework.
 * O ganho mais visível está no dashboard: no EF Core eram 4 consultas
 * (count, sum, group by atividade, group by dia); aqui um único $facet
 * devolve tudo em uma ida ao banco.
 * ===================================================================== */

// ---------------------------------------------------------------------
// Dashboard ESG
// ---------------------------------------------------------------------
public interface IAnalyticsService
{
    Task<DashboardViewModel> GetDashboardAsync(string? ownerCode, int trendDays, CancellationToken ct = default);
}

public class AnalyticsService : IAnalyticsService
{
    private const double KgCo2PerTreePerYear = 21.0;

    private readonly MongoContext _ctx;

    public AnalyticsService(MongoContext ctx) => _ctx = ctx;

    public async Task<DashboardViewModel> GetDashboardAsync(string? ownerCode, int trendDays, CancellationToken ct = default)
    {
        if (trendDays < 1) trendDays = 30;

        var match = new BsonDocument();
        if (!string.IsNullOrWhiteSpace(ownerCode))
            match.Add("ownerCode", ownerCode);

        var since = DateTime.UtcNow.Date.AddDays(-(trendDays - 1));

        var pipeline = new[]
        {
            new BsonDocument("$match", match),
            new BsonDocument("$facet", new BsonDocument
            {
                {
                    "totals", new BsonArray
                    {
                        new BsonDocument("$group", new BsonDocument
                        {
                            { "_id", BsonNull.Value },
                            { "count", new BsonDocument("$sum", 1) },
                            { "total", new BsonDocument("$sum", "$estimatedKgCo2") },
                            { "avg", new BsonDocument("$avg", "$estimatedKgCo2") }
                        })
                    }
                },
                {
                    "breakdown", new BsonArray
                    {
                        new BsonDocument("$group", new BsonDocument
                        {
                            { "_id", "$activity" },
                            { "count", new BsonDocument("$sum", 1) },
                            { "total", new BsonDocument("$sum", "$estimatedKgCo2") }
                        }),
                        new BsonDocument("$sort", new BsonDocument("total", -1))
                    }
                },
                {
                    "trend", new BsonArray
                    {
                        new BsonDocument("$match",
                            new BsonDocument("createdAt", new BsonDocument("$gte", since))),
                        new BsonDocument("$group", new BsonDocument
                        {
                            {
                                "_id", new BsonDocument("$dateToString", new BsonDocument
                                {
                                    { "format", "%Y-%m-%d" },
                                    { "date", "$createdAt" }
                                })
                            },
                            { "total", new BsonDocument("$sum", "$estimatedKgCo2") }
                        }),
                        new BsonDocument("$sort", new BsonDocument("_id", 1))
                    }
                },
                {
                    "bySource", new BsonArray
                    {
                        new BsonDocument("$group", new BsonDocument
                        {
                            { "_id", "$source" },
                            { "count", new BsonDocument("$sum", 1) }
                        }),
                        new BsonDocument("$sort", new BsonDocument("count", -1))
                    }
                }
            })
        };

        var result = await _ctx.Raw("simulations")
            .Aggregate<BsonDocument>(pipeline, cancellationToken: ct)
            .FirstOrDefaultAsync(ct);

        if (result is null) return new DashboardViewModel();

        var totalsArray = result["totals"].AsBsonArray;
        if (totalsArray.Count == 0) return new DashboardViewModel();

        var totals = totalsArray[0].AsBsonDocument;
        var totalCount = totals["count"].ToInt64();
        var totalKg = totals["total"].ToDouble();
        var avgKg = totals["avg"].ToDouble();

        var breakdown = result["breakdown"].AsBsonArray
            .Select(x => x.AsBsonDocument)
            .Select(d => new ActivityBreakdownItem
            {
                Activity = d["_id"].IsBsonNull ? "(sem atividade)" : d["_id"].AsString,
                Count = d["count"].ToInt32(),
                TotalKgCo2 = Math.Round(d["total"].ToDouble(), 2),
                PercentOfTotal = totalKg == 0 ? 0 : Math.Round(d["total"].ToDouble() / totalKg * 100, 2)
            })
            .ToList();

        var trend = result["trend"].AsBsonArray
            .Select(x => x.AsBsonDocument)
            .Select(d => new TrendPoint
            {
                Date = d["_id"].AsString,
                TotalKgCo2 = Math.Round(d["total"].ToDouble(), 2)
            })
            .ToList();

        var bySource = result["bySource"].AsBsonArray
            .Select(x => x.AsBsonDocument)
            .Select(d => new SourceBreakdownItem
            {
                Source = d["_id"].IsBsonNull ? "(nao informado)" : d["_id"].AsString,
                Count = d["count"].ToInt32()
            })
            .ToList();

        return new DashboardViewModel
        {
            TotalSimulations = totalCount,
            TotalKgCo2 = Math.Round(totalKg, 2),
            AverageKgCo2 = Math.Round(avgKg, 2),
            TopEmittingActivity = breakdown.FirstOrDefault()?.Activity,
            Breakdown = breakdown,
            DailyTrend = trend,
            BySource = bySource,
            EquivalentTreesPerYear = Math.Round(totalKg / KgCo2PerTreePerYear, 2)
        };
    }
}

// ---------------------------------------------------------------------
// Motor de recomendações
// ---------------------------------------------------------------------
public interface IRecommendationService
{
    Task<RecommendationPlanViewModel> BuildPlanAsync(string? ownerCode, CancellationToken ct = default);
}

public class RecommendationService : IRecommendationService
{
    private static readonly Dictionary<string, string> Alternatives = new()
    {
        ["AVIAO"] = "ONIBUS",
        ["CARRO"] = "ONIBUS",
        ["MOTO"] = "ONIBUS",
        ["ONIBUS"] = "BICICLETA",
        ["METRO"] = "BICICLETA"
    };

    private readonly MongoContext _ctx;

    public RecommendationService(MongoContext ctx) => _ctx = ctx;

    public async Task<RecommendationPlanViewModel> BuildPlanAsync(string? ownerCode, CancellationToken ct = default)
    {
        var match = new BsonDocument();
        if (!string.IsNullOrWhiteSpace(ownerCode))
            match.Add("ownerCode", ownerCode);

        var pipeline = new[]
        {
            new BsonDocument("$match", match),
            new BsonDocument("$group", new BsonDocument
            {
                { "_id", "$activityCode" },
                { "activity", new BsonDocument("$first", "$activity") },
                { "count", new BsonDocument("$sum", 1) },
                { "sumValue", new BsonDocument("$sum", "$value") },
                { "sumKgCo2", new BsonDocument("$sum", "$estimatedKgCo2") }
            })
        };

        var grouped = await _ctx.Raw("simulations")
            .Aggregate<BsonDocument>(pipeline, cancellationToken: ct)
            .ToListAsync(ct);

        var factors = await _ctx.EmissionFactors.Find(Builders<EmissionFactor>.Filter.Empty).ToListAsync(ct);
        var factorByCode = factors.ToDictionary(f => f.Code, f => f);

        var analyzed = grouped.Sum(g => g["count"].ToInt32());
        var currentTotal = grouped.Sum(g => g["sumKgCo2"].ToDouble());

        var recommendations = new List<RecommendationItem>();

        foreach (var g in grouped)
        {
            var code = g["_id"].IsBsonNull ? string.Empty : g["_id"].AsString;
            var activityName = g["activity"].IsBsonNull ? code : g["activity"].AsString;
            var sumValue = g["sumValue"].ToDouble();
            var sumKg = g["sumKgCo2"].ToDouble();

            if (Alternatives.TryGetValue(code, out var altCode) && factorByCode.TryGetValue(altCode, out var altFactor))
            {
                var projected = sumValue * altFactor.FactorKgCo2PerUnit;
                var saving = Math.Round(sumKg - projected, 2);
                if (saving > 0.01)
                {
                    recommendations.Add(new RecommendationItem
                    {
                        Activity = activityName,
                        SuggestedAlternative = altFactor.Activity,
                        Message = $"Substituir parte dos trajetos de '{activityName}' por '{altFactor.Activity}' " +
                                  $"reduziria cerca de {saving:F2} kg de CO₂.",
                        EstimatedSavingKgCo2 = saving
                    });
                }
            }
            else if (code == "CARNE_BOVINA" && sumKg > 0)
            {
                var saving = Math.Round(sumKg * 0.5, 2);
                recommendations.Add(new RecommendationItem
                {
                    Activity = activityName,
                    SuggestedAlternative = null,
                    Message = $"Reduzir o consumo de carne bovina em 50% economizaria cerca de {saving:F2} kg de CO₂.",
                    EstimatedSavingKgCo2 = saving
                });
            }
        }

        recommendations = recommendations.OrderByDescending(r => r.EstimatedSavingKgCo2).ToList();
        for (var i = 0; i < recommendations.Count; i++)
            recommendations[i].Priority = i + 1;

        var totalSaving = Math.Round(recommendations.Sum(r => r.EstimatedSavingKgCo2), 2);

        return new RecommendationPlanViewModel
        {
            AnalyzedSimulations = analyzed,
            CurrentKgCo2 = Math.Round(currentTotal, 2),
            ProjectedKgCo2 = Math.Round(Math.Max(currentTotal - totalSaving, 0), 2),
            PotentialSavingKgCo2 = totalSaving,
            PotentialSavingPercent = currentTotal <= 0 ? 0 : Math.Round(totalSaving / currentTotal * 100, 2),
            Recommendations = recommendations
        };
    }
}

// ---------------------------------------------------------------------
// Compensação de carbono
// ---------------------------------------------------------------------
public interface IOffsetService
{
    Task<OffsetQuoteResponse> QuoteAsync(OffsetQuoteRequest request, CancellationToken ct = default);
}

public class OffsetService : IOffsetService
{
    private const double KgCo2PerTreePerYear = 21.0;
    private static readonly string[] SupportedCurrencies = { "BRL", "USD", "EUR" };

    private readonly MongoContext _ctx;

    public OffsetService(MongoContext ctx) => _ctx = ctx;

    public async Task<OffsetQuoteResponse> QuoteAsync(OffsetQuoteRequest request, CancellationToken ct = default)
    {
        var currency = string.IsNullOrWhiteSpace(request.Currency) ? "BRL" : request.Currency.ToUpperInvariant();
        if (!SupportedCurrencies.Contains(currency))
            throw new DomainValidationException($"Moeda '{currency}' não suportada. Use BRL, USD ou EUR.");

        double kgCo2;
        if (request.KgCo2.HasValue)
        {
            if (request.KgCo2.Value < 0)
                throw new DomainValidationException("KgCo2 não pode ser negativo.");
            kgCo2 = request.KgCo2.Value;
        }
        else
        {
            var match = new BsonDocument();
            if (!string.IsNullOrWhiteSpace(request.OwnerCode))
                match.Add("ownerCode", request.OwnerCode);

            var sumPipeline = new[]
            {
                new BsonDocument("$match", match),
                new BsonDocument("$group", new BsonDocument
                {
                    { "_id", BsonNull.Value },
                    { "total", new BsonDocument("$sum", "$estimatedKgCo2") }
                })
            };

            var doc = await _ctx.Raw("simulations")
                .Aggregate<BsonDocument>(sumPipeline, cancellationToken: ct)
                .FirstOrDefaultAsync(ct);

            kgCo2 = doc is null ? 0 : doc["total"].ToDouble();
        }

        var tons = kgCo2 / 1000.0;

        // O preço deixa de ser um Dictionary fixo no código: vem dos
        // projetos reais cadastrados na collection offset_projects.
        var projectFilter = Builders<OffsetProject>.Filter.And(
            Builders<OffsetProject>.Filter.Eq(p => p.Status, "ativo"),
            Builders<OffsetProject>.Filter.ElemMatch(p => p.Pricing, x => x.Currency == currency));

        var projects = await _ctx.OffsetProjects.Find(projectFilter).ToListAsync(ct);

        var quotes = projects
            .Select(p =>
            {
                var price = p.Pricing.First(x => x.Currency == currency).PricePerTon;
                return new OffsetProjectQuote
                {
                    Code = p.Code,
                    Name = p.Name,
                    Type = p.Type,
                    PricePerTon = price,
                    EstimatedCost = Math.Round(tons * price, 2)
                };
            })
            .OrderBy(q => q.EstimatedCost)
            .ToList();

        return new OffsetQuoteResponse
        {
            KgCo2 = Math.Round(kgCo2, 2),
            TonsCo2 = Math.Round(tons, 4),
            Currency = currency,
            EquivalentTrees = (int)Math.Ceiling(kgCo2 / KgCo2PerTreePerYear),
            QuotedAt = DateTime.UtcNow,
            Projects = quotes
        };
    }
}
