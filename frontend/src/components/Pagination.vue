<!-- src/components/Pagination.vue -->
<template>
  <nav class="pagination" v-if="totalPages > 1" aria-label="Paginación">
    <!-- Botón anterior -->
    <button 
      class="pagination-btn prev-btn"
      :disabled="currentPage === 1"
      @click="goToPage(currentPage - 1)"
      aria-label="Página anterior"
    >
      <svg viewBox="0 0 24 24" class="pagination-icon">
        <path d="M15.41 7.41L14 6l-6 6 6 6 1.41-1.41L10.83 12z"/>
      </svg>
    </button>

    <!-- Páginas -->
    <div class="pagination-pages">
      <!-- Primera página si no está visible -->
      <button
        v-if="showFirstPage"
        class="pagination-number"
        :class="{ active: 1 === currentPage }"
        @click="goToPage(1)"
      >
        1
      </button>
      
      <!-- Separador inicial -->
      <span v-if="showStartEllipsis" class="pagination-ellipsis">...</span>
      
      <!-- Páginas visibles -->
      <button
        v-for="page in visiblePages"
        :key="page"
        class="pagination-number"
        :class="{ active: page === currentPage }"
        @click="goToPage(page)"
      >
        {{ page }}
      </button>
      
      <!-- Separador final -->
      <span v-if="showEndEllipsis" class="pagination-ellipsis">...</span>
      
      <!-- Última página si no está visible -->
      <button
        v-if="showLastPage"
        class="pagination-number"
        :class="{ active: totalPages === currentPage }"
        @click="goToPage(totalPages)"
      >
        {{ totalPages }}
      </button>
    </div>

    <!-- Botón siguiente -->
    <button 
      class="pagination-btn next-btn"
      :disabled="currentPage === totalPages"
      @click="goToPage(currentPage + 1)"
      aria-label="Página siguiente"
    >
      <svg viewBox="0 0 24 24" class="pagination-icon">
        <path d="M10 6L8.59 7.41 13.17 12l-4.58 4.59L10 18l6-6z"/>
      </svg>
    </button>
  </nav>
  
  <!-- Información de paginación -->
  <div class="pagination-info" v-if="totalItems">
    <p class="pagination-summary">
      Mostrando {{ startItem }} - {{ endItem }} de {{ totalItems }} propiedades
    </p>
  </div>
</template>

<script setup>
import { computed } from 'vue';

const props = defineProps({
  currentPage: { type: Number, required: true },
  totalPages: { type: Number, required: true },
  totalItems: { type: Number, default: 0 },
  itemsPerPage: { type: Number, default: 12 },
  maxVisiblePages: { type: Number, default: 5 }
});

const emit = defineEmits(['page-changed']);

// Páginas visibles en el centro
const visiblePages = computed(() => {
  const { currentPage, totalPages, maxVisiblePages } = props;
  const pages = [];
  
  let start = Math.max(1, currentPage - Math.floor(maxVisiblePages / 2));
  let end = Math.min(totalPages, start + maxVisiblePages - 1);
  
  // Ajustar el inicio si no tenemos suficientes páginas al final
  if (end - start + 1 < maxVisiblePages) {
    start = Math.max(1, end - maxVisiblePages + 1);
  }
  
  for (let i = start; i <= end; i++) {
    pages.push(i);
  }
  
  return pages;
});

// ¿Mostrar primera página separada?
const showFirstPage = computed(() => {
  return props.totalPages > props.maxVisiblePages && visiblePages.value[0] > 1;
});

// ¿Mostrar última página separada?
const showLastPage = computed(() => {
  const lastVisible = visiblePages.value[visiblePages.value.length - 1];
  return props.totalPages > props.maxVisiblePages && lastVisible < props.totalPages;
});

// ¿Mostrar puntos suspensivos al inicio?
const showStartEllipsis = computed(() => {
  return showFirstPage.value && visiblePages.value[0] > 2;
});

// ¿Mostrar puntos suspensivos al final?
const showEndEllipsis = computed(() => {
  const lastVisible = visiblePages.value[visiblePages.value.length - 1];
  return showLastPage.value && lastVisible < props.totalPages - 1;
});

// Cálculos para información de elementos
const startItem = computed(() => {
  return (props.currentPage - 1) * props.itemsPerPage + 1;
});

const endItem = computed(() => {
  return Math.min(props.currentPage * props.itemsPerPage, props.totalItems);
});

function goToPage(page) {
  if (page >= 1 && page <= props.totalPages && page !== props.currentPage) {
    emit('page-changed', page);
  }
}
</script>

<style scoped>
.pagination {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 0.5rem;
  margin: 2rem 0;
  padding: 1rem;
}

.pagination-btn {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 40px;
  height: 40px;
  border: 1px solid var(--border);
  background: var(--card);
  color: var(--text);
  border-radius: 8px;
  cursor: pointer;
  transition: all 0.2s ease;
}

.pagination-btn:hover:not(:disabled) {
  background: var(--accent);
  color: white;
  border-color: var(--accent);
}

.pagination-btn:disabled {
  opacity: 0.4;
  cursor: not-allowed;
}

.pagination-icon {
  width: 20px;
  height: 20px;
  fill: currentColor;
}

.pagination-pages {
  display: flex;
  align-items: center;
  gap: 0.25rem;
}

.pagination-number {
  min-width: 40px;
  height: 40px;
  padding: 0 0.75rem;
  border: 1px solid var(--border);
  background: var(--card);
  color: var(--text);
  border-radius: 8px;
  cursor: pointer;
  font-weight: 500;
  transition: all 0.2s ease;
  display: flex;
  align-items: center;
  justify-content: center;
}

.pagination-number:hover {
  background: var(--accent);
  color: white;
  border-color: var(--accent);
}

.pagination-number.active {
  background: var(--accent);
  color: white;
  border-color: var(--accent);
  font-weight: 600;
}

.pagination-ellipsis {
  padding: 0 0.5rem;
  color: var(--muted);
  font-weight: 500;
  display: flex;
  align-items: center;
  height: 40px;
}

.pagination-info {
  text-align: center;
  margin-top: 1rem;
}

.pagination-summary {
  color: var(--muted);
  font-size: 0.9rem;
  margin: 0;
}

/* Responsive design */
@media (max-width: 768px) {
  .pagination {
    gap: 0.25rem;
    padding: 0.5rem;
  }
  
  .pagination-btn,
  .pagination-number {
    width: 36px;
    height: 36px;
    min-width: 36px;
  }
  
  .pagination-number {
    padding: 0 0.5rem;
    font-size: 0.9rem;
  }
  
  .pagination-icon {
    width: 18px;
    height: 18px;
  }
  
  /* En móvil, mostrar menos páginas */
  .pagination-pages {
    max-width: 200px;
    overflow-x: auto;
    scrollbar-width: none;
    -ms-overflow-style: none;
  }
  
  .pagination-pages::-webkit-scrollbar {
    display: none;
  }
}

@media (max-width: 480px) {
  .pagination-summary {
    font-size: 0.8rem;
  }
}
</style>