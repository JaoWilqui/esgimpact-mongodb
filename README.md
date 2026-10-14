# Projeto - Cidades ESGInteligentes

**ESGImpact** — API .NET 8 + MongoDB para medir, reduzir e compensar a pegada de carbono
de uma cidade inteligente, com containerização em Docker e pipeline de **CI/CD** que
testa, empacota e implanta automaticamente em **staging** e **produção**.

| | |
|---|---|
| Integrante | **NOME COMPLETO — RM 000000** |
| Disciplina | Navegando pelo mundo DevOps — FIAP |
| Repositório | https://github.com/JaoWilqui/esgimpact-mongodb |
| Imagem | `ghcr.io/jaowilqui/esgimpact-api` |

---

## Como executar localmente com Docker

**Pré-requisitos:** Docker Desktop (ou Docker Engine + Compose v2) e Git.

```bash
git clone https://github.com/JaoWilqui/esgimpact-mongodb.git
cd esgimpact-mongodb
cp .env.example .env            # opcional: ajusta portas e senhas
docker compose up -d --build    # API + MongoDB + Mongo Express
docker compose ps               # os três serviços devem ficar "healthy"/"running"
```

| Serviço | Endereço | Acesso |
|---|---|---|
| API / Swagger | http://localhost:8080/swagger | login `analista` / `Esg@2026` |
| Identificação do ambiente | http://localhost:8080/api/info | – |
| Saúde | http://localhost:8080/api/health | – |
| Mongo Express | http://localhost:8081 | `admin` / `esgadmin` |
| MongoDB | `localhost:27017` | `esgadmin` / `Esg@Mongo2026` |

Na primeira subida (volume vazio) o MongoDB executa os scripts de `mongo/init`, que criam as
5 collections, os índices, o validador `$jsonSchema` e a carga inicial de 62 documentos.

Comandos úteis:

```bash
docker compose logs -f api                      # logs da API
docker compose exec mongo mongosh -u esgadmin -p 'Esg@Mongo2026' \
  --authenticationDatabase admin /scripts/crud/00-inventario.js   # inventário do banco
docker compose down                             # para (mantém os dados)
docker compose down -v                          # para e apaga os volumes
```

Executar os testes fora do container (com o MongoDB do compose no ar):

```bash
dotnet test tests/ESGImpact.Tests --filter Category=Unit
MongoDb__Username=esgadmin MongoDb__Password='Esg@Mongo2026' \
  dotnet test tests/ESGImpact.Tests --filter Category=Integration
```

Subir manualmente um ambiente igual ao do pipeline (staging na porta 9081):

```powershell
$env:API_IMAGE = "ghcr.io/jaowilqui/esgimpact-api:latest"
$env:MONGO_ROOT_PASSWORD = "Esg@Mongo2026"; $env:JWT_KEY = "uma-chave-com-32-caracteres-ou-mais!!"
powershell -ExecutionPolicy Bypass -File deploy\deploy.ps1 -Environment staging
```

```bash
API_IMAGE=ghcr.io/jaowilqui/esgimpact-api:latest MONGO_ROOT_PASSWORD='Esg@Mongo2026' \
JWT_KEY='uma-chave-com-32-caracteres-ou-mais!!' ./deploy/deploy.sh staging
```

---

## Pipeline CI/CD

Ferramenta: **GitHub Actions** — [`.github/workflows/ci-cd.yml`](.github/workflows/ci-cd.yml).

```
push na main ─► build-test ─► docker ─► deploy-staging ─► [aprovação] ─► deploy-production
pull request ─► build-test
```

| # | Job | Onde roda | O que faz |
|---|---|---|---|
| 1 | **Build e testes** | `ubuntu-latest` + MongoDB 7.0 como *service container* | `dotnet restore` e `build` (Release); **testes de unidade** (cálculo de emissão, classificação, hash PBKDF2, paginação, validadores); **testes de integração** da API inteira contra um MongoDB real (login JWT, simulação, CRUD, índice único → 409, dashboard, cotação, auditoria); execução dos scripts `mongo/init` e `mongo/crud` com conferência da carga; publica os `.trx` e a cobertura como artefato |
| 2 | **Build e push da imagem** | `ubuntu-latest` | Build multi-stage com Buildx e cache do GitHub Actions; publica no GHCR com três tags: `sha-<commit>` (imutável, usada no deploy), `1.0.<execução>` e `latest` |
| 3 | **Deploy staging** | runner **self-hosted** (Windows + Docker Desktop) | `docker compose pull` + `up --wait` do projeto `esgimpact-staging` na porta **9081**; smoke tests HTTP |
| 4 | **Deploy produção** | runner **self-hosted** | Só depois do staging aprovado nos smoke tests **e** da aprovação manual do environment `production`; mesma imagem, projeto `esgimpact-production`, porta **9080** |

**Smoke tests** executados após cada deploy (`deploy/deploy.ps1` · `deploy/deploy.sh`):

1. `GET /api/health` → `status=ok`, `mongo=up`
2. `GET /api/info` → nome do ambiente (`Staging`/`Production`) **e** commit iguais aos esperados — garante que a versão certa chegou ao ambiente certo
3. `GET /api/collections/stats` → carga inicial presente (≥ 12 fatores de emissão)
4. `POST /api/auth/login` → JWT emitido
5. `GET /api/analytics/dashboard` com o token → 200

Se algum passo falhar, o script imprime `docker compose ps` e os logs dos containers e o job
termina com erro, interrompendo a promoção para produção.

### Diferenças entre os ambientes

| | Staging | Produção |
|---|---|---|
| Projeto Compose | `esgimpact-staging` | `esgimpact-production` |
| Porta da API | 9081 | 9080 |
| `ASPNETCORE_ENVIRONMENT` | `Staging` | `Production` |
| Expiração do JWT | 120 min | 60 min |
| Limites da API | 0,5 CPU · 384 MB | 1 CPU · 512 MB |
| Aprovação | automática | revisor obrigatório |
| Configuração | `deploy/staging.env` | `deploy/production.env` |
| Segredos | environment `staging` | environment `production` |

Cada ambiente tem containers, rede e volumes próprios; a porta do MongoDB não é publicada.

### Configuração do repositório (uma vez)

1. **Runner self-hosted** — *Settings → Actions → Runners → New self-hosted runner → Windows*.
   Execute os comandos exibidos em `C:\actions-runner` e inicie com `.\run.cmd` na mesma
   sessão de usuário em que o Docker Desktop está aberto (labels padrão `self-hosted`, `Windows`).
2. **Environments** — *Settings → Environments*:
   - `staging`: sem regras.
   - `production`: marque **Required reviewers** e adicione seu usuário.
3. **Segredos** (em cada environment): `MONGO_ROOT_PASSWORD` e `JWT_KEY` (≥ 32 caracteres).
   Sem eles o pipeline usa valores de desenvolvimento e emite um *warning*.
   A senha do MongoDB é gravada na criação do volume; para trocá-la depois, remova o volume
   (`docker compose -p esgimpact-staging -f deploy/docker-compose.deploy.yml down -v`).
4. **Permissões** — *Settings → Actions → General → Workflow permissions*: *Read and write*
   (necessário para publicar a imagem no GHCR).

---

## Containerização

### Dockerfile (raiz do projeto)

```dockerfile
# syntax=docker/dockerfile:1
# =====================================================================
# ESGImpact API — imagem de produção (.NET 8)
# ---------------------------------------------------------------------
# Multi-stage:
#   1) restore  — restaura dependências só a partir dos .csproj, para
#                 aproveitar o cache de camadas do Docker
#   2) build    — compila e publica em Release
#   3) final    — runtime ASP.NET enxuto, usuário não-root, healthcheck
# =====================================================================

ARG DOTNET_VERSION=8.0

# ---------- 1) restore ------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS restore
WORKDIR /src
COPY src/ESGImpact.Api/ESGImpact.Api.csproj src/ESGImpact.Api/
RUN dotnet restore src/ESGImpact.Api/ESGImpact.Api.csproj

# ---------- 2) build / publish ---------------------------------------
FROM restore AS build
COPY src/ src/
RUN dotnet publish src/ESGImpact.Api/ESGImpact.Api.csproj \
      -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ---------- 3) runtime -----------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS final

# metadados de rastreabilidade (preenchidos pelo pipeline)
ARG APP_VERSION=local
ARG GIT_SHA=local
LABEL org.opencontainers.image.title="esgimpact-api" \
      org.opencontainers.image.description="ESGImpact — API ESG para cidades inteligentes (.NET 8 + MongoDB)" \
      org.opencontainers.image.source="https://github.com/JaoWilqui/esgimpact-mongodb" \
      org.opencontainers.image.version="${APP_VERSION}" \
      org.opencontainers.image.revision="${GIT_SHA}"

# curl apenas para o HEALTHCHECK
RUN apt-get update \
 && apt-get install -y --no-install-recommends curl \
 && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true \
    APP_VERSION=${APP_VERSION} \
    GIT_SHA=${GIT_SHA}

EXPOSE 8080

# usuário sem privilégios já existente nas imagens .NET 8
USER app

HEALTHCHECK --interval=15s --timeout=5s --start-period=20s --retries=5 \
  CMD curl -fsS http://localhost:8080/api/health || exit 1

ENTRYPOINT ["dotnet", "ESGImpact.Api.dll"]
```

### Estratégias adotadas

| Estratégia | Como foi aplicada |
|---|---|
| **Multi-stage build** | SDK (≈ 800 MB) só nos estágios `restore`/`build`; a imagem final usa apenas o runtime `aspnet:8.0` |
| **Cache de camadas** | O `.csproj` é copiado e restaurado antes do código-fonte: alterar código não refaz o `restore`. No pipeline, cache `type=gha` do Buildx |
| **Usuário não-root** | `USER app` (UID 1654, nativo das imagens .NET 8) |
| **Healthcheck** | `curl /api/health`, que também faz `ping` no MongoDB — o Compose só considera a API pronta quando o banco responde |
| **Rastreabilidade** | `APP_VERSION` e `GIT_SHA` entram como *build args*, viram labels OCI e são expostos em `/api/info` |
| **Configuração externa** | Nenhuma senha na imagem: conexão, credenciais e chave JWT chegam por variáveis de ambiente (`.env`, `deploy/*.env` e segredos do GitHub) |
| **Contexto enxuto** | `.dockerignore` exclui `bin/`, `obj/`, testes, documentação e scripts de deploy |
| **Imagem imutável** | O deploy usa a tag `sha-<commit>`: staging e produção recebem exatamente a mesma imagem |

### Orquestração com Docker Compose

| Arquivo | Uso | Serviços |
|---|---|---|
| `docker-compose.yml` | desenvolvimento local | `api` (build local) · `mongo` · `mongo-express` |
| `deploy/docker-compose.deploy.yml` | staging e produção (pipeline) | `api` (imagem do GHCR) · `mongo` |

- **Volumes:** `mongo-data` (`/data/db`) e `mongo-config` (`/data/configdb`) persistem os dados
  entre recriações; `mongo/init` é montado em `/docker-entrypoint-initdb.d` (somente leitura).
- **Variáveis de ambiente:** `MongoDb__ConnectionString`, `MongoDb__Username`,
  `MongoDb__Password`, `MongoDb__Database`, `Jwt__Key`, `Jwt__ExpiryMinutes`,
  `ASPNETCORE_ENVIRONMENT` — modelo em [`.env.example`](.env.example).
- **Rede:** bridge `backend` por projeto; a API acessa o banco pelo nome `mongo`.
- **Dependências:** `depends_on: condition: service_healthy` — a API só sobe depois que o
  MongoDB terminou a carga inicial (o healthcheck conecta pelo hostname, que só aceita
  conexões quando o `mongod` definitivo está no ar).
- **Recursos e logs (deploy):** limites de CPU/memória por ambiente e rotação de logs
  `json-file` (3 × 10 MB).

```bash
docker image ls ghcr.io/jaowilqui/esgimpact-api
docker compose -p esgimpact-production -f deploy/docker-compose.deploy.yml \
  --env-file deploy/production.env ps
```

---

## Prints do funcionamento

As evidências ficam em [`docs/prints/`](docs/prints/) e no PDF da entrega.

| Evidência | Arquivo |
|---|---|
| Execução completa do pipeline | `docs/prints/01-pipeline.png` |
| Job de build e testes | `docs/prints/02-build-testes.png` |
| Imagem publicada no GHCR | `docs/prints/03-imagem-ghcr.png` |
| Deploy em staging (smoke tests) | `docs/prints/04-deploy-staging.png` |
| Aprovação de produção | `docs/prints/05-aprovacao-producao.png` |
| Deploy em produção (smoke tests) | `docs/prints/06-deploy-producao.png` |
| Staging em execução (`:9081/api/info`) | `docs/prints/07-staging-info.png` |
| Produção em execução (`:9080/api/info`) | `docs/prints/08-producao-info.png` |
| Containers dos dois ambientes | `docs/prints/09-docker-ps.png` |

---

## Tecnologias utilizadas

| Camada | Tecnologia |
|---|---|
| Linguagem / framework | C# · .NET 8 · ASP.NET Core Web API |
| Banco de dados | MongoDB 7.0 · MongoDB.Driver 2.28 · Mongo Express 1.0.2 |
| Segurança | JWT Bearer · PBKDF2 · FluentValidation |
| Documentação da API | Swagger / OpenAPI (Swashbuckle) |
| Testes | xUnit · Microsoft.AspNetCore.Mvc.Testing (WebApplicationFactory) · Coverlet |
| Containers | Docker · Docker Compose v2 · Docker Buildx |
| CI/CD | GitHub Actions · GitHub Environments · runner self-hosted |
| Registro de imagens | GitHub Container Registry (GHCR) |
| Scripts | PowerShell · Bash · mongosh |

---

## Estrutura do projeto

```
.
├── Dockerfile                      # imagem da API (multi-stage)
├── docker-compose.yml              # ambiente local
├── .env.example                    # variáveis de ambiente
├── .github/workflows/ci-cd.yml     # pipeline
├── deploy/
│   ├── docker-compose.deploy.yml   # staging e produção
│   ├── staging.env  production.env # configuração não sensível por ambiente
│   └── deploy.ps1   deploy.sh      # deploy + smoke tests
├── src/ESGImpact.Api/              # API .NET 8
├── tests/ESGImpact.Tests/          # testes de unidade e integração
├── mongo/init/                     # criação das collections e carga inicial
├── mongo/crud/                     # roteiro de CRUD e agregações
└── docs/                           # documentação e prints
```

---

## Checklist de entrega

| Item | Status | Onde |
|---|---|---|
| Projeto compactado em `.ZIP` com estrutura organizada | ☑ | `ESGImpact-DevOps.zip` |
| Dockerfile funcional | ☑ | [`Dockerfile`](Dockerfile) |
| `docker-compose.yml` (app + banco, volumes, variáveis, rede) | ☑ | [`docker-compose.yml`](docker-compose.yml) · [`deploy/docker-compose.deploy.yml`](deploy/docker-compose.deploy.yml) |
| Pipeline com build, testes e deploy | ☑ | [`.github/workflows/ci-cd.yml`](.github/workflows/ci-cd.yml) |
| `README.md` com instruções e prints | ☑ | este arquivo |
| Documentação técnica com evidências (PDF) | ☑ | `docs/ESGImpact-DevOps.pdf` |
| Deploy realizado nos ambientes staging e produção | ☑ | seção *Prints do funcionamento* |

---

## Apêndice — modelo de dados (MongoDB)

| # | Collection | Pilar | O que representa |
|---|---|---|---|
| 1 | `emission_factors` | **E** | Fatores de emissão de CO₂ por atividade (transporte, energia, alimentação, resíduos), com metodologia e fonte |
| 2 | `simulations` | **E** | Cada medição de pegada de carbono — do app, de planilha importada ou de **sensor IoT** |
| 3 | `participants` | **S** | Cidadãos, organizações e órgãos públicos: engajamento, metas, programas sociais e consentimento LGPD |
| 4 | `offset_projects` | **E + S** | Projetos de compensação: capacidade, preço por moeda, certificações e **impacto social** (empregos, famílias, alunos) |
| 5 | `governance_audits` | **G** | Trilha append-only de auditoria: alterações de fator, revisões de política, acessos a dados, incidentes e relatórios publicados |

#### Recursos de modelagem usados

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

### Endpoints

#### CRUD das 5 collections (esquema flexível de ponta a ponta)

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

#### Endpoints de domínio

| Método | Rota | Descrição | Auth |
|---|---|---|---|
| `POST` | `/api/auth/login` | Emite o JWT (credenciais embutidas em `participants`) | – |
| `POST` | `/api/simulations` | Calcula, classifica e persiste com `factorSnapshot` | – |
| `GET` | `/api/simulations` | Histórico paginado | – |
| `GET` | `/api/analytics/dashboard` | Dashboard ESG em **um único `$facet`** | 🔒 |
| `GET` | `/api/recommendations` | Economia de CO₂ quantificada por troca de modal | – |
| `POST` | `/api/offset/quote` | Cotação nos **projetos reais** da collection | 🔒 |
| `GET` | `/api/health` · `/api/collections/stats` | Diagnóstico | – |
| `GET` | `/api/info` | Ambiente, versão e commit em execução | – |

### Credenciais (ambiente de estudo)

| Onde | Usuário | Senha |
|---|---|---|
| MongoDB (root) | `esgadmin` | `Esg@Mongo2026` |
| Mongo Express | `admin` | `esgadmin` |
| API (analista) | `analista` | `Esg@2026` |
| API (gestor) | `gestor.esg` | `Gov@2026` |
| API (cidadão) | `cidadao.ml` | `Cid@2026` |

> Valores de demonstração, versionados de propósito para o trabalho acadêmico.
> Em produção, use `.env` fora do controle de versão e segredos gerenciados.
