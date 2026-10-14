/* =====================================================================
 * ESGImpact / MongoDB — 01 | Criação das 5 collections e dos índices
 * ---------------------------------------------------------------------
 * Executado automaticamente pelo container (docker-entrypoint-initdb.d)
 * ou manualmente:
 *   mongosh "mongodb://esgadmin:Esg%40Mongo2026@localhost:27017/esgimpact?authSource=admin" \
 *           mongo/init/01-create-collections.js
 * ===================================================================== */

const dbName = 'esgimpact';
db = db.getSiblingDB(dbName);

print('== ESGImpact | criando collections em ' + dbName + ' ==');

/* ---------------------------------------------------------------------
 * Cria a collection se ela ainda não existir; caso já exista, apenas
 * aplica o validador via collMod. Assim o script pode ser executado
 * mais de uma vez.
 * ------------------------------------------------------------------- */
function createCollection(name, options) {
  if (db.getCollectionNames().indexOf(name) >= 0) {
    print('-- ' + name + ' já existe');
    if (options && options.validator) {
      db.runCommand({
        collMod: name,
        validator: options.validator,
        validationLevel: options.validationLevel || 'moderate',
        validationAction: options.validationAction || 'error'
      });
      print('   validador reaplicado via collMod');
    }
    return;
  }
  db.createCollection(name, options);
  print('-- ' + name + ' criada');
}

// ---------------------------------------------------------------------
// 1) emission_factors  (E - Environmental)
//    Fatores de emissão de CO2 por atividade. Validação PERMISSIVA de
//    propósito: o schema exige apenas o núcleo obrigatório e libera
//    campos extras (variantes regionais, mix energético, sazonalidade).
// ---------------------------------------------------------------------
createCollection('emission_factors', {
  validator: {
    $jsonSchema: {
      bsonType: 'object',
      required: ['code', 'activity', 'factorKgCo2PerUnit', 'unit', 'isActive'],
      properties: {
        code: { bsonType: 'string', description: 'Código único da atividade' },
        activity: { bsonType: 'string' },
        // 'number' aceita qualquer tipo numérico BSON (int, long, double, decimal)
        factorKgCo2PerUnit: { bsonType: 'number', minimum: 0 },
        unit: { bsonType: 'string' },
        isActive: { bsonType: 'bool' },
        pillar: { enum: ['E', 'S', 'G'] }
      }
    }
  },
  validationLevel: 'moderate',
  validationAction: 'error'
});

// ---------------------------------------------------------------------
// 2) simulations  (E - Environmental)
//    Cada simulação de pegada de carbono feita por um participante.
//    Coleção de alto volume -> índices de série temporal e geoespacial.
// ---------------------------------------------------------------------
createCollection('simulations');

// ---------------------------------------------------------------------
// 3) participants  (S - Social)
//    Cidadãos, organizações e órgãos públicos aderentes ao programa.
//    Coleção polimórfica: o campo `type` define a forma do documento.
// ---------------------------------------------------------------------
createCollection('participants');

// ---------------------------------------------------------------------
// 4) offset_projects  (E + S)
//    Projetos de compensação de carbono disponíveis no marketplace.
// ---------------------------------------------------------------------
createCollection('offset_projects');

// ---------------------------------------------------------------------
// 5) governance_audits  (G - Governance)
//    Trilha de auditoria e conformidade. Somente-append por natureza.
// ---------------------------------------------------------------------
createCollection('governance_audits');

print('-- collections criadas: ' + db.getCollectionNames().sort().join(', '));

/* =====================================================================
 * ÍNDICES
 * ===================================================================== */
print('== criando índices ==');

// emission_factors
db.emission_factors.createIndex({ code: 1 }, { unique: true, name: 'ux_factor_code' });
db.emission_factors.createIndex({ category: 1, isActive: 1 }, { name: 'ix_factor_category_active' });

// simulations
db.simulations.createIndex({ createdAt: -1 }, { name: 'ix_sim_createdAt' });
db.simulations.createIndex({ ownerCode: 1, createdAt: -1 }, { name: 'ix_sim_owner_createdAt' });
db.simulations.createIndex({ activityCode: 1 }, { name: 'ix_sim_activity' });
db.simulations.createIndex({ location: '2dsphere' }, { name: 'ix_sim_geo', sparse: true });

// participants
db.participants.createIndex({ code: 1 }, { unique: true, name: 'ux_participant_code' });
db.participants.createIndex(
  { 'credentials.username': 1 },
  { unique: true, sparse: true, name: 'ux_participant_username' }
);
db.participants.createIndex({ type: 1, district: 1 }, { name: 'ix_participant_type_district' });

// offset_projects
db.offset_projects.createIndex({ code: 1 }, { unique: true, name: 'ux_project_code' });
db.offset_projects.createIndex({ status: 1, type: 1 }, { name: 'ix_project_status_type' });
db.offset_projects.createIndex({ 'pricing.currency': 1 }, { name: 'ix_project_currency' });

// governance_audits
db.governance_audits.createIndex({ occurredAt: -1 }, { name: 'ix_audit_occurredAt' });
db.governance_audits.createIndex({ eventType: 1, occurredAt: -1 }, { name: 'ix_audit_event' });
db.governance_audits.createIndex(
  { 'resource.collection': 1, 'resource.documentCode': 1 },
  { name: 'ix_audit_resource' }
);

print('-- índices criados com sucesso');
print('== 01-create-collections.js concluído ==');
