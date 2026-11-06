# Cold Room Assessment

Cold Room Assessment er et portfolio-projekt, jeg har lavet for at arbejde praktisk med C#, .NET og AI-assisteret softwareudvikling og AI implementering i software eller opgave løsning.

Projektet tager udgangspunkt i et offentligt datasæt med temperatur- og fugtighedsmålinger fra ni DHT22-sensorer i et kølerum.

https://zenodo.org/records/15130001

## Hvad jeg især har fået ud af projektet

Projektet har især givet mig erfaring med nogle af de praktiske dilemmaer, der opstår, når klassisk software, sensordata og AI skal arbejde sammen.

En af de vigtigste udfordringer var datamængde. Sensorerne måler hvert femte sekund, og over mange timer bliver det hurtigt til meget store datasæt. Første version af analysen producerede så mange små events, at AI-requesten blev for stor til modellens context window.

Løsningen blev ikke bare at fjerne data tilfældigt. Backend gennemgår stadig de relevante målinger, men reducerer dem til mere meningsfulde facts som statistik, signifikante temperaturændringer og stabile perioder, før noget sendes til AI'en.

Det gav mig især erfaring med spørgsmål som:

- Hvor meget data skal behandles i backend, og hvor meget skal sendes videre?
- Hvordan reducerer man støj uden at fjerne et vigtigt signal?
- Hvordan laver man et evidensgrundlag, som en AI-model kan arbejde med uden at få rå data ukritisk?
- Hvordan holder man AI-fortolkning adskilt fra deterministiske beregninger?
- Hvordan kan en separat verifier kontrollere, om AI'ens observationer faktisk er understøttet af data?
- Hvordan håndterer man kendt kontekst som døråbninger uden at gøre en tidsmæssig sammenhæng til en påstået årsag?

Jeg har også oplevet mere almindelige integrationsproblemer undervejs, blandt andet omkring API-request-format, SDK-adfærd, databaseintegration, CSV-import og testmiljø.

Det er netop den slags problemer, jeg gerne ville møde gennem projektet frem for kun at bygge en lille isoleret demo.

## AI-assisteret udvikling

Projektet har samtidig været et forsøg på at lære at arbejde mere systematisk med AI som udviklingsværktøj.

Jeg har primært brugt Cursor som udviklingsagent, mens ChatGPT har fungeret som sparringspartner og senior-assistent til blandt andet arkitektur, debugging, review, afgrænsning og udformning af opgaver til Cursor.

Jeg har derfor ikke selv skrevet hver eneste linje kode eller formuleret alle prompts alene.

En del af læringen har netop været at arbejde med AI-værktøjer som en del af udviklingsprocessen: forstå forslagene, opdage fejl, stille bedre spørgsmål, afgrænse slices og kontrollere resultatet bagefter. Jeg har brugt AI mere end ønsket til at prompte cursor, planlægge slices eller opbygning af API'en. Men som start projekt, har det stadig givet lidt indblik i hvad man bør tage højde for, for at kontrollere den slags udviklingsværktøjer. Samt hvor hurtigt, selv et mindre system som dette, kan blive komplekst, selvom der er forholdsvis få data operationer.

Afgrænset tid til projektet gør også at det ikke kan betegnes som et færdigt projekt. Så det er ikke rigtigt blevet belyst, om det system jeg, hovedsageligt med støtte fra AI på begge sider af prompten, har bygge, har noget til fælles, med den opsætning som det tyske forsker team har benyttet. Om AI ville kunne komme med forskellige hypoteser for hvordan data fra sensorerne i kølerummet kunne forklares, eller lignende.

## Hvad systemet gør

Systemet kan:

- oprette faciliteter, sensortyper og sensorer
- importere CSV-filer med temperatur- og fugtighedsmålinger
- gemme rå observationer i PostgreSQL
- gøre manglende og tvetydige målinger eksplicitte
- beregne statistik og temperaturhændelser deterministisk i backend
- importere kendte hændelser som døråbninger og flytning af produkter
- sende et struktureret evidensgrundlag til en AI Investigator
- lade en separat AI Verifier kontrollere Investigatorens observationer og hypoteser
- vise analyse, AI-resultat og verificering i en React-frontend

Flowet er overordnet:

```text
CSV-data
   ↓
PostgreSQL
   ↓
Deterministisk analyse
   ↓
Evidence catalog
   ↓
AI Investigator
   ↓
AI Verifier
   ↓
React UI
```

## Hvorfor den deterministiske analyse ligger før AI

Sensorerne måler hvert femte sekund. Det ville være både dyrt og upraktisk bare at sende alle rå målinger direkte til en sprogmodel.

Backend gennemgår derfor stadig alle relevante målinger, men reducerer dem til strukturerede facts som blandt andet:

- minimum, maksimum, gennemsnit og median
- manglende og tvetydige samples
- signifikante temperaturstigninger og -fald
- stabile temperaturperioder
- tidspunkt og varighed for hændelser

AI'en får dermed et mindre og mere kontrolleret evidensgrundlag, mens de oprindelige målinger stadig bevares.

## Investigator og Verifier

Projektet bruger OpenAI Responses API til to separate AI-komponenter.

Investigator får det evidenskatalog, backend har produceret, og forsøger at finde observationer og mulige forklaringer.

Verifier får derefter Investigatorens resultat og kontrollerer det mod et nyt evidenskatalog, som backend bygger fra de lagrede målinger.

Verifieren kan eksempelvis vurdere en påstand som:

- supported
- partially supported
- unsupported

Formålet er ikke at bevise, at modellen har ret. Formålet er at gøre AI-output mere gennemsigtigt og knytte påstande til konkrete data.

Kendte hændelser som døråbninger og flytning af produkter kan tilføjes som kontekst. De holdes adskilt fra selve sensormålingerne, så systemet også kan køres uden denne viden.

## Teknologier

Projektet bruger blandt andet:

- C# / .NET 10
- ASP.NET Core
- Entity Framework Core
- PostgreSQL
- Docker Compose
- React
- TypeScript
- Vite
- OpenAI Responses API
- xUnit
- Testcontainers
- Cursor

## Datagrundlag

Projektet bruger det offentlige datasæt:

Temperature and Humidity Time Series of Cold Storage Room Monitoring

Datasættet indeholder målinger fra ni DHT22-sensorer i et kølerum med en samplingrate på fem sekunder samt registrerede eksperimenthændelser som døråbninger og flytning af produkter.

Forskningsdatasættet bruges som realistisk input til softwareprojektet. Dette projekt forsøger ikke at reproducere eller erstatte forskernes videnskabelige metode.

## Begrænsninger

Cold Room Assessment er et portfolio- og læringsprojekt.

Det er ikke:

- et valideret fødevaresikkerhedssystem
- et produktionsklart alarmsystem
- en videnskabeligt valideret analysemodel
- et bevis på, at AI'ens observationer eller hypoteser er korrekte

AI-output skal betragtes som fortolkning af det evidensgrundlag, systemet giver modellen.

## Kør projektet lokalt

### Krav

- .NET 10 SDK
- Docker
- Node.js og npm
- en OpenAI API-nøgle, hvis Investigator og Verifier skal bruges

### Database

```bash
docker compose up -d

dotnet ef database update \
  --project src/ColdRoomAssessment.Infrastructure \
  --startup-project src/ColdRoomAssessment.Api
```

### OpenAI

API-nøglen bør gemmes med .NET User Secrets og ikke lægges i repository:

```bash
dotnet user-secrets set "OpenAI:ApiKey" "<YOUR_API_KEY>" \
  --project src/ColdRoomAssessment.Api

dotnet user-secrets set "OpenAI:Model" "<MODEL_NAME>" \
  --project src/ColdRoomAssessment.Api
```

### Backend

```bash
dotnet run --project src/ColdRoomAssessment.Api
```

API'et kører som standard på:

http://localhost:5080

### Frontend

```bash
cd frontend
npm install
npm run dev
```

Frontend kører som standard på:

http://localhost:5173

### Tests

Backend:

```bash
dotnet test tests/ColdRoomAssessment.IntegrationTests/ColdRoomAssessment.IntegrationTests.csproj
```

Frontend:

```bash
cd frontend
npm run build
```
