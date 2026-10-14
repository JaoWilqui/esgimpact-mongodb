/* =====================================================================
 * ESGImpact / MongoDB — Amostra de documentos
 * ---------------------------------------------------------------------
 * Mostra documentos de FORMATOS DIFERENTES dentro de cada collection —
 * a evidência central de "modelo flexível" pedida na entrega.
 *   mongosh "$MONGO_URI" /scripts/crud/00b-amostra-documentos.js
 * ===================================================================== */

db = db.getSiblingDB('esgimpact');

function header(t) { print(''); print('='.repeat(72)); print('  ' + t); print('='.repeat(72)); }

/* --- pares escolhidos justamente por terem estruturas distintas ------ */
const AMOSTRAS = [
  { col: 'emission_factors', filtros: [{ code: 'CARRO' }, { code: 'ENERGIA_ELETRICA' }, { code: 'BICICLETA' }],
    nota: 'transporte com variants[] · energia com gridMix e monthlyFactors[] · documento mínimo' },

  { col: 'simulations', filtros: [{ source: 'mobile' }, { source: 'iot' }, { source: 'manual' }],
    nota: 'app com location GeoJSON · sensor com iotDevice · entrada manual enxuta' },

  { col: 'participants', filtros: [{ type: 'cidadao' }, { type: 'organizacao' }, { type: 'orgao_publico' }],
    nota: 'collection POLIMÓRFICA: três formas de documento' },

  { col: 'offset_projects', filtros: [{ type: 'reflorestamento' }, { type: 'biogas' }, { type: 'energia_solar' }],
    nota: 'blocos técnicos distintos: forestry · waste · energy' },

  { col: 'governance_audits', filtros: [{ eventType: 'factor_updated' }, { eventType: 'policy_review' }, { eventType: 'consent_change' }],
    nota: 'cada eventType carrega um payload próprio' }
];

AMOSTRAS.forEach(function (a) {
  header(a.col.toUpperCase() + '  —  ' + a.nota);
  a.filtros.forEach(function (f) {
    const doc = db.getCollection(a.col).findOne(f);
    print('');
    print('---- filtro: ' + JSON.stringify(f) + ' ----');
    if (doc) { printjson(doc); } else { print('(nenhum documento encontrado)'); }
  });
});

header('CHAVES DE PRIMEIRO NÍVEL PRESENTES EM CADA COLLECTION');
AMOSTRAS.forEach(function (a) {
  const chaves = db.getCollection(a.col).aggregate([
    { $project: { arrayofkeyvalue: { $objectToArray: '$$ROOT' } } },
    { $unwind: '$arrayofkeyvalue' },
    { $group: { _id: null, keys: { $addToSet: '$arrayofkeyvalue.k' } } }
  ]).toArray();
  const lista = chaves.length ? chaves[0].keys.sort() : [];
  print('');
  print('-- ' + a.col + '  (' + lista.length + ' campos distintos)');
  print('   ' + lista.join(', '));
});
