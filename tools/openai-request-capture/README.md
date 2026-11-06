# OpenAI request capture

Listens on `http://localhost:5099` and prints the raw `POST /v1/responses` body. It does not call OpenAI.

## Start the capture API

From the repository root:

```bash
dotnet run --project tools/openai-request-capture --urls http://localhost:5099
```

Leave this terminal open. The host log includes `Now listening on: http://localhost:5099`.

## Confirm it listens

In a second terminal:

```bash
curl -sS -D - http://localhost:5099/v1/responses \
  -H 'Content-Type: application/json' \
  -H 'Authorization: Bearer sk-example' \
  -d '{"model":"gpt-6-luna","input":"ping"}'
```

The curl response body is `{"status":"captured"}`. The capture terminal prints `Authorization: present` and does not print the bearer token. `Model` is `gpt-6-luna`.

## Point the Cold Room investigator at it

OpenAI .NET 2.14.0 has no API-key constructor that takes an endpoint. The supported override is `ResponsesClientOptions.Endpoint` on `new ResponsesClient(new ApiKeyCredential(apiKey), options)`. The SDK appends `/responses` to that base URI, so the value must end in `/v1`.

```bash
dotnet user-secrets set "OpenAI:Endpoint" "http://localhost:5099/v1" --project src/ColdRoomAssessment.Api
dotnet run --project src/ColdRoomAssessment.Api
```

`OpenAI:ApiKey` and `OpenAI:Model` stay as they are. A blank or missing `OpenAI:Endpoint` keeps the default `https://api.openai.com/v1`. This override is only used by the investigator client. The verifier is unchanged.

## Run one minimal SDK request

Trigger one facility investigation from the app. The investigator sends the minimal options probe first. The capture terminal prints a `CAPTURE` block for it:

```text
Method: POST
Path: /v1/responses
HttpVersion: HTTP/1.1
Content-Type: application/json
Content-Length: 181
Transfer-Encoding: absent
Expect: absent
Authorization: present
TopLevelProperties: model,input,store
Model: gpt-6-luna
BodyBytes: 181
```

The body begins with `{"model":"gpt-6-luna"` and ends with `"store":false}`. The next block is the convenience overload: `Content-Length: 167`, `TopLevelProperties: model,input`, and no `"store"` property. The investigator log reports those calls as failed because `{"status":"captured"}` is not a Responses payload. That failure is expected. The raw request is in the capture terminal.

Remove the override when finished:

```bash
dotnet user-secrets remove "OpenAI:Endpoint" --project src/ColdRoomAssessment.Api
```
