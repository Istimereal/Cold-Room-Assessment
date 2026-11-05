# Cold Room Assessment

A portfolio project for analysing cold-room DHT22 logs. It imports measurements, keeps the raw observations, makes missing and ambiguous data explicit, computes deterministic statistics, asks an AI investigator to interpret that evidence, and asks a separate AI verifier to check the investigation against the same evidence.

This is a software-development and internship portfolio. It is not a production monitoring system, and it does not claim scientific validation of the models or of any cold room.

## Motivation

Sensor logs already support straightforward statistics. A language model can suggest observations and hypotheses, but it should not replace the measurements those claims depend on. The deterministic analysis stays inspectable. The investigator may only cite a backend evidence catalog. The verifier then checks that investigation against a catalog the backend builds again. The project is a place to practice that boundary: API design, persistence, tests, and a small UI. It does not claim that the verifier is correct, or that the models have found real faults in a room.

## Architecture

```text
CSV upload → PostgreSQL → deterministic analysis → AI Investigator → AI Verifier → React UI
```

| Layer | Responsibility |
| --- | --- |
| Domain | Facility, equipment, import, and measurement types |
| Application | Import, coverage, aligned samples, statistics, investigator and verifier use cases, validation |
| Infrastructure | EF Core persistence and the OpenAI Responses clients |
| API | HTTP endpoints and controlled error responses |
| React | Facility and equipment selection, CSV upload, analysis, investigation, and verification |

OpenAI SDK types stay in Infrastructure. Application defines `IInvestigatorClient` and `IVerifierClient` and sees only its own evidence catalog and result records. There is no shared “generic AI” service. The verifier is not an OpenAI hosted multi-agent workflow. It is a second request with its own instructions and schema.

The investigator and the verifier use the same `OpenAI:ApiKey`. `OpenAI:Model` selects the investigator model. `OpenAI:VerifierModel` is optional and falls back to `OpenAI:Model`, so a second key or a second dashboard setup is not required.

## Data semantics

- Source timestamps are stored as recorded. They are not converted to UTC.
- Analysis uses a 5-second grid over a half-open interval `[from, to)`.
- A requested interval longer than 24 hours is rejected.
- Missing expected samples stay missing. The analysis does not interpolate them.
- Duplicate values at one timestamp are ambiguous. The analysis does not pick one of them.
- Temperature and relative humidity are separate series.
- Equipment is not averaged together.
- The models receive a structured evidence catalog built from those statistics. They do not receive withheld experiment actions, research conclusions, or hidden labels.

## Features

- Facilities, equipment types, and equipment.
- DHT22 CSV import at `POST /api/equipment/{equipmentId}/imports` (`multipart/form-data`, field name `file`).
- Preserved imports and measurements in PostgreSQL.
- Facility coverage, analysis-period coverage, and an aligned analysis dataset.
- Deterministic temperature and relative-humidity statistics for one facility and interval.
- AI Investigator at `POST /api/facilities/{facilityId}/investigations?from&to`.
- AI Verifier at `POST /api/facilities/{facilityId}/verifications?from&to`. The body is the investigation already returned to the UI. The API rebuilds the evidence catalog from stored measurements and does not trust a client-supplied catalog.
- React page for upload, analysis, investigation, and verification. Investigation and verification run only from their own buttons.

## Technologies

Versions below are the ones this repository builds with.

- .NET 10 (`net10.0`) and ASP.NET Core
- Entity Framework Core 10.0.13
- PostgreSQL 17 (`postgres:17` in Docker Compose) and Npgsql.EntityFrameworkCore.PostgreSQL 10.0.3
- xUnit 2.9.3 and Testcontainers.PostgreSql 4.16.0
- OpenAI .NET SDK 2.14.0, using the Responses API with a strict JSON schema
- React 19.2 and TypeScript 5.9
- Vite 7.1
- Docker Compose

## Prerequisites

- .NET 10 SDK
- Docker, for PostgreSQL and for the backend integration tests
- Node.js and npm, for the frontend
- An OpenAI API key only if you want the investigator or verifier. Import and deterministic analysis do not call OpenAI.

## Run locally

```bash
git clone https://github.com/Istimereal/Cold-Room-Assessment.git
cd Cold-Room-Assessment
docker compose up -d
dotnet ef database update --project src/ColdRoomAssessment.Infrastructure --startup-project src/ColdRoomAssessment.Api
```

`dotnet ef` comes from the EF Core tools. The design-time factory and `appsettings.Development.json` both target the Compose database: host `localhost`, port `5432`, database `coldroom`, user `coldroom`, password `coldroom`.

Store the OpenAI settings with [user secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets). Do not put a real key in appsettings or in git. The project already has a user-secrets id. The placeholders below are not a key.

```bash
dotnet user-secrets init --project src/ColdRoomAssessment.Api
dotnet user-secrets set "OpenAI:ApiKey" "<YOUR_API_KEY>" --project src/ColdRoomAssessment.Api
dotnet user-secrets set "OpenAI:Model" "<MODEL_NAME>" --project src/ColdRoomAssessment.Api
```

Optional. When it is omitted or blank, the verifier uses `OpenAI:Model`.

```bash
dotnet user-secrets set "OpenAI:VerifierModel" "<MODEL_NAME>" --project src/ColdRoomAssessment.Api
```

Start the API. The launch profile listens on `http://localhost:5080`.

```bash
dotnet run --project src/ColdRoomAssessment.Api
```

Start the frontend:

```bash
cd frontend
npm install
npm run dev
```

Vite serves the UI at `http://localhost:5173` and proxies `/api` to `http://localhost:5080`. `VITE_API_BASE_URL` can override that proxy, but the dev setup does not need it.

## Demo workflow

1. Start PostgreSQL, the API, and the frontend.
2. Select a facility and a piece of equipment.
3. Choose a DHT22 `.csv` file and click **Upload data**. The page shows the import summary and does not start analysis or AI.
4. Choose an interval and click **Analyze**.
5. Read the coverage, missing and ambiguous samples, and the temperature and humidity statistics.
6. Click **Run AI investigation**. The analysis stays on the page.
7. Click **Verify investigation**. The investigator text stays visible while the verifier runs.
8. Read the overall verdict and the check beside each observation and hypothesis.

Changing the facility or the interval, uploading more data for the analyzed facility, or running a new investigation clears a previous verification. A successful upload refreshes the analysis only when an analysis is already on screen.

## Testing

Backend integration tests use PostgreSQL through Testcontainers. Investigator and verifier tests replace the OpenAI clients with fakes. They do not call the live API.

```bash
dotnet test tests/ColdRoomAssessment.IntegrationTests/ColdRoomAssessment.IntegrationTests.csproj
```

Frontend production build:

```bash
cd frontend
npm install
npm run build
```

## Security

The OpenAI key stays on the server. The React app never receives it and never calls OpenAI itself. Configuration belongs in user secrets or another secret store outside the repository. This repo does not commit an API key. Error responses use fixed messages. They do not include the key, a stack trace, or the raw provider body.

The local database password in Compose and `appsettings.Development.json` is for that local database only.

## Conclusions

The useful part of this slice is that the AI steps stay inspectable. Missing and ambiguous samples remain visible instead of being filled in. Claims point at evidence ids the backend created. The investigator proposes a reading of that evidence. The verifier checks that reading and can say the evidence is missing, conflicting, or simply insufficient. Deterministic statistics are repeatable. The model output is not, and a passing verification is not a guarantee that the conclusion is true.

## Future work

Charts, an evaluator that compares an investigation with findings withheld from the model, deployment, authentication, and support for more sensors or live ingestion are out of scope for this slice.
