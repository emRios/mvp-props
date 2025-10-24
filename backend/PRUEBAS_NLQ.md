========================================
REPORTE DE PRUEBAS NLQ - 2025-10-23 22:42:10
========================================

TEST 1: Conteo de propiedades
Query: 'Cuantas propiedades hay disponibles'
 EXITOSO
Respuesta: Hay 100 propiedades disponibles.
Tool Args: estado=disponible, limit=1
Latencia: 2394ms
Trace ID: 74e7f847098c435181ef603741d26ea6

TEST 2: Listado de propiedades
Query: 'Propiedades disponibles'
 EXITOSO
Respuesta: Listó 100 propiedades con detalles
Tool Args: fields=id,propiedad,precio,imagenes.url, limit=10
Latencia: 8731ms

TEST 3: Filtro por estado vendido
Query: 'Dame propiedades vendidas con precio mayor a 500000'
 EXITOSO
Respuesta: Listó propiedades vendidas
Tool Args: estado=vendido, limit=10
Latencia: 8550ms

========================================
RESUMEN
========================================
 Endpoint /api/nlq funcionando correctamente
 LLM (Mock) procesando queries en lenguaje natural
 PropsTool conectando exitosamente a http://localhost:5002/api/propiedades/miraiz
 Rate limiting activo (30 req/min)
 Respuestas formateadas en Markdown
 Trace IDs generados para debugging

CONFIGURACIÓN ACTUAL:
- PropsLiteBaseUrl: http://localhost:5002/api/propiedades/miraiz
- LLM Provider: Mock (development mode)
- Rate Limit: 30 requests/minute
- Cache TTL: 90 seconds
- Timeout PropsTool: 1.5s

ARQUITECTURA VALIDADA:
Usuario  POST /api/nlq  Rate Limiter  LlmChatMock  PropsTool  API miraiz:5002  Respuesta NL
