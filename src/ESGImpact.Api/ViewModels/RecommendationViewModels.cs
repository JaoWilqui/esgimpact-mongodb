namespace ESGImpact.Api.ViewModels;

public class RecommendationPlanViewModel
{
    public int AnalyzedSimulations { get; set; }
    public double CurrentKgCo2 { get; set; }
    public double ProjectedKgCo2 { get; set; }
    public double PotentialSavingKgCo2 { get; set; }
    public double PotentialSavingPercent { get; set; }

    public IReadOnlyList<RecommendationItem> Recommendations { get; set; } = Array.Empty<RecommendationItem>();
}

public class RecommendationItem
{
    public string Activity { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? SuggestedAlternative { get; set; }
    public double EstimatedSavingKgCo2 { get; set; }
    public int Priority { get; set; }
}
