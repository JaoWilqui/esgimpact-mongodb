/* =====================================================================
 * ESGImpact / MongoDB — 05 | Seed da collection `simulations`  (E)
 * ---------------------------------------------------------------------
 * 14 documentos. Collection de maior volume do domínio (uma linha por
 * cálculo de pegada de carbono). Duas decisões de modelagem NoSQL:
 *
 *  1) DESNORMALIZAÇÃO CONTROLADA — o subdocumento `factorSnapshot`
 *     congela o fator usado no momento do cálculo. No modelo relacional
 *     uma alteração no fator distorceria todo o histórico; aqui o
 *     histórico é auditável e o dashboard não precisa de JOIN.
 *
 *  2) ESQUEMA FLEXÍVEL POR ORIGEM — a mesma collection guarda leituras
 *     manuais do app (sem `location`), leituras com geolocalização
 *     (GeoJSON + índice 2dsphere) e leituras vindas de sensores IoT
 *     (bloco `iotDevice`), sem nenhuma coluna nula.
 * ===================================================================== */

db = db.getSiblingDB('esgimpact');
db.simulations.deleteMany({});

// --- helpers ---------------------------------------------------------
function classify(kg) {
  if (kg <= 1) return 'Baixo impacto';
  if (kg <= 10) return 'Impacto moderado';
  return 'Alto impacto';
}

function build(doc) {
  const f = db.emission_factors.findOne({ code: doc.activityCode });
  if (!f) throw new Error('Fator não encontrado: ' + doc.activityCode);

  const kg = Math.round(doc.value * f.factorKgCo2PerUnit * 100) / 100;

  return Object.assign(
    {
      activityCode: f.code,
      activity: f.activity,
      value: doc.value,
      unit: f.unit,
      estimatedKgCo2: kg,
      classification: classify(kg),
      ownerCode: doc.ownerCode,
      createdAt: doc.createdAt,
      // desnormalização controlada: fotografia do fator no instante do cálculo
      factorSnapshot: {
        code: f.code,
        factorKgCo2PerUnit: f.factorKgCo2PerUnit,
        unit: f.unit,
        capturedAt: doc.createdAt
      }
    },
    doc.extra || {}
  );
}

const d = (s) => new Date(s);

db.simulations.insertMany([
  /* ---- origem: app mobile, com geolocalização ---------------------- */
  build({
    activityCode: 'CARRO', value: 120, ownerCode: 'CID-003', createdAt: d('2026-08-14T08:12:00Z'),
    extra: {
      source: 'mobile',
      context: { channel: 'android', appVersion: '2.1.0' },
      location: { type: 'Point', coordinates: [-46.7220, -23.5680] },
      district: 'Butantã',
      tags: ['casa-trabalho']
    }
  }),
  build({
    activityCode: 'ONIBUS', value: 42, ownerCode: 'CID-003', createdAt: d('2026-08-16T07:55:00Z'),
    extra: {
      source: 'mobile',
      context: { channel: 'android', appVersion: '2.1.0' },
      location: { type: 'Point', coordinates: [-46.7010, -23.5710] },
      district: 'Butantã',
      tags: ['casa-trabalho']
    }
  }),
  build({
    activityCode: 'AVIAO', value: 1580, ownerCode: 'ANL-001', createdAt: d('2026-08-18T20:40:00Z'),
    extra: {
      source: 'mobile',
      context: { channel: 'ios', appVersion: '2.0.4' },
      location: { type: 'Point', coordinates: [-46.6555, -23.6270] },
      trip: { origin: 'GRU', destination: 'REC', purpose: 'trabalho' }
    }
  }),
  build({
    activityCode: 'BICICLETA', value: 18.5, ownerCode: 'CID-005', createdAt: d('2026-08-19T06:30:00Z'),
    extra: {
      source: 'mobile',
      location: { type: 'Point', coordinates: [-46.7050, -23.6480] },
      district: 'Santo Amaro',
      tags: ['lazer', 'meta-mensal']
    }
  }),

  /* ---- origem: entrada manual (documento enxuto) ------------------- */
  build({
    activityCode: 'CARNE_BOVINA', value: 1.2, ownerCode: 'CID-004', createdAt: d('2026-08-20T12:05:00Z'),
    extra: { source: 'manual' }
  }),
  build({
    activityCode: 'VEGETAL', value: 6, ownerCode: 'CID-004', createdAt: d('2026-08-21T12:10:00Z'),
    extra: { source: 'manual' }
  }),
  build({
    activityCode: 'FRANGO', value: 0.8, ownerCode: 'CID-006', createdAt: d('2026-08-22T19:45:00Z'),
    extra: { source: 'manual' }
  }),
  build({
    activityCode: 'METRO', value: 26, ownerCode: 'CID-007', createdAt: d('2026-08-22T07:20:00Z'),
    extra: { source: 'manual', tags: ['casa-trabalho'] }
  }),

  /* ---- origem: sensores IoT (bloco exclusivo `iotDevice`) ---------- */
  build({
    activityCode: 'ENERGIA_ELETRICA', value: 318, ownerCode: 'ORG-001', createdAt: d('2026-08-23T23:59:00Z'),
    extra: {
      source: 'iot',
      iotDevice: {
        sensorId: 'MED-SP-0471',
        model: 'SmartMeter X3',
        firmware: '4.2.1',
        readingWindow: { start: d('2026-08-23T00:00:00Z'), end: d('2026-08-23T23:59:00Z') },
        signalQuality: 0.97
      },
      location: { type: 'Point', coordinates: [-46.6410, -23.5480] },
      site: { building: 'Sede TechVerde', floor: 3 }
    }
  }),
  build({
    activityCode: 'ENERGIA_ELETRICA', value: 402, ownerCode: 'ORG-001', createdAt: d('2026-08-24T23:59:00Z'),
    extra: {
      source: 'iot',
      iotDevice: {
        sensorId: 'MED-SP-0471',
        model: 'SmartMeter X3',
        firmware: '4.2.1',
        readingWindow: { start: d('2026-08-24T00:00:00Z'), end: d('2026-08-24T23:59:00Z') },
        signalQuality: 0.93,
        anomalyFlag: 'pico_noturno'
      },
      location: { type: 'Point', coordinates: [-46.6410, -23.5480] },
      site: { building: 'Sede TechVerde', floor: 3 }
    }
  }),
  build({
    activityCode: 'GAS_ENCANADO', value: 47, ownerCode: 'ORG-003', createdAt: d('2026-08-25T23:59:00Z'),
    extra: {
      source: 'iot',
      iotDevice: { sensorId: 'GAS-GRU-0092', model: 'FlowSense 200', readingWindow: { start: d('2026-08-25T00:00:00Z'), end: d('2026-08-25T23:59:00Z') } }
    }
  }),

  /* ---- origem: importação de frota (lote corporativo) -------------- */
  build({
    activityCode: 'CARRO', value: 2140, ownerCode: 'ORG-001', createdAt: d('2026-08-26T10:00:00Z'),
    extra: {
      source: 'import',
      batch: { fileName: 'frota_agosto_2026.csv', rows: 46, importedBy: 'ANL-001' },
      fleet: { vehicles: 12, fuel: 'gasolina' }
    }
  }),
  build({
    activityCode: 'ONIBUS', value: 8600, ownerCode: 'GOV-001', createdAt: d('2026-08-27T09:30:00Z'),
    extra: {
      source: 'import',
      batch: { fileName: 'linhas_metropolitanas_ago.csv', rows: 218, importedBy: 'ANL-002' },
      fleet: { vehicles: 34, fuel: 'diesel' },
      lineCodes: ['8000-10', '875A-10', '702U-10']
    }
  }),
  build({
    activityCode: 'MOTO', value: 310, ownerCode: 'ORG-002', createdAt: d('2026-08-28T18:15:00Z'),
    extra: {
      source: 'import',
      batch: { fileName: 'entregas_agosto.csv', rows: 89, importedBy: 'ANL-001' },
      fleet: { vehicles: 7, fuel: 'gasolina' },
      tags: ['ultima-milha']
    }
  })
]);

print('-- simulations: ' + db.simulations.countDocuments() + ' documentos inseridos');
