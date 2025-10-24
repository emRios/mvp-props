# BACKEND BUILD SPEC (mvp-props / backend)

## Objetivo
Generar un **.NET 8 Minimal API** (KISS + Clean Architecture con énfasis en **SRP/OCP**) que:
- Exponga el catálogo (proxy + **cache 90s**).
- Gestione **interacciones** (en memoria) con **guardrails anti-alucinación**.
- Entregue **métricas** básicas.
- Aplique **CORS**, **API Key** en rutas sensibles, **timeouts**, **validación / saneamiento**.

## Reglas globales
- **No hacer preguntas** ni pedir confirmaciones. Crear/sobrescribir archivos aquí.
- **Sin paquetes extra** (solo SDK Web).
- Código minimal, legible y separando responsabilidades.
- Variables por **appsettings.json** y/o **env vars**.

## Estructura a generar (exacta)
backend/
├─ backend.csproj
├─ appsettings.json
├─ Program.cs
└─ Services/
└─ Llm/
├─ ILlmClient.cs
├─ LlmMock.cs
└─ LlmOpenAi.cs # opcional; implementar solo si hay llave LLM, si no dejar stub/documentado

markdown
Copiar código

## Endpoints requeridos
1) **GET `/properties`**
   - Proxy a `CatalogUrl` (por defecto: `https://test.controldepropiedades.com/api/propiedades/miraiz`)
   - **Cache en memoria**: 90s (configurable).
   - Devuelve **tal cual** la estructura de la API interna (con soporte para claves con tilde: `"baños"`, `"año"`).
2) **GET `/interactions?userId=...`**
   - Lista de interacciones en memoria (filtrable por `userId`).
   - **Protegido por API Key**.
3) **POST `/interactions`**
   - Crea interacción `{ userId, propiedadId?, pregunta }`.
   - **Validación**: body ≤ 4KB; campos requeridos; longitud de `pregunta` ≤ 500.
   - **Saneamiento**: trim + eliminar caracteres de control.
   - **Anti-prompt-injection**: bloquear patrones obvios (`ignore previous`, `system:`, `Bearer `, `sk-`, etc.).
   - Llama a `ILlmClient.AskAsync` con **contexto de propiedad** (si `propiedadId` viene).
   - Respuesta siempre basada **solo** en datos del catálogo: si falta el dato, devolver `"No tengo ese dato en el catálogo."`.
   - **Protegido por API Key**.
4) **GET `/metrics/interactions`**
   - `{ counts: { pendiente, respondida, cancelada }, total }`.
   - **Protegido por API Key**.

## Seguridad
- **CORS**: permitir `*` (demo). Responder `204` en `OPTIONS`.
- **API Key**:
  - Header: `Authorization: Bearer <API_KEY>`.
  - Requerido en `/interactions` y `/metrics`.
  - `API_KEY` viene de `appsettings.json` o `BACK_API_KEY` env var.

## Rendimiento y fiabilidad
- `HttpClient.Timeout = 5s`.
- `IMemoryCache` para `/properties` (TTL configurable).
- Serialización JSON:
  - Mantener nombres **exactos** (sin camelCase).
  - **Permitir tildes** en claves (`baños`, `año`) al serializar.

## Clean Architecture / SRP-OCP
- `ILlmClient` define el contrato.
- `LlmMock` (por defecto) **no requiere llaves** y aplica reglas deterministas.
- `LlmOpenAi` (opcional): usar si `LLM_PROVIDER=openai` + `LLM_API_KEY` presente.
- `InteractionStore` (SRP): almacenamiento en memoria + métricas.

## Variables de configuración
- `CatalogUrl` (string) — default: `"https://test.controldepropiedades.com/api/propiedades/miraiz"`
- `CacheSeconds` (int) — default: `90`
- `API_KEY` (string) — default: `"demo-key"`
- `LLM_PROVIDER` (string) — `"mock"` (default) o `"openai"`
- `LLM_API_KEY` (string) — requerido si `openai`
- `LLM_MODEL` (string) — default `"gpt-4o-mini"`

## Requisitos por archivo (contenido esperado)

### `backend.csproj`
- SDK Web, `TargetFramework` = `net8.0`
- `<Nullable>enable</Nullable>` y `<ImplicitUsings>enable</ImplicitUsings>`

### `appsettings.json`
```json
{
  "CatalogUrl": "https://test.controldepropiedades.com/api/propiedades/miraiz",
  "CacheSeconds": "90",
  "API_KEY": "demo-key",
  "LLM_PROVIDER": "mock",
  "LLM_API_KEY": "",
  "LLM_MODEL": "gpt-4o-mini"
}
Program.cs
Imports: System.Net.Http.Json, System.Text.Json, System.Text.Encodings.Web, System.Text.Json.Serialization, Microsoft.Extensions.Caching.Memory, System.Text.RegularExpressions.

Services:

AddMemoryCache()

AddHttpClient() (para inyectar clientes)

Registro ILlmClient: si LLM_PROVIDER=openai && LLM_API_KEY → LlmOpenAi, si no → LlmMock.

Middleware:

CORS (simple): Access-Control-Allow-Origin: *, headers y methods básicos; manejo de OPTIONS.

API Key: aplicar a rutas /interactions y /metrics.

HttpClient global con Timeout = 5s.

Endpoints:

GET /properties:

Revisar IMemoryCache. Si hit → devolver JSON cacheado.

Fetch CatalogUrl, deserializar en DTO ApiResp con propiedades que permitan claves "baños"/"año".

Opcional (normalización): si vienen banos/ano, copiar a "baños"/"año" antes de responder.

Guardar en cache (TTL CacheSeconds).

Serializar con PropertyNamingPolicy = null y Encoder = UnsafeRelaxedJsonEscaping.

GET /interactions:

Leer de InteractionStore.List(userId).

POST /interactions:

Validar tamaño de body (≤ 4096).

Saneamiento y validación de campos.

Construir PropertyContext solo con campos necesarios de la propiedad solicitada.

await llm.AskAsync(new LlmRequest(pregunta, contexto)).

Guardar y devolver la interacción.

GET /metrics/interactions:

Devolver conteos y total.

Modelos / Helpers dentro del mismo archivo o en región:

record Interaction { Id, UserId, PropiedadId?, Pregunta, Respuesta?, Status, CreatedAt }

class InteractionStore con _list, Add, List, Metrics.

static string Sanitize(string?) (trim + remover control chars).

static bool IsPromptInjection(string) (bloqueo de patrones).

DTOs de catálogo: ApiResp, PropertyItem, Proyecto, ImagenItem.

Atributos JsonPropertyName para claves "baños" y "año" más variantes sin tilde (banos, ano).

Services/Llm/ILlmClient.cs
record LlmRequest(string Question, PropertyContext? Context);

record LlmResponse(string Answer);

record PropertyContext(int? Id, decimal? Precio, int? Habitaciones, decimal? Banos, int? Parqueos, decimal? M2Construccion, string? Ubicacion);

interface ILlmClient { Task<LlmResponse> AskAsync(LlmRequest req, CancellationToken ct = default); }

Services/Llm/LlmMock.cs
Implementación determinista que responde solo con datos del contexto:

Si no hay Context, dar mensaje general con conteo del catálogo (opcional).

Reglas por palabra clave en Question: “precio”, “habitac”, “baño/banio/banos”, “parqueo”, “m2/metros”, “ubic”.

Si el campo no existe en contexto → "No tengo ese dato en el catálogo.".

No llamar APIs externas ni requerir llaves.

Services/Llm/LlmOpenAi.cs (opcional)
Usar HttpClient + LLM_API_KEY + LLM_MODEL si LLM_PROVIDER=openai.

Prompt de sistema: “Responde SOLO con los datos provistos en el contexto… si falta, di ‘No tengo ese dato en el catálogo.’”

temperature = 0.1.

Si no hay llave o falla la llamada, devolver respuesta segura "No tengo ese dato en el catálogo.".

Ejecución esperada (comandos)
bash
Copiar código
# En la carpeta backend/
dotnet build
dotnet run
# Escucha en http://localhost:5000
Pruebas rápidas
bash
Copiar código
curl http://localhost:5000/properties | head
curl -H "Authorization: Bearer demo-key" "http://localhost:5000/interactions?userId=u-demo"
curl -X POST http://localhost:5000/interactions \
  -H "Authorization: Bearer demo-key" -H "content-type: application/json" \
  -d '{"userId":"u-demo","propiedadId":942,"pregunta":"¿Tiene parqueos?"}'
curl -H "Authorization: Bearer demo-key" http://localhost:5000/metrics/interactions
Resultado deseado de Copilot
Plan de cambios + contenido completo de todos los archivos listados.

Sin preguntas ni texto extra. Listo para “Apply All”.

Copiar código
