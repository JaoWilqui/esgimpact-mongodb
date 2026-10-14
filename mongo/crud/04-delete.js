/* =====================================================================
 * ESGImpact / MongoDB — CRUD 4/4 | DELETE
 * (deleteOne / deleteMany / findOneAndDelete / soft delete)
 * ---------------------------------------------------------------------
 * Remove apenas os documentos criados pelo script 01-create.js, de modo
 * que a base volte ao estado semeado e o roteiro possa ser repetido.
 *   mongosh "$MONGO_URI" mongo/crud/04-delete.js
 * ===================================================================== */

db = db.getSiblingDB('esgimpact');

function header(t) { print(''); print('='.repeat(72)); print('  ' + t); print('='.repeat(72)); }

header('DELETE — contagem ANTES');
printjson({
  emission_factors: db.emission_factors.countDocuments(),
  simulations: db.simulations.countDocuments(),
  participants: db.participants.countDocuments(),
  offset_projects: db.offset_projects.countDocuments(),
  governance_audits: db.governance_audits.countDocuments()
});

/* ---------------------------------------------------------------------
 * 1) simulations — deleteMany por filtro de lote
 * ------------------------------------------------------------------- */
header('DELETE 1/5 — simulations.deleteMany({ "batch.fileName": "demo_crud.csv" })');
print('-- alvo: ' + db.simulations.countDocuments({ 'batch.fileName': 'demo_crud.csv' }) + ' documento(s)');
printjson(db.simulations.deleteMany({ 'batch.fileName': 'demo_crud.csv' }));
print('-- restantes: ' + db.simulations.countDocuments());

/* ---------------------------------------------------------------------
 * 2) participants — deleteOne + findOneAndDelete
 * ------------------------------------------------------------------- */
header('DELETE 2/5 — participants.deleteOne() e findOneAndDelete()');
printjson(db.participants.deleteOne({ code: 'CID-008' }));

print('\n-- findOneAndDelete devolve o documento removido (útil para log de auditoria):');
printjson(db.participants.findOneAndDelete(
  { code: 'ORG-004' },
  { projection: { _id: 0, code: 1, name: 1, type: 1 } }
));

print('\n-- remoção do participante criado por upsert:');
printjson(db.participants.deleteOne({ code: 'ORG-005', createdBy: 'upsert-demo' }));
print('-- restantes: ' + db.participants.countDocuments());

/* ---------------------------------------------------------------------
 * 3) offset_projects — deleteOne
 * ------------------------------------------------------------------- */
header('DELETE 3/5 — offset_projects.deleteOne({ code: "PRJ-013" })');
printjson(db.offset_projects.deleteOne({ code: 'PRJ-013' }));
print('-- restantes: ' + db.offset_projects.countDocuments());

/* ---------------------------------------------------------------------
 * 4) emission_factors — SOFT DELETE vs. DELETE físico
 * ------------------------------------------------------------------- */
header('DELETE 4/5 — emission_factors: soft delete e delete físico');
print('-- SOFT DELETE (preserva o histórico das simulações que usaram o fator):');
printjson(db.emission_factors.updateOne(
  { code: 'TREM_REGIONAL' },
  { $set: { isActive: false, deactivatedAt: new Date(), deactivationReason: 'demonstração de soft delete' } }
));
printjson(db.emission_factors.findOne({ code: 'TREM_REGIONAL' }, { _id: 0, code: 1, isActive: 1, deactivationReason: 1 }));

print('\n-- DELETE FÍSICO do fator de demonstração:');
printjson(db.emission_factors.deleteOne({ code: 'TREM_REGIONAL' }));
print('-- restantes: ' + db.emission_factors.countDocuments());

/* ---------------------------------------------------------------------
 * 5) governance_audits — deleteMany apenas dos registros de demonstração
 * ------------------------------------------------------------------- */
header('DELETE 5/5 — governance_audits.deleteMany({ "demo.script": "01-create" })');
print('-- OBS.: em produção esta collection é APPEND-ONLY; a remoção abaixo');
print('--       existe apenas para tornar o roteiro de CRUD repetível.');
printjson(db.governance_audits.deleteMany({ 'demo.script': '01-create' }));
print('-- restantes: ' + db.governance_audits.countDocuments());

header('DELETE — contagem DEPOIS');
printjson({
  emission_factors: db.emission_factors.countDocuments(),
  simulations: db.simulations.countDocuments(),
  participants: db.participants.countDocuments(),
  offset_projects: db.offset_projects.countDocuments(),
  governance_audits: db.governance_audits.countDocuments()
});
