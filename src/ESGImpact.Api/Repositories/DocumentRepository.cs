using ESGImpact.Api.Data;
using ESGImpact.Api.Exceptions;
using MongoDB.Bson;
using MongoDB.Driver;

namespace ESGImpact.Api.Repositories;

/// <summary>
/// Repositório genérico sobre <see cref="BsonDocument"/>. É ele que dá à
/// API o mesmo grau de liberdade que o mongosh: qualquer documento, com
/// qualquer forma, pode ser criado, lido, alterado e removido em qualquer
/// uma das 5 collections — sem classe fixa e sem migração de esquema.
/// </summary>
public interface IDocumentRepository
{
    Task<(IReadOnlyList<BsonDocument> Items, long Total)> ListAsync(
        string collection, BsonDocument filter, int page, int pageSize, string? sortField, bool descending, CancellationToken ct = default);

    Task<BsonDocument?> GetByIdAsync(string collection, string id, CancellationToken ct = default);

    Task<BsonDocument> InsertAsync(string collection, BsonDocument document, CancellationToken ct = default);

    Task<int> InsertManyAsync(string collection, IEnumerable<BsonDocument> documents, CancellationToken ct = default);

    /// <summary>Atualização parcial ($set) — preserva os campos não enviados.</summary>
    Task<BsonDocument?> PatchAsync(string collection, string id, BsonDocument changes, CancellationToken ct = default);

    /// <summary>Substituição integral do documento (replaceOne).</summary>
    Task<BsonDocument?> ReplaceAsync(string collection, string id, BsonDocument document, CancellationToken ct = default);

    Task<bool> DeleteAsync(string collection, string id, CancellationToken ct = default);

    Task<long> CountAsync(string collection, BsonDocument filter, CancellationToken ct = default);
}

public class DocumentRepository : IDocumentRepository
{
    private readonly MongoContext _ctx;

    public DocumentRepository(MongoContext ctx) => _ctx = ctx;

    private static BsonDocument IdFilter(string id)
    {
        if (ObjectId.TryParse(id, out var oid))
            return new BsonDocument("_id", oid);

        // permite localizar também pelo código de negócio (CARRO, CID-003, PRJ-001...)
        return new BsonDocument("$or", new BsonArray
        {
            new BsonDocument("_id", id),
            new BsonDocument("code", id)
        });
    }

    public async Task<(IReadOnlyList<BsonDocument> Items, long Total)> ListAsync(
        string collection, BsonDocument filter, int page, int pageSize, string? sortField, bool descending, CancellationToken ct = default)
    {
        var col = _ctx.Raw(collection);

        var total = await col.CountDocumentsAsync(filter, cancellationToken: ct);

        var find = col.Find(filter);

        if (!string.IsNullOrWhiteSpace(sortField))
        {
            var sort = descending
                ? Builders<BsonDocument>.Sort.Descending(sortField)
                : Builders<BsonDocument>.Sort.Ascending(sortField);
            find = find.Sort(sort);
        }

        var items = await find
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<BsonDocument?> GetByIdAsync(string collection, string id, CancellationToken ct = default)
        => await _ctx.Raw(collection).Find(IdFilter(id)).FirstOrDefaultAsync(ct);

    public async Task<BsonDocument> InsertAsync(string collection, BsonDocument document, CancellationToken ct = default)
    {
        document.Remove("_id");
        await _ctx.Raw(collection).InsertOneAsync(document, cancellationToken: ct);
        return document;
    }

    public async Task<int> InsertManyAsync(string collection, IEnumerable<BsonDocument> documents, CancellationToken ct = default)
    {
        var list = documents.ToList();
        if (list.Count == 0) return 0;
        foreach (var d in list) d.Remove("_id");
        await _ctx.Raw(collection).InsertManyAsync(list, cancellationToken: ct);
        return list.Count;
    }

    public async Task<BsonDocument?> PatchAsync(string collection, string id, BsonDocument changes, CancellationToken ct = default)
    {
        changes.Remove("_id");
        if (changes.ElementCount == 0)
            throw new DomainValidationException("Informe ao menos um campo para atualizar.");

        var update = new BsonDocument("$set", changes);

        return await _ctx.Raw(collection).FindOneAndUpdateAsync<BsonDocument>(
            IdFilter(id),
            update,
            new FindOneAndUpdateOptions<BsonDocument, BsonDocument> { ReturnDocument = ReturnDocument.After },
            ct);
    }

    public async Task<BsonDocument?> ReplaceAsync(string collection, string id, BsonDocument document, CancellationToken ct = default)
    {
        document.Remove("_id");

        return await _ctx.Raw(collection).FindOneAndReplaceAsync<BsonDocument>(
            IdFilter(id),
            document,
            new FindOneAndReplaceOptions<BsonDocument, BsonDocument> { ReturnDocument = ReturnDocument.After },
            ct);
    }

    public async Task<bool> DeleteAsync(string collection, string id, CancellationToken ct = default)
    {
        var result = await _ctx.Raw(collection).DeleteOneAsync(IdFilter(id), ct);
        return result.DeletedCount > 0;
    }

    public async Task<long> CountAsync(string collection, BsonDocument filter, CancellationToken ct = default)
        => await _ctx.Raw(collection).CountDocumentsAsync(filter, cancellationToken: ct);
}
