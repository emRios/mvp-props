# 🎯 NLQ + LLM Integration - Documentación

## 📋 Resumen

Sistema de **Natural Language Query (NLQ)** integrado con **LLM + Function Calling** para consultar el catálogo de propiedades usando lenguaje natural.

## 🏗️ Arquitectura

```
Frontend (voz→texto) → POST /api/nlq → LlmChat → [Function Calling]
                                           ↓
                                      get_props tool
                                           ↓
                                      PropsTool → GET /api/propiedades/miraiz-lite
                                           ↓
                                      Respuesta JSON con datos
```

## 🔗 Endpoints Nuevos

### POST /api/nlq

Endpoint principal para consultas en lenguaje natural.

**Request:**
```json
{
  "query": "¿Cuántas propiedades disponibles hay?",
  "locale": "es",
  "limit": 10,
  "estado": "disponible"
}
```

**Response:**
```json
{
  "success": true,
  "answer": "Hay 5 propiedades disponibles en el catálogo.",
  "toolPayload": {
    "success": true,
    "data": [...],
    "cursor": 123
  },
  "toolArgs": {
    "estado": "disponible",
    "limit": 10,
    "fields": "id,propiedad,precio",
    "cursor": null
  },
  "latency_ms": 1234,
  "trace_id": "abc123..."
}
```

**Características:**
- ✅ Rate limiting: 30 req/min por IP
- ✅ Timeout total: 3-5s
- ✅ Validación de entrada
- ✅ Trace ID para debugging
- ✅ Métricas de latencia

### GET /api/propiedades/miraiz-lite

Endpoint "lite" con field mask y cursor para optimización.

**Query Parameters:**
- `fields`: Campos a retornar (ej: `id,propiedad,precio,imagenes.url`)
- `estado`: Filtro por estado (`disponible`, `vendido`, `reservado`)
- `afterId`: Cursor para paginación
- `limit`: Límite de resultados (1-100, default: 20)

**Ejemplo:**
```bash
GET /api/propiedades/miraiz-lite?fields=id,propiedad,precio,imagenes.url&estado=disponible&limit=20
```

**Response:**
```json
{
  "success": true,
  "data": [
    {
      "id": 1,
      "propiedad": "Casa en la playa",
      "precio": 250000,
      "imagenes": [
        { "tipo": "principal", "url": "https://...", "formato": "jpg" }
      ]
    }
  ],
  "cursor": 20
}
```

## 🤖 LLM Function Calling

### Tool: get_props

**Descripción:** Obtiene propiedades del catálogo con filtros y campos específicos

**Parámetros:**
```json
{
  "estado": "disponible",        // opcional: disponible, vendido, reservado
  "limit": 10,                   // requerido: 1-100
  "cursor": null,                // opcional: ID para paginación
  "fields": "id,propiedad,precio" // opcional: campos separados por coma
}
```

### System Prompt

**Español:**
```
Eres un asistente inmobiliario. Si la respuesta depende del catálogo, 
llama SIEMPRE al tool get_props con limit y SOLO los campos necesarios (fields). 
Si piden imágenes, incluye imagenes.url; si piden precios, incluye precio. 
Responde de forma concisa.
```

**English:**
```
You are a real estate assistant. If the answer depends on the catalog, 
ALWAYS call the get_props tool with limit and ONLY the necessary fields. 
If they ask for images, include imagenes.url; if they ask for prices, include precio. 
Answer concisely.
```

## 📊 Flujo de Ejecución

1. **Frontend envía query** (voz convertida a texto)
2. **Backend recibe** en `/api/nlq`
3. **Validación**: query no vacío, limit válido
4. **LlmChat.RunAsync**:
   - Primera llamada LLM con tool definition
   - LLM decide si necesita `get_props`
   - Si sí: ejecuta PropsTool con args optimizados
   - Segunda llamada LLM con resultado del tool
   - LLM genera respuesta natural
5. **Respuesta** con answer + metadata

## 🔒 Seguridad y Límites

### Rate Limiting
- **Límite:** 30 requests/minuto por IP
- **Ventana:** Fixed window de 60 segundos
- **Queue:** 0 (rechaza inmediatamente si excede)
- **Response:** 503 Service Unavailable

### Timeouts
- **Request total:** 3-5 segundos (configurable)
- **HTTP PropsApi:** 1.5 segundos
- **LLM calls:** Sin timeout explícito (depende de red)

### Validación
- Query no vacío
- Limit entre 1-100
- Estado en valores permitidos

## 🧪 Pruebas

### Pruebas Manuales

```powershell
# Ejecutar servidor
dotnet run

# En otra terminal, ejecutar tests
.\test-nlq.ps1
```

### Ejemplos de Queries

```json
// Consulta simple
{ "query": "¿Cuántas propiedades hay?" }

// Con filtros
{ 
  "query": "Muéstrame casas disponibles con precio", 
  "limit": 5,
  "estado": "disponible"
}

// Con imágenes
{ 
  "query": "Quiero ver propiedades con fotos",
  "limit": 3
}

// En inglés
{
  "query": "Show me available properties",
  "locale": "en"
}
```

### Tests Automatizados (xUnit)

Los tests de integración validan el contrato del socio:

```bash
cd ../Tests
dotnet test
```

**Tests incluidos:**
1. ✅ Validación contra JSON Schema
2. ✅ Claves con tilde (`baños`, `año`)
3. ✅ Estructura de imágenes

## ⚙️ Configuración

### appsettings.json

```json
{
  "PropsLiteBaseUrl": "http://localhost:5002/api/propiedades/miraiz-lite",
  "DefaultFields": "id,propiedad,precio,imagenes.url",
  "DefaultLimit": "20",
  "OpenAI": {
    "Model": "gpt-4o-mini"
  },
  "LLM_PROVIDER": "openai",
  "LLM_API_KEY": ""
}
```

### Variables de Entorno

```bash
# Producción
export OPENAI_API_KEY="sk-..."
export ASPNETCORE_ENVIRONMENT="Production"

# Desarrollo
export LLM_PROVIDER="mock"  # Usa LlmChatMock
```

## 📈 Optimizaciones

### Field Mask
El tool `get_props` solo solicita campos necesarios:
- Pregunta sobre precios → `fields=id,propiedad,precio`
- Pregunta sobre imágenes → `fields=id,propiedad,imagenes.url`
- Pregunta general → usa `DefaultFields`

### Cursor Pagination
Soporte de paginación con `afterId`:
```json
{
  "cursor": 20,  // Último ID retornado
  "limit": 10
}
```

### Caché
- **Endpoint lite:** Caché de 90s por combinación de parámetros
- **Endpoint original:** ETag + Cache-Control para 304 Not Modified

## 🐛 Troubleshooting

### Error: "NLQ_FAILED"
- Verificar que PropsTool pueda conectar a la API
- Revisar logs con el `trace_id`
- Verificar timeout de red

### Rate Limited (503)
- Esperar 60 segundos
- Reducir frecuencia de requests
- Implementar retry con backoff en cliente

### LLM no llama al tool
- Verificar OPENAI_API_KEY
- Revisar system prompt
- Cambiar a modo mock para debugging

## 📚 Servicios Nuevos

### ILlmChat
Interface para NLQ con function calling.

**Implementaciones:**
- `LlmChat`: Usa OpenAI con tools
- `LlmChatMock`: Mock para desarrollo sin API key

### PropsTool
Handler del tool `get_props`.

**Responsabilidades:**
- Construir query string
- Llamar a endpoint lite
- Validar respuesta
- Manejo de errores

## 🔄 Diferencias con /interactions

| Feature | /interactions | /api/nlq |
|---------|--------------|----------|
| LLM | `ILlmClient` (simple) | `ILlmChat` (function calling) |
| Contexto | Propiedad específica | Catálogo completo |
| Rate Limit | No | Sí (30/min) |
| Timeout | No | Sí (3-5s) |
| Paginación | No | Sí (cursor) |
| Field Mask | No | Sí |

## ✅ Checklist de Implementación

- [x] Endpoint `/api/nlq` con DTOs
- [x] `ILlmChat` + implementaciones (OpenAI + Mock)
- [x] `PropsTool` con retry y timeout
- [x] Endpoint `/api/propiedades/miraiz-lite` con field mask
- [x] Rate limiting (30/min)
- [x] ETag + Cache-Control en endpoint original
- [x] JSON Schema para validación
- [x] Tests xUnit de contrato
- [x] Script de pruebas PowerShell
- [x] Documentación completa

## 🚀 Próximos Pasos

1. ✅ Implementar Polly retry en PropsTool
2. ✅ Agregar logging estructurado
3. ✅ Métricas de uso del endpoint NLQ
4. ✅ Optimizar prompts según feedback
5. ✅ Caché de respuestas LLM frecuentes

---

**Autor:** GitHub Copilot  
**Versión:** 1.0  
**Fecha:** 24 de octubre de 2025
