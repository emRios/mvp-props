<template>
  <div class="hero-search">
    <div class="search-form">
      <div class="form-row">
        <div class="form-group">
          <label>Ubicación</label>
          <input 
            v-model="filters.location" 
            type="text" 
            class="form-input" 
            placeholder="Ciudad, colonia..."
          />
        </div>
        <button @click="search" class="btn search-btn">
          Buscar
        </button>
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
        if (typeof val === 'string' && val.length) {
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

.search-btn {
  height: fit-content;
  white-space: nowrap;
}

@media (max-width: 768px) {
  .form-row {
    grid-template-columns: 1fr;
  }
}
</style>