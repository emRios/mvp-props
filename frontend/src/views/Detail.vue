<template>
  <div>
    <!-- Navigation -->
    <nav class="navbar">
      <div class="container navbar-content">
        <router-link to="/" class="navbar-brand">MVP Props</router-link>
        <ul class="navbar-nav">
          <li><router-link to="/">Inicio</router-link></li>
          <li><router-link to="/interactions">Interacciones</router-link></li>
          <li><router-link to="/metrics">Métricas</router-link></li>
        </ul>
      </div>
    </nav>

    <div class="container detail-container">
      <div v-if="loading" class="text-center">
        <p>Cargando propiedad...</p>
      </div>
      
      <div v-else-if="error" class="text-center">
        <p class="text-muted">Error: {{ error }}</p>
        <router-link to="/" class="btn">Volver al inicio</router-link>
      </div>
      
      <div v-else-if="property" class="detail-content">
        <!-- Image Carousel -->
        <div class="detail-images">
          <ImageCarousel :images="property.imagenes || []" />
        </div>
        
        <!-- Property Info -->
        <div class="detail-info">
          <h1>{{ property.titulo || 'Propiedad sin título' }}</h1>
          <div class="price">
            ${{ formatPrice(property.precio) }}
          </div>
          
          <div class="specs">
            <div class="spec-item">
              <strong>Área:</strong> {{ area }} m²
            </div>
            <div class="spec-item">
              <strong>Habitaciones:</strong> {{ bedrooms }}
            </div>
            <div class="spec-item">
              <strong>Baños:</strong> {{ bathrooms }}
            </div>
            <div class="spec-item">
              <strong>Tipo:</strong> {{ property.tipo || 'No especificado' }}
            </div>
            <div class="spec-item">
              <strong>Ubicación:</strong> {{ property.ubicacion || 'No especificada' }}
            </div>
          </div>
          
          <div v-if="property.descripcion" class="description">
            <h3>Descripción</h3>
            <p>{{ property.descripcion }}</p>
          </div>
          
          <!-- WhatsApp CTA -->
          <div class="cta-section">
            <a 
              :href="whatsappUrl" 
              target="_blank" 
              class="btn whatsapp-btn"
            >
              📱 Contactar por WhatsApp
            </a>
          </div>
          
          <!-- Question Form -->
          <div class="question-section">
            <h3>¿Tienes alguna pregunta?</h3>
            <form @submit.prevent="submitQuestion" class="question-form">
              <div class="form-group">
                <textarea 
                  v-model="question" 
                  placeholder="Escribe tu pregunta aquí..."
                  rows="4"
                  class="form-input"
                  :disabled="submitting"
                ></textarea>
              </div>
              <button 
                type="submit" 
                class="btn"
                :disabled="!question.trim() || submitting"
              >
                {{ submitting ? 'Enviando...' : 'Enviar pregunta' }}
              </button>
            </form>
            
            <!-- Question Response -->
            <div v-if="questionResponse" class="question-response">
              <h4>Respuesta:</h4>
              <div class="response-content">
                {{ questionResponse }}
              </div>
            </div>
            
            <div v-if="questionError" class="question-error">
              <p class="text-muted">Error al enviar pregunta: {{ questionError }}</p>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script>
import { api } from '../api.js'
import ImageCarousel from '../components/ImageCarousel.vue'

export default {
  name: 'Detail',
  components: {
    ImageCarousel
  },
  props: {
    id: {
      type: String,
      required: true
    }
  },
  data() {
    return {
      property: null,
      loading: true,
      error: null,
      question: '',
      submitting: false,
      questionResponse: '',
      questionError: null
    }
  },
  async mounted() {
    await this.loadProperty()
  },
  computed: {
    area() {
      return this.property?.m2construccion || this.property?.area || 0
    },
    bedrooms() {
      return this.property?.habitaciones || this.property?.recamaras || 0
    },
    bathrooms() {
      return this.property?.['baños'] || this.property?.banos || 0
    },
    whatsappUrl() {
      const message = `Hola, me interesa la propiedad: ${this.property?.titulo || 'Propiedad'} - $${this.formatPrice(this.property?.precio)}`
      return `https://wa.me/525512345678?text=${encodeURIComponent(message)}`
    }
  },
  methods: {
    async loadProperty() {
      try {
        this.loading = true
        this.error = null
        const properties = await api.getProperties()
        this.property = properties.find(p => p.id.toString() === this.id)
        
        if (!this.property) {
          this.error = 'Propiedad no encontrada'
        }
      } catch (err) {
        this.error = err.message
      } finally {
        this.loading = false
      }
    },
    
    async submitQuestion() {
      if (!this.question.trim()) return
      
      try {
        this.submitting = true
        this.questionError = null
        this.questionResponse = ''
        
        const response = await api.postInteraction({
          propertyId: this.property.id,
          userId: 'u-demo',
          question: this.question.trim(),
          timestamp: new Date().toISOString()
        })
        
        this.questionResponse = response.response || 'Pregunta enviada correctamente'
        this.question = ''
      } catch (err) {
        this.questionError = err.message
      } finally {
        this.submitting = false
      }
    },
    
    formatPrice(price) {
      if (!price) return '0'
      return new Intl.NumberFormat('es-MX').format(price)
    }
  }
}
</script>

<style scoped>
.detail-container {
  padding: 2rem 0;
}

.detail-content {
  display: grid;
  grid-template-columns: 2fr 1fr;
  gap: 3rem;
  margin-top: 2rem;
}

.detail-images {
  /* ImageCarousel styles are in component */
}

.detail-info h1 {
  font-size: 2.5rem;
  margin-bottom: 1rem;
}

.price {
  font-size: 2rem;
  font-weight: bold;
  color: var(--accent);
  margin-bottom: 2rem;
}

.specs {
  background: var(--card);
  padding: 1.5rem;
  border-radius: 12px;
  margin-bottom: 2rem;
}

.spec-item {
  display: flex;
  justify-content: space-between;
  padding: 0.5rem 0;
  border-bottom: 1px solid var(--border);
}

.spec-item:last-child {
  border-bottom: none;
}

.description {
  margin-bottom: 2rem;
}

.description h3 {
  margin-bottom: 1rem;
}

.cta-section {
  margin-bottom: 2rem;
}

.whatsapp-btn {
  background: #25d366;
  width: 100%;
  font-size: 1.1rem;
}

.whatsapp-btn:hover {
  background: #128c7e;
}

.question-section {
  background: var(--card);
  padding: 1.5rem;
  border-radius: 12px;
}

.question-section h3 {
  margin-bottom: 1rem;
}

.question-form {
  margin-bottom: 1rem;
}

.question-response {
  background: rgba(37, 99, 235, 0.1);
  padding: 1rem;
  border-radius: 8px;
  border-left: 4px solid var(--accent);
}

.question-response h4 {
  margin-bottom: 0.5rem;
  color: var(--accent);
}

.question-error {
  background: rgba(239, 68, 68, 0.1);
  padding: 1rem;
  border-radius: 8px;
  border-left: 4px solid #ef4444;
}

@media (max-width: 768px) {
  .detail-content {
    grid-template-columns: 1fr;
    gap: 2rem;
  }
  
  .detail-info h1 {
    font-size: 2rem;
  }
  
  .price {
    font-size: 1.5rem;
  }
}
</style>