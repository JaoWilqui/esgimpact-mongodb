namespace ESGImpact.Api.ViewModels;

public class SimulationRequest
{
    /// <summary>Código do fator (ex.: "CARRO") ou o nome da atividade ("Carro").</summary>
    public string Activity { get; set; } = string.Empty;

    public double Value { get; set; }

    /// <summary>Código do participante dono da simulação (ex.: "CID-003").</summary>
    public string? OwnerCode { get; set; }

    /// <summary>Origem da leitura: mobile | manual | iot | import.</summary>
    public string? Source { get; set; }
}

public class SimulationResponse
{
    public string Id { get; set; } = string.Empty;
    public string ActivityCode { get; set; } = string.Empty;
    public string Activity { get; set; } = string.Empty;
    public double Value { get; set; }
    public string Unit { get; set; } = string.Empty;
    public double EstimatedKgCo2 { get; set; }
    public string Classification { get; set; } = string.Empty;
    public string? OwnerCode { get; set; }
    public string? Source { get; set; }
    public double FactorUsed { get; set; }
    public DateTime CreatedAt { get; set; }
}
