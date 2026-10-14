# ESGImpact — MongoDB (NoSQL para Cidades Inteligentes / ESG)

Migração do backend **ESGImpact** (ASP.NET Core 8 + EF Core + SQL Server) para **MongoDB**,
com toda a infraestrutura em **Docker**. O domínio continua o mesmo — medir, reduzir e
compensar a pegada de carbono em uma cidade —, mas o modelo de dados passa de 3 tabelas
normalizadas para **5 collections de documentos** que cobrem os três pilares ESG.

---

## 1. Sumário rápido

| Item | Valor |
|---|---|
| Banco | MongoDB 7.0 (container) |
| Interface web | Mongo Express — http://localhost:8081 (`admin` / `esgadmin`) |
| Database | `esgimpact` |
| Collections | 5 (12–14 documentos cada) |
| Backend | .NET 8 + `MongoDB.Driver` 2.28 |
| Swagger | http://localhost:8080/swagger (perfil `api`) |
| Login de teste | `analista` / `Esg@2026` |

```bash
cp .env.example .env          # opcional
docker compose up -d          # sobe MongoDB + Mongo Express e carrega os dados
```

---

## 2. Decisão: **migrar**

O projeto anterior (4ª fase) usava SQL Server com três tabelas (`EmissionFactors`,
`Simulations`, `Users`) e um `AppDbContext` do EF Core. Três limitações práticas motivaram
a migração para NoSQL:

1. **O dado ESG não tem forma única.** Um fator de emissão de transporte tem variantes por
   combustível; um de energia tem mix da matriz elétrica; um de alimentação tem pegada
   hídrica. No modelo relacional isso vira coluna nula, tabela satélite ou JSON em `VARCHAR`.
2. **Cada projeto de compensação é tecnicamente diferente.** Reflorestamento tem hectares e
   espécies; biogás tem m³/dia; solar tem kWp. Modelar isso relacionalmente exige herança
   por tabela (ou uma tabela larga cheia de nulos).
3. **Governança é um fluxo de eventos heterogêneos.** Alteração de fator, revisão de
   política, acesso a dados (LGPD) e incidente têm payloads distintos, e novos tipos de
   evento surgem o tempo todo — o que no Oracle/SQL Server significaria uma migração a cada
   novo requisito de compliance.

O MongoDB resolve os três com o **esquema flexível**: documentos da mesma collection podem
ter formas diferentes, e um campo novo entra sem `ALTER TABLE`, sem migration e sem downtime
— o que importa em uma plataforma de cidade inteligente que precisa absorver novos sensores,
novas metodologias de cálculo e novas exigências regulatórias continuamente.

---

## 3. As 5 collections e os pilares ESG

| # | Collection | Pilar | O que representa |
|---|---|---|---|
| 1 | `emission_factors` | **E** | Fatores de emissão de CO₂ por atividade (transporte, energia, alimentação, resíduos), com metodologia e fonte |
| 2 | `simulations` | **E** | Cada medição de pegada de carbono — do app, de planilha importada ou de **sensor IoT** |
| 3 | `participants` | **S** | Cidadãos, organizações e órgãos públicos: engajamento, metas, programas sociais e consentimento LGPD |
| 4 | `offset_projects` | **E + S** | Projetos de compensação: capacidade, preço por moeda, certificações e **impacto social** (empregos, famílias, alunos) |
| 5 | `governance_audits` | **G** | Trilha append-only de auditoria: alterações de fator, revisões de política, acessos a dados, incidentes e relatórios publicados |

### Recursos de modelagem usados

- **Documentos polimórficos** — `participants` guarda três formas (`cidadao`,
  `organizacao`, `orgao_publico`) na mesma collection.
- **Subdocumentos e arrays embutidos** — `pricing[]`, `variants[]`, `goals[]`, `checklist[]`
  eliminam tabelas de junção.
- **Desnormalização controlada** — `simulations.factorSnapshot` congela o fator usado no
  cálculo; alterar o fator hoje não distorce o histórico nem exige JOIN no dashboard.
- **GeoJSON + índice `2dsphere`** — consultas por proximidade em `simulations.location`.
- **Validação `$jsonSchema`** com `validationLevel: moderate` — exige o núcleo obrigatório
  de `emission_factors` sem engessar os campos extras.
- **`[BsonExtraElements]`** no C# — a API tipada lê e regrava campos que nenhuma classe
  declara.

---

## 4. Estrutura de pastas

```
ESGImpact-mongo/
├── docker-compose.yml            MongoDB + Mongo Express + API (perfil `api`)
├── .env.example
├── ESGImpact.Mongo.sln
├── mongo/
│   ├── init/                     executado na 1ª subida do container
│   │   ├── 01-create-collections.js    createCollection, $jsonSchema e índices
│   │   ├── 02-seed-emission-factors.js
│   │   ├── 03-seed-participants.js
│   │   ├── 04-seed-offset-projects.js
│   │   ├── 05-seed-simulations.js
│   │   └── 06-seed-governance-audits.js
│   └── crud/                     roteiro de CRUD da entrega
│       ├── 00-inventario.js      collections, contagens, índices e validador
│       ├── 00b-amostra-documentos.js  formatos distintos por collection
│       ├── 01-create.js          insertOne / insertMany nas 5 collections
│       ├── 02-read.js            find, projeção, array, $exists, geoespacial, explain
│       ├── 03-update.js          $set, $inc, $push, $unset, upsert, arrayFilters, pipeline
│       ├── 04-delete.js          deleteOne, deleteMany, findOneAndDelete, soft delete
│       └── 05-aggregations.js    $facet, $lookup, $unwind, $group
└── src/ESGImpact.Api/            backend .NET 8 migrado
    ├── Configuration/MongoSettings.cs
    ├── Data/MongoContext.cs              substitui o AppDbContext
    ├── Data/MongoBootstrapper.cs         substitui migrations + DbSeeder
    ├── Models/Entities.cs                5 entidades com [BsonExtraElements]
    ├── Repositories/DocumentRepository.cs CRUD genérico sobre BsonDocument
    ├── Services/                         negócio sobre Aggregation Framework
    ├── Controllers/                      CRUD das 5 collections + endpoints ESG
    └── ESGImpact.Api.http                roteiro completo de chamadas
```

---

## 5. Execução

### 5.1 Banco de dados

```bash
docker compose up -d
docker compose ps
docker compose logs -f esgimpact-mongo
```

Na **primeira** subida (volume vazio), o MongoDB executa sozinho os scripts de `mongo/init`
em ordem alfabética, criando as collections, os índices e a carga inicial.

Para recarregar do zero:

```bash
docker compose down -v && docker compose up -d
```

### 5.2 Acessar o shell

```bash
docker compose exec esgimpact-mongo mongosh -u esgadmin -p 'Esg@Mongo2026' \
  --authenticationDatabase admin esgimpact
```

### 5.3 Executar o roteiro de CRUD

```bash
docker compose exec -T esgimpact-mongo mongosh --quiet -u esgadmin -p 'Esg@Mongo2026' \
  --authenticationDatabase admin /scripts/crud/01-create.js
# idem para 02-read.js, 03-update.js, 04-delete.js, 05-aggregations.js
```

O inventário do banco (collections, contagens, índices e validador) e a amostra de
documentos de formatos diferentes saem de:

```bash
docker compose exec -T esgimpact-mongo mongosh --quiet -u esgadmin -p 'Esg@Mongo2026' \
  --authenticationDatabase admin /scripts/crud/00-inventario.js
docker compose exec -T esgimpact-mongo mongosh --quiet -u esgadmin -p 'Esg@Mongo2026' \
  --authenticationDatabase admin /scripts/crud/00b-amostra-documentos.js
```

Os scripts de `mongo/init` também ficam montados em `/scripts/init` e podem ser
reexecutados a qualquer momento — são idempotentes.

### 5.4 API .NET

```bash
# opção A — pelo Visual Studio / CLI, apontando para localhost:27017
dotnet run --project src/ESGImpact.Api

# opção B — tudo em container
docker compose --profile api up -d --build
```

Swagger: <http://localhost:8080/swagger>

---

## 6. Endpoints

### CRUD das 5 collections (esquema flexível de ponta a ponta)

| Método | Rota | Operação MongoDB |
|---|---|---|
| `GET` | `/api/{collection}` | `find` + paginação + `filter` JSON livre |
| `GET` | `/api/{collection}/{id}` | `findOne` (aceita ObjectId **ou** o `code` de negócio) |
| `POST` | `/api/{collection}` | `insertOne` |
| `POST` | `/api/{collection}/bulk` | `insertMany` |
| `PATCH` | `/api/{collection}/{id}` | `updateOne` com `$set` |
| `PUT` | `/api/{collection}/{id}` | `replaceOne` |
| `DELETE` | `/api/{collection}/{id}` | `deleteOne` |

Rotas: `emission-factors`, `simulation-documents`, `participants`, `offset-projects`,
`governance-audits` (esta exige JWT).

### Endpoints de domínio

| Método | Rota | Descrição | Auth |
|---|---|---|---|
| `POST` | `/api/auth/login` | Emite o JWT (credenciais embutidas em `participants`) | – |
| `POST` | `/api/simulations` | Calcula, classifica e persiste com `factorSnapshot` | – |
| `GET` | `/api/simulations` | Histórico paginado | – |
| `GET` | `/api/analytics/dashboard` | Dashboard ESG em **um único `$facet`** | 🔒 |
| `GET` | `/api/recommendations` | Economia de CO₂ quantificada por troca de modal | – |
| `POST` | `/api/offset/quote` | Cotação nos **projetos reais** da collection | 🔒 |
| `GET` | `/api/health` · `/api/collections/stats` | Diagnóstico | – |

---

## 7. O que mudou em relação ao projeto relacional

| Projeto anterior (SQL Server + EF Core) | Projeto atual (MongoDB) |
|---|---|
| 3 tabelas | 5 collections cobrindo E, S e G |
| `AppDbContext` / `DbSet<T>` | `MongoContext` / `IMongoCollection<T>` |
| Migrations + `Database.Migrate()` | `MongoBootstrapper` (índices idempotentes) + scripts de `mongo/init` |
| `DbSeeder` | `03-seed-participants.js` (mesmo hash PBKDF2) |
| Tabela `Users` separada | `credentials` embutido no participante |
| Preço de carbono fixo em `Dictionary` no código | `offset_projects.pricing[]` no banco |
| 4 consultas para montar o dashboard | 1 pipeline com `$facet` |
| Fator alterado distorce o histórico | `factorSnapshot` congela o fator usado |
| Nova característica ⇒ migration | Novo campo entra no documento sem migração |

---

## 8. Credenciais (ambiente de estudo)

| Onde | Usuário | Senha |
|---|---|---|
| MongoDB (root) | `esgadmin` | `Esg@Mongo2026` |
| Mongo Express | `admin` | `esgadmin` |
| API (analista) | `analista` | `Esg@2026` |
| API (gestor) | `gestor.esg` | `Gov@2026` |
| API (cidadão) | `cidadao.ml` | `Cid@2026` |

> Valores de demonstração, versionados de propósito para o trabalho acadêmico.
> Em produção, use `.env` fora do controle de versão e segredos gerenciados.
