<!-- src/views/Home.vue -->
<template>
  <section class="home">
    <header class="hero">
      <h1>Propiedades</h1>
      <p class="hint">{{ nlqAnswer || 'Catálogo de propiedades disponibles' }}</p>
    </header>

    <!-- Buscador con filtro 'Tipo Propiedad' que se rellena con la voz -->
    <HeroSearch 
      :dictated-location="dictatedLocation"
      @search="handleFormSearch"
    />

    <div v-if="loading" class="loading">
      <div class="spinner"></div>
      <p>Cargando propiedades...</p>
    </div>
    
    <div v-else-if="error" class="error">
      No se pudo cargar el catálogo.
      <br>
      Error: {{ error }}
      <br>
      <button @click="retry" class="retry-btn">Reintentar</button>
    </div>
    
    <div v-else-if="items.length === 0" class="empty">
      <h2>No hay propiedades disponibles</h2>
      <p>Intenta recargar la página o contacta al administrador.</p>
    </div>
    
    <div v-else>
      <div class="stats">
        <p>
          {{ items.length }} propiedades en página {{ currentPage }}
          <span v-if="totalPages > 0">de {{ totalPages }}</span>
          · <strong>Total:</strong> {{ totalItems }}
        </p>
        <p v-if="hasMore" class="page-info">Hay más propiedades disponibles</p>
  <p v-if="latency" class="latency">Cargadas en {{ latency }}ms</p>
        <div class="agent-controls">
          <button 
            class="agent-btn" 
            :class="{ active: agentMode }"
            @click="toggleVoiceAgent"
            :aria-pressed="agentMode"
          >
            {{ agentMode ? (isListening ? 'Grabando (toca para detener)' : 'Agente activo') : 'Activa agente' }}
          </button>
          <span v-if="voiceStatus" class="agent-status">{{ voiceStatus }}</span>
        </div>
      </div>
      
      <div class="grid">
        <PropertyCard 
          v-for="(p, index) in items" 
          :key="p.id || index" 
          :item="p" 
          :priority="index < 6"
        />
      </div>
      
      <!-- Paginación numérica (soporta cursor y fallback cliente) -->
      <nav class="pagination-numeric" v-if="loadedPages > 0">
        <button 
          class="page-btn"
          :disabled="currentPage === 1"
          @click="handlePageChange(currentPage - 1)"
          aria-label="Página anterior"
        >
          ‹
        </button>

        <button
          v-for="p in loadedPages"
          :key="p"
          class="page-number"
          :class="{ active: p === currentPage }"
          @click="handlePageChange(p)"
        >
          {{ p }}
        </button>

        <button 
          class="page-btn"
          :disabled="!hasMore && currentPage >= loadedPages"
          @click="handlePageChange(currentPage + 1)"
          aria-label="Página siguiente"
        >
          ›
        </button>
      </nav>
    </div>
    
    <!-- Debug info (removible en producción) -->
    <details class="debug" v-if="showDebug">
      <summary>Debug Info</summary>
      <div class="debug-content">
        <p><strong>Loading:</strong> {{ loading }}</p>
        <p><strong>Error:</strong> {{ error || 'None' }}</p>
        <p><strong>Items count:</strong> {{ items.length }}</p>
        <p><strong>API Base:</strong> {{ apiBase }}</p>
        <p><strong>NLQ Answer:</strong> {{ nlqAnswer }}</p>
        <p><strong>Latency:</strong> {{ latency }}ms</p>
        <p><strong>Sample item:</strong></p>
        <pre v-if="items[0]">{{ JSON.stringify(items[0], null, 2) }}</pre>
      </div>
    </details>
  </section>
</template>

<script setup>
import { ref, onMounted } from 'vue';
import { getProperties, getAllProperties, askNLQ } from '../api';
import HeroSearch from '../components/HeroSearch.vue';
import PropertyCard from '../components/PropertyCard.vue';
import { preloadImage, getImageUrl } from '../utils/images';
import { generateOptimizedUrl } from '../utils/imageOptimization';
import { useNetworkQuality } from '../composables/useNetworkQuality';

const items = ref([]);
const itemsByPage = ref({}); // cache de items por página
const allItemsBuffer = ref(null); // fallback cuando el backend devuelve bloque grande
const loading = ref(true);
const error = ref(null);
const apiBase = ref(import.meta.env.VITE_API_BASE || 'http://localhost:5000');
const nlqAnswer = ref('');
const latency = ref(null);
const showDebug = ref(import.meta.env.DEV); // Solo en desarrollo

// Paginación cursor-based
const currentPage = ref(1);
const itemsPerPage = ref(25);
const totalItems = ref(0);
const totalPages = ref(1);
const cursors = ref([]); // Almacenar cursor devuelto por cada página (para cargar la siguiente)
const hasMore = ref(true);
const loadedPages = ref(0); // número de páginas actualmente disponibles (cacheadas)

// Network quality detection
const { qualityLevel, getPreloadCount, getImageConfig } = useNetworkQuality();

// Estado para agente de voz / NLQ
const isListening = ref(false);
const voiceStatus = ref('');
let recognition = null;
const dictatedLocation = ref('');
const agentMode = ref(false); // cuando está activo, el botón Buscar usa NLQ

// Carga TODO el catálogo y pagina en cliente
const loadAllData = async () => {
  try {
    loading.value = true;
    error.value = null;
  console.log('Cargando catálogo completo para paginar en cliente…');

    const start = Date.now();
    // Lote más grande para minimizar viajes (ajustable)
    const r = await getAllProperties(500);
    latency.value = Date.now() - start;

    if (!r?.success) throw new Error('Error en la respuesta del servidor');

    const all = Array.isArray(r.data) ? r.data : [];
    // Paginar del lado del cliente estrictamente a 12 por página
    allItemsBuffer.value = all;
    const total = all.length;
    totalItems.value = total;
    totalPages.value = Math.max(1, Math.ceil(total / itemsPerPage.value));
    loadedPages.value = totalPages.value;
    hasMore.value = false;

    itemsByPage.value = {};
    for (let p = 1; p <= totalPages.value; p++) {
      const startIdx = (p - 1) * itemsPerPage.value;
      const endIdx = startIdx + itemsPerPage.value;
      itemsByPage.value[p] = all.slice(startIdx, endIdx);
    }

    currentPage.value = 1;
    items.value = itemsByPage.value[1] || [];
  // Precargar imágenes del primer page para respuesta inmediata
  preloadFirstImages();
  // Y precargar imágenes principales del resto para que la paginación sea instantánea
  preloadAllImages();

  console.log(`Catálogo completo cargado (${total} items) en ${latency.value}ms`);
  } catch (e) {
  console.error('Error cargando catálogo completo:', e);
    error.value = e.message;
  } finally {
    loading.value = false;
  }
};

// Modo cursor (queda disponible si se necesitara en el futuro)
const loadData = async (page = 1) => {
  try {
    loading.value = true;
    error.value = null;
  console.log(`Cargando página ${page} con endpoint ultra-rápido`);
    
    // Determinar cursor para esta página
    let afterId = null;
    if (page > 1 && cursors.value[page - 2]) {
      afterId = cursors.value[page - 2];
    }
    
    const start = Date.now();
    const r = await getProperties(page, itemsPerPage.value, afterId);
    latency.value = Date.now() - start;
    
  console.log('Respuesta ultra-rápida procesada:', r);
    
    if (!r.success) {
      throw new Error('Error en la respuesta del servidor');
    }
    
    nlqAnswer.value = r?.nlqAnswer ?? '';

    // 1) Modo normal (cursor presente): servidor devuelve páginas
    if (r.pagination?.cursor !== undefined) {
      // Enforzar 12 por página aunque el backend envíe más por cualquier motivo
      const pageItems = (r?.data ?? []).slice(0, itemsPerPage.value);
      itemsByPage.value[page] = pageItems;
      items.value = pageItems;
      currentPage.value = page;

      // Guardar cursor para pedir la siguiente página
      if (r.pagination.cursor && page === cursors.value.length + 1) {
        cursors.value.push(r.pagination.cursor);
      }

      // Estado de navegación
      hasMore.value = !!r.pagination.hasNext;
      loadedPages.value = Math.max(loadedPages.value, page);

      // Estimaciones (opcionales)
      itemsPerPage.value = r.pagination.limit || itemsPerPage.value;
      totalPages.value = hasMore.value ? Math.max(totalPages.value, page + 1) : Math.max(totalPages.value, page);
      totalItems.value = (page - 1) * itemsPerPage.value + pageItems.length + (hasMore.value ? 1 : 0);
      
    } else {
      // 2) Fallback (sin cursor): el backend devolvió un bloque grande; paginamos del lado cliente
      if (page === 1) {
        allItemsBuffer.value = r?.data ?? [];
        const total = allItemsBuffer.value.length;
        totalPages.value = Math.max(1, Math.ceil(total / itemsPerPage.value));
        loadedPages.value = totalPages.value;
        hasMore.value = false; // no hay más de lo que ya llegó

        // Crear cache por página
        for (let p = 1; p <= totalPages.value; p++) {
          const startIdx = (p - 1) * itemsPerPage.value;
          const endIdx = startIdx + itemsPerPage.value;
          itemsByPage.value[p] = allItemsBuffer.value.slice(startIdx, endIdx);
        }
      }
      // Mostrar la página solicitada desde cache
      items.value = itemsByPage.value[page] || [];
      currentPage.value = page;
      totalItems.value = allItemsBuffer.value ? allItemsBuffer.value.length : items.value.length;
    }

  console.log(`Página ${page} cargada en ${latency.value}ms: ${items.value.length} propiedades`);
    console.log(`📊 Páginas disponibles: ${loadedPages.value} | hasMore: ${hasMore.value}`);
    
    // Precargar imágenes solo de la página actual
    preloadFirstImages();
    
    // Scroll al inicio después de cambiar página (excepto primera carga)
    if (page > 1) {
      window.scrollTo({ top: 0, behavior: 'smooth' });
    }
    
  } catch (e) {
  console.error('Error cargando propiedades:', e);
    error.value = e.message;
  } finally {
    loading.value = false;
  }
};

const handlePageChange = (page) => {
  if (page === currentPage.value) return;
  // Con paginación en cliente, mostramos desde cache
  if (itemsByPage.value[page]) {
    currentPage.value = page;
    items.value = itemsByPage.value[page];
    preloadFirstImages();
    window.scrollTo({ top: 0, behavior: 'smooth' });
    return;
  }
  // Si no está cacheada pero hay más, pedir siguiente secuencialmente (cursor-based)
  if (page === loadedPages.value + 1 && hasMore.value) {
    loadData(page);
  }
};

const preloadFirstImages = () => {
  // Usar número de preload basado en calidad de conexión
  const preloadCount = getPreloadCount();
  const firstProperties = items.value.slice(0, preloadCount);
  
  console.log(`🌐 Conexión: ${qualityLevel.value} - Precargando ${preloadCount} imágenes`);
  
  firstProperties.forEach(property => {
    if (property.imagenes && property.imagenes.length > 0) {
      const firstImage = property.imagenes[0];
      if (firstImage?.url) {
        // Precargar la misma URL optimizada que usa OptimizedImage (para hit de cache real)
  const baseUrl = getImageUrl(firstImage.url);
  const optimized = generateOptimizedUrl(baseUrl, 800, 85);
        preloadImage(optimized).catch(() => {
          // Ignorar errores de precarga silenciosamente
        });
      }
    }
  });
};

// Precarga la imagen principal de todas las propiedades cargadas en memoria
const preloadAllImages = () => {
  const all = allItemsBuffer.value || [];
  all.forEach(property => {
    if (property.imagenes && property.imagenes.length > 0) {
      const firstImage = property.imagenes[0];
      if (firstImage?.url) {
        const baseUrl = getImageUrl(firstImage.url);
        const optimized = generateOptimizedUrl(baseUrl, 800, 85);
        preloadImage(optimized).catch(() => {});
      }
    }
  });
};

const retry = () => {
  loadData(currentPage.value);
};

// --- Agente de voz (Web Speech API) ---
function initRecognition() {
  const SpeechRecognition = window.SpeechRecognition || window.webkitSpeechRecognition;
  if (!SpeechRecognition) {
    voiceStatus.value = 'Voz no soportada en este navegador';
    return null;
  }
  const rec = new SpeechRecognition();
  rec.lang = 'es-MX';
  rec.continuous = false;
  rec.interimResults = true;
  rec.maxAlternatives = 1;
  rec.onstart = () => { voiceStatus.value = 'Escuchando…'; };
  rec.onerror = (e) => { voiceStatus.value = `Error de micrófono: ${e.error || 'desconocido'}`; };
  rec.onend = () => { isListening.value = false; };
  rec.onresult = async (event) => {
    const result = event.results[event.results.length - 1];
    const text = result[0]?.transcript?.trim();
    if (!text) return;
    if (result.isFinal) {
      voiceStatus.value = `Consulta: “${text}”`;
      await runNLQ(text);
    } else {
      voiceStatus.value = `Escuchando: ${text}`;
    }
  };
  return rec;
}

async function runNLQ(query) {
  try {
    loading.value = true;
    dictatedLocation.value = query; // Rellenar 'Ubicación' con lo dictado
  const r = await askNLQ(query);
    if (!r?.success) {
      const errMsg = (r?.error || '').toString();
      const isTimeout = errMsg.toLowerCase().includes('abort');
      nlqAnswer.value = isTimeout
        ? `La búsqueda tardó más de 5s y fue cancelada.`
        : `No se pudo completar la búsqueda. Reintenta en unos segundos.`;
      // Sin fallback: y además vaciamos la grilla para no mostrar datos previos
      allItemsBuffer.value = [];
      itemsByPage.value = { 1: [] };
      items.value = [];
      currentPage.value = 1;
      totalItems.value = 0;
      totalPages.value = 1;
      loadedPages.value = 1;
      hasMore.value = false;
      return;
    }
    nlqAnswer.value = r?.answer || `Resultados para: ${query}`;
    const raw = r?.toolPayload?.data || [];
    if (Array.isArray(raw) && raw.length) {
      // El backend puede ignorar limit/fields: aplicamos filtro/ranking y paginación en cliente
      const prepared = filterAndRankResults(raw, query);

      // Construir buffer completo y cache por página
      allItemsBuffer.value = prepared;
      totalItems.value = prepared.length;
      totalPages.value = Math.max(1, Math.ceil(prepared.length / itemsPerPage.value));
      loadedPages.value = totalPages.value; // todo el bloque está en cliente
      hasMore.value = false; // no hay más en servidor para esta consulta (gestiona cliente)

      itemsByPage.value = {};
      for (let p = 1; p <= totalPages.value; p++) {
        const startIdx = (p - 1) * itemsPerPage.value;
        const endIdx = startIdx + itemsPerPage.value;
        itemsByPage.value[p] = prepared.slice(startIdx, endIdx);
      }

      // Mostrar página 1
      currentPage.value = 1;
      items.value = itemsByPage.value[1] || [];
      preloadFirstImages();
    } else {
      // NLQ respondió 0 resultados: limpiar grilla y paginación
      allItemsBuffer.value = [];
      itemsByPage.value = { 1: [] };
      items.value = [];
      currentPage.value = 1;
      totalItems.value = 0;
      totalPages.value = 1;
      loadedPages.value = 1;
      hasMore.value = false;
    }
  } catch (e) {
    console.error('NLQ error:', e);
    voiceStatus.value = 'No se pudo completar la búsqueda';
  } finally {
    loading.value = false;
    // Desactivar el agente para que el botón muestre "Activa agente" tras la búsqueda
    try {
      if (recognition && isListening.value) {
        recognition.stop();
      }
    } catch (e) {
      // ignorar errores de parada
    }
    isListening.value = false;
    agentMode.value = false;
  }
}

// Filtrado y ranking simple para mejorar precisión local cuando el NLQ devuelve un bloque amplio
function normalize(str) {
  return (str || '').toString().toLowerCase();
}

function filterAndRankResults(list, query) {
  const q = normalize(query);
  const wantsAntigua = q.includes('antigua');
  const wantsTerreno = q.includes('terreno') || q.includes('lote');

  const scoreItem = (it) => {
    const ubic = normalize(it.ubicacion) + ' ' + normalize(it?.proyecto?.ubicacion);
    const tipo = normalize(it.tipo) + ' ' + normalize(it.clase_tipo);
    let s = 0;
    if (wantsAntigua && (ubic.includes('antigua guatemala') || ubic.includes(' la antigua') || ubic.includes('antigua'))) s += 5;
    if (wantsTerreno && (tipo.includes('terreno') || tipo.includes('lote'))) s += 5;
    // señales adicionales
    if (ubic.includes('sacatepéquez') || ubic.includes('sacatepequez')) s += 2;
    if (tipo.includes('zona urbana') && wantsTerreno) s -= 1; // ligero castigo si es muy genérico
    return s;
  };

  // Clasificar por puntaje y presencia de ambos términos si aplica
  const annotated = list.map(it => ({ it, s: scoreItem(it) }));
  const primary = annotated.filter(a => a.s >= (wantsAntigua || wantsTerreno ? 5 : 0));
  const secondary = annotated.filter(a => !primary.includes(a));

  primary.sort((a, b) => b.s - a.s);
  secondary.sort((a, b) => b.s - a.s);

  const merged = [...primary, ...secondary].map(a => a.it);

  // Si el usuario solicitó "antigua" y "terreno", fuerza un filtro mínimo para evitar ruido extremo
  if (wantsAntigua || wantsTerreno) {
    const hardFiltered = merged.filter(it => {
      const ubic = normalize(it.ubicacion) + ' ' + normalize(it?.proyecto?.ubicacion);
      const tipo = normalize(it.tipo) + ' ' + normalize(it.clase_tipo);
      const geoOk = wantsAntigua ? (ubic.includes('antigua guatemala') || ubic.includes(' la antigua') || ubic.includes('antigua') || ubic.includes('sacatepéquez') || ubic.includes('sacatepequez')) : true;
      const kindOk = wantsTerreno ? (tipo.includes('terreno') || tipo.includes('lote')) : true;
      return geoOk && kindOk;
    });
    // Si el filtro duro dejó muy pocos, caemos al ranking general
    if (hardFiltered.length >= 1) return hardFiltered;
  }

  return merged;
}

function toggleVoiceAgent() {
  if (!recognition) recognition = initRecognition();
  if (!recognition) return;
  // Alternar modo agente
  agentMode.value = !agentMode.value;
  if (agentMode.value) {
    // Activado: iniciar reconocimiento
    try {
      recognition.start();
      isListening.value = true;
    } catch (e) {
      console.warn('Recognition start issue:', e);
    }
  } else {
    // Desactivado: detener reconocimiento si estaba activo
    if (isListening.value) {
      recognition.stop();
      isListening.value = false;
      voiceStatus.value = 'Micrófono detenido';
    }
  }
}

// Búsqueda desde el formulario HeroSearch
async function handleFormSearch(filters) {
  const q = (filters.location || '').trim();
  if (!q) return;
  // El botón Buscar SIEMPRE consulta al endpoint NLQ (no hay filtrado local aquí)
  await runNLQ(q);
}

// (Sin fallback local por petición del usuario)

onMounted(() => {
  console.log('Home.vue montado - iniciando carga con paginación servidor (25 por página)');
  loadData(1);
});

// Cuando NLQ esté listo, mover filtros a HeroSearch.vue y llamar askNLQ(); si hay toolPayload.data, reemplazar items.
</script>

<style scoped>
.home { 
  padding: 16px; 
  color: var(--text); 
  max-width: 1200px;
  margin: 0 auto;
}

.hero { 
  margin-bottom: 24px; 
  text-align: center; 
}

.hero h1 {
  font-size: 2.5rem;
  margin-bottom: 8px;
  background: linear-gradient(135deg, var(--accent), #1d4ed8);
  -webkit-background-clip: text;
  -webkit-text-fill-color: transparent;
  background-clip: text;
}

.hint { 
  color: var(--muted); 
  font-size: 1.1rem;
  max-width: 600px;
  margin: 0 auto;
}

.loading {
  display: flex;
  flex-direction: column;
  align-items: center;
  padding: 40px;
  color: var(--muted);
}

.spinner {
  width: 40px;
  height: 40px;
  border: 3px solid var(--border);
  border-top: 3px solid var(--accent);
  border-radius: 50%;
  animation: spin 1s linear infinite;
  margin-bottom: 16px;
}

@keyframes spin {
  0% { transform: rotate(0deg); }
  100% { transform: rotate(360deg); }
}

.error { 
  color: #ff6b6b; 
  padding: 24px; 
  text-align: center;
  background: rgba(255, 107, 107, 0.1);
  border-radius: 12px;
  margin: 24px 0;
}

.retry-btn {
  margin-top: 16px;
  padding: 8px 16px;
  background: var(--accent);
  color: white;
  border: none;
  border-radius: 6px;
  cursor: pointer;
  font-size: 0.9rem;
}

.retry-btn:hover {
  opacity: 0.9;
}

.empty {
  text-align: center;
  padding: 40px;
  color: var(--muted);
}

.empty h2 {
  margin-bottom: 12px;
}

.stats {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 24px;
  padding: 12px;
  background: var(--card);
  border-radius: 8px;
  font-size: 0.9rem;
  flex-wrap: wrap;
  gap: 0.5rem;
}

.stats p {
  margin: 0;
  color: var(--muted);
}

.page-info {
  font-weight: 500;
  color: var(--accent) !important;
}

.latency {
  color: var(--muted);
  font-size: 0.8rem;
}

.agent-controls {
  margin-left: auto;
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.agent-btn {
  padding: 0.5rem 0.75rem;
  background: var(--card);
  border: 1px solid var(--border);
  color: var(--text);
  border-radius: 8px;
  cursor: pointer;
  font-weight: 600;
}
.agent-btn.active {
  border-color: #ef4444;
  color: #ef4444;
}
.agent-status { color: var(--muted); font-size: 0.85rem; }

.grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
  gap: 20px;
}

.debug {
  margin-top: 40px;
  padding: 16px;
  background: var(--card);
  border-radius: 8px;
  font-family: monospace;
  font-size: 12px;
}

.debug summary {
  cursor: pointer;
  color: var(--accent);
  margin-bottom: 12px;
}

.debug-content pre {
  background: rgba(0, 0, 0, 0.3);
  padding: 12px;
  border-radius: 4px;
  overflow-x: auto;
  font-size: 11px;
  margin-top: 8px;
}

@media (max-width: 768px) {
  .grid {
    grid-template-columns: 1fr;
  }
  
  .stats {
    flex-direction: column;
    gap: 8px;
  }
  
  .hero h1 {
    font-size: 2rem;
  }
  
  .pagination-numeric {
    gap: 0.25rem;
  }
  .page-number, .page-btn {
    min-width: 40px;
  }
}

/* Paginación numérica */
.pagination-numeric {
  display: flex;
  justify-content: center;
  align-items: center;
  gap: 0.5rem;
  margin: 2rem 0;
  flex-wrap: wrap;
}

.page-number, .page-btn {
  min-width: 36px;
  height: 36px;
  padding: 0 0.75rem;
  background: var(--card);
  border: 1px solid var(--border);
  color: var(--text);
  border-radius: 8px;
  cursor: pointer;
  font-weight: 500;
  transition: all 0.2s ease;
}

.page-number.active {
  background: var(--accent);
  color: white;
  border-color: var(--accent);
}

.page-number:hover, .page-btn:hover {
  background: var(--accent);
  color: white;
  border-color: var(--accent);
}

.page-btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}
</style>