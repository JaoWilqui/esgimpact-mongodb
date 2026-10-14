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

    /// <summary>
    /// Usuário e senha opcionais. Quando informados, sobrescrevem as
    /// credenciais da connection string — evita ter de codificar
    /// caracteres especiais da senha (como @) dentro da URL.
    /// </summary>
    public string? Username { get; set; }

    public string? Password { get; set; }

    /// <summary>Banco de autenticação (padrão: admin).</summary>
    public string AuthenticationSource { get; set; } = "admin";

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
