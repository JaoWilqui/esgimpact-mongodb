/* =====================================================================
 * ESGImpact / MongoDB — 06 | Seed da collection `governance_audits` (G)
 * ---------------------------------------------------------------------
 * 12 documentos. É o pilar de GOVERNANÇA: trilha append-only de tudo o
 * que afeta a credibilidade do dado ESG. É a collection que mais se
 * beneficia do esquema flexível — cada `eventType` tem um payload
 * próprio e novos tipos de evento entram sem migração:
 *
 *   factor_updated   -> `changes.before` / `changes.after`
 *   policy_review    -> `checklist[]` com itens e responsáveis
 *   data_access      -> `query` executada e volume retornado
 *   incident         -> `severity`, `impact`, `resolution`
 *   report_published -> `period`, `metrics`, `assurance`
 *   consent_change   -> `consent.before` / `consent.after`  (LGPD)
 * ===================================================================== */

db = db.getSiblingDB('esgimpact');
db.governance_audits.deleteMany({});

const dt = (s) => new Date(s);

db.governance_audits.insertMany([
  /* ---------- factor_updated ---------------------------------------- */
  {
    eventType: 'factor_updated',
    occurredAt: dt('2026-07-02T14:03:00Z'),
    actor: { participantCode: 'ANL-001', name: 'Ana Lúcia Ferreira', role: 'Analyst' },
    resource: { collection: 'emission_factors', documentCode: 'CARRO' },
    changes: {
      before: { factorKgCo2PerUnit: 0.20 },
      after: { factorKgCo2PerUnit: 0.21 },
      reason: 'Atualização do inventário nacional 2025'
    },
    compliance: { framework: 'GHG Protocol', status: 'conforme' },
    origin: { ip: '10.12.4.31', userAgent: 'ESGImpact-Admin/1.4' }
  },
  {
    eventType: 'factor_updated',
    occurredAt: dt('2026-07-18T09:41:00Z'),
    actor: { participantCode: 'ANL-002', name: 'Secretaria Municipal de Meio Ambiente', role: 'Analyst' },
    resource: { collection: 'emission_factors', documentCode: 'ENERGIA_ELETRICA' },
    changes: {
      before: { factorKgCo2PerUnit: 0.0426 },
      after: { factorKgCo2PerUnit: 0.0385 },
      reason: 'Novo fator médio do SIN divulgado pelo MCTI'
    },
    compliance: { framework: 'GHG Protocol', status: 'conforme' },
    approval: { requiredBy: 'comite_esg', approvedBy: 'ANL-001', approvedAt: dt('2026-07-18T11:00:00Z') }
  },
  {
    eventType: 'factor_updated',
    occurredAt: dt('2026-08-01T16:22:00Z'),
    actor: { participantCode: 'ANL-001', name: 'Ana Lúcia Ferreira', role: 'Analyst' },
    resource: { collection: 'emission_factors', documentCode: 'RESIDUO_ATERRO' },
    changes: {
      before: { isActive: true },
      after: { isActive: false },
      reason: 'Fator substituído pelo índice municipal 2026'
    },
    compliance: { framework: 'ISO 14064', status: 'conforme' }
  },

  /* ---------- policy_review ----------------------------------------- */
  {
    eventType: 'policy_review',
    occurredAt: dt('2026-07-25T13:00:00Z'),
    actor: { participantCode: 'GOV-001', name: 'Companhia de Transporte Metropolitano', role: 'Reviewer' },
    resource: { collection: 'participants', documentCode: 'GOV-001' },
    policy: { code: 'CTM-EL-01', title: 'Eletrificação de 100% da frota até 2035' },
    checklist: [
      { item: 'Meta quantificada e datada', status: 'ok', reviewer: 'ANL-002' },
      { item: 'Orçamento aprovado para 2026', status: 'ok', reviewer: 'ANL-002' },
      { item: 'Plano de infraestrutura de recarga', status: 'pendente', reviewer: 'ANL-001', dueAt: dt('2026-10-01T00:00:00Z') }
    ],
    compliance: { framework: 'GRI 305', status: 'parcial' }
  },
  {
    eventType: 'policy_review',
    occurredAt: dt('2026-08-06T10:15:00Z'),
    actor: { participantCode: 'ANL-002', name: 'Secretaria Municipal de Meio Ambiente', role: 'Reviewer' },
    resource: { collection: 'participants', documentCode: 'ORG-001' },
    policy: { code: 'TV-ESG-2026', title: 'Política interna de descarbonização TechVerde' },
    checklist: [
      { item: 'Inventário de escopos 1, 2 e 3', status: 'ok', reviewer: 'ANL-002' },
      { item: 'Verificação independente', status: 'nao_aplicavel', reviewer: 'ANL-002' }
    ],
    compliance: { framework: 'GRI 305', status: 'conforme' }
  },

  /* ---------- data_access (LGPD / rastreabilidade) ------------------- */
  {
    eventType: 'data_access',
    occurredAt: dt('2026-08-19T15:47:00Z'),
    actor: { participantCode: 'ANL-001', name: 'Ana Lúcia Ferreira', role: 'Analyst' },
    resource: { collection: 'simulations' },
    query: { filter: { ownerCode: 'ORG-001' }, projection: ['activity', 'estimatedKgCo2', 'createdAt'] },
    result: { documentsReturned: 3, containsPersonalData: false },
    legalBasis: 'legitimo_interesse',
    compliance: { framework: 'LGPD', status: 'conforme' }
  },
  {
    eventType: 'data_access',
    occurredAt: dt('2026-08-27T11:02:00Z'),
    actor: { participantCode: 'GOV-002', name: 'Agência Municipal de Resíduos', role: 'Auditor' },
    resource: { collection: 'offset_projects' },
    query: { filter: { status: 'ativo', type: 'captura_metano' } },
    result: { documentsReturned: 1, containsPersonalData: false },
    legalBasis: 'obrigacao_legal',
    compliance: { framework: 'LGPD', status: 'conforme' }
  },

  /* ---------- consent_change (LGPD) --------------------------------- */
  {
    eventType: 'consent_change',
    occurredAt: dt('2026-08-12T08:30:00Z'),
    actor: { participantCode: 'CID-005', name: 'Rafael Tavares', role: 'User' },
    resource: { collection: 'participants', documentCode: 'CID-005' },
    consent: {
      before: { shareAnonymizedData: true },
      after: { shareAnonymizedData: false },
      channel: 'app'
    },
    legalBasis: 'consentimento',
    compliance: { framework: 'LGPD', status: 'conforme' }
  },
  {
    eventType: 'consent_change',
    occurredAt: dt('2026-08-29T17:12:00Z'),
    actor: { participantCode: 'ORG-003', name: 'Rede Alimenta Bairro', role: 'User' },
    resource: { collection: 'participants', documentCode: 'ORG-003' },
    consent: { before: { lgpdAccepted: true }, after: { lgpdAccepted: false }, channel: 'portal' },
    legalBasis: 'consentimento',
    compliance: { framework: 'LGPD', status: 'conforme' },
    followUp: { action: 'suspender_uso_em_relatorios', dueAt: dt('2026-09-05T00:00:00Z') }
  },

  /* ---------- incident ---------------------------------------------- */
  {
    eventType: 'incident',
    occurredAt: dt('2026-08-24T23:10:00Z'),
    actor: { system: 'iot-gateway', role: 'System' },
    resource: { collection: 'simulations', documentCode: 'MED-SP-0471' },
    severity: 'media',
    impact: { description: 'Pico noturno inconsistente no medidor MED-SP-0471', affectedDocuments: 1 },
    resolution: { status: 'resolvido', action: 'leitura marcada com anomalyFlag e revalidada', closedAt: dt('2026-08-25T09:00:00Z') },
    compliance: { framework: 'ISO 14064', status: 'conforme' }
  },
  {
    eventType: 'incident',
    occurredAt: dt('2026-06-11T04:22:00Z'),
    actor: { system: 'api-gateway', role: 'System' },
    resource: { collection: 'participants' },
    severity: 'alta',
    impact: { description: 'Tentativas de login por força bruta', affectedDocuments: 0, blockedRequests: 1420 },
    resolution: { status: 'resolvido', action: 'rate limit por IP + expiração de token reduzida', closedAt: dt('2026-06-11T06:40:00Z') },
    compliance: { framework: 'LGPD', status: 'conforme' }
  },

  /* ---------- report_published -------------------------------------- */
  {
    eventType: 'report_published',
    occurredAt: dt('2026-08-31T18:00:00Z'),
    actor: { participantCode: 'ANL-001', name: 'Ana Lúcia Ferreira', role: 'Analyst' },
    resource: { collection: 'simulations' },
    period: { from: dt('2026-08-01T00:00:00Z'), to: dt('2026-08-31T23:59:59Z'), label: 'Agosto/2026' },
    metrics: { totalKgCo2: 1178.21, simulations: 14, participantsActive: 8, offsetContractedTon: 0.42 },
    assurance: { level: 'limitada', provider: 'Auditoria Verde Consultoria' },
    compliance: { framework: 'GRI 305', status: 'conforme' },
    publishedTo: ['portal_transparencia', 'app_mobile']
  }
]);

print('-- governance_audits: ' + db.governance_audits.countDocuments() + ' documentos inseridos');
print('== seed concluído ==');
printjson({
  emission_factors: db.emission_factors.countDocuments(),
  simulations: db.simulations.countDocuments(),
  participants: db.participants.countDocuments(),
  offset_projects: db.offset_projects.countDocuments(),
  governance_audits: db.governance_audits.countDocuments()
});
