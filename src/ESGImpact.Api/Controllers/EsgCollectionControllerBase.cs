using System.Text.Json;
using ESGImpact.Api.Exceptions;
using ESGImpact.Api.Repositories;
using ESGImpact.Api.Services;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Bson.IO;

namespace ESGImpact.Api.Controllers;

/// <summary>
/// Base dos endpoints de CRUD das 5 collections.
/// ---------------------------------------------------------------------
/// Trabalha com <see cref="BsonDocument"/> de ponta a ponta: o corpo da
/// requisição é convertido direto em documento BSON e a resposta devolve
/// o documento como ele está no banco. É isso que preserva o esquema
/// flexível — um POST pode enviar campos que nenhuma classe C# declara
/// (variants, gridMix, socialImpact, checklist...) e eles são gravados,
/// lidos e devolvidos sem qualquer alteração de código ou de esquema.
/// </summary>
public abstract class EsgCollectionControllerBase : ControllerBase
{
    private static readonly JsonWriterSettings JsonSettings =
        new() { OutputMode = JsonOutputMode.RelaxedExtendedJson, Indent = false };

    protected readonly IDocumentRepository Repo;
    protected readonly IAuditService Audit;

    protected EsgCollectionControllerBase(IDocumentRepository repo, IAuditService audit)
    {
        Repo = repo;
        Audit = audit;
    }

    /// <summary>Nome da collection no MongoDB.</summary>
    protected abstract string CollectionName { get; }

    /// <summary>Campo usado como ordenação padrão da listagem.</summary>
    protected virtual string DefaultSortField => "_id";

    // --- helpers -------------------------------------------------------
    private ContentResult BsonJson(BsonDocument value, int statusCode = StatusCodes.Status200OK)
        => new()
        {
            Content = value.ToJson(JsonSettings),
            ContentType = "application/json; charset=utf-8",
            StatusCode = statusCode
        };

    private static BsonDocument ToBson(JsonElement element)
    {
        var doc = BsonDocument.Parse(element.GetRawText());
        return doc;
    }

    // =====================================================================
    // READ — listagem paginada com filtro livre em JSON
    // GET /api/{collection}?page=1&pageSize=20&sort=createdAt&desc=true
    //                       &filter={"category":"transporte"}
    // =====================================================================
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? sort = null,
        [FromQuery] bool desc = false,
        [FromQuery] string? filter = null,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 1;
        if (pageSize > 100) pageSize = 100;

        BsonDocument mongoFilter;
        try
        {
            mongoFilter = string.IsNullOrWhiteSpace(filter) ? new BsonDocument() : BsonDocument.Parse(filter);
        }
        catch (Exception ex)
        {
            throw new DomainValidationException("Filtro JSON inválido: " + ex.Message);
        }

        var (items, total) = await Repo.ListAsync(
            CollectionName, mongoFilter, page, pageSize, sort ?? DefaultSortField, desc, ct);

        var envelope = new BsonDocument
        {
            { "collection", CollectionName },
            { "page", page },
            { "pageSize", pageSize },
            { "totalItems", total },
            { "totalPages", (int)Math.Ceiling(total / (double)pageSize) },
            { "items", new BsonArray(items.Cast<BsonValue>()) }
        };

        return BsonJson(envelope);
    }

    // =====================================================================
    // READ — documento único (aceita o ObjectId ou o código de negócio)
    // GET /api/{collection}/{id}
    // =====================================================================
    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(string id, CancellationToken ct)
    {
        var doc = await Repo.GetByIdAsync(CollectionName, id, ct);
        if (doc is null)
            throw new NotFoundException($"Documento '{id}' não encontrado em '{CollectionName}'.");

        return BsonJson(doc);
    }

    // =====================================================================
    // CREATE — insertOne
    // POST /api/{collection}
    // =====================================================================
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] JsonElement body, CancellationToken ct)
    {
        var doc = ToBson(body);
        var created = await Repo.InsertAsync(CollectionName, doc, ct);

        await Audit.LogAsync("document_created", CollectionName,
            created.GetValue("code", BsonNull.Value).IsBsonNull ? created["_id"].ToString() : created["code"].AsString,
            ct: ct);

        return BsonJson(created, StatusCodes.Status201Created);
    }

    // =====================================================================
    // CREATE em lote — insertMany
    // POST /api/{collection}/bulk
    // =====================================================================
    [HttpPost("bulk")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateMany([FromBody] JsonElement body, CancellationToken ct)
    {
        if (body.ValueKind != JsonValueKind.Array)
            throw new DomainValidationException("O corpo deve ser um array de documentos JSON.");

        var docs = body.EnumerateArray().Select(ToBson).ToList();
        var inserted = await Repo.InsertManyAsync(CollectionName, docs, ct);

        await Audit.LogAsync("documents_bulk_created", CollectionName, null,
            new BsonDocument("insertedCount", inserted), ct);

        return BsonJson(new BsonDocument
        {
            { "collection", CollectionName },
            { "insertedCount", inserted }
        }, StatusCodes.Status201Created);
    }

    // =====================================================================
    // UPDATE parcial — $set (preserva os campos não enviados)
    // PATCH /api/{collection}/{id}
    // =====================================================================
    [HttpPatch("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Patch(string id, [FromBody] JsonElement body, CancellationToken ct)
    {
        var changes = ToBson(body);
        var updated = await Repo.PatchAsync(CollectionName, id, changes, ct);

        if (updated is null)
            throw new NotFoundException($"Documento '{id}' não encontrado em '{CollectionName}'.");

        await Audit.LogAsync("document_updated", CollectionName, id,
            new BsonDocument("changedFields", new BsonArray(changes.Names)), ct);

        return BsonJson(updated);
    }

    // =====================================================================
    // UPDATE integral — replaceOne
    // PUT /api/{collection}/{id}
    // =====================================================================
    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Replace(string id, [FromBody] JsonElement body, CancellationToken ct)
    {
        var doc = ToBson(body);
        var replaced = await Repo.ReplaceAsync(CollectionName, id, doc, ct);

        if (replaced is null)
            throw new NotFoundException($"Documento '{id}' não encontrado em '{CollectionName}'.");

        await Audit.LogAsync("document_replaced", CollectionName, id, ct: ct);

        return BsonJson(replaced);
    }

    // =====================================================================
    // DELETE — deleteOne
    // DELETE /api/{collection}/{id}
    // =====================================================================
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        var removed = await Repo.DeleteAsync(CollectionName, id, ct);
        if (!removed)
            throw new NotFoundException($"Documento '{id}' não encontrado em '{CollectionName}'.");

        await Audit.LogAsync("document_deleted", CollectionName, id, ct: ct);

        return NoContent();
    }
}
