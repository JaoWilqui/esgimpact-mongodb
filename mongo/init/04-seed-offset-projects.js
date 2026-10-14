/* =====================================================================
 * ESGImpact / MongoDB — 04 | Seed da collection `offset_projects` (E+S)
 * ---------------------------------------------------------------------
 * 12 documentos. Cada tipo de projeto tem características técnicas
 * completamente diferentes (espécies plantadas x potência instalada x
 * volume de biogás). O array `pricing` embute a tabela de preços por
 * moeda — que no modelo relacional era um Dictionary fixo no código
 * (OffsetService.PricePerTonByCurrency).
 * ===================================================================== */

db = db.getSiblingDB('esgimpact');
db.offset_projects.deleteMany({});

db.offset_projects.insertMany([
  /* -------- Reflorestamento: bloco `forestry` ------------------------ */
  {
    code: 'PRJ-001',
    name: 'Reflorestamento Mata Atlântica - Vale do Ribeira',
    type: 'reflorestamento',
    status: 'ativo',
    capacityTonCo2Year: 1200,
    pricing: [
      { currency: 'BRL', pricePerTon: 90.0 },
      { currency: 'USD', pricePerTon: 18.0 },
      { currency: 'EUR', pricePerTon: 16.5 }
    ],
    location: { type: 'Point', coordinates: [-47.8825, -24.4802], state: 'SP', city: 'Registro' },
    certifications: ['VCS', 'CCB'],
    forestry: { hectares: 340, species: ['Ipê-amarelo', 'Jequitibá', 'Palmito-juçara'], survivalRate: 0.87 },
    socialImpact: { jobsCreated: 45, communitiesBenefited: 6, familiesInProgram: 120 },
    sdgTargets: [13, 15, 8],
    startedAt: new Date('2024-06-01T00:00:00Z')
  },
  {
    code: 'PRJ-002',
    name: 'Restauração de Nascentes - Serra da Cantareira',
    type: 'reflorestamento',
    status: 'ativo',
    capacityTonCo2Year: 480,
    pricing: [{ currency: 'BRL', pricePerTon: 105.0 }, { currency: 'USD', pricePerTon: 21.0 }],
    location: { type: 'Point', coordinates: [-46.6333, -23.4167], state: 'SP', city: 'Mairiporã' },
    certifications: ['Gold Standard'],
    forestry: { hectares: 96, species: ['Embaúba', 'Guanandi'], survivalRate: 0.91 },
    socialImpact: { jobsCreated: 18, communitiesBenefited: 3 },
    sdgTargets: [6, 13, 15],
    startedAt: new Date('2025-02-10T00:00:00Z')
  },
  {
    code: 'PRJ-003',
    name: 'Agrofloresta Comunitária Cantareira Norte',
    type: 'reflorestamento',
    status: 'em_captacao',
    capacityTonCo2Year: 260,
    pricing: [{ currency: 'BRL', pricePerTon: 82.0 }],
    location: { type: 'Point', coordinates: [-46.5980, -23.3720], state: 'SP', city: 'Franco da Rocha' },
    forestry: { hectares: 54, species: ['Cacau', 'Banana', 'Cedro'], agroforestrySystem: 'SAF biodiverso' },
    socialImpact: { jobsCreated: 22, communitiesBenefited: 2, femaleLeadershipShare: 0.64 },
    sdgTargets: [2, 5, 13],
    fundingProgress: { goalBRL: 850000, raisedBRL: 312000 }
  },

  /* -------- Energia renovável: bloco `energy` ------------------------ */
  {
    code: 'PRJ-004',
    name: 'Usina Solar Comunitária - Zona Leste',
    type: 'energia_solar',
    status: 'ativo',
    capacityTonCo2Year: 890,
    pricing: [
      { currency: 'BRL', pricePerTon: 74.0 },
      { currency: 'USD', pricePerTon: 15.0 },
      { currency: 'EUR', pricePerTon: 13.8 }
    ],
    location: { type: 'Point', coordinates: [-46.4700, -23.5400], state: 'SP', city: 'São Paulo' },
    certifications: ['I-REC'],
    energy: { installedKwp: 2400, annualGenerationMwh: 3650, panelTechnology: 'monocristalino' },
    socialImpact: { householdsWithDiscountedBill: 420, jobsCreated: 31 },
    sdgTargets: [7, 11, 13],
    startedAt: new Date('2024-11-20T00:00:00Z')
  },
  {
    code: 'PRJ-005',
    name: 'Parque Eólico Litoral Norte',
    type: 'energia_eolica',
    status: 'ativo',
    capacityTonCo2Year: 5400,
    pricing: [{ currency: 'BRL', pricePerTon: 68.0 }, { currency: 'USD', pricePerTon: 13.5 }],
    location: { type: 'Point', coordinates: [-45.4000, -23.7800], state: 'SP', city: 'Caraguatatuba' },
    certifications: ['I-REC', 'VCS'],
    energy: { installedKwp: 42000, annualGenerationMwh: 121000, turbines: 12, hubHeightMeters: 120 },
    sdgTargets: [7, 13],
    startedAt: new Date('2023-08-15T00:00:00Z')
  },
  {
    code: 'PRJ-006',
    name: 'Telhados Solares em Escolas Municipais',
    type: 'energia_solar',
    status: 'em_captacao',
    capacityTonCo2Year: 210,
    pricing: [{ currency: 'BRL', pricePerTon: 96.0 }],
    location: { type: 'Point', coordinates: [-46.5333, -23.4550], state: 'SP', city: 'Guarulhos' },
    energy: { installedKwp: 520, schoolsCovered: 14 },
    socialImpact: { studentsBenefited: 8600, educationProgram: 'Currículo de energia limpa' },
    sdgTargets: [4, 7, 13],
    fundingProgress: { goalBRL: 1900000, raisedBRL: 640000 }
  },

  /* -------- Resíduos / biogás: bloco `waste` ------------------------- */
  {
    code: 'PRJ-007',
    name: 'Biodigestor de Resíduos Orgânicos - CEAGESP',
    type: 'biogas',
    status: 'ativo',
    capacityTonCo2Year: 1750,
    pricing: [{ currency: 'BRL', pricePerTon: 79.0 }, { currency: 'USD', pricePerTon: 16.0 }],
    location: { type: 'Point', coordinates: [-46.7180, -23.5230], state: 'SP', city: 'São Paulo' },
    certifications: ['VCS'],
    waste: { tonPerDay: 120, biogasM3PerDay: 9800, digestateUse: 'composto agrícola' },
    socialImpact: { catadoresIntegrated: 64, jobsCreated: 27 },
    sdgTargets: [11, 12, 13],
    startedAt: new Date('2025-01-05T00:00:00Z')
  },
  {
    code: 'PRJ-008',
    name: 'Captura de Metano em Aterro Sanitário',
    type: 'captura_metano',
    status: 'ativo',
    capacityTonCo2Year: 9800,
    pricing: [
      { currency: 'BRL', pricePerTon: 58.0 },
      { currency: 'USD', pricePerTon: 11.5 },
      { currency: 'EUR', pricePerTon: 10.7 }
    ],
    location: { type: 'Point', coordinates: [-46.3900, -23.4100], state: 'SP', city: 'Guarulhos' },
    certifications: ['VCS', 'CDM'],
    waste: { landfillAreaHectares: 78, methaneCaptureEfficiency: 0.74, flaringOnly: false },
    sdgTargets: [11, 13],
    startedAt: new Date('2022-04-01T00:00:00Z')
  },
  {
    code: 'PRJ-009',
    name: 'Compostagem Descentralizada de Bairro',
    type: 'compostagem',
    status: 'piloto',
    capacityTonCo2Year: 85,
    pricing: [{ currency: 'BRL', pricePerTon: 120.0 }],
    location: { type: 'Point', coordinates: [-46.6890, -23.5610], state: 'SP', city: 'São Paulo' },
    waste: { tonPerDay: 4, pointsOfCollection: 22 },
    socialImpact: { volunteersEngaged: 140, schoolsPartnered: 5 },
    sdgTargets: [11, 12]
  },

  /* -------- Mobilidade e outros ------------------------------------- */
  {
    code: 'PRJ-010',
    name: 'Corredor Cicloviário Metropolitano',
    type: 'mobilidade',
    status: 'em_captacao',
    capacityTonCo2Year: 430,
    pricing: [{ currency: 'BRL', pricePerTon: 88.0 }, { currency: 'USD', pricePerTon: 17.5 }],
    location: { type: 'Point', coordinates: [-46.6500, -23.5900], state: 'SP', city: 'São Paulo' },
    mobility: { kmOfBikeLanes: 34, expectedDailyTrips: 18000, bikeParkingSpots: 900 },
    socialImpact: { neighborhoodsConnected: 9, accessibilityUpgrades: true },
    sdgTargets: [3, 9, 11, 13],
    fundingProgress: { goalBRL: 12000000, raisedBRL: 2750000 }
  },
  {
    code: 'PRJ-011',
    name: 'Eletrificação de Frota de Ônibus Escolares',
    type: 'mobilidade',
    status: 'ativo',
    capacityTonCo2Year: 1340,
    pricing: [{ currency: 'BRL', pricePerTon: 71.0 }, { currency: 'USD', pricePerTon: 14.2 }],
    location: { type: 'Point', coordinates: [-46.5610, -23.6800], state: 'SP', city: 'Diadema' },
    certifications: ['I-REC'],
    mobility: { vehiclesReplaced: 46, kmPerYear: 1_120_000, chargingStations: 8 },
    socialImpact: { studentsBenefited: 5200, noiseReductionDb: 12 },
    sdgTargets: [4, 11, 13],
    startedAt: new Date('2025-03-01T00:00:00Z')
  },
  {
    code: 'PRJ-012',
    name: 'Manguezal Protegido - Baixada Santista',
    type: 'conservacao',
    status: 'encerrado',
    capacityTonCo2Year: 610,
    pricing: [{ currency: 'BRL', pricePerTon: 110.0 }],
    location: { type: 'Point', coordinates: [-46.3330, -23.9600], state: 'SP', city: 'Santos' },
    certifications: ['Blue Carbon Initiative'],
    conservation: { hectaresProtected: 210, ecosystem: 'manguezal', blueCarbon: true },
    closedAt: new Date('2026-01-31T00:00:00Z'),
    closingNote: 'Meta de créditos atingida; área transferida para gestão municipal.',
    sdgTargets: [14, 15]
  }
]);

print('-- offset_projects: ' + db.offset_projects.countDocuments() + ' documentos inseridos');
