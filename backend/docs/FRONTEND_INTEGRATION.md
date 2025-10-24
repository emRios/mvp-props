# Guía de Integración Frontend - MVP Props Backend

## 📋 Índice
1. [Endpoints Disponibles](#endpoints-disponibles)
2. [Estructura de Datos](#estructura-de-datos)
3. [Consumo de Imágenes](#consumo-de-imágenes)
4. [Ejemplos de Código](#ejemplos-de-código)
5. [NLQ - Lenguaje Natural](#nlq---lenguaje-natural)

---

## 🔌 Endpoints Disponibles

### 1. GET `/properties` - Catálogo de Propiedades (Público)
```http
GET http://localhost:5000/properties?limit=20&afterId=0
```

**Query Parameters:**
- `limit` (opcional): Número de propiedades (default: 20, max: 100)
- `afterId` (opcional): ID para paginación (cursor)

**Response Headers:**
- `ETag`: Hash del contenido para cache
- `Cache-Control`: `public, max-age=90`

**Response Body:**
```json
{
  "success": true,
  "data": [
    {
      "id": 1,
      "propiedad": "A-1",
      "area": 185,
      "precio": 458299,
      "tipo": "Lote",
      "clase_tipo": "Esquina",
      "modelo": "M-1",
      "estado": "disponible",
      "baños": 2,
      "año": 2024,
      "imagenes": [
        {
          "tipo": "promocional1",
          "url": "/mvp-props/images/casa-modelo-1.jpg",
          "formato": "imagen"
        },
        {
          "tipo": "promocional2",
          "url": "/mvp-props/images/aaron-huber-G7sE2S4Lab4-unsplash.jpg",
          "formato": "imagen"
        }
      ]
    }
  ],
  "cursor": 100
}
```

---

### 2. POST `/api/nlq` - Consulta en Lenguaje Natural (Público)
```http
POST http://localhost:5000/api/nlq
Content-Type: application/json

{
  "query": "Cuantas propiedades hay disponibles",
  "limit": 10
}
```

**Request Body:**
- `query` (requerido, 5-500 chars): Pregunta en lenguaje natural
- `limit` (opcional): Límite para herramientas (default: 20)

**Response:**
```json
{
  "success": true,
  "answer": "Hay 100 propiedades disponibles.",
  "toolPayload": {
    "success": true,
    "data": [ /* propiedades */ ]
  },
  "toolArgs": {
    "estado": "disponible",
    "limit": 1,
    "fields": "id"
  },
  "latency_ms": 2394,
  "trace_id": "74e7f847098c435181ef603741d26ea6"
}
```

**Rate Limiting:**
- 30 requests por minuto por IP
- Status 503 si excede límite

---

### 3. POST `/interactions` - Interacciones LLM (Protegido)
```http
POST http://localhost:5000/interactions
Authorization: Bearer demo-key
Content-Type: application/json

{
  "userId": "user123",
  "query": "Busco casa con 3 habitaciones",
  "context": {
    "budget": 500000,
    "location": "zona norte"
  }
}
```

**Headers Requeridos:**
- `Authorization: Bearer demo-key`

**Response:**
```json
{
  "success": true,
  "response": "Basándome en tu presupuesto de $500,000...",
  "interaction_id": "int_abc123",
  "timestamp": "2025-10-23T22:30:00Z"
}
```

---

### 4. GET `/metrics/interactions` - Métricas (Protegido)
```http
GET http://localhost:5000/metrics/interactions
Authorization: Bearer demo-key
```

**Response:**
```json
{
  "success": true,
  "metrics": {
    "totalInteractions": 145,
    "avgResponseTime": 1250
  }
}
```

---

## 📦 Estructura de Datos

### PropertyItem
```typescript
interface PropertyItem {
  id: number;
  propiedad: string;        // "A-1", "B-23", etc.
  area: number;             // metros cuadrados
  precio: number;           // precio en $
  tipo: string;             // "Lote", "Casa", "Departamento"
  clase_tipo: string;       // "Esquina", "Intermedio", "Premium"
  modelo: string;           // "M-1", "M-2", etc.
  estado: string;           // "disponible", "vendido", "apartado"
  baños: number;            // número de baños
  año: number;              // año de construcción
  imagenes: ImagenItem[];
}

interface ImagenItem {
  tipo: string;             // "promocional1", "promocional2", etc.
  url: string;              // URL relativa: "/mvp-props/images/..."
  formato: string;          // "imagen"
}
```

---

## 🖼️ Consumo de Imágenes

### Problema: URLs Relativas
El API retorna URLs **relativas** que apuntan al servidor de imágenes estático:
```
/mvp-props/images/casa-modelo-1.jpg
```

### Solución 1: Proxy en Frontend (Recomendado)
**Vite (React/Vue):**
```javascript
// vite.config.js
export default {
  server: {
    proxy: {
      '/mvp-props': {
        target: 'http://localhost:5002',
        changeOrigin: true
      }
    }
  }
}
```

**Next.js:**
```javascript
// next.config.js
module.exports = {
  async rewrites() {
    return [
      {
        source: '/mvp-props/:path*',
        destination: 'http://localhost:5002/mvp-props/:path*'
      }
    ]
  }
}
```

### Solución 2: Transformar URLs en el Cliente
```typescript
// utils/images.ts
const IMAGE_BASE_URL = 'http://localhost:5002';

export function getImageUrl(relativeUrl: string): string {
  return `${IMAGE_BASE_URL}${relativeUrl}`;
}

// Uso:
<img src={getImageUrl(propiedad.imagenes[0].url)} alt={propiedad.propiedad} />
```

### Solución 3: Backend como Proxy (Implementar)
Agregar endpoint en el backend:
```csharp
app.MapGet("/images/{**path}", async (string path, IHttpClientFactory factory) =>
{
  var http = factory.CreateClient();
  var imageUrl = $"http://localhost:5002/mvp-props/images/{path}";
  var response = await http.GetAsync(imageUrl);
  
  if (!response.IsSuccessStatusCode)
    return Results.NotFound();
    
  var content = await response.Content.ReadAsByteArrayAsync();
  var contentType = response.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
  
  return Results.File(content, contentType);
});
```

Luego en frontend:
```typescript
<img src={`http://localhost:5000/images/casa-modelo-1.jpg`} />
```

---

## 💻 Ejemplos de Código

### React + TypeScript
```tsx
import { useState, useEffect } from 'react';

interface Property {
  id: number;
  propiedad: string;
  precio: number;
  imagenes: Array<{
    tipo: string;
    url: string;
    formato: string;
  }>;
}

const IMAGE_BASE = 'http://localhost:5002';

function PropertyList() {
  const [properties, setProperties] = useState<Property[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    fetch('http://localhost:5000/properties?limit=10')
      .then(res => res.json())
      .then(data => {
        if (data.success) {
          setProperties(data.data);
        }
      })
      .finally(() => setLoading(false));
  }, []);

  if (loading) return <div>Cargando...</div>;

  return (
    <div className="grid grid-cols-3 gap-4">
      {properties.map(prop => (
        <div key={prop.id} className="card">
          <h3>{prop.propiedad}</h3>
          <p>${prop.precio.toLocaleString()}</p>
          
          {/* Galería de imágenes */}
          <div className="images">
            {prop.imagenes.map((img, idx) => (
              <img
                key={idx}
                src={`${IMAGE_BASE}${img.url}`}
                alt={`${prop.propiedad} - ${img.tipo}`}
                loading="lazy"
              />
            ))}
          </div>
        </div>
      ))}
    </div>
  );
}
```

### Vue 3 + Composition API
```vue
<script setup lang="ts">
import { ref, onMounted } from 'vue';

const IMAGE_BASE = 'http://localhost:5002';
const properties = ref([]);

onMounted(async () => {
  const res = await fetch('http://localhost:5000/properties?limit=10');
  const data = await res.json();
  if (data.success) {
    properties.value = data.data;
  }
});

function getImageUrl(relativeUrl: string) {
  return `${IMAGE_BASE}${relativeUrl}`;
}
</script>

<template>
  <div class="grid">
    <div v-for="prop in properties" :key="prop.id" class="card">
      <h3>{{ prop.propiedad }}</h3>
      <p>${{ prop.precio.toLocaleString() }}</p>
      
      <div class="images">
        <img
          v-for="(img, idx) in prop.imagenes"
          :key="idx"
          :src="getImageUrl(img.url)"
          :alt="`${prop.propiedad} - ${img.tipo}`"
          loading="lazy"
        />
      </div>
    </div>
  </div>
</template>
```

### Vanilla JavaScript
```javascript
const IMAGE_BASE = 'http://localhost:5002';

async function loadProperties() {
  const response = await fetch('http://localhost:5000/properties?limit=10');
  const data = await response.json();
  
  if (!data.success) return;
  
  const container = document.getElementById('properties');
  
  data.data.forEach(prop => {
    const card = document.createElement('div');
    card.className = 'property-card';
    
    // Galería de imágenes
    const gallery = prop.imagenes.map(img => 
      `<img src="${IMAGE_BASE}${img.url}" alt="${prop.propiedad}" loading="lazy">`
    ).join('');
    
    card.innerHTML = `
      <h3>${prop.propiedad}</h3>
      <p class="price">$${prop.precio.toLocaleString()}</p>
      <div class="gallery">${gallery}</div>
    `;
    
    container.appendChild(card);
  });
}

loadProperties();
```

---

## 🤖 NLQ - Lenguaje Natural

### Ejemplo: Chat de Búsqueda
```typescript
interface NLQRequest {
  query: string;
  limit?: number;
}

interface NLQResponse {
  success: boolean;
  answer: string;
  toolPayload?: any;
  toolArgs?: any;
  latency_ms: number;
  trace_id: string;
}

async function askNLQ(query: string): Promise<NLQResponse> {
  const response = await fetch('http://localhost:5000/api/nlq', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json'
    },
    body: JSON.stringify({ query, limit: 10 })
  });
  
  return response.json();
}

// Uso:
const result = await askNLQ("Muéstrame propiedades disponibles con precio menor a 400000");
console.log(result.answer);
// "Aquí tienes algunas propiedades disponibles con precio menor a $400,000: ..."

// Acceso a datos originales:
if (result.toolPayload?.data) {
  const properties = result.toolPayload.data;
  // Renderizar propiedades...
}
```

### Ejemplo: Chat UI Completo
```tsx
function NLQChat() {
  const [messages, setMessages] = useState<Array<{role: string, content: string}>>([]);
  const [input, setInput] = useState('');
  const [loading, setLoading] = useState(false);

  async function sendMessage() {
    if (!input.trim()) return;
    
    // Agregar mensaje del usuario
    setMessages(prev => [...prev, { role: 'user', content: input }]);
    setLoading(true);
    
    try {
      const response = await fetch('http://localhost:5000/api/nlq', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ query: input, limit: 5 })
      });
      
      const data = await response.json();
      
      // Agregar respuesta del asistente
      setMessages(prev => [...prev, { 
        role: 'assistant', 
        content: data.answer 
      }]);
      
      setInput('');
    } catch (error) {
      console.error('Error:', error);
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="chat-container">
      <div className="messages">
        {messages.map((msg, idx) => (
          <div key={idx} className={`message ${msg.role}`}>
            {msg.content}
          </div>
        ))}
        {loading && <div className="loading">Pensando...</div>}
      </div>
      
      <div className="input-area">
        <input
          value={input}
          onChange={e => setInput(e.target.value)}
          onKeyPress={e => e.key === 'Enter' && sendMessage()}
          placeholder="Pregunta sobre propiedades..."
        />
        <button onClick={sendMessage}>Enviar</button>
      </div>
    </div>
  );
}
```

---

## 🎨 Optimización de Imágenes

### Lazy Loading
```tsx
<img 
  src={getImageUrl(imagen.url)} 
  alt={propiedad.propiedad}
  loading="lazy"  // ← Carga diferida nativa
/>
```

### Placeholder con Blur
```tsx
import { useState } from 'react';

function PropertyImage({ src, alt }) {
  const [loaded, setLoaded] = useState(false);

  return (
    <div className="image-wrapper">
      <img
        src={src}
        alt={alt}
        onLoad={() => setLoaded(true)}
        className={loaded ? 'loaded' : 'loading'}
        loading="lazy"
      />
    </div>
  );
}

// CSS
.image-wrapper img.loading {
  filter: blur(10px);
  transition: filter 0.3s;
}
.image-wrapper img.loaded {
  filter: blur(0);
}
```

### Carrusel de Imágenes
```tsx
function ImageCarousel({ imagenes, propiedad }) {
  const [current, setCurrent] = useState(0);

  return (
    <div className="carousel">
      <img 
        src={getImageUrl(imagenes[current].url)} 
        alt={`${propiedad} - ${imagenes[current].tipo}`}
      />
      
      <div className="controls">
        <button onClick={() => setCurrent(Math.max(0, current - 1))}>
          ← Anterior
        </button>
        <span>{current + 1} / {imagenes.length}</span>
        <button onClick={() => setCurrent(Math.min(imagenes.length - 1, current + 1))}>
          Siguiente →
        </button>
      </div>
      
      {/* Thumbnails */}
      <div className="thumbnails">
        {imagenes.map((img, idx) => (
          <img
            key={idx}
            src={getImageUrl(img.url)}
            onClick={() => setCurrent(idx)}
            className={idx === current ? 'active' : ''}
          />
        ))}
      </div>
    </div>
  );
}
```

---

## 🔒 Autenticación (Endpoints Protegidos)

```typescript
const API_KEY = 'demo-key';

async function fetchProtected(endpoint: string, options: RequestInit = {}) {
  const response = await fetch(`http://localhost:5000${endpoint}`, {
    ...options,
    headers: {
      ...options.headers,
      'Authorization': `Bearer ${API_KEY}`
    }
  });
  
  return response.json();
}

// Uso:
const metrics = await fetchProtected('/metrics/interactions');
console.log(metrics);
```

---

## 📊 Paginación con Cursor

```typescript
async function loadMoreProperties(afterId: number = 0) {
  const response = await fetch(
    `http://localhost:5000/properties?limit=20&afterId=${afterId}`
  );
  const data = await response.json();
  
  return {
    properties: data.data,
    nextCursor: data.cursor  // Usar esto para la siguiente página
  };
}

// Implementación scroll infinito:
function PropertyListInfinite() {
  const [properties, setProperties] = useState([]);
  const [cursor, setCursor] = useState(0);
  const [hasMore, setHasMore] = useState(true);

  async function loadMore() {
    const { properties: newProps, nextCursor } = await loadMoreProperties(cursor);
    
    setProperties(prev => [...prev, ...newProps]);
    setCursor(nextCursor);
    
    if (newProps.length === 0) {
      setHasMore(false);
    }
  }

  return (
    <div>
      {properties.map(prop => <PropertyCard key={prop.id} {...prop} />)}
      {hasMore && <button onClick={loadMore}>Cargar más</button>}
    </div>
  );
}
```

---

## 🚀 Tips de Performance

1. **Cache del navegador**: El backend envía headers `ETag` y `Cache-Control`. Aprovéchalos con `fetch`:
```typescript
fetch(url, { cache: 'default' })  // Respeta Cache-Control
```

2. **Debounce en búsquedas NLQ**:
```typescript
import { debounce } from 'lodash';

const debouncedSearch = debounce(async (query) => {
  const result = await askNLQ(query);
  setResults(result);
}, 500);
```

3. **Rate limit handling**:
```typescript
async function askNLQWithRetry(query: string, retries = 3) {
  try {
    const response = await fetch('http://localhost:5000/api/nlq', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ query })
    });
    
    if (response.status === 503) {
      // Rate limit exceeded
      if (retries > 0) {
        await new Promise(r => setTimeout(r, 2000)); // Esperar 2s
        return askNLQWithRetry(query, retries - 1);
      }
      throw new Error('Rate limit exceeded');
    }
    
    return response.json();
  } catch (error) {
    console.error('Error:', error);
    throw error;
  }
}
```

---

## 📝 Resumen

### URLs de Imágenes
- **API retorna**: `/mvp-props/images/casa-modelo-1.jpg` (relativa)
- **Frontend debe usar**: `http://localhost:5002/mvp-props/images/casa-modelo-1.jpg` (absoluta)

### Métodos de Integración
1. ✅ **Proxy en dev server** (Vite/Next.js) - Recomendado
2. ✅ **Transformar URLs en cliente** - Simple y directo
3. ✅ **Backend como proxy** - Requiere implementación

### Endpoints Clave
- `GET /properties` - Listado con imágenes
- `POST /api/nlq` - Búsqueda en lenguaje natural
- `POST /interactions` - Interacciones LLM (protegido)
- `GET /metrics/interactions` - Métricas (protegido)

---

**¿Necesitas ayuda con algún framework específico? ¡Pregunta!** 🚀
