/* =====================================================================
 * ESGImpact / MongoDB — CRUD 3/4 | UPDATE
 * ($set, $inc, $push, $unset, upsert, findOneAndUpdate, updateMany)
 * ---------------------------------------------------------------------
 *   mongosh "$MONGO_URI" mongo/crud/03-update.js
 * ===================================================================== */

db = db.getSiblingDB('esgimpact');

function header(t) { print(''); print('='.repeat(72)); print('  ' + t); print('='.repeat(72)); }

/* ---------------------------------------------------------------------
 * 1) emission_factors — $set + $push em array aninhado
 * ------------------------------------------------------------------- */
header('UPDATE 1/5 — emission_factors.updateOne() com $set e $push');
print('-- ANTES:');
printjson(db.emission_factors.findOne({ code: 'CARRO' }, { _id: 0, code: 1, factorKgCo2PerUnit: 1, variants: 1 }));

printjson(db.emission_factors.updateOne(
  { code: 'CARRO' },
  {
    $set: { factorKgCo2PerUnit: 0.219, updatedAt: new Date(), 'source.year': 2026 },
    $push: { variants: { fuel: 'hibrido', factorKgCo2PerUnit: 0.12 } }
  }
));

print('-- DEPOIS:');
printjson(db.emission_factors.findOne({ code: 'CARRO' }, { _id: 0, code: 1, factorKgCo2PerUnit: 1, source: 1, variants: 1 }));

print('\n-- updateMany: marca todos os fatores de alimentação como revisados');
printjson(db.emission_factors.updateMany(
  { category: 'alimentacao' },
  { $set: { review: { status: 'revisado', reviewedAt: new Date(), reviewer: 'ANL-001' } } }
));
printjson(db.emission_factors.find({ category: 'alimentacao' }, { _id: 0, code: 1, review: 1 }).toArray());

/* ---------------------------------------------------------------------
 * 2) simulations — updateMany com $set condicional + $unset
 * ------------------------------------------------------------------- */
header('UPDATE 2/5 — simulations.updateMany() e $unset');
print('-- reclassifica como "Crítico" tudo acima de 500 kg CO2:');
printjson(db.simulations.updateMany(
  { estimatedKgCo2: { $gt: 500 } },
  { $set: { classification: 'Impacto crítico', reviewedAt: new Date() } }
));
printjson(db.simulations.find(
  { classification: 'Impacto crítico' },
  { _id: 0, activity: 1, estimatedKgCo2: 1, classification: 1 }
).toArray());

print('\n-- $unset: remove o flag de anomalia após revalidação do sensor');
printjson(db.simulations.updateOne(
  { 'iotDevice.anomalyFlag': { $exists: true } },
  { $unset: { 'iotDevice.anomalyFlag': '' }, $set: { 'iotDevice.revalidatedAt': new Date() } }
));
printjson(db.simulations.findOne(
  { 'iotDevice.sensorId': 'MED-SP-0471', 'iotDevice.revalidatedAt': { $exists: true } },
  { _id: 0, activity: 1, iotDevice: 1 }
));

/* ---------------------------------------------------------------------
 * 3) participants — $inc, $push e UPSERT
 * ------------------------------------------------------------------- */
header('UPDATE 3/5 — participants: $inc (gamificação), $push e upsert');
print('-- ANTES:');
printjson(db.participants.findOne({ code: 'CID-003' }, { _id: 0, code: 1, engagement: 1, badges: 1 }));

printjson(db.participants.updateOne(
  { code: 'CID-003' },
  {
    $inc: { 'engagement.points': 150, 'engagement.streakDays': 1 },
    $set: { 'engagement.level': 'ouro' },
    $push: { badges: 'meta-agosto' }
  }
));
print('-- DEPOIS:');
printjson(db.participants.findOne({ code: 'CID-003' }, { _id: 0, code: 1, engagement: 1, badges: 1 }));

print('\n-- findOneAndUpdate atualizando um item específico do array (operador posicional $):');
printjson(db.participants.findOneAndUpdate(
  { code: 'CID-005', 'goals.metric': 'kgCo2Mes' },
  { $set: { 'goals.$.achieved': true, 'goals.$.achievedAt': new Date() } },
  { returnDocument: 'after', projection: { _id: 0, code: 1, goals: 1 } }
));

print('\n-- UPSERT: cria o participante se ainda não existir (não existe -> insere)');
printjson(db.participants.updateOne(
  { code: 'ORG-005' },
  {
    $set: {
      type: 'organizacao',
      name: 'Padaria Bairro Verde',
      sector: 'alimentacao',
      size: 'micro',
      city: 'São Paulo',
      consent: { lgpdAccepted: true, shareAnonymizedData: true }
    },
    $setOnInsert: { joinedAt: new Date(), createdBy: 'upsert-demo' }
  },
  { upsert: true }
));
printjson(db.participants.findOne({ code: 'ORG-005' }, { _id: 0, code: 1, name: 1, createdBy: 1 }));

/* ---------------------------------------------------------------------
 * 4) offset_projects — $set em array + $addToSet + pipeline update
 * ------------------------------------------------------------------- */
header('UPDATE 4/5 — offset_projects: reajuste de preço e certificações');
print('-- ANTES:');
printjson(db.offset_projects.findOne({ code: 'PRJ-001' }, { _id: 0, code: 1, pricing: 1, certifications: 1 }));

print('\n-- reajusta o preço em BRL usando o operador posicional filtrado:');
printjson(db.offset_projects.updateOne(
  { code: 'PRJ-001' },
  { $set: { 'pricing.$[el].pricePerTon': 97.5, updatedAt: new Date() } },
  { arrayFilters: [{ 'el.currency': 'BRL' }] }
));

print('\n-- $addToSet: adiciona certificação sem duplicar:');
printjson(db.offset_projects.updateOne(
  { code: 'PRJ-001' },
  { $addToSet: { certifications: 'Gold Standard' } }
));
print('-- DEPOIS:');
printjson(db.offset_projects.findOne({ code: 'PRJ-001' }, { _id: 0, code: 1, pricing: 1, certifications: 1 }));

print('\n-- UPDATE COM PIPELINE: aplica +8% em todo projeto em captação (campo calculado):');
printjson(db.offset_projects.updateMany(
  { status: 'em_captacao' },
  [
    {
      $set: {
        pricing: {
          $map: {
            input: '$pricing',
            as: 'p',
            in: {
              currency: '$$p.currency',
              pricePerTon: { $round: [{ $multiply: ['$$p.pricePerTon', 1.08] }, 2] }
            }
          }
        },
        lastRepricedAt: '$$NOW'
      }
    }
  ]
));
printjson(db.offset_projects.find({ status: 'em_captacao' }, { _id: 0, code: 1, pricing: 1 }).toArray());

/* ---------------------------------------------------------------------
 * 5) governance_audits — $push em checklist + fechamento de incidente
 * ------------------------------------------------------------------- */
header('UPDATE 5/5 — governance_audits: conclusão de revisão e incidente');
print('-- ANTES:');
printjson(db.governance_audits.findOne(
  { eventType: 'policy_review', 'policy.code': 'CTM-EL-01' },
  { _id: 0, eventType: 1, checklist: 1, compliance: 1 }
));

printjson(db.governance_audits.updateOne(
  { eventType: 'policy_review', 'policy.code': 'CTM-EL-01', 'checklist.item': 'Plano de infraestrutura de recarga' },
  {
    $set: {
      'checklist.$.status': 'ok',
      'checklist.$.completedAt': new Date(),
      'compliance.status': 'conforme',
      closedAt: new Date()
    }
  }
));
print('-- DEPOIS:');
printjson(db.governance_audits.findOne(
  { eventType: 'policy_review', 'policy.code': 'CTM-EL-01' },
  { _id: 0, eventType: 1, checklist: 1, compliance: 1, closedAt: 1 }
));

print('\n-- updateMany: reclassifica incidentes já resolvidos há mais de 30 dias');
printjson(db.governance_audits.updateMany(
  { eventType: 'incident', 'resolution.status': 'resolvido', occurredAt: { $lt: ISODate('2026-08-01T00:00:00Z') } },
  { $set: { archived: true, archivedAt: new Date() } }
));
printjson(db.governance_audits.find(
  { eventType: 'incident' },
  { _id: 0, occurredAt: 1, severity: 1, 'resolution.status': 1, archived: 1 }
).toArray());
