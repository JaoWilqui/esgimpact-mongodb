namespace ESGImpact.Api.ViewModels;

public class OffsetQuoteRequest
{
    /// <summary>Quando informado, ignora o histórico e cota este valor.</summary>
    public double? KgCo2 { get; set; }

    /// <summary>Código do participante — restringe o total do histórico.</summary>
    public string? OwnerCode { get; set; }

    public string Currency { get; set; } = "BRL";
}

public class OffsetQuoteResponse
{
    public double KgCo2 { get; set; }
    public double TonsCo2 { get; set; }
    public string Currency { get; set; } = "BRL";
    public int EquivalentTrees { get; set; }
    public DateTime QuotedAt { get; set; }

    /// <summary>Cotação por projeto real da collection offset_projects.</summary>
    public IReadOnlyList<OffsetProjectQuote> Projects { get; set; } = Array.Empty<OffsetProjectQuote>();
}

public class OffsetProjectQuote
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public double PricePerTon { get; set; }
    public double EstimatedCost { get; set; }
}
