<template>
  <div class="image-carousel">
    <div class="carousel-container" ref="container">
      <div 
        class="carousel-track" 
        :style="{ transform: `translateX(-${currentIndex * 100}%)` }"
        @touchstart="handleTouchStart"
        @touchmove="handleTouchMove"
        @touchend="handleTouchEnd"
      >
        <div 
          v-for="(image, index) in images" 
          :key="index" 
          class="carousel-slide"
        >
          <!-- Loading indicator -->
          <div v-if="isImageLoading(index)" class="image-loading">
            <div class="loading-spinner"></div>
          </div>
          
          <img 
            :src="getImageUrl(image)" 
            :alt="`Imagen ${index + 1}`"
            :loading="shouldLazyLoad(index) ? 'lazy' : 'eager'"
            @load="markImageLoaded(index)"
            @error="handleImageError(index)"
            :style="{ opacity: isImageLoading(index) ? 0 : 1 }"
          />
          
          <!-- Preload next/prev images when current is visible -->
          <link 
            v-if="shouldPreload(index)"
            rel="preload" 
            as="image" 
            :href="getImageUrl(images[index])"
          />
        </div>
      </div>
      
      <button 
        v-if="images.length > 1"
        @click="prevSlide" 
        class="carousel-btn prev-btn"
        :disabled="currentIndex === 0"
        aria-label="Imagen anterior"
      >
        ←
      </button>
      
      <button 
        v-if="images.length > 1"
        @click="nextSlide" 
        class="carousel-btn next-btn"
        :disabled="currentIndex === images.length - 1"
        aria-label="Siguiente imagen"
      >
        →
      </button>
    </div>
    
    <div v-if="images.length > 1" class="carousel-dots">
      <button 
        v-for="(_, index) in images" 
        :key="index"
        @click="goToSlide(index)"
        :class="['dot', { active: currentIndex === index }]"
        :aria-label="`Ir a imagen ${index + 1}`"
      ></button>
    </div>
  </div>
</template>

<script>
export default {
  name: 'ImageCarousel',
  props: {
    images: {
      type: Array,
      default: () => []
    }
  },
  data() {
    return {
      currentIndex: 0,
      touchStartX: 0,
      touchEndX: 0,
      imageErrors: new Set(),
      loadedImages: new Set(),
      loadingImages: new Set()
    }
  },
  mounted() {
    document.addEventListener('keydown', this.handleKeydown)
    this.preloadCurrentAndNext()
  },
  beforeUnmount() {
    document.removeEventListener('keydown', this.handleKeydown)
  },
  watch: {
    currentIndex() {
      this.preloadCurrentAndNext()
    }
  },
  methods: {
    getImageUrl(image) {
      if (this.imageErrors.has(image)) {
        return 'data:image/svg+xml;base64,PHN2ZyB3aWR0aD0iNjAwIiBoZWlnaHQ9IjQwMCIgeG1sbnM9Imh0dHA6Ly93d3cudzMub3JnLzIwMDAvc3ZnIj48cmVjdCB3aWR0aD0iMTAwJSIgaGVpZ2h0PSIxMDAlIiBmaWxsPSIjZGRkIi8+PHRleHQgeD0iNTAlIiB5PSI1MCUiIGZvbnQtZmFtaWx5PSJBcmlhbCIgZm9udC1zaXplPSIyMCIgZmlsbD0iIzk5OSIgdGV4dC1hbmNob3I9Im1pZGRsZSIgZHk9Ii4zZW0iPkltYWdlbiBubyBkaXNwb25pYmxlPC90ZXh0Pjwvc3ZnPg=='
      }
      
      if (typeof image === 'string') {
        return image
      }
      
      return image?.url || 'data:image/svg+xml;base64,PHN2ZyB3aWR0aD0iNjAwIiBoZWlnaHQ9IjQwMCIgeG1sbnM9Imh0dHA6Ly93d3cudzMub3JnLzIwMDAvc3ZnIj48cmVjdCB3aWR0aD0iMTAwJSIgaGVpZ2h0PSIxMDAlIiBmaWxsPSIjZGRkIi8+PHRleHQgeD0iNTAlIiB5PSI1MCUiIGZvbnQtZmFtaWx5PSJBcmlhbCIgZm9udC1zaXplPSIyMCIgZmlsbD0iIzk5OSIgdGV4dC1hbmNob3I9Im1pZGRsZSIgZHk9Ii4zZW0iPkltYWdlbiBubyBkaXNwb25pYmxlPC90ZXh0Pjwvc3ZnPg=='
    },
    handleImageError(index) {
      this.imageErrors.add(this.images[index])
      this.loadingImages.delete(index)
    },
    markImageLoaded(index) {
      this.loadedImages.add(index)
      this.loadingImages.delete(index)
    },
    isImageLoading(index) {
      return this.loadingImages.has(index) && !this.loadedImages.has(index)
    },
    shouldLazyLoad(index) {
      // Solo carga eager la imagen actual y las adyacentes
      return Math.abs(index - this.currentIndex) > 1
    },
    shouldPreload(index) {
      // Preload solo las imágenes adyacentes
      return Math.abs(index - this.currentIndex) <= 1
    },
    preloadCurrentAndNext() {
      // Marca como loading las imágenes que vamos a necesitar
      for (let i = Math.max(0, this.currentIndex - 1); i <= Math.min(this.images.length - 1, this.currentIndex + 1); i++) {
        if (!this.loadedImages.has(i) && !this.imageErrors.has(this.images[i])) {
          this.loadingImages.add(i)
        }
      }
    },
    prevSlide() {
      if (this.currentIndex > 0) {
        this.currentIndex--
      }
    },
    nextSlide() {
      if (this.currentIndex < this.images.length - 1) {
        this.currentIndex++
      }
    },
    goToSlide(index) {
      this.currentIndex = index
    },
    handleKeydown(event) {
      if (event.key === 'ArrowLeft') {
        this.prevSlide()
      } else if (event.key === 'ArrowRight') {
        this.nextSlide()
      }
    },
    handleTouchStart(event) {
      this.touchStartX = event.touches[0].clientX
    },
    handleTouchMove(event) {
      this.touchEndX = event.touches[0].clientX
    },
    handleTouchEnd() {
      const swipeThreshold = 50
      const diff = this.touchStartX - this.touchEndX
      
      if (Math.abs(diff) > swipeThreshold) {
        if (diff > 0) {
          this.nextSlide()
        } else {
          this.prevSlide()
        }
      }
    }
  }
}
</script>

<style scoped>
.image-carousel {
  position: relative;
  width: 100%;
}

.carousel-container {
  position: relative;
  overflow: hidden;
  border-radius: 12px;
  background: var(--card);
}

.carousel-track {
  display: flex;
  transition: transform 0.3s ease-in-out;
}

.carousel-slide {
  flex: 0 0 100%;
  height: 400px;
}

.carousel-slide img {
  width: 100%;
  height: 100%;
  object-fit: cover;
  transition: opacity 0.3s ease;
}

.image-loading {
  position: absolute;
  top: 0;
  left: 0;
  width: 100%;
  height: 100%;
  display: flex;
  align-items: center;
  justify-content: center;
  background: var(--card);
  z-index: 1;
}

.loading-spinner {
  width: 40px;
  height: 40px;
  border: 3px solid var(--muted);
  border-top: 3px solid var(--accent);
  border-radius: 50%;
  animation: spin 1s linear infinite;
}

@keyframes spin {
  0% { transform: rotate(0deg); }
  100% { transform: rotate(360deg); }
}

.carousel-btn {
  position: absolute;
  top: 50%;
  transform: translateY(-50%);
  background: rgba(0, 0, 0, 0.7);
  color: white;
  border: none;
  width: 40px;
  height: 40px;
  border-radius: 50%;
  cursor: pointer;
  font-size: 1.2rem;
  z-index: 2;
  transition: background 0.2s;
}

.carousel-btn:hover:not(:disabled) {
  background: rgba(0, 0, 0, 0.9);
}

.carousel-btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.prev-btn {
  left: 1rem;
}

.next-btn {
  right: 1rem;
}

.carousel-dots {
  display: flex;
  justify-content: center;
  gap: 0.5rem;
  margin-top: 1rem;
}

.dot {
  width: 12px;
  height: 12px;
  border-radius: 50%;
  border: none;
  background: var(--muted);
  cursor: pointer;
  transition: background 0.2s;
}

.dot.active {
  background: var(--accent);
}

.dot:hover {
  background: var(--accent);
}

@media (max-width: 768px) {
  .carousel-slide {
    height: 250px;
  }
  
  .carousel-btn {
    width: 35px;
    height: 35px;
    font-size: 1rem;
  }
}
</style>