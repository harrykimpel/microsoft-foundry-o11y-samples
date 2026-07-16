# Realtime Sample

A .NET sample that drives the OpenAI **Realtime API** (via a Microsoft Foundry / Azure OpenAI endpoint) with audio input and output, function calling, and full **OpenTelemetry** instrumentation. Traces, metrics, and logs are exported via OTLP — the included `run.sh` is preconfigured for New Relic.

The sample sends a pre-recorded German voice prompt asking about the weather and date/time at Heide-Park Soltau. The model — instructed to reply in German with a North German pirate accent — invokes the `GetCurrentWeather` and `GetDateTime` tools and streams an audio response back.

## What the sample demonstrates

- **Realtime conversation session** with `gpt-realtime` over WebSocket, including server-side VAD turn detection and input transcription via `gpt-4o-transcribe`.
- **Function calling / tool use** with two tools (`GetCurrentWeather`, `GetDateTime`) the model can invoke mid-conversation.
- **Audio streaming**: a pre-recorded WAV is sent as input audio; the model's PCM audio response is written incrementally to disk.
- **OpenTelemetry tracing** of the conversation with custom activities (`main`, `RunAsync`, `Conversation`, and one per server update type) plus GenAI semantic conventions enabled via env vars.
- **OTLP export** of traces, metrics, and logs to any OTLP/HTTP backend (defaults to New Relic).

## Prerequisites

- .NET 10 SDK or newer (the sample uses the [single-file `dotnet run`](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/sdk#run-c-files-without-a-project) workflow — no `.csproj`).
- An Azure OpenAI / Microsoft Foundry resource with a `gpt-realtime` deployment.
- Optional: a New Relic account (or any OTLP/HTTP backend) for observability.
- Optional: the [OpenTelemetry .NET auto-instrumentation](https://github.com/open-telemetry/opentelemetry-dotnet-instrumentation) installed at `~/.otel-dotnet-auto`. Only needed if you want auto-instrumentation on top of the manual instrumentation in `Program.cs`.

## Configuration

The following environment variables are read at runtime:

| Variable | Purpose | Required |
|---|---|---|
| `MSFT_FOUNDRY_ENDPOINT_2` | Azure OpenAI / Foundry endpoint, e.g. `https://<resource>.openai.azure.com/openai/v1` | Yes |
| `MSFT_FOUNDRY_API_KEY_2` | API key for the endpoint above | Yes |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | OTLP/HTTP collector endpoint. Defaults to `http://localhost:4318` | No |
| `NEW_RELIC_LICENSE_KEY` | If set, used as the `api-key` header on OTLP exports | No |

`run.sh` exports a sensible default set:

- Points OTLP at New Relic (`https://otlp.nr-data.net`) using your New Relic license key.
- Enables GenAI semantic conventions and message/tool content capture (`OTEL_INSTRUMENTATION_GENAI_CAPTURE_*`).
- Enables OpenAI's experimental OpenTelemetry hooks (`OPENAI_EXPERIMENTAL_ENABLE_OPEN_TELEMETRY=true`).

Edit [run.sh](./run.sh) to point at your own Foundry endpoint and OTLP backend.

## Run

```bash
# from the realtime/ directory
export NEW_RELIC_LICENSE_KEY=...      # optional, only if exporting to New Relic
export MSFT_FOUNDRY_API_KEY_2=...     # required
./run.sh
```

The script runs `dotnet Program.cs` directly (single-file C# script mode).

## Inputs and outputs

- **Input audio**: [Assets/realtime-wetter-heide-park.wav](./Assets/realtime-wetter-heide-park.wav) — pre-recorded German prompt sent to the model. Swap to a different file by editing `inputAudioFilePath` in [Program.cs](./Program.cs). The model expects 24 kHz mono PCM16 WAV. Convert a recording (e.g. an `.m4a`) to that format with ffmpeg:

  ```bash
  ffmpeg -i Assets/realtime-weather-munich.m4a -acodec pcm_s16le -ar 24000 -ac 1 Assets/realtime-weather-munich.wav
  ```

- **Output audio**: [Output/output.raw](./Output/) — raw 24 kHz mono PCM16 written incrementally as the model streams its response. Convert to WAV with ffmpeg if you want to listen to it:

  ```bash
  ffmpeg -f s16le -ar 24000 -ac 1 -i Output/output.raw Output/output.wav
  ```

## Files

- [Program.cs](./Program.cs) — main sample: realtime session setup, tool definitions, update loop, OTel wiring.
- [run.sh](./run.sh) — environment setup and launcher.
- [Assets/](./Assets/) — pre-recorded audio prompts.
- [Output/](./Output/) — destination for streamed audio responses.
