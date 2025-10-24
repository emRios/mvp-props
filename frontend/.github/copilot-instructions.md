# Copilot Instructions for MVP Props Frontend

## Project Overview
Vue 3 + Vite frontend for property management MVP following Clean Architecture with SRP/OCP principles. Minimal dependencies, KISS approach.

## Architecture & Core Principles

### Directory Structure
```
src/
├── api.js              # Data layer with timeout, auth headers
├── router.js           # Vue Router configuration
├── style.css           # Dark theme with CSS variables
├── components/         # Reusable UI components
├── utils/             # Pure functions (debounce, etc.)
└── views/             # Page-level components
```

### Key Patterns
- **SRP**: Separate concerns - views compose components, api.js handles data
- **OCP**: Components are extensible via props, api.js abstracts fetch logic
- **KISS**: No extra dependencies, minimal configuration

## Critical Conventions

### API Layer (`src/api.js`)
- Uses `AbortController` with 5s timeout for all requests
- Adds `Authorization: Bearer <VITE_BACK_API_KEY>` ONLY for `/interactions` and `/metrics` endpoints
- Exposes: `getProperties()`, `postInteraction()`, `getInteractions()`, `getMetrics()`

### Component Patterns
```vue
<!-- Standard component structure -->
<template>
  <div class="component-name">
    <!-- Template -->
  </div>
</template>

<script>
export default {
  name: 'ComponentName',
  props: {
    // Define props
  },
  setup(props) {
    // Composition API logic
    return {}
  }
}
</script>
```

### Data Mapping Specifics
- Images: Use `imagenes` array, extract `url` field
- Area: Use `m2construccion ?? area`
- Bathrooms: Handle accented `["baños"] ?? banos`

## Performance Requirements
- **Debounce**: 250ms for search inputs (use `src/utils/debounce.js`)
- **Lazy Loading**: Images in carousels and cards
- **Timeout**: 5s max for API calls
- **Client Filtering**: Search happens in browser, not server

## Security Model
- API key stored in `.env` as `VITE_BACK_API_KEY`
- Only sent to protected endpoints (`/interactions`, `/metrics`)
- Public endpoints (`/properties`) have no auth headers

## File Naming
- Components: PascalCase (`PropertyCard.vue`)
- Views: PascalCase (`Detail.vue`)
- Utils: camelCase (`debounce.js`)
- Use exact names from spec - no variations

## Environment Setup
```bash
# Required after generation
npm i
cp .env.example .env
npm run dev
```

## Critical Endpoints
- `GET /properties` - Public catalog
- `POST /interactions` - Protected interaction creation
- `GET /interactions?userId=u-demo` - Protected interaction list
- `GET /metrics/interactions` - Protected metrics

Base URL: `http://localhost:5000` (configurable via `VITE_API_BASE`)