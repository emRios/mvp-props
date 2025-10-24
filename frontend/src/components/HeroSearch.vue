<template>
  <div class="hero-search">
    <div class="search-form">
      <div class="form-row">
        <div class="form-group">
          <label for="location-input">Ubicación</label>
          <input
            id="location-input"
            v-model="filters.location"
            type="text"
            class="form-input"
            placeholder="Ciudad, colonia..."
            @keyup.enter="search"
          />
        </div>
        <button @click="search" class="btn search-btn" aria-label="Buscar">Buscar</button>
      </div>
    </div>
  </div>
</template>

<script>
export default {
  name: 'HeroSearch',
  emits: ['search'],
  props: {
    dictatedLocation: { type: String, default: '' }
  },
  data() {
    return {
      filters: {
        location: ''
      }
    }
  },
  watch: {
    dictatedLocation: {
      immediate: true,
      handler(val) {
        // Permitir limpiar el campo cuando val === ''
        if (typeof val === 'string') {
          this.filters.location = val;
        }
      }
    }
  },
  methods: {
    search() {
      this.$emit('search', { ...this.filters })
    }
  }
}
</script>

<style scoped>
.hero-search {
  background: rgba(0, 0, 0, 0.1);
  padding: 2rem;
  border-radius: 12px;
  backdrop-filter: blur(10px);
}

.search-form {
  max-width: 800px;
  margin: 0 auto;
}

.form-row {
  display: grid;
  grid-template-columns: 2fr auto;
  gap: 1rem;
  align-items: end;
}

.form-group label {
  font-weight: 500;
  margin-bottom: 0.5rem;
  display: block;
}

.form-input {
  width: 100%;
  padding: 0.6rem 0.75rem;
  border: 1px solid var(--border);
  background: var(--card);
  color: var(--text);
  border-radius: 8px;
  outline: none;
}

.form-input:focus {
  border-color: var(--accent);
  box-shadow: 0 0 0 2px rgba(59, 130, 246, 0.25);
}

.search-btn {
  height: fit-content;
  white-space: nowrap;
  padding: 0.6rem 1rem;
  border-radius: 8px;
  border: 1px solid var(--border);
  background: var(--card);
  color: var(--text);
}

@media (max-width: 768px) {
  .form-row {
    grid-template-columns: 1fr;
  }
}
</style>