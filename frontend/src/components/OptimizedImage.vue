<!-- src/components/OptimizedImage.vue -->
<template>
  <div class="optimized-image" :class="{ loading: isLoading, loaded: !isLoading }">
    <!-- Placeholder con blur effect -->
    <div 
      class="image-placeholder"
      :style="{ 
        backgroundImage: `url(${placeholderSrc})`,
        aspectRatio: aspectRatio 
      }"
    >
      <div class="blur-overlay"></div>
    </div>
    
    <!-- Imagen principal con soporte WebP -->
    <picture class="picture-element">
      <!-- WebP para navegadores compatibles -->
      <source 
        v-if="webpSrc" 
        :srcset="webpSrcSet"
        type="image/webp"
      />
      
      <!-- Fallback JPEG/PNG -->
      <img
        ref="imageRef"
        :src="currentSrc"
        :srcset="srcSet"
        :alt="alt"
        :loading="lazyLoading ? 'lazy' : 'eager'"
        class="optimized-img"
        @load="handleLoad"
        @error="handleError"
        :style="{ aspectRatio: aspectRatio }"
      />
    </picture>
    
    <!-- Loading spinner elegante -->
    <div v-if="isLoading" class="loading-indicator">
      <div class="loading-dots">
        <span></span>
        <span></span>
        <span></span>
      </div>
    </div>
    
    <!-- Error fallback -->
    <div v-if="hasError" class="error-fallback">
      <svg viewBox="0 0 24 24" class="error-icon">
        <path d="M19 7v2.99s-1.99.01-2 0V7c0-2.8-2.2-5-5-5s-5 2.2-5 5v3H5c-1.1 0-2 .9-2 2v8c0 1.1.9 2 2 2h14c1.1 0 2-.9 2-2v-8c0-1.1-.9-2-2-2h-2V7zM12 2c3.9 0 7 3.1 7 7v3H5V9c0-3.9 3.1-7 7-7z"/>
        <path d="M9 18V16h6v2H9z"/>
      </svg>
      <p>Imagen no disponible</p>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted, onUnmounted } from 'vue';
import { generateOptimizedUrl } from '../utils/imageOptimization';
import { isImageCached } from '../utils/images';

const props = defineProps({
  src: { type: String, required: true },
  alt: { type: String, default: '' },
  aspectRatio: { type: String, default: '16/9' },
  sizes: { type: String, default: '(max-width: 768px) 100vw, (max-width: 1200px) 50vw, 25vw' },
  lazyLoading: { type: Boolean, default: true },
  priority: { type: Boolean, default: false }, // Para imágenes críticas
  placeholder: { type: String, default: null },
  quality: { type: Number, default: 85 }
});

const imageRef = ref(null);
const isLoading = ref(true);
const hasError = ref(false);
const showPlaceholder = ref(true);
const observer = ref(null);

// Generar URLs optimizadas
const currentSrc = computed(() => {
  if (!props.src) return '';
  return generateOptimizedUrl(props.src, 800, props.quality);
});

const srcSet = computed(() => {
  if (!props.src) return '';
  const sizes = [400, 800, 1200, 1600];
  return sizes.map(size => 
    `${generateOptimizedUrl(props.src, size, props.quality)} ${size}w`
  ).join(', ');
});

const webpSrc = computed(() => {
  if (!props.src) return '';
  return generateOptimizedUrl(props.src, 800, props.quality, 'webp');
});

const webpSrcSet = computed(() => {
  if (!props.src) return '';
  const sizes = [400, 800, 1200, 1600];
  return sizes.map(size => 
    `${generateOptimizedUrl(props.src, size, props.quality, 'webp')} ${size}w`
  ).join(', ');
});

const placeholderSrc = computed(() => {
  if (props.placeholder) return props.placeholder;
  // Generar placeholder de baja calidad (blur effect)
  return generateOptimizedUrl(props.src, 40, 20, 'webp');
});

// Cache de imágenes decodificadas (para evitar parpadeo en re-montajes)
const decodedCache = new Set();

function handleLoad() {
  isLoading.value = false;
  showPlaceholder.value = false;
  if (currentSrc.value) decodedCache.add(currentSrc.value);
}

function handleError() {
  isLoading.value = false;
  hasError.value = true;
  showPlaceholder.value = false;
}

// Intersection Observer para lazy loading avanzado
function setupIntersectionObserver() {
  if (!props.lazyLoading || props.priority) return;
  
  if ('IntersectionObserver' in window && imageRef.value) {
    observer.value = new IntersectionObserver(
      (entries) => {
        entries.forEach(entry => {
          if (entry.isIntersecting) {
            // Imagen está visible, no hacer nada especial
            // El browser ya maneja el lazy loading nativo
            observer.value?.unobserve(entry.target);
          }
        });
      },
      {
        rootMargin: '50px', // Cargar 50px antes de ser visible
        threshold: 0.1
      }
    );
    
    observer.value.observe(imageRef.value);
  }
}

onMounted(() => {
  // Si ya se cargó/decodificó antes, mostrar de inmediato sin placeholder
  const url = currentSrc.value;
  if (url && (decodedCache.has(url) || isImageCached(url) === true)) {
    isLoading.value = false;
    showPlaceholder.value = false;
  }

  // Para imágenes prioritarias, precargar inmediatamente
  if (props.priority) {
    isLoading.value = true;
    const img = new Image();
    img.onload = handleLoad;
    img.onerror = handleError;
    img.src = currentSrc.value;
  }
  
  setupIntersectionObserver();
});

onUnmounted(() => {
  if (observer.value && imageRef.value) {
    observer.value.unobserve(imageRef.value);
  }
});
</script>

<style scoped>
.optimized-image {
  position: relative;
  overflow: hidden;
  background: var(--card);
  border-radius: 8px;
}

.image-placeholder {
  position: absolute;
  top: 0;
  left: 0;
  width: 100%;
  height: 100%;
  background-size: cover;
  background-position: center;
  filter: blur(10px);
  transform: scale(1.1); /* Evitar bordes del blur */
  opacity: 1;
  transition: opacity 0.25s ease;
}

.blur-overlay {
  position: absolute;
  top: 0;
  left: 0;
  width: 100%;
  height: 100%;
  background: rgba(255, 255, 255, 0.1);
}

.picture-element {
  display: block;
  width: 100%;
  height: 100%;
}

.optimized-img {
  width: 100%;
  height: 100%;
  object-fit: cover;
  transition: opacity 0.3s ease, transform 0.3s ease;
}

.optimized-image.loading .optimized-img {
  opacity: 0;
}

.optimized-image.loaded .image-placeholder {
  opacity: 0;
}

.loading-indicator {
  position: absolute;
  top: 50%;
  left: 50%;
  transform: translate(-50%, -50%);
  z-index: 2;
}

.loading-dots {
  display: flex;
  gap: 4px;
}

.loading-dots span {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background: var(--accent);
  animation: pulse 1.4s ease-in-out infinite both;
}

.loading-dots span:nth-child(1) { animation-delay: -0.32s; }
.loading-dots span:nth-child(2) { animation-delay: -0.16s; }

@keyframes pulse {
  0%, 80%, 100% {
    transform: scale(0);
    opacity: 0.5;
  }
  40% {
    transform: scale(1);
    opacity: 1;
  }
}

.error-fallback {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  height: 100%;
  color: var(--muted);
  padding: 2rem;
}

.error-icon {
  width: 48px;
  height: 48px;
  fill: currentColor;
  margin-bottom: 0.5rem;
}

.error-fallback p {
  margin: 0;
  font-size: 0.9rem;
  text-align: center;
}

/* Hover effects */
.optimized-image:hover .optimized-img {
  transform: scale(1.05);
}

/* Responsive adjustments */
@media (max-width: 768px) {
  .loading-dots span {
    width: 6px;
    height: 6px;
  }
  
  .error-icon {
    width: 32px;
    height: 32px;
  }
}
</style>