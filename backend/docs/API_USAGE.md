# Guía de Uso de API - MVP Props

## 🚀 Endpoints Rápidos (Recomendados)

### **Para Landing y Paginación**

**Endpoint:** `http://localhost:5002/api/propiedades/miraiz`

**Latencia:** ~30-50ms ⚡

---

## 📋 Uso Básico

### 1. Carga Inicial del Landing
```http
GET http://localhost:5002/api/propiedades/miraiz?limit=12&estado=disponible
```

**Response:**
```json
{
  "success": true,
  "data": [
    {
      "id": 1,
      "propiedad": "A-1",
      "precio": 458299,
      "area": 185,
      "tipo": "Lote",
      "estado": "disponible",
      "baños": 2,
      "año": 2024,
      "imagenes": [
        {
          "tipo": "promocional1",
          "url": "/mvp-props/images/casa-modelo-1.jpg",
          "formato": "imagen"
        }
      ]
    }
  ],
  "cursor": 12
}
```

---

### 2. Paginación (Client-Side)

⚠️ **El API retorna TODAS las propiedades (100) de una vez**

**Estrategia recomendada:**
1. Cargar todas las propiedades una sola vez
2. Paginar en el cliente usando `.slice()`

```javascript
// Carga inicial (obtener todo)
const response = await fetch('http://localhost:5002/api/propiedades/miraiz');
const allProperties = response.data;  // 100 propiedades

// Paginar en cliente
const itemsPerPage = 12;
const page = 1;
const startIndex = (page - 1) * itemsPerPage;
const endIndex = startIndex + itemsPerPage;
const currentPage = allProperties.slice(startIndex, endIndex);
```

---

### 3. Filtros Adicionales
```http
GET http://localhost:5002/api/propiedades/miraiz?limit=20&estado=disponible
```

**Query Params Disponibles:**
- `limit` - Cantidad de propiedades (max: 100)
- `afterId` - Cursor para paginación
- `estado` - Filtrar por estado

---

## 🖼️ Imágenes

**URLs retornadas:** Relativas
```
/mvp-props/images/casa-modelo-1.jpg
```

**URL completa para frontend:**
```javascript
const IMAGE_BASE = 'http://localhost:5002';
const fullUrl = `${IMAGE_BASE}${propiedad.imagenes[0].url}`;
```

**Ejemplo:**
```html
<img src="http://localhost:5002/mvp-props/images/casa-modelo-1.jpg" alt="Propiedad" />
```

---

## 💻 Ejemplo Completo (React)

```typescript
const API_BASE = 'http://localhost:5002';
const IMAGE_BASE = 'http://localhost:5002';
const ITEMS_PER_PAGE = 12;

function PropertyLanding() {
  const [allProperties, setAllProperties] = useState([]);
  const [currentPage, setCurrentPage] = useState(1);
  const [loading, setLoading] = useState(true);

  // Cargar TODAS las propiedades una sola vez
  useEffect(() => {
    loadAllProperties();
  }, []);

  async function loadAllProperties() {
    setLoading(true);
    const response = await fetch(`${API_BASE}/api/propiedades/miraiz`);
    const data = await response.json();
    
    if (data.success) {
      setAllProperties(data.data);
    }
    setLoading(false);
  }

  // Calcular propiedades de la página actual
  const startIndex = (currentPage - 1) * ITEMS_PER_PAGE;
  const endIndex = startIndex + ITEMS_PER_PAGE;
  const currentProperties = allProperties.slice(startIndex, endIndex);
  const totalPages = Math.ceil(allProperties.length / ITEMS_PER_PAGE);

  return (
    <div className="landing">
      <h1>Propiedades Disponibles ({allProperties.length})</h1>
      
      {loading ? (
        <p>Cargando...</p>
      ) : (
        <>
          {/* Grid de propiedades */}
          <div className="grid grid-cols-4 gap-6">
            {currentProperties.map(property => (
              <PropertyCard 
                key={property.id} 
                property={property}
                imageBase={IMAGE_BASE}
              />
            ))}
          </div>

          {/* Paginación */}
          <div className="pagination">
            <button 
              onClick={() => setCurrentPage(p => Math.max(1, p - 1))}
              disabled={currentPage === 1}
            >
              ← Anterior
            </button>
            
            <span>Página {currentPage} de {totalPages}</span>
            
            <button 
              onClick={() => setCurrentPage(p => Math.min(totalPages, p + 1))}
              disabled={currentPage === totalPages}
            >
              Siguiente →
            </button>
          </div>
        </>
      )}
    </div>
  );
}

function PropertyCard({ property, imageBase }) {
  const imageUrl = property.imagenes?.[0]?.url 
    ? `${imageBase}${property.imagenes[0].url}`
    : '/placeholder.jpg';

  return (
    <div className="property-card">
      <img 
        src={imageUrl} 
        alt={property.propiedad}
        loading="lazy"
      />
      <h3>{property.propiedad}</h3>
      <p>${property.precio.toLocaleString()}</p>
    </div>
  );
}
```

---

## 🤖 Búsqueda en Lenguaje Natural (Opcional)

**Endpoint:** `http://localhost:5000/api/nlq`

**Método:** POST

**Latencia:** 
- Mock: ~15-40ms ⚡
- OpenAI: ~4-10 segundos

**Request:**
```json
{
  "query": "propiedades disponibles con imagen y precio",
  "limit": 10
}
```

**⚠️ Importante:** El campo es `query` (no `pregunta`)

**Ejemplo PowerShell:**
```powershell
# Desde PowerShell (sintaxis con variable)
$body = @{ query = "lotes disponibles con precio menor a 300000"; limit = 5 } | ConvertTo-Json
Invoke-RestMethod -Uri "http://localhost:5000/api/nlq" -Method POST -ContentType "application/json" -Body $body

# One-liner (funciona desde PowerShell y cmd.exe)
Invoke-RestMethod -Uri 'http://localhost:5000/api/nlq' -Method POST -ContentType 'application/json' -Body '{"query":"terreno en la antigua","limit":12}' | ConvertTo-Json -Depth 5
```

**Desde cmd.exe:**
```cmd
powershell -Command "Invoke-RestMethod -Uri 'http://localhost:5000/api/nlq' -Method POST -ContentType 'application/json' -Body '{\"query\":\"terreno en la antigua\",\"limit\":12}' | ConvertTo-Json -Depth 5"
```

**Response:**
```json
{
  "success": true,
  "answer": "Aquí tienes 5 propiedades disponibles...",
  "toolPayload": {
    "success": true,
    "data": [ /* propiedades */ ]
  },
  "latency_ms": 42,
  "trace_id": "abc123"
}
```

**Usar para:**
- Chat conversacional
- Búsqueda en lenguaje natural
- Asistente virtual

**NO usar para:**
- Listados normales (muy lento)
- Paginación (innecesario)

---

## ⚡ Resumen de Decisiones

| Caso de Uso | Endpoint | Estrategia | Latencia |
|-------------|----------|------------|----------|
| **Landing inicial** | `localhost:5002/miraiz` | Cargar todo (100 props) | ~50ms |
| **Paginación** | N/A | Client-side con `.slice()` | 0ms (instantáneo) |
| **Filtros** | `localhost:5002/miraiz?estado=disponible` | Filtrar en cliente | 0ms |
| **Chat/NLQ** | `localhost:5000/api/nlq` (POST) | Server-side | ~40ms (Mock) |

**Nota:** El API retorna las 100 propiedades de una vez. No hay paginación en el servidor.

---

## 🔧 Configuración

**Producción:**
```javascript
const API_BASE = 'https://api.tudominio.com';
const IMAGE_BASE = 'https://cdn.tudominio.com';
```

**Desarrollo:**
```javascript
const API_BASE = 'http://localhost:5002';
const IMAGE_BASE = 'http://localhost:5002';
```

---

## 📊 Campos Disponibles

```typescript
interface Property {
  id: number;
  propiedad: string;      // "A-1", "B-23"
  precio: number;         // 458299
  area: number;           // 185 (m²)
  tipo: string;           // "Lote", "Casa", "Departamento"
  clase_tipo: string;     // "Esquina", "Intermedio"
  modelo: string;         // "M-1", "M-2"
  estado: string;         // "disponible", "vendido", "apartado"
  baños: number;          // 2
  año: number;            // 2024
  imagenes: Array<{
    tipo: string;         // "promocional1"
    url: string;          // "/mvp-props/images/..."
    formato: string;      // "imagen"
  }>;
}
```

---

## ✅ Checklist de Implementación

- [ ] Usar `localhost:5002/miraiz` para carga inicial
- [ ] Usar `afterId` para paginación
- [ ] Agregar `IMAGE_BASE` a URLs de imágenes
- [ ] Implementar `loading="lazy"` en imágenes
- [ ] Manejar caso cuando `data.length === 0` (sin más resultados)
- [ ] Mostrar indicador de carga al paginar
- [ ] (Opcional) Agregar chat con `/api/nlq`

---

**¿Dudas?** Revisa `FRONTEND_INTEGRATION.md` para ejemplos completos.
