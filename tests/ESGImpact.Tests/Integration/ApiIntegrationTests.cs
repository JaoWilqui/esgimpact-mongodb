using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace ESGImpact.Tests.Integration;

/// <summary>
/// Testes de integração: exercitam a API de ponta a ponta (HTTP → serviços
/// → MongoDB real). Executados no pipeline com o filtro Category=Integration.
/// </summary>
[Trait("Category", "Integration")]
[Collection(ApiCollection.Name)]
public class ApiIntegrationTests
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public ApiIntegrationTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // ---------------------------------------------------------------------
    // helpers
    // ---------------------------------------------------------------------
    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(text);
        return doc.RootElement.Clone();
    }

    private async Task<HttpClient> AuthenticatedClientAsync()
    {
        var login = await _client.PostAsJsonAsync("/api/auth/login",
            new { username = "analista", password = "Esg@2026" });
        login.EnsureSuccessStatusCode();

        var token = (await JsonAsync(login)).GetProperty("accessToken").GetString();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    // ---------------------------------------------------------------------
    // diagnóstico
    // ---------------------------------------------------------------------
    [Fact]
    public async Task Health_responde_ok_com_mongo_up()
    {
        var response = await _client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await JsonAsync(response);
        Assert.Equal("ok", body.GetProperty("status").GetString());
        Assert.Equal("up", body.GetProperty("mongo").GetString());
    }

    [Fact]
    public async Task Info_identifica_o_ambiente_e_o_banco()
    {
        var body = await JsonAsync(await _client.GetAsync("/api/info"));

        Assert.Equal("Testing", body.GetProperty("environment").GetString());
        Assert.Equal(_factory.DatabaseName, body.GetProperty("database").GetString());
    }

    // ---------------------------------------------------------------------
    // autenticação
    // ---------------------------------------------------------------------
    [Fact]
    public async Task Login_valido_devolve_token_jwt()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new { username = "analista", password = "Esg@2026" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await JsonAsync(response);
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("accessToken").GetString()));
        Assert.Equal("Analyst", body.GetProperty("role").GetString());
    }

    [Fact]
    public async Task Login_com_senha_errada_devolve_400()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new { username = "analista", password = "errada" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Endpoint_protegido_sem_token_devolve_401()
    {
        var response = await _client.GetAsync("/api/analytics/dashboard");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---------------------------------------------------------------------
    // regra de negócio: simulação de emissão
    // ---------------------------------------------------------------------
    [Fact]
    public async Task Simulacao_calcula_classifica_e_persiste()
    {
        var response = await _client.PostAsJsonAsync("/api/simulations",
            new { activity = "CARRO", value = 100, ownerCode = "CID-TESTE" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await JsonAsync(response);
        Assert.Equal(21.0, body.GetProperty("estimatedKgCo2").GetDouble(), 2);
        Assert.Equal("Alto impacto", body.GetProperty("classification").GetString());
        Assert.Equal(0.21, body.GetProperty("factorUsed").GetDouble(), 3);

        var history = await JsonAsync(await _client.GetAsync("/api/simulations?ownerCode=CID-TESTE"));
        Assert.True(history.GetProperty("totalItems").GetInt64() >= 1);
    }

    [Fact]
    public async Task Simulacao_de_atividade_inexistente_devolve_404()
    {
        var response = await _client.PostAsJsonAsync("/api/simulations",
            new { activity = "FOGUETE", value = 1 });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---------------------------------------------------------------------
    // CRUD genérico sobre a collection emission_factors
    // ---------------------------------------------------------------------
    [Fact]
    public async Task Crud_completo_de_emission_factors()
    {
        const string code = "TREM_TESTE";

        // CREATE — campo `source` não existe na classe C#: esquema flexível
        var created = await _client.PostAsJsonAsync("/api/emission-factors", new
        {
            code,
            activity = "Trem regional",
            factorKgCo2PerUnit = 0.041,
            unit = "km",
            category = "transporte",
            isActive = true,
            source = new { agency = "ANTT", year = 2026 }
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        // READ
        var read = await _client.GetAsync($"/api/emission-factors/{code}");
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        var doc = await JsonAsync(read);
        Assert.Equal("ANTT", doc.GetProperty("source").GetProperty("agency").GetString());

        // UPDATE ($set parcial)
        var patch = new HttpRequestMessage(HttpMethod.Patch, $"/api/emission-factors/{code}")
        {
            Content = JsonContent.Create(new { factorKgCo2PerUnit = 0.038 })
        };
        var updated = await _client.SendAsync(patch);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal(0.038, (await JsonAsync(updated)).GetProperty("factorKgCo2PerUnit").GetDouble(), 3);

        // DELETE
        var deleted = await _client.DeleteAsync($"/api/emission-factors/{code}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        var gone = await _client.GetAsync($"/api/emission-factors/{code}");
        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
    }

    [Fact]
    public async Task Codigo_duplicado_viola_indice_unico_e_devolve_409()
    {
        var response = await _client.PostAsJsonAsync("/api/emission-factors", new
        {
            code = "CARRO",
            activity = "Carro duplicado",
            factorKgCo2PerUnit = 0.3,
            unit = "km",
            isActive = true
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // ---------------------------------------------------------------------
    // endpoints analíticos e de governança (exigem JWT)
    // ---------------------------------------------------------------------
    [Fact]
    public async Task Dashboard_autenticado_devolve_indicadores()
    {
        await _client.PostAsJsonAsync("/api/simulations", new { activity = "ONIBUS", value = 50 });

        var client = await AuthenticatedClientAsync();
        var response = await client.GetAsync("/api/analytics/dashboard");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True((await JsonAsync(response)).GetProperty("totalSimulations").GetInt64() >= 1);
    }

    [Fact]
    public async Task Cotacao_de_compensacao_usa_os_projetos_cadastrados()
    {
        var client = await AuthenticatedClientAsync();
        var response = await client.PostAsJsonAsync("/api/offset/quote", new { kgCo2 = 1000, currency = "BRL" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await JsonAsync(response);
        var projects = body.GetProperty("projects");
        Assert.True(projects.GetArrayLength() >= 1);
        Assert.Equal(90.0, projects[0].GetProperty("estimatedCost").GetDouble(), 2); // 1 t x R$ 90
    }

    [Fact]
    public async Task Trilha_de_auditoria_exige_token_e_registra_eventos()
    {
        var anonymous = await _client.GetAsync("/api/governance-audits");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        await _client.PostAsJsonAsync("/api/simulations", new { activity = "BICICLETA", value = 5 });

        var client = await AuthenticatedClientAsync();
        var response = await client.GetAsync("/api/governance-audits");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True((await JsonAsync(response)).GetProperty("totalItems").GetInt64() >= 1);
    }
}
