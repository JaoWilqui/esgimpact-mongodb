/* =====================================================================
 * ESGImpact / MongoDB — Inventário do banco
 * ---------------------------------------------------------------------
 * Lista collections, contagem de documentos, índices e o validador
 * $jsonSchema. É a evidência de que a estrutura foi criada corretamente.
 *   mongosh "$MONGO_URI" /scripts/crud/00-inventario.js
 * ===================================================================== */

db = db.getSiblingDB('esgimpact');

const COLLECTIONS = [
  'emission_factors',
  'simulations',
  'participants',
  'offset_projects',
  'governance_audits'
];

function header(t) { print(''); print('='.repeat(72)); print('  ' + t); print('='.repeat(72)); }

header('COLLECTIONS EXISTENTES');
printjson(db.getCollectionNames().sort());

header('CONTAGEM DE DOCUMENTOS');
const counts = {};
COLLECTIONS.forEach(function (c) { counts[c] = db.getCollection(c).countDocuments(); });
printjson(counts);
print('TOTAL: ' + COLLECTIONS.reduce(function (a, c) { return a + counts[c]; }, 0) + ' documentos');

header('ÍNDICES');
COLLECTIONS.forEach(function (c) {
  print('-- ' + c);
  db.getCollection(c).getIndexes().forEach(function (i) {
    const flags = [];
    if (i.unique) flags.push('unique');
    if (i.sparse) flags.push('sparse');
    print('   ' + i.name + '  ' + JSON.stringify(i.key) + (flags.length ? '  [' + flags.join(', ') + ']' : ''));
  });
});

header('VALIDADOR $jsonSchema DE emission_factors');
const info = db.getCollectionInfos({ name: 'emission_factors' });
printjson(info.length ? info[0].options : { aviso: 'collection não encontrada' });

header('ESTATÍSTICAS DE ARMAZENAMENTO');
COLLECTIONS.forEach(function (c) {
  const s = db.getCollection(c).stats();
  print('   ' + c.padEnd(20) + ' docs=' + String(s.count).padStart(3) +
        '  tamanho=' + s.size + 'B  índices=' + s.nindexes);
});
