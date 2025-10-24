# MVP Props Frontend

> Frontend Vue 3 + Vite para sistema de gestión de propiedades inmobiliarias

## 🏗️ Arquitectura del Proyecto

### Tecnologías Base
- **Vue 3** con Composition API
- **Vite** como bundler y servidor de desarrollo  
- **Vue Router** para navegación SPA
- **CSS Variables** para tema oscuro

### Estructura de Directorios
```
src/
├── api.js              # Capa de datos con timeout y autenticación
├── router.js           # Configuración de rutas
├── style.css           # Tema oscuro global con variables CSS
├── App.vue            # Componente raíz
├── main.js            # Punto de entrada
├── components/        # Componentes reutilizables
│   ├── HeroSearch.vue     # Formulario de búsqueda
│   ├── PropertyCard.vue   # Tarjeta de propiedad
│   └── ImageCarousel.vue  # Carrusel de imágenes
├── utils/            # Funciones utilitarias
│   └── debounce.js       # Función de debounce (250ms)
└── views/            # Páginas/vistas principales
    ├── Home.vue          # Página inicial con grid de propiedades
    ├── Detail.vue        # Detalle de propiedad
    ├── Interactions.vue  # Lista de interacciones del usuario
    └── Metrics.vue       # Dashboard de métricas
```

## 🎯 Principios de Diseño

### Clean Architecture
- **SRP (Single Responsibility Principle)**: Cada componente tiene una responsabilidad específica
- **OCP (Open/Closed Principle)**: Componentes extensibles vía props
- **KISS (Keep It Simple)**: Mínimas dependencias, configuración simple

### Separación de Responsabilidades
- **Presentación**: `components/` y `views/`
- **Lógica de Negocio**: `utils/`
- **Acceso a Datos**: `api.js`

## 🔌 Capa de Datos (API)

### Configuración
```javascript
// api.js - Función central con timeout y auth
async function j(url, opts = {}, ms = 5000) {
  // AbortController con timeout de 5s
  // Headers de auth solo para endpoints protegidos
}
```

### Endpoints
| Endpoint | Método | Autenticación | Descripción |
|----------|--------|---------------|-------------|
| `/properties` | GET | ❌ Público | Catálogo de propiedades |
| `/interactions` | POST | ✅ API Key | Crear interacción |
| `/interactions?userId=` | GET | ✅ API Key | Listar interacciones |
| `/metrics/interactions` | GET | ✅ API Key | Métricas del sistema |

### Seguridad
- API Key en `.env` como `VITE_BACK_API_KEY`
- Headers `Authorization: Bearer` **solo** para endpoints protegidos
- Validación de entrada en formularios

## 🎨 Componentes

### HeroSearch.vue
- Formulario de búsqueda principal
- Filtros: ubicación, tipo, precio máximo
- Emite eventos de búsqueda con debounce

### PropertyCard.vue
- Tarjeta reutilizable de propiedad
- Imagen con fallback, precio, especificaciones
- Link a vista de detalle

### ImageCarousel.vue
- Carrusel accesible con navegación por teclado
- Soporte para swipe en móviles
- Lazy loading de imágenes
- Indicadores (dots) y botones prev/next

## 📱 Vistas (Páginas)

### Home.vue
- Hero section con búsqueda
- Grid responsivo de propiedades
- Filtrado en tiempo real (cliente)

### Detail.vue
- Carrusel de imágenes
- Ficha técnica de la propiedad
- CTA de WhatsApp
- Formulario de preguntas

### Interactions.vue
- Lista de interacciones del usuario
- Estados: pendiente, respondida, cancelada
- Timestamps formateados

### Metrics.vue
- Dashboard con métricas visuales
- Tarjetas de conteo por estado
- Barras de porcentaje
- Navegación a interacciones

## ⚡ Performance

### Optimizaciones Implementadas
- **Debounce**: 250ms para búsquedas en tiempo real
- **Lazy Loading**: Imágenes en carruseles y tarjetas
- **Client-side Filtering**: Sin llamadas al servidor para filtros
- **Timeout**: 5s máximo para todas las llamadas API
- **Responsive**: CSS Grid adaptativo

### Mapeo de Datos
```javascript
// Manejo de campos con tildes y variaciones
area: property.m2construccion ?? property.area
bathrooms: property['baños'] ?? property.banos
images: property.imagenes.map(img => img.url)
```

## 🎨 Tema y Estilos

### Variables CSS
```css
:root {
  --bg: #1a1a1a;        /* Fondo principal */
  --card: #2a2a2a;      /* Fondo de tarjetas */
  --text: #fff;         /* Texto principal */
  --accent: #2563eb;    /* Color de acento */
  --muted: #666;        /* Texto secundario */
  --border: #404040;    /* Bordes */
}
```

### Características UI
- **Tema oscuro** por defecto
- **Responsive design** mobile-first
- **Transiciones suaves** en hover/focus
- **Feedback visual** para estados de carga

## 🚀 Desarrollo

### Comandos
```bash
# Instalación
npm i

# Configuración
cp .env.example .env

# Desarrollo
npm run dev

# Build de producción
npm run build

# Preview del build
npm run preview
```

### Variables de Entorno
```env
VITE_API_BASE=http://localhost:5000
VITE_BACK_API_KEY=demo-key
```

## 🔗 Flujo de Datos

### Ciclo de Vida Típico
1. **Carga inicial**: `Home.vue` → `api.getProperties()` → renderizado
2. **Búsqueda**: Usuario filtra → debounce → filtrado local → actualización UI
3. **Detalle**: Click propiedad → `Detail.vue` → carga datos específicos
4. **Interacción**: Usuario pregunta → `api.postInteraction()` → respuesta mostrada
5. **Métricas**: Admin ve dashboard → `api.getMetrics()` → visualización

### Estado de la Aplicación
- **Local**: Cada vista maneja su propio estado
- **Props**: Comunicación parent → child
- **Events**: Comunicación child → parent
- **Router**: Navegación y parámetros de URL

## 📋 Convenciones

### Nomenclatura
- **Componentes**: PascalCase (`PropertyCard.vue`)
- **Vistas**: PascalCase (`Detail.vue`)
- **Utils**: camelCase (`debounce.js`)
- **Estilos**: kebab-case para clases CSS

### Estructura de Componentes
```vue
<template>
  <!-- Template aquí -->
</template>

<script>
export default {
  name: 'ComponentName',
  props: { /* props */ },
  setup(props) {
    // Composition API
    return {}
  }
}
</script>

<style scoped>
/* Estilos del componente */
</style>
```

## 🎯 Roadmap

### Próximas Mejoras
- [ ] Skeleton loaders para mejor UX
- [ ] Infinite scroll en listados
- [ ] PWA capabilities
- [ ] Tests unitarios con Vitest
- [ ] Optimización de bundle
- [ ] Modo offline básico

---

**Desarrollado con ❤️ usando Vue 3 + Vite**