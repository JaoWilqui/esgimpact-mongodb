/* =====================================================================
 * ESGImpact / MongoDB — CRUD 2/4 | READ  (find / countDocuments / etc.)
 * ---------------------------------------------------------------------
 *   mongosh "$MONGO_URI" mongo/crud/02-read.js
 * ===================================================================== */

db = db.getSiblingDB('esgimpact');

function header(t) { print(''); print('='.repeat(72)); print('  ' + t); print('='.repeat(72)); }

/* ---------------------------------------------------------------------
 * 1) emission_factors
 * ------------------------------------------------------------------- */
header('READ 1/5 — emission_factors: ativos de transporte, ordenados');
db.emission_factors
  .find({ category: 'transporte', isActive: true }, { _id: 0, code: 1, activity: 1, factorKgCo2PerUnit: 1, unit: 1 })
  .sort({ factorKgCo2PerUnit: -1 })
  .forEach(printjson);

print('\n-- consulta em campo de ARRAY ANINHADO (variants.fuel = "etanol"):');
printjson(db.emission_factors.findOne({ 'variants.fuel': 'etanol' }, { _id: 0, code: 1, variants: 1 }));

print('\n-- distinct das categorias:');
printjson(db.emission_factors.distinct('category'));

print('\n-- documentos que possuem o bloco regionalFactors ($exists):');
printjson(db.emission_factors.find({ regionalFactors: { $exists: true } }, { _id: 0, code: 1 }).toArray());

/* ---------------------------------------------------------------------
 * 2) simulations
 * ------------------------------------------------------------------- */
header('READ 2/5 — simulations: histórico paginado (page=1, pageSize=5)');
db.simulations
  .find({}, { _id: 0, activity: 1, value: 1, unit: 1, estimatedKgCo2: 1, classification: 1, ownerCode: 1, createdAt: 1 })
  .sort({ createdAt: -1 })
  .skip(0)
  .limit(5)
  .forEach(printjson);

print('\n-- total de documentos: ' + db.simulations.countDocuments());
print('-- alto impacto: ' + db.simulations.countDocuments({ classification: 'Alto impacto' }));

print('\n-- filtro por faixa de data + emissão mínima:');
printjson(db.simulations.find(
  { createdAt: { $gte: ISODate('2026-08-20T00:00:00Z') }, estimatedKgCo2: { $gt: 10 } },
  { _id: 0, activity: 1, estimatedKgCo2: 1, createdAt: 1 }
).toArray());

print('\n-- somente leituras de sensores IoT (campo exclusivo desses documentos):');
printjson(db.simulations.find(
  { 'iotDevice.sensorId': { $exists: true } },
  { _id: 0, activity: 1, 'iotDevice.sensorId': 1, 'iotDevice.signalQuality': 1 }
).toArray());

print('\n-- consulta GEOESPACIAL: simulações a até 8 km da Praça da Sé');
printjson(db.simulations.find(
  {
    location: {
      $near: {
        $geometry: { type: 'Point', coordinates: [-46.6333, -23.5505] },
        $maxDistance: 8000
      }
    }
  },
  { _id: 0, activity: 1, district: 1, estimatedKgCo2: 1 }
).toArray());

/* ---------------------------------------------------------------------
 * 3) participants
 * ------------------------------------------------------------------- */
header('READ 3/5 — participants: polimorfismo e busca por credenciais');
print('-- contagem por tipo:');
printjson(db.participants.aggregate([
  { $group: { _id: '$type', total: { $sum: 1 } } },
  { $sort: { total: -1 } }
]).toArray());

print('\n-- login: busca do usuário "analista" (índice ux_participant_username):');
printjson(db.participants.findOne(
  { 'credentials.username': 'analista' },
  { _id: 0, code: 1, name: 1, role: 1, 'credentials.username': 1 }
));

print('\n-- cidadãos nível ouro com meta atingida ($elemMatch):');
printjson(db.participants.find(
  { type: 'cidadao', 'engagement.level': 'ouro', goals: { $elemMatch: { achieved: true } } },
  { _id: 0, code: 1, name: 1, engagement: 1 }
).toArray());

print('\n-- organizações que reportam por framework GRI:');
printjson(db.participants.find(
  { 'esgReporting.framework': 'GRI' },
  { _id: 0, code: 1, name: 1, esgReporting: 1 }
).toArray());

/* ---------------------------------------------------------------------
 * 4) offset_projects
 * ------------------------------------------------------------------- */
header('READ 4/5 — offset_projects: catálogo e preço por moeda');
db.offset_projects
  .find({ status: 'ativo' }, { _id: 0, code: 1, name: 1, type: 1, capacityTonCo2Year: 1 })
  .sort({ capacityTonCo2Year: -1 })
  .forEach(printjson);

print('\n-- projetos cotados em USD com preço <= 16 (consulta em array de subdocumentos):');
printjson(db.offset_projects.find(
  { pricing: { $elemMatch: { currency: 'USD', pricePerTon: { $lte: 16 } } } },
  { _id: 0, code: 1, name: 1, pricing: 1 }
).toArray());

print('\n-- projetos que atendem ao ODS 13 (array simples):');
printjson(db.offset_projects.find({ sdgTargets: 13 }, { _id: 0, code: 1, sdgTargets: 1 }).toArray());

print('\n-- projetos com impacto social declarado ($exists) e >= 20 empregos:');
printjson(db.offset_projects.find(
  { 'socialImpact.jobsCreated': { $gte: 20 } },
  { _id: 0, code: 1, name: 1, 'socialImpact.jobsCreated': 1 }
).toArray());

/* ---------------------------------------------------------------------
 * 5) governance_audits
 * ------------------------------------------------------------------- */
header('READ 5/5 — governance_audits: trilha de auditoria');
print('-- eventos por tipo:');
printjson(db.governance_audits.aggregate([
  { $group: { _id: '$eventType', total: { $sum: 1 } } },
  { $sort: { _id: 1 } }
]).toArray());

print('\n-- histórico completo do fator CARRO (rastreabilidade):');
printjson(db.governance_audits.find(
  { 'resource.collection': 'emission_factors', 'resource.documentCode': 'CARRO' },
  { _id: 0, eventType: 1, occurredAt: 1, 'actor.name': 1, changes: 1 }
).sort({ occurredAt: -1 }).toArray());

print('\n-- eventos LGPD (consentimento e acesso a dados):');
printjson(db.governance_audits.find(
  { 'compliance.framework': 'LGPD' },
  { _id: 0, eventType: 1, occurredAt: 1, legalBasis: 1, 'compliance.status': 1 }
).sort({ occurredAt: 1 }).toArray());

print('\n-- pendências de conformidade (status != conforme):');
printjson(db.governance_audits.find(
  { 'compliance.status': { $ne: 'conforme' } },
  { _id: 0, eventType: 1, 'resource.documentCode': 1, 'compliance': 1 }
).toArray());

/* ---------------------------------------------------------------------
 * Uso de índice — evidência de performance
 * ------------------------------------------------------------------- */
header('READ — explain() comprovando uso do índice ix_sim_owner_createdAt');
const plan = db.simulations
  .find({ ownerCode: 'ORG-001' })
  .sort({ createdAt: -1 })
  .explain('executionStats');
printjson({
  stage: plan.queryPlanner.winningPlan.inputStage ? plan.queryPlanner.winningPlan.inputStage.stage : plan.queryPlanner.winningPlan.stage,
  indexName: JSON.stringify(plan.queryPlanner.winningPlan).match(/"indexName":"([^"]+)"/),
  totalDocsExamined: plan.executionStats.totalDocsExamined,
  nReturned: plan.executionStats.nReturned,
  executionTimeMillis: plan.executionStats.executionTimeMillis
});
