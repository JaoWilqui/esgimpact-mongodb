/* =====================================================================
 * ESGImpact / MongoDB — EXTRA | Aggregation Framework
 * ---------------------------------------------------------------------
 * Reproduz, em um único pipeline, o que no projeto Oracle/SQL Server
 * exigia várias consultas com JOIN e GROUP BY:
 *   - dashboard ESG          (AnalyticsService)
 *   - motor de recomendações (RecommendationService)
 *   - cotação de compensação (OffsetService)
 *   mongosh "$MONGO_URI" mongo/crud/05-aggregations.js
 * ===================================================================== */

db = db.getSiblingDB('esgimpact');

function header(t) { print(''); print('='.repeat(72)); print('  ' + t); print('='.repeat(72)); }

/* ---------------------------------------------------------------------
 * A) Dashboard ESG — $facet resolve totais, ranking e série temporal
 *    em UMA ÚNICA ida ao banco.
 * ------------------------------------------------------------------- */
header('AGG 1 — Dashboard ESG ($facet)');
printjson(db.simulations.aggregate([
  {
    $facet: {
      totais: [
        {
          $group: {
            _id: null,
            totalSimulacoes: { $sum: 1 },
            totalKgCo2: { $sum: '$estimatedKgCo2' },
            mediaKgCo2: { $avg: '$estimatedKgCo2' }
          }
        },
        { $project: { _id: 0, totalSimulacoes: 1, totalKgCo2: { $round: ['$totalKgCo2', 2] }, mediaKgCo2: { $round: ['$mediaKgCo2', 2] } } }
      ],
      rankingPorAtividade: [
        { $group: { _id: '$activity', simulacoes: { $sum: 1 }, totalKgCo2: { $sum: '$estimatedKgCo2' } } },
        { $project: { _id: 0, atividade: '$_id', simulacoes: 1, totalKgCo2: { $round: ['$totalKgCo2', 2] } } },
        { $sort: { totalKgCo2: -1 } },
        { $limit: 5 }
      ],
      tendenciaDiaria: [
        {
          $group: {
            _id: { $dateToString: { format: '%Y-%m-%d', date: '$createdAt' } },
            totalKgCo2: { $sum: '$estimatedKgCo2' }
          }
        },
        { $project: { _id: 0, dia: '$_id', totalKgCo2: { $round: ['$totalKgCo2', 2] } } },
        { $sort: { dia: 1 } }
      ],
      porOrigem: [
        { $group: { _id: '$source', total: { $sum: 1 } } },
        { $project: { _id: 0, origem: '$_id', total: 1 } },
        { $sort: { total: -1 } }
      ]
    }
  }
]).toArray());

/* ---------------------------------------------------------------------
 * B) $lookup — o "JOIN" do MongoDB, usado só onde faz sentido:
 *    emissão total por participante, com nome e tipo.
 * ------------------------------------------------------------------- */
header('AGG 2 — Emissão por participante ($lookup + $group)');
printjson(db.simulations.aggregate([
  { $group: { _id: '$ownerCode', simulacoes: { $sum: 1 }, totalKgCo2: { $sum: '$estimatedKgCo2' } } },
  {
    $lookup: {
      from: 'participants',
      localField: '_id',
      foreignField: 'code',
      as: 'participante'
    }
  },
  { $unwind: { path: '$participante', preserveNullAndEmptyArrays: true } },
  {
    $project: {
      _id: 0,
      codigo: '$_id',
      nome: '$participante.name',
      tipo: '$participante.type',
      simulacoes: 1,
      totalKgCo2: { $round: ['$totalKgCo2', 2] }
    }
  },
  { $sort: { totalKgCo2: -1 } }
]).toArray());

/* ---------------------------------------------------------------------
 * C) Motor de recomendações — projeta a economia ao trocar de modal.
 * ------------------------------------------------------------------- */
header('AGG 3 — Recomendações: economia projetada por troca de modal');
function fatorDe(code, padrao) {
  const f = db.emission_factors.findOne({ code: code });
  if (!f) { print('AVISO: fator ' + code + ' não encontrado; usando ' + padrao); return padrao; }
  return f.factorKgCo2PerUnit;
}
const fatorOnibus = fatorDe('ONIBUS', 0.08);
const fatorBike = fatorDe('BICICLETA', 0.0);

printjson(db.simulations.aggregate([
  { $match: { activityCode: { $in: ['CARRO', 'AVIAO', 'MOTO', 'ONIBUS'] } } },
  {
    $group: {
      _id: '$activityCode',
      atividade: { $first: '$activity' },
      somaValor: { $sum: '$value' },
      somaKgCo2: { $sum: '$estimatedKgCo2' }
    }
  },
  {
    $addFields: {
      alternativa: { $cond: [{ $eq: ['$_id', 'ONIBUS'] }, 'Bicicleta', 'Ônibus'] },
      fatorAlternativa: { $cond: [{ $eq: ['$_id', 'ONIBUS'] }, fatorBike, fatorOnibus] }
    }
  },
  {
    $addFields: {
      kgCo2Projetado: { $round: [{ $multiply: ['$somaValor', '$fatorAlternativa'] }, 2] }
    }
  },
  {
    $addFields: {
      economiaKgCo2: { $round: [{ $subtract: ['$somaKgCo2', '$kgCo2Projetado'] }, 2] }
    }
  },
  { $match: { economiaKgCo2: { $gt: 0.01 } } },
  {
    $project: {
      _id: 0, atividade: 1, alternativa: 1,
      atualKgCo2: { $round: ['$somaKgCo2', 2] },
      projetadoKgCo2: '$kgCo2Projetado',
      economiaKgCo2: 1
    }
  },
  { $sort: { economiaKgCo2: -1 } }
]).toArray());

/* ---------------------------------------------------------------------
 * D) Cotação de compensação — casa a emissão total com os projetos.
 * ------------------------------------------------------------------- */
header('AGG 4 — Cotação de compensação em BRL (top 3 projetos mais baratos)');
const totalAgg = db.simulations.aggregate([
  { $group: { _id: null, t: { $sum: '$estimatedKgCo2' } } }
]).toArray();
const totalKg = totalAgg.length ? totalAgg[0].t : 0;
const totalTon = totalKg / 1000;
print('-- emissão total da base: ' + totalKg.toFixed(2) + ' kg CO2  (' + totalTon.toFixed(4) + ' t)');

printjson(db.offset_projects.aggregate([
  { $match: { status: 'ativo' } },
  { $unwind: '$pricing' },
  { $match: { 'pricing.currency': 'BRL' } },
  {
    $project: {
      _id: 0,
      codigo: '$code',
      projeto: '$name',
      tipo: '$type',
      precoPorTonBRL: '$pricing.pricePerTon',
      custoEstimadoBRL: { $round: [{ $multiply: [totalTon, '$pricing.pricePerTon'] }, 2] },
      arvoresEquivalentes: { $ceil: { $divide: [totalKg, 21] } }
    }
  },
  { $sort: { precoPorTonBRL: 1 } },
  { $limit: 3 }
]).toArray());

/* ---------------------------------------------------------------------
 * E) Governança — indicador de conformidade por framework.
 * ------------------------------------------------------------------- */
header('AGG 5 — Conformidade por framework (pilar G)');
printjson(db.governance_audits.aggregate([
  {
    $group: {
      _id: '$compliance.framework',
      eventos: { $sum: 1 },
      conformes: { $sum: { $cond: [{ $eq: ['$compliance.status', 'conforme'] }, 1, 0] } }
    }
  },
  {
    $project: {
      _id: 0,
      framework: '$_id',
      eventos: 1,
      conformes: 1,
      percentualConformidade: {
        $round: [{ $multiply: [{ $divide: ['$conformes', '$eventos'] }, 100] }, 1]
      }
    }
  },
  { $sort: { framework: 1 } }
]).toArray());

/* ---------------------------------------------------------------------
 * F) Social — engajamento por bairro (pilar S).
 * ------------------------------------------------------------------- */
header('AGG 6 — Engajamento de cidadãos por bairro (pilar S)');
printjson(db.participants.aggregate([
  { $match: { type: 'cidadao' } },
  {
    $group: {
      _id: '$district',
      cidadaos: { $sum: 1 },
      pontosMedios: { $avg: '$engagement.points' },
      nivelOuro: { $sum: { $cond: [{ $eq: ['$engagement.level', 'ouro'] }, 1, 0] } }
    }
  },
  { $project: { _id: 0, bairro: '$_id', cidadaos: 1, pontosMedios: { $round: ['$pontosMedios', 0] }, nivelOuro: 1 } },
  { $sort: { pontosMedios: -1 } }
]).toArray());
