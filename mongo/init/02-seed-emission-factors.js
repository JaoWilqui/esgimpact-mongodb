/* =====================================================================
 * ESGImpact / MongoDB — 02 | Seed da collection `emission_factors`  (E)
 * ---------------------------------------------------------------------
 * 12 documentos. Demonstra ESQUEMA FLEXÍVEL: todos compartilham o núcleo
 * (code / activity / factorKgCo2PerUnit / unit), mas cada categoria
 * carrega blocos próprios — `variants`, `regionalFactors`, `gridMix`,
 * `seasonality` — sem qualquer ALTER TABLE.
 * ===================================================================== */

db = db.getSiblingDB('esgimpact');
db.emission_factors.deleteMany({});

const now = new Date();

db.emission_factors.insertMany([
  // --- Transporte: documento com VARIANTES por combustível -------------
  {
    code: 'CARRO',
    activity: 'Carro',
    factorKgCo2PerUnit: 0.21,
    unit: 'km',
    fieldLabel: 'Quilômetros percorridos',
    category: 'transporte',
    pillar: 'E',
    scope: 'escopo_3',
    isActive: true,
    source: { agency: 'GHG Protocol Brasil', year: 2025, methodology: 'tank-to-wheel' },
    variants: [
      { fuel: 'gasolina', factorKgCo2PerUnit: 0.21 },
      { fuel: 'etanol', factorKgCo2PerUnit: 0.14 },
      { fuel: 'diesel', factorKgCo2PerUnit: 0.25 },
      { fuel: 'eletrico', factorKgCo2PerUnit: 0.05 }
    ],
    updatedAt: now
  },
  {
    code: 'AVIAO',
    activity: 'Avião',
    factorKgCo2PerUnit: 0.15,
    unit: 'km',
    fieldLabel: 'Quilômetros voados',
    category: 'transporte',
    pillar: 'E',
    scope: 'escopo_3',
    isActive: true,
    source: { agency: 'ICAO', year: 2024, methodology: 'well-to-wake' },
    // bloco exclusivo deste documento
    haulFactors: { domestico_curto: 0.18, domestico_longo: 0.15, internacional: 0.11 },
    radiativeForcingMultiplier: 1.9,
    updatedAt: now
  },
  {
    code: 'ONIBUS',
    activity: 'Ônibus',
    factorKgCo2PerUnit: 0.08,
    unit: 'km',
    fieldLabel: 'Quilômetros percorridos',
    category: 'transporte',
    pillar: 'E',
    scope: 'escopo_3',
    isActive: true,
    // fatores regionais: mesma atividade, intensidade diferente por cidade
    regionalFactors: [
      { region: 'SP', factorKgCo2PerUnit: 0.08, fleetShareEletrica: 0.12 },
      { region: 'RJ', factorKgCo2PerUnit: 0.09, fleetShareEletrica: 0.04 },
      { region: 'CWB', factorKgCo2PerUnit: 0.06, fleetShareEletrica: 0.31 }
    ],
    updatedAt: now
  },
  {
    code: 'METRO',
    activity: 'Metrô',
    factorKgCo2PerUnit: 0.03,
    unit: 'km',
    fieldLabel: 'Quilômetros percorridos',
    category: 'transporte',
    pillar: 'E',
    isActive: true,
    updatedAt: now
  },
  {
    code: 'BICICLETA',
    activity: 'Bicicleta',
    factorKgCo2PerUnit: Double(0.0), // armazenado explicitamente como Double
    unit: 'km',
    fieldLabel: 'Quilômetros percorridos',
    category: 'transporte',
    pillar: 'E',
    isActive: true,
    // documento propositalmente MÍNIMO — nenhum bloco extra
    updatedAt: now
  },
  {
    code: 'MOTO',
    activity: 'Motocicleta',
    factorKgCo2PerUnit: 0.11,
    unit: 'km',
    fieldLabel: 'Quilômetros percorridos',
    category: 'transporte',
    pillar: 'E',
    isActive: true,
    variants: [
      { engineCc: 125, factorKgCo2PerUnit: 0.08 },
      { engineCc: 300, factorKgCo2PerUnit: 0.11 },
      { engineCc: 650, factorKgCo2PerUnit: 0.16 }
    ],
    updatedAt: now
  },

  // --- Alimentação: bloco de SAZONALIDADE ------------------------------
  {
    code: 'CARNE_BOVINA',
    activity: 'Carne bovina',
    factorKgCo2PerUnit: Double(27.0), // armazenado explicitamente como Double
    unit: 'kg',
    fieldLabel: 'Quilos consumidos',
    category: 'alimentacao',
    pillar: 'E',
    scope: 'escopo_3',
    isActive: true,
    source: { agency: 'Embrapa', year: 2024, methodology: 'ACV berço-ao-portão' },
    landUseChangeIncluded: true,
    waterFootprintLiters: 15400,
    updatedAt: now
  },
  {
    code: 'FRANGO',
    activity: 'Carne de frango',
    factorKgCo2PerUnit: 6.9,
    unit: 'kg',
    fieldLabel: 'Quilos consumidos',
    category: 'alimentacao',
    pillar: 'E',
    isActive: true,
    waterFootprintLiters: 4300,
    updatedAt: now
  },
  {
    code: 'VEGETAL',
    activity: 'Refeição vegetariana',
    factorKgCo2PerUnit: 1.4,
    unit: 'refeicao',
    fieldLabel: 'Refeições consumidas',
    category: 'alimentacao',
    pillar: 'E',
    isActive: true,
    seasonality: { verao: 1.2, outono: 1.4, inverno: 1.7, primavera: 1.3 },
    updatedAt: now
  },

  // --- Energia / resíduos: bloco de MIX ENERGÉTICO ---------------------
  {
    code: 'ENERGIA_ELETRICA',
    activity: 'Energia elétrica residencial',
    factorKgCo2PerUnit: 0.0385,
    unit: 'kWh',
    fieldLabel: 'kWh consumidos',
    category: 'energia',
    pillar: 'E',
    scope: 'escopo_2',
    isActive: true,
    source: { agency: 'MCTI - Fator médio SIN', year: 2025, methodology: 'grid average' },
    gridMix: { hidrica: 0.58, eolica: 0.14, solar: 0.09, termica: 0.16, nuclear: 0.03 },
    monthlyFactors: [
      { month: 1, factor: 0.041 }, { month: 2, factor: 0.040 }, { month: 3, factor: 0.038 },
      { month: 4, factor: 0.036 }, { month: 5, factor: 0.035 }, { month: 6, factor: 0.037 }
    ],
    updatedAt: now
  },
  {
    code: 'GAS_ENCANADO',
    activity: 'Gás natural encanado',
    factorKgCo2PerUnit: 2.02,
    unit: 'm3',
    fieldLabel: 'Metros cúbicos consumidos',
    category: 'energia',
    pillar: 'E',
    scope: 'escopo_1',
    isActive: true,
    updatedAt: now
  },
  {
    code: 'RESIDUO_ATERRO',
    activity: 'Resíduo enviado a aterro',
    factorKgCo2PerUnit: 0.58,
    unit: 'kg',
    fieldLabel: 'Quilos descartados',
    category: 'residuos',
    pillar: 'E',
    isActive: false, // inativo: preservado para histórico, fora do cálculo
    deactivationReason: 'Substituído pelo fator municipal de 2026',
    updatedAt: now
  }
]);

print('-- emission_factors: ' + db.emission_factors.countDocuments() + ' documentos inseridos');
