<!-- src/components/PropertyCard.vue -->
<template>
  <article class="card">
    <div class="image-container">
      <OptimizedImage
        v-if="img"
        :src="img"
        :alt="item.titulo || item.propiedad"
        aspect-ratio="4/3"
        :priority="isPriority"
        :lazy-loading="!isPriority"
        sizes="(max-width: 768px) 100vw, (max-width: 1200px) 50vw, 33vw"
        :quality="85"
      />
      <div v-else class="no-image">
        🏠
      </div>
    </div>
    <div class="body">
      <h3 class="title">{{ item.titulo || item.propiedad }}</h3>
      <p class="meta">
        <span v-if="item.precio">Q {{ Number(item.precio).toLocaleString() }}</span>
        <span v-if="banos"> · {{ banos }} baños</span>
        <span v-if="item.habitaciones"> · {{ item.habitaciones }} hab</span>
        <span v-if="area"> · {{ area }} m²</span>
      </p>
      <p class="type" v-if="item.tipo">{{ item.tipo }} - {{ item.clase_tipo || 'Estándar' }}</p>
      <p class="loc" v-if="displayLoc">
        {{ displayLoc }}
      </p>
    </div>
  </article>
</template>

<script setup>
import { computed } from 'vue';
import { getImageUrl } from '../utils/images';
import { getOptimizedImageUrl, adaptToNetworkSpeed } from '../utils/imageOptimization';
import OptimizedImage from './OptimizedImage.vue';

const props = defineProps({ 
  item: { type: Object, required: true },
  priority: { type: Boolean, default: false } // Para las primeras cards
});

const banos = computed(() => props.item.baños ?? props.item.banos ?? null);

const area = computed(() => {
  // Priorizar m2construccion sobre area
  return props.item.m2construccion || props.item.area || null;
});

const img = computed(() => {
  const list = Array.isArray(props.item.imagenes) ? props.item.imagenes : [];
  const first = list.find(x => x?.url);
  if (!first) return '';
  
  const baseUrl = getImageUrl(first.url);
  // Aplicar optimizaciones para cards y adaptación de red
  return adaptToNetworkSpeed(getOptimizedImageUrl(baseUrl, 'card'));
});

const isPriority = computed(() => props.priority);

// Mostrar dirección antes que ubicación si existen
const displayLoc = computed(() => {
  const direccion = props.item?.proyecto?.direccion || props.item?.direccion || '';
  const ubic = props.item?.ubicacion || props.item?.proyecto?.ubicacion || '';
  if (direccion && ubic) return `${direccion}, ${ubic}`;
  return direccion || ubic || '';
});
</script>

<style scoped>
.card { 
  background: var(--card); 
  border: 1px solid var(--border); 
  border-radius: 12px; 
  overflow: hidden;
  transition: transform 0.2s, box-shadow 0.2s;
}

.card:hover {
  transform: translateY(-2px);
  box-shadow: 0 8px 25px rgba(0, 0, 0, 0.15);
}

.image-container {
  position: relative;
  height: 180px;
  overflow: hidden;
  border-radius: 12px 12px 0 0;
}

.no-image {
  width: 100%;
  height: 100%;
  display: flex;
  align-items: center;
  justify-content: center;
  background: var(--muted);
  font-size: 2rem;
  color: var(--text);
}

.body { 
  padding: 12px; 
  color: var(--text); 
}

.title { 
  margin: 0 0 6px; 
  font-size: 1rem; 
  font-weight: 600;
}

.meta { 
  color: var(--muted); 
  font-size: .9rem; 
  margin-bottom: 8px;
}

.type {
  color: var(--accent);
  font-size: 0.85rem;
  font-weight: 500;
  margin-bottom: 4px;
}

.loc { 
  color: var(--muted); 
  font-size: .85rem; 
  margin: 0;
}
</style>