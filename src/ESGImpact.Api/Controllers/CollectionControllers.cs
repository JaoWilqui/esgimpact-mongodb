using ESGImpact.Api.Configuration;
using ESGImpact.Api.Repositories;
using ESGImpact.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ESGImpact.Api.Controllers;

/* =====================================================================
 * CRUD das 5 COLLECTIONS
 * ---------------------------------------------------------------------
 * Cada controller expõe o conjunto completo de operações herdado de
 * EsgCollectionControllerBase:
 *   GET    /api/<rota>            -> find + paginação + filtro JSON
 *   GET    /api/<rota>/{id}       -> findOne (ObjectId ou código)
 *   POST   /api/<rota>            -> insertOne
 *   POST   /api/<rota>/bulk       -> insertMany
 *   PATCH  /api/<rota>/{id}       -> updateOne com $set
 *   PUT    /api/<rota>/{id}       -> replaceOne
 *   DELETE /api/<rota>/{id}       -> deleteOne
 * ===================================================================== */

/// <summary>Collection <c>emission_factors</c> — pilar Environmental.</summary>
[ApiController]
[Route("api/emission-factors")]
[Produces("application/json")]
public class EmissionFactorsController : EsgCollectionControllerBase
{
    public EmissionFactorsController(IDocumentRepository repo, IAuditService audit) : base(repo, audit) { }

    protected override string CollectionName => MongoSettings.Collections.EmissionFactors;
    protected override string DefaultSortField => "code";
}

/// <summary>Collection <c>simulations</c> — pilar Environmental.</summary>
[ApiController]
[Route("api/simulation-documents")]
[Produces("application/json")]
public class SimulationDocumentsController : EsgCollectionControllerBase
{
    public SimulationDocumentsController(IDocumentRepository repo, IAuditService audit) : base(repo, audit) { }

    protected override string CollectionName => MongoSettings.Collections.Simulations;
    protected override string DefaultSortField => "createdAt";
}

/// <summary>Collection <c>participants</c> — pilar Social.</summary>
[ApiController]
[Route("api/participants")]
[Produces("application/json")]
public class ParticipantsController : EsgCollectionControllerBase
{
    public ParticipantsController(IDocumentRepository repo, IAuditService audit) : base(repo, audit) { }

    protected override string CollectionName => MongoSettings.Collections.Participants;
    protected override string DefaultSortField => "code";
}

/// <summary>Collection <c>offset_projects</c> — pilares Environmental e Social.</summary>
[ApiController]
[Route("api/offset-projects")]
[Produces("application/json")]
public class OffsetProjectsController : EsgCollectionControllerBase
{
    public OffsetProjectsController(IDocumentRepository repo, IAuditService audit) : base(repo, audit) { }

    protected override string CollectionName => MongoSettings.Collections.OffsetProjects;
    protected override string DefaultSortField => "code";
}

/// <summary>
/// Collection <c>governance_audits</c> — pilar Governance.
/// Consulta protegida por JWT: a trilha de auditoria é informação
/// sensível de governança.
/// </summary>
[ApiController]
[Route("api/governance-audits")]
[Produces("application/json")]
[Authorize]
public class GovernanceAuditsController : EsgCollectionControllerBase
{
    public GovernanceAuditsController(IDocumentRepository repo, IAuditService audit) : base(repo, audit) { }

    protected override string CollectionName => MongoSettings.Collections.GovernanceAudits;
    protected override string DefaultSortField => "occurredAt";
}
