using ESGImpact.Api.Configuration;
using ESGImpact.Api.Data;
using ESGImpact.Api.Models;
using ESGImpact.Api.Security;
using ESGImpact.Api.Services;
using ESGImpact.Api.Validators;
using ESGImpact.Api.ViewModels;
using Microsoft.Extensions.Options;

namespace ESGImpact.Tests.Unit;

/// <summary>
/// Testes de unidade: não dependem de banco nem de rede.
/// Executados no pipeline com o filtro Category=Unit.
/// </summary>
[Trait("Category", "Unit")]
public class ImpactCalculatorTests
{
    // O MongoClient não abre conexão no construtor, então o serviço pode
    // ser instanciado sem um MongoDB disponível.
    private static ImpactCalculatorService NewCalculator() =>
        new(new MongoContext(Options.Create(new MongoSettings())));

    [Theory]
    [InlineData(0.0, "Baixo impacto")]
    [InlineData(1.0, "Baixo impacto")]
    [InlineData(1.01, "Impacto moderado")]
    [InlineData(10.0, "Impacto moderado")]
    [InlineData(25.2, "Alto impacto")]
    [InlineData(500.0, "Alto impacto")]
    [InlineData(500.01, "Impacto crítico")]
    public void Classify_retorna_a_faixa_correta(double kgCo2, string esperado)
    {
        Assert.Equal(esperado, NewCalculator().Classify(kgCo2));
    }

    [Theory]
    [InlineData(120, 0.21, 25.2)]    // carro
    [InlineData(1.2, 27.0, 32.4)]    // carne bovina
    [InlineData(18.5, 0.0, 0.0)]     // bicicleta
    [InlineData(3, 0.333, 1.0)]      // arredondamento em 2 casas
    public void CalculateEmission_multiplica_e_arredonda(double valor, double fator, double esperado)
    {
        var factor = new EmissionFactor { Code = "X", FactorKgCo2PerUnit = fator, Unit = "km" };
        Assert.Equal(esperado, NewCalculator().CalculateEmission(factor, valor), 2);
    }
}

[Trait("Category", "Unit")]
public class PasswordHasherTests
{
    [Fact]
    public void Hash_e_Verify_aceitam_a_senha_correta()
    {
        var (hash, salt) = PasswordHasher.Hash("Esg@2026");
        Assert.True(PasswordHasher.Verify("Esg@2026", hash, salt));
    }

    [Fact]
    public void Verify_rejeita_senha_errada()
    {
        var (hash, salt) = PasswordHasher.Hash("Esg@2026");
        Assert.False(PasswordHasher.Verify("outra-senha", hash, salt));
    }

    [Fact]
    public void Hash_usa_salt_aleatorio()
    {
        var a = PasswordHasher.Hash("mesma-senha");
        var b = PasswordHasher.Hash("mesma-senha");
        Assert.NotEqual(a.salt, b.salt);
        Assert.NotEqual(a.hash, b.hash);
    }

    [Fact]
    public void Hash_gravado_na_carga_inicial_confere_com_o_algoritmo_da_api()
    {
        // valores de mongo/init/03-seed-participants.js (usuário "analista")
        const string hash = "lFAlRaMmgBEwNvyXIxq4Q7p3mJLO0tFhVzgmwSVyTww=";
        const string salt = "DHKjHQY3EUmiDauSOckAew==";
        Assert.True(PasswordHasher.Verify("Esg@2026", hash, salt));
    }
}

[Trait("Category", "Unit")]
public class PaginationTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(3, 3)]
    public void Page_minimo_e_1(int informado, int esperado)
    {
        Assert.Equal(esperado, new PaginationQuery { Page = informado }.Page);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(50, 50)]
    [InlineData(500, 100)]
    public void PageSize_fica_entre_1_e_100(int informado, int esperado)
    {
        Assert.Equal(esperado, new PaginationQuery { PageSize = informado }.PageSize);
    }

    [Fact]
    public void PagedResult_calcula_total_de_paginas_e_navegacao()
    {
        var r = new PagedResult<int> { Page = 2, PageSize = 5, TotalItems = 12 };
        Assert.Equal(3, r.TotalPages);
        Assert.True(r.HasPrevious);
        Assert.True(r.HasNext);
    }
}

[Trait("Category", "Unit")]
public class ValidatorTests
{
    [Fact]
    public void Simulacao_valida_passa()
    {
        var r = new SimulationRequestValidator().Validate(new SimulationRequest { Activity = "CARRO", Value = 10 });
        Assert.True(r.IsValid);
    }

    [Fact]
    public void Simulacao_sem_atividade_falha()
    {
        var r = new SimulationRequestValidator().Validate(new SimulationRequest { Activity = "", Value = 10 });
        Assert.False(r.IsValid);
    }

    [Fact]
    public void Simulacao_com_valor_negativo_falha()
    {
        var r = new SimulationRequestValidator().Validate(new SimulationRequest { Activity = "CARRO", Value = -1 });
        Assert.False(r.IsValid);
    }

    [Fact]
    public void Cotacao_com_kg_negativo_falha()
    {
        var r = new OffsetQuoteRequestValidator().Validate(new OffsetQuoteRequest { KgCo2 = -10, Currency = "BRL" });
        Assert.False(r.IsValid);
    }

    [Fact]
    public void Login_vazio_falha()
    {
        var r = new LoginRequestValidator().Validate(new LoginRequest());
        Assert.False(r.IsValid);
    }
}

[Trait("Category", "Unit")]
public class MongoContextTests
{
    [Fact]
    public void Credenciais_separadas_sobrescrevem_a_connection_string()
    {
        var ctx = new MongoContext(Options.Create(new MongoSettings
        {
            ConnectionString = "mongodb://mongo:27017",
            Database = "esgimpact",
            Username = "esgadmin",
            Password = "Esg@Mongo2026" // '@' não precisa ser codificado
        }));

        var credential = ctx.Database.Client.Settings.Credential;
        Assert.NotNull(credential);
        Assert.Equal("esgadmin", credential.Username);
        Assert.Equal("admin", credential.Source);
        Assert.Equal("esgimpact", ctx.Database.DatabaseNamespace.DatabaseName);
    }

    [Fact]
    public void Sem_credenciais_separadas_usa_a_connection_string_como_esta()
    {
        var ctx = new MongoContext(Options.Create(new MongoSettings
        {
            ConnectionString = "mongodb://localhost:27017",
            Database = "outro"
        }));

        Assert.Null(ctx.Database.Client.Settings.Credential);
        Assert.Equal("outro", ctx.Database.DatabaseNamespace.DatabaseName);
    }
}
