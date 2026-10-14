/* =====================================================================
 * ESGImpact / MongoDB — 03 | Seed da collection `participants`  (S)
 * ---------------------------------------------------------------------
 * 12 documentos. É a collection POLIMÓRFICA do projeto: um mesmo
 * `participants` guarda três formas distintas de documento —
 *   type = 'cidadao'       -> engajamento, metas pessoais, acessibilidade
 *   type = 'organizacao'   -> CNPJ, porte, setor, metas corporativas
 *   type = 'orgao_publico' -> esfera, secretaria, políticas vinculadas
 * Alguns documentos ainda carregam o subdocumento `credentials`
 * (analistas que acessam o painel de governança). Em um modelo
 * relacional isso exigiria 3 tabelas + tabela de usuários + joins.
 *
 * Senhas de teste (PBKDF2-SHA256, 100.000 iterações, 32 bytes):
 *   analista   -> Esg@2026
 *   gestor.esg -> Gov@2026
 *   cidadao.ml -> Cid@2026
 * ===================================================================== */

db = db.getSiblingDB('esgimpact');
db.participants.deleteMany({});

const joined = (d) => new Date(d);

db.participants.insertMany([
  /* ---------------- CIDADÃOS (com credenciais) ---------------------- */
  {
    code: 'ANL-001',
    type: 'cidadao',
    name: 'Ana Lúcia Ferreira',
    district: 'Sé',
    city: 'São Paulo',
    joinedAt: joined('2026-01-15T10:00:00Z'),
    role: 'Analyst',
    credentials: {
      username: 'analista',
      passwordHash: 'lFAlRaMmgBEwNvyXIxq4Q7p3mJLO0tFhVzgmwSVyTww=',
      passwordSalt: 'DHKjHQY3EUmiDauSOckAew=='
    },
    engagement: { level: 'ouro', points: 2480, streakDays: 41 },
    goals: [
      { metric: 'kgCo2Mes', target: 60, achieved: true },
      { metric: 'simulacoesMes', target: 20, achieved: true }
    ],
    consent: { lgpdAccepted: true, acceptedAt: joined('2026-01-15T10:02:00Z'), shareAnonymizedData: true }
  },
  {
    code: 'ANL-002',
    type: 'orgao_publico',
    name: 'Secretaria Municipal de Meio Ambiente',
    sphere: 'municipal',
    city: 'São Paulo',
    joinedAt: joined('2026-01-08T09:00:00Z'),
    role: 'Analyst',
    credentials: {
      username: 'gestor.esg',
      passwordHash: 'sQDvnIw53iUm6rbITMkFP2siOrGZHgGSSCPR7bFvB3I=',
      passwordSalt: 'YCwqnaWXimoiNhyWdMJGbg=='
    },
    policies: [
      { code: 'PDE-2026-14', title: 'Plano de Descarbonização da Frota', status: 'em_execucao' },
      { code: 'PDE-2026-22', title: 'Meta de 30% de ciclovias até 2030', status: 'aprovada' }
    ],
    oversightScope: ['emission_factors', 'offset_projects'],
    consent: { lgpdAccepted: true, shareAnonymizedData: false }
  },
  {
    code: 'CID-003',
    type: 'cidadao',
    name: 'Marcos Lima',
    district: 'Butantã',
    city: 'São Paulo',
    joinedAt: joined('2026-02-03T14:20:00Z'),
    role: 'User',
    credentials: {
      username: 'cidadao.ml',
      passwordHash: 'b0zxRepyHHEOfsKV0Qfg5t/TUgJP0EUGdruxmX/gQE0=',
      passwordSalt: 'ivSwpdbvR3sCKz3M72Sn2g=='
    },
    engagement: { level: 'prata', points: 940, streakDays: 12 },
    goals: [{ metric: 'kgCo2Mes', target: 120, achieved: false }],
    accessibility: { preferredContrast: 'alto', fontScale: 1.3 },
    consent: { lgpdAccepted: true, shareAnonymizedData: true }
  },

  /* ---------------- CIDADÃOS (sem credenciais) ---------------------- */
  {
    code: 'CID-004',
    type: 'cidadao',
    name: 'Beatriz Nunes',
    district: 'Pinheiros',
    city: 'São Paulo',
    joinedAt: joined('2026-02-11T08:45:00Z'),
    engagement: { level: 'bronze', points: 310, streakDays: 3 },
    goals: [{ metric: 'kgCo2Mes', target: 150, achieved: false }],
    consent: { lgpdAccepted: true, shareAnonymizedData: true }
  },
  {
    code: 'CID-005',
    type: 'cidadao',
    name: 'Rafael Tavares',
    district: 'Santo Amaro',
    city: 'São Paulo',
    joinedAt: joined('2026-02-19T19:05:00Z'),
    engagement: { level: 'prata', points: 1120, streakDays: 18 },
    goals: [
      { metric: 'kgCo2Mes', target: 90, achieved: false },
      { metric: 'kmBicicletaMes', target: 60, achieved: true }
    ],
    transportProfile: { primaryMode: 'bicicleta', hasCar: false, commuteKm: 9.5 },
    consent: { lgpdAccepted: true, shareAnonymizedData: false }
  },
  {
    code: 'CID-006',
    type: 'cidadao',
    name: 'Juliana Prado',
    district: 'Ipiranga',
    city: 'São Paulo',
    joinedAt: joined('2026-03-02T11:30:00Z'),
    engagement: { level: 'bronze', points: 180, streakDays: 1 },
    consent: { lgpdAccepted: true, shareAnonymizedData: true }
    // documento propositalmente enxuto: participante recém-cadastrado
  },
  {
    code: 'CID-007',
    type: 'cidadao',
    name: 'Otávio Mendes',
    district: 'Mooca',
    city: 'São Paulo',
    joinedAt: joined('2026-03-07T16:10:00Z'),
    engagement: { level: 'ouro', points: 2015, streakDays: 33 },
    goals: [{ metric: 'kgCo2Mes', target: 70, achieved: true }],
    badges: ['primeira-simulacao', 'semana-sem-carro', 'compensador'],
    consent: { lgpdAccepted: true, shareAnonymizedData: true }
  },

  /* ---------------- ORGANIZAÇÕES ------------------------------------ */
  {
    code: 'ORG-001',
    type: 'organizacao',
    name: 'TechVerde Sistemas Ltda',
    cnpj: '12.345.678/0001-90',
    sector: 'tecnologia',
    size: 'media',
    employees: 240,
    city: 'São Paulo',
    joinedAt: joined('2026-01-22T13:00:00Z'),
    corporateGoals: [
      { metric: 'tonCo2Ano', target: 180, baseline: 260, deadline: joined('2026-12-31T00:00:00Z') },
      { metric: 'percentualFrotaEletrica', target: 40, baseline: 8 }
    ],
    esgReporting: { framework: 'GRI', lastReportYear: 2025, assuranceLevel: 'limitada' },
    consent: { lgpdAccepted: true, shareAnonymizedData: true }
  },
  {
    code: 'ORG-002',
    type: 'organizacao',
    name: 'Cooperativa Ciclo Urbano',
    cnpj: '98.765.432/0001-10',
    sector: 'logistica',
    size: 'pequena',
    employees: 38,
    city: 'São Paulo',
    joinedAt: joined('2026-02-05T09:15:00Z'),
    corporateGoals: [{ metric: 'tonCo2Ano', target: 12, baseline: 25 }],
    socialPrograms: [
      { name: 'Entregadores em transição para bike elétrica', beneficiaries: 26 },
      { name: 'Capacitação em logística verde', beneficiaries: 40 }
    ],
    consent: { lgpdAccepted: true, shareAnonymizedData: true }
  },
  {
    code: 'ORG-003',
    type: 'organizacao',
    name: 'Rede Alimenta Bairro',
    cnpj: '45.678.901/0001-55',
    sector: 'alimentacao',
    size: 'micro',
    employees: 9,
    city: 'Guarulhos',
    joinedAt: joined('2026-03-12T10:40:00Z'),
    esgReporting: { framework: 'SASB', lastReportYear: 2025 },
    consent: { lgpdAccepted: false, shareAnonymizedData: false }
  },

  /* ---------------- ÓRGÃOS PÚBLICOS --------------------------------- */
  {
    code: 'GOV-001',
    type: 'orgao_publico',
    name: 'Companhia de Transporte Metropolitano',
    sphere: 'estadual',
    city: 'São Paulo',
    joinedAt: joined('2026-01-30T08:00:00Z'),
    policies: [
      { code: 'CTM-EL-01', title: 'Eletrificação de 100% da frota até 2035', status: 'em_execucao' }
    ],
    fleet: { total: 1420, eletrica: 178, diesel: 1242 },
    consent: { lgpdAccepted: true, shareAnonymizedData: true }
  },
  {
    code: 'GOV-002',
    type: 'orgao_publico',
    name: 'Agência Municipal de Resíduos',
    sphere: 'municipal',
    city: 'Guarulhos',
    joinedAt: joined('2026-02-27T15:25:00Z'),
    policies: [{ code: 'AMR-RS-07', title: 'Coleta seletiva em 90% dos bairros', status: 'planejada' }],
    oversightScope: ['governance_audits'],
    consent: { lgpdAccepted: true, shareAnonymizedData: true }
  }
]);

print('-- participants: ' + db.participants.countDocuments() + ' documentos inseridos');
