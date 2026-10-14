namespace ESGImpact.Api.ViewModels;

public class DashboardViewModel
{
    public long TotalSimulations { get; set; }
    public double TotalKgCo2 { get; set; }
    public double AverageKgCo2 { get; set; }

    public string? TopEmittingActivity { get; set; }

    public IReadOnlyList<ActivityBreakdownItem> Breakdown { get; set; } = Array.Empty<ActivityBreakdownItem>();

    public IReadOnlyList<TrendPoint> DailyTrend { get; set; } = Array.Empty<TrendPoint>();

    public IReadOnlyList<SourceBreakdownItem> BySource { get; set; } = Array.Empty<SourceBreakdownItem>();

    public double EquivalentTreesPerYear { get; set; }
}

public class ActivityBreakdownItem
{
    public string Activity { get; set; } = string.Empty;
    public int Count { get; set; }
    public double TotalKgCo2 { get; set; }
    public double PercentOfTotal { get; set; }
}

public class TrendPoint
{
    public string Date { get; set; } = string.Empty;
    public double TotalKgCo2 { get; set; }
}

public class SourceBreakdownItem
{
    public string Source { get; set; } = string.Empty;
    public int Count { get; set; }
}
