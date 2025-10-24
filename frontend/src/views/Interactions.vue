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

    <div class="container interactions-container">
      <h1 class="mb-4">Mis Interacciones</h1>
      
      <div v-if="loading" class="text-center">
        <p>Cargando interacciones...</p>
      </div>
      
      <div v-else-if="error" class="text-center">
        <p class="text-muted">Error al cargar interacciones: {{ error }}</p>
        <button @click="loadInteractions" class="btn">Reintentar</button>
      </div>
      
      <div v-else-if="interactions.length === 0" class="empty-state">
        <h2>No tienes interacciones aún</h2>
        <p class="text-muted">Cuando hagas preguntas sobre propiedades, aparecerán aquí.</p>
        <router-link to="/" class="btn">Explorar propiedades</router-link>
      </div>
      
      <div v-else class="interactions-list">
        <div 
          v-for="interaction in interactions" 
          :key="interaction.id" 
          class="interaction-card card p-4 mb-4"
        >
          <div class="interaction-header">
            <div class="property-info">
              <h3>Propiedad ID: {{ interaction.propertyId }}</h3>
              <span class="timestamp text-muted">
                {{ formatDate(interaction.timestamp) }}
              </span>
            </div>
            <div class="status-badge" :class="getStatusClass(interaction.status)">
              {{ getStatusText(interaction.status) }}
            </div>
          </div>
          
          <div class="interaction-content">
            <div class="question">
              <strong>Pregunta:</strong>
              <p>{{ interaction.question }}</p>
            </div>
            
            <div v-if="interaction.response" class="response">
              <strong>Respuesta:</strong>
              <p>{{ interaction.response }}</p>
            </div>
            
            <div v-else-if="interaction.status === 'pendiente'" class="no-response">
              <p class="text-muted">Esperando respuesta...</p>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script>
import { api } from '../api.js'

export default {
  name: 'Interactions',
  data() {
    return {
      interactions: [],
      loading: true,
      error: null
    }
  },
  async mounted() {
    await this.loadInteractions()
  },
  methods: {
    async loadInteractions() {
      try {
        this.loading = true
        this.error = null
        this.interactions = await api.getInteractions('u-demo')
      } catch (err) {
        this.error = err.message
      } finally {
        this.loading = false
      }
    },
    
    formatDate(dateString) {
      if (!dateString) return 'Fecha no disponible'
      
      try {
        const date = new Date(dateString)
        return date.toLocaleDateString('es-MX', {
          year: 'numeric',
          month: 'long',
          day: 'numeric',
          hour: '2-digit',
          minute: '2-digit'
        })
      } catch {
        return 'Fecha inválida'
      }
    },
    
    getStatusClass(status) {
      switch (status) {
        case 'pendiente':
          return 'status-pending'
        case 'respondida':
          return 'status-answered'
        case 'cancelada':
          return 'status-cancelled'
        default:
          return 'status-unknown'
      }
    },
    
    getStatusText(status) {
      switch (status) {
        case 'pendiente':
          return 'Pendiente'
        case 'respondida':
          return 'Respondida'
        case 'cancelada':
          return 'Cancelada'
        default:
          return 'Desconocido'
      }
    }
  }
}
</script>

<style scoped>
.interactions-container {
  padding: 2rem 0;
  min-height: 60vh;
}

.interactions-container h1 {
  text-align: center;
  margin-bottom: 2rem;
}

.empty-state {
  text-align: center;
  padding: 4rem 2rem;
}

.empty-state h2 {
  margin-bottom: 1rem;
  color: var(--muted);
}

.empty-state p {
  margin-bottom: 2rem;
}

.interactions-list {
  max-width: 800px;
  margin: 0 auto;
}

.interaction-card {
  transition: transform 0.2s;
}

.interaction-card:hover {
  transform: translateY(-2px);
}

.interaction-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  margin-bottom: 1rem;
  padding-bottom: 1rem;
  border-bottom: 1px solid var(--border);
}

.property-info h3 {
  margin-bottom: 0.5rem;
}

.timestamp {
  font-size: 0.9rem;
}

.status-badge {
  padding: 0.25rem 0.75rem;
  border-radius: 20px;
  font-size: 0.8rem;
  font-weight: 500;
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

.status-pending {
  background: rgba(251, 191, 36, 0.2);
  color: #f59e0b;
}

.status-answered {
  background: rgba(34, 197, 94, 0.2);
  color: #22c55e;
}

.status-cancelled {
  background: rgba(239, 68, 68, 0.2);
  color: #ef4444;
}

.status-unknown {
  background: rgba(156, 163, 175, 0.2);
  color: #9ca3af;
}

.interaction-content .question,
.interaction-content .response {
  margin-bottom: 1rem;
}

.interaction-content .question p,
.interaction-content .response p {
  margin-top: 0.5rem;
  padding: 1rem;
  background: rgba(255, 255, 255, 0.05);
  border-radius: 8px;
  border-left: 4px solid var(--accent);
}

.interaction-content .response p {
  border-left-color: #22c55e;
  background: rgba(34, 197, 94, 0.1);
}

.no-response {
  text-align: center;
  padding: 1rem;
}

@media (max-width: 768px) {
  .interaction-header {
    flex-direction: column;
    gap: 1rem;
  }
  
  .status-badge {
    align-self: flex-start;
  }
}
</style>