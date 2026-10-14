/* =====================================================================
 * ESGImpact / MongoDB — CRUD 1/4 | CREATE  (insertOne / insertMany)
 * ---------------------------------------------------------------------
 * Executa uma operação de inserção em CADA uma das 5 collections.
 *   mongosh "$MONGO_URI" mongo/crud/01-create.js
 * ===================================================================== */

db = db.getSiblingDB('esgimpact');

function header(t) { print(''); print('='.repeat(72)); print('  ' + t); print('='.repeat(72)); }

/* ---------------------------------------------------------------------
 * 1) emission_factors — insertOne
 * ------------------------------------------------------------------- */
header('CREATE 1/5 — emission_factors.insertOne()');
db.emission_factors.deleteMany({ code: 'TREM_REGIONAL' }); // idempotência
const rFactor = db.emission_factors.insertOne({
  code: 'TREM_REGIONAL',
  activity: 'Trem regional',
  factorKgCo2PerUnit: 0.041,
  unit: 'km',
  fieldLabel: 'Quilômetros percorridos',
  category: 'transporte',
  pillar: 'E',
  scope: 'escopo_3',
  isActive: true,
  source: { agency: 'ANTT', year: 2026, methodology: 'tank-to-wheel' },
  updatedAt: new Date()
});
printjson(rFactor);
printjson(db.emission_factors.findOne({ code: 'TREM_REGIONAL' }));

/* ---------------------------------------------------------------------
 * 2) participants — insertMany (formas diferentes no mesmo comando)
 * ------------------------------------------------------------------- */
header('CREATE 2/5 — participants.insertMany()');
db.participants.deleteMany({ code: { $in: ['CID-008', 'ORG-004'] } });
const rParticipants = db.participants.insertMany([
  {
    code: 'CID-008',
    type: 'cidadao',
    name: 'Helena Duarte',
    district: 'Vila Mariana',
    city: 'São Paulo',
    joinedAt: new Date(),
    engagement: { level: 'bronze', points: 0, streakDays: 0 },
    consent: { lgpdAccepted: true, shareAnonymizedData: true }
  },
  {
    // MESMA collection, documento com estrutura totalmente diferente
    code: 'ORG-004',
    type: 'organizacao',
    name: 'Bike Express Entregas',
    cnpj: '11.222.333/0001-44',
    sector: 'logistica',
    size: 'pequena',
    employees: 54,
    city: 'São Paulo',
    joinedAt: new Date(),
    corporateGoals: [{ metric: 'tonCo2Ano', target: 9, baseline: 21 }],
    socialPrograms: [{ name: 'Bolsa-manutenção para ciclistas', beneficiaries: 54 }],
    consent: { lgpdAccepted: true, shareAnonymizedData: true }
  }
]);
printjson(rParticipants);
db.participants.find({ code: { $in: ['CID-008', 'ORG-004'] } }, { code: 1, type: 1, name: 1 }).forEach(printjson);

/* ---------------------------------------------------------------------
 * 3) simulations — insertOne com desnormalização do fator
 * ------------------------------------------------------------------- */
header('CREATE 3/5 — simulations.insertOne()');
db.simulations.deleteMany({ 'batch.fileName': 'demo_crud.csv' });
const factor = db.emission_factors.findOne({ code: 'TREM_REGIONAL' });
const value = 260;
const kg = Math.round(value * factor.factorKgCo2PerUnit * 100) / 100;
const rSim = db.simulations.insertOne({
  activityCode: factor.code,
  activity: factor.activity,
  value: value,
  unit: factor.unit,
  estimatedKgCo2: kg,
  classification: kg <= 1 ? 'Baixo impacto' : (kg <= 10 ? 'Impacto moderado' : 'Alto impacto'),
  ownerCode: 'ORG-004',
  createdAt: new Date(),
  source: 'import',
  batch: { fileName: 'demo_crud.csv', rows: 1, importedBy: 'ANL-001' },
  factorSnapshot: {
    code: factor.code,
    factorKgCo2PerUnit: factor.factorKgCo2PerUnit,
    unit: factor.unit,
    capturedAt: new Date()
  },
  location: { type: 'Point', coordinates: [-46.6390, -23.5890] }
});
printjson(rSim);
printjson(db.simulations.findOne({ _id: rSim.insertedId }));

/* ---------------------------------------------------------------------
 * 4) offset_projects — insertOne
 * ------------------------------------------------------------------- */
header('CREATE 4/5 — offset_projects.insertOne()');
db.offset_projects.deleteMany({ code: 'PRJ-013' });
const rProject = db.offset_projects.insertOne({
  code: 'PRJ-013',
  name: 'Hortas Urbanas em Terrenos Ociosos',
  type: 'agricultura_urbana',
  status: 'piloto',
  capacityTonCo2Year: 42,
  pricing: [{ currency: 'BRL', pricePerTon: 135.0 }],
  location: { type: 'Point', coordinates: [-46.6120, -23.5450], state: 'SP', city: 'São Paulo' },
  urbanFarming: { plots: 18, areaM2: 7400, cropsPerYear: 3 },
  socialImpact: { familiesSupplied: 260, jobsCreated: 14 },
  sdgTargets: [2, 11, 13],
  createdAt: new Date()
});
printjson(rProject);
printjson(db.offset_projects.findOne({ code: 'PRJ-013' }));

/* ---------------------------------------------------------------------
 * 5) governance_audits — insertMany (dois eventos de tipos distintos)
 * ------------------------------------------------------------------- */
header('CREATE 5/5 — governance_audits.insertMany()');
db.governance_audits.deleteMany({ 'demo.script': '01-create' });
const rAudits = db.governance_audits.insertMany([
  {
    eventType: 'factor_created',
    occurredAt: new Date(),
    actor: { participantCode: 'ANL-001', name: 'Ana Lúcia Ferreira', role: 'Analyst' },
    resource: { collection: 'emission_factors', documentCode: 'TREM_REGIONAL' },
    changes: { after: { factorKgCo2PerUnit: 0.041 }, reason: 'Inclusão do modal ferroviário regional' },
    compliance: { framework: 'GHG Protocol', status: 'conforme' },
    demo: { script: '01-create' }
  },
  {
    eventType: 'project_registered',
    occurredAt: new Date(),
    actor: { participantCode: 'ANL-002', name: 'Secretaria Municipal de Meio Ambiente', role: 'Analyst' },
    resource: { collection: 'offset_projects', documentCode: 'PRJ-013' },
    dueDiligence: {
      checklist: [
        { item: 'Titularidade do terreno', status: 'ok' },
        { item: 'Certificação de crédito', status: 'pendente' }
      ],
      riskLevel: 'baixo'
    },
    compliance: { framework: 'ISO 14064', status: 'parcial' },
    demo: { script: '01-create' }
  }
]);
printjson(rAudits);

/* ---------------------------------------------------------------------
 * Contagem final
 * ------------------------------------------------------------------- */
header('CREATE — contagem por collection');
printjson({
  emission_factors: db.emission_factors.countDocuments(),
  simulations: db.simulations.countDocuments(),
  participants: db.participants.countDocuments(),
  offset_projects: db.offset_projects.countDocuments(),
  governance_audits: db.governance_audits.countDocuments()
});
