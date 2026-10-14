using ESGImpact.Api.Services;
using ESGImpact.Api.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ESGImpact.Api.Controllers;

/* =====================================================================
 * ENDPOINTS DE NEGÓCIO (migrados do projeto relacional)
 * ===================================================================== */

/// <summary>Autenticação — credenciais embutidas na collection participants.</summary>
[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService) => _authService = authService;

    /// <summary>Emite o token JWT. Usuário semeado: analista / Esg@2026.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TokenResponse>> Login([FromBody] LoginRequest request, CancellationToken ct)
        => Ok(await _authService.LoginAsync(request, ct));
}

/// <summary>Cálculo e histórico de simulações de pegada de carbono.</summary>
[ApiController]
[Route("api/simulations")]
[Produces("application/json")]
public class SimulationsController : ControllerBase
{
    private readonly ISimulationService _service;

    public SimulationsController(ISimulationService service) => _service = service;

    /// <summary>Calcula, classifica e PERSISTE uma simulação.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(SimulationResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SimulationResponse>> Create([FromBody] SimulationRequest request, CancellationToken ct)
    {
        var result = await _service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetHistory), new { }, result);
    }

    /// <summary>Histórico paginado de simulações.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<SimulationResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<SimulationResponse>>> GetHistory(
        [FromQuery] PaginationQuery pagination,
        [FromQuery] string? ownerCode,
        [FromQuery] string? activity,
        CancellationToken ct)
        => Ok(await _service.GetHistoryAsync(pagination, ownerCode, activity, ct));
}

/// <summary>Dashboard ESG — endpoint crítico, exige JWT.</summary>
[ApiController]
[Route("api/analytics")]
[Produces("application/json")]
[Authorize]
public class AnalyticsController : ControllerBase
{
    private readonly IAnalyticsService _service;

    public AnalyticsController(IAnalyticsService service) => _service = service;

    /// <summary>Totais, ranking por atividade, tendência diária e origem das leituras.</summary>
    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(DashboardViewModel), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<DashboardViewModel>> GetDashboard(
        [FromQuery] string? ownerCode,
        [FromQuery] int trendDays = 30,
        CancellationToken ct = default)
        => Ok(await _service.GetDashboardAsync(ownerCode, trendDays, ct));
}

/// <summary>Motor de recomendações com economia de CO₂ quantificada.</summary>
[ApiController]
[Route("api/recommendations")]
[Produces("application/json")]
public class RecommendationsController : ControllerBase
{
    private readonly IRecommendationService _service;

    public RecommendationsController(IRecommendationService service) => _service = service;

    [HttpGet]
    [ProducesResponseType(typeof(RecommendationPlanViewModel), StatusCodes.Status200OK)]
    public async Task<ActionResult<RecommendationPlanViewModel>> GetPlan(
        [FromQuery] string? ownerCode, CancellationToken ct)
        => Ok(await _service.BuildPlanAsync(ownerCode, ct));
}

/// <summary>Cotação de compensação com base nos projetos reais cadastrados.</summary>
[ApiController]
[Route("api/offset")]
[Produces("application/json")]
[Authorize]
public class OffsetController : ControllerBase
{
    private readonly IOffsetService _service;

    public OffsetController(IOffsetService service) => _service = service;

    [HttpPost("quote")]
    [ProducesResponseType(typeof(OffsetQuoteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<OffsetQuoteResponse>> Quote([FromBody] OffsetQuoteRequest request, CancellationToken ct)
        => Ok(await _service.QuoteAsync(request, ct));
}
