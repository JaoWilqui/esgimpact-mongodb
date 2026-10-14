namespace ESGImpact.Api.Configuration;

/// <summary>
/// Configuração de conexão com o MongoDB (seção "MongoDb" do appsettings
/// ou variáveis de ambiente MongoDb__ConnectionString / MongoDb__Database).
/// </summary>
public class MongoSettings
{
    public const string SectionName = "MongoDb";

    public string ConnectionString { get; set; } = "mongodb://localhost:27017";

    public string Database { get; set; } = "esgimpact";

    /// <summary>Nomes das 5 collections do domínio ESG.</summary>
    public static class Collections
    {
        public const string EmissionFactors = "emission_factors";
        public const string Simulations = "simulations";
        public const string Participants = "participants";
        public const string OffsetProjects = "offset_projects";
        public const string GovernanceAudits = "governance_audits";

        public static readonly string[] All =
        {
            EmissionFactors, Simulations, Participants, OffsetProjects, GovernanceAudits
        };
    }
}
