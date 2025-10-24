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

    <div class="container metrics-container">
      <h1 class="mb-4">Métricas de Interacciones</h1>
      
      <div v-if="loading" class="text-center">
        <p>Cargando métricas...</p>
      </div>
      
      <div v-else-if="error" class="text-center">
        <p class="text-muted">Error al cargar métricas: {{ error }}</p>
        <button @click="loadMetrics" class="btn">Reintentar</button>
      </div>
      
      <div v-else class="metrics-content">
        <!-- Summary Cards -->
        <div class="metrics-grid grid grid-2">
          <div class="metric-card card p-4">
            <div class="metric-icon pending">📋</div>
            <div class="metric-info">
              <h3>Pendientes</h3>
              <div class="metric-value">{{ metrics.pendiente || 0 }}</div>
              <p class="text-muted">Interacciones sin responder</p>
            </div>
          </div>
          
          <div class="metric-card card p-4">
            <div class="metric-icon answered">✅</div>
            <div class="metric-info">
              <h3>Respondidas</h3>
              <div class="metric-value">{{ metrics.respondida || 0 }}</div>
              <p class="text-muted">Interacciones completadas</p>
            </div>
          </div>
          
          <div class="metric-card card p-4">
            <div class="metric-icon cancelled">❌</div>
            <div class="metric-info">
              <h3>Canceladas</h3>
              <div class="metric-value">{{ metrics.cancelada || 0 }}</div>
              <p class="text-muted">Interacciones canceladas</p>
            </div>
          </div>
          
          <div class="metric-card card p-4 total-card">
            <div class="metric-icon total">📊</div>
            <div class="metric-info">
              <h3>Total</h3>
              <div class="metric-value">{{ totalInteractions }}</div>
              <p class="text-muted">Total de interacciones</p>
            </div>
          </div>
        </div>
        
        <!-- Percentage Overview -->
        <div class="percentage-overview card p-4">
          <h2 class="mb-4">Distribución por Estado</h2>
          <div class="percentage-bars">
            <div class="percentage-item">
              <div class="percentage-label">
                <span>Pendientes</span>
                <span class="percentage-value">{{ pendingPercentage }}%</span>
              </div>
              <div class="percentage-bar">
                <div 
                  class="percentage-fill pending" 
                  :style="{ width: pendingPercentage + '%' }"
                ></div>
              </div>
            </div>
            
            <div class="percentage-item">
              <div class="percentage-label">
                <span>Respondidas</span>
                <span class="percentage-value">{{ answeredPercentage }}%</span>
              </div>
              <div class="percentage-bar">
                <div 
                  class="percentage-fill answered" 
                  :style="{ width: answeredPercentage + '%' }"
                ></div>
              </div>
            </div>
            
            <div class="percentage-item">
              <div class="percentage-label">
                <span>Canceladas</span>
                <span class="percentage-value">{{ cancelledPercentage }}%</span>
              </div>
              <div class="percentage-bar">
                <div 
                  class="percentage-fill cancelled" 
                  :style="{ width: cancelledPercentage + '%' }"
                ></div>
              </div>
            </div>
          </div>
        </div>
        
        <!-- Actions -->
        <div class="actions-section text-center">
          <router-link to="/interactions" class="btn">
            Ver todas las interacciones
          </router-link>
          <button @click="loadMetrics" class="btn" style="margin-left: 1rem;">
            Actualizar métricas
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<script>
import { api } from '../api.js'

export default {
  name: 'Metrics',
  data() {
    return {
      metrics: {},
      loading: true,
      error: null
    }
  },
  computed: {
    totalInteractions() {
      return (this.metrics.pendiente || 0) + 
             (this.metrics.respondida || 0) + 
             (this.metrics.cancelada || 0)
    },
    
    pendingPercentage() {
      if (this.totalInteractions === 0) return 0
      return Math.round((this.metrics.pendiente || 0) / this.totalInteractions * 100)
    },
    
    answeredPercentage() {
      if (this.totalInteractions === 0) return 0
      return Math.round((this.metrics.respondida || 0) / this.totalInteractions * 100)
    },
    
    cancelledPercentage() {
      if (this.totalInteractions === 0) return 0
      return Math.round((this.metrics.cancelada || 0) / this.totalInteractions * 100)
    }
  },
  async mounted() {
    await this.loadMetrics()
  },
  methods: {
    async loadMetrics() {
      try {
        this.loading = true
        this.error = null
        this.metrics = await api.getMetrics()
      } catch (err) {
        this.error = err.message
      } finally {
        this.loading = false
      }
    }
  }
}
</script>

<style scoped>
.metrics-container {
  padding: 2rem 0;
  min-height: 60vh;
}

.metrics-container h1 {
  text-align: center;
  margin-bottom: 3rem;
}

.metrics-content {
  max-width: 1000px;
  margin: 0 auto;
}

.metrics-grid {
  margin-bottom: 3rem;
}

.metric-card {
  display: flex;
  align-items: center;
  gap: 1.5rem;
  transition: transform 0.2s;
}

.metric-card:hover {
  transform: translateY(-4px);
}

.total-card {
  background: linear-gradient(135deg, var(--accent), #1d4ed8);
}

.metric-icon {
  font-size: 3rem;
  width: 80px;
  height: 80px;
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: 50%;
  flex-shrink: 0;
}

.metric-icon.pending {
  background: rgba(251, 191, 36, 0.2);
}

.metric-icon.answered {
  background: rgba(34, 197, 94, 0.2);
}

.metric-icon.cancelled {
  background: rgba(239, 68, 68, 0.2);
}

.metric-icon.total {
  background: rgba(255, 255, 255, 0.2);
}

.metric-info h3 {
  margin-bottom: 0.5rem;
  font-size: 1.2rem;
}

.metric-value {
  font-size: 2.5rem;
  font-weight: bold;
  color: var(--accent);
  margin-bottom: 0.25rem;
}

.total-card .metric-value {
  color: white;
}

.percentage-overview {
  margin-bottom: 3rem;
}

.percentage-overview h2 {
  text-align: center;
}

.percentage-bars {
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
}

.percentage-item {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.percentage-label {
  display: flex;
  justify-content: space-between;
  font-weight: 500;
}

.percentage-value {
  color: var(--accent);
}

.percentage-bar {
  height: 20px;
  background: var(--border);
  border-radius: 10px;
  overflow: hidden;
}

.percentage-fill {
  height: 100%;
  border-radius: 10px;
  transition: width 0.5s ease-in-out;
}

.percentage-fill.pending {
  background: linear-gradient(90deg, #f59e0b, #fbbf24);
}

.percentage-fill.answered {
  background: linear-gradient(90deg, #22c55e, #34d399);
}

.percentage-fill.cancelled {
  background: linear-gradient(90deg, #ef4444, #f87171);
}

.actions-section {
  margin-top: 3rem;
}

@media (max-width: 768px) {
  .metrics-grid {
    grid-template-columns: 1fr;
  }
  
  .metric-card {
    flex-direction: column;
    text-align: center;
    gap: 1rem;
  }
  
  .percentage-label {
    font-size: 0.9rem;
  }
  
  .actions-section .btn {
    display: block;
    margin: 0.5rem auto !important;
    width: fit-content;
  }
}
</style>