// src/composables/useNetworkQuality.js

import { ref, onMounted, onUnmounted } from 'vue';

/**
 * Composable para detectar y adaptarse a la calidad de la conexión de red
 */
export function useNetworkQuality() {
  const connectionType = ref('4g'); // default: buena conexión
  const saveData = ref(false);
  const effectiveType = ref('4g');
  const downlink = ref(10); // Mbps
  const rtt = ref(100); // ms

  const qualityLevel = ref('high'); // low, medium, high
  
  // Actualizar información de conexión
  function updateConnectionInfo() {
    if ('connection' in navigator) {
      const conn = navigator.connection;
      
      connectionType.value = conn.type || '4g';
      saveData.value = conn.saveData || false;
      effectiveType.value = conn.effectiveType || '4g';
      downlink.value = conn.downlink || 10;
      rtt.value = conn.rtt || 100;
      
      // Determinar nivel de calidad basado en la conexión
      determineQualityLevel();
    }
  }
  
  function determineQualityLevel() {
    // Si el usuario tiene "Save Data" activado, usar calidad baja
    if (saveData.value) {
      qualityLevel.value = 'low';
      return;
    }
    
    // Basado en el tipo efectivo de conexión
    switch (effectiveType.value) {
      case 'slow-2g':
      case '2g':
        qualityLevel.value = 'low';
        break;
      case '3g':
        qualityLevel.value = 'medium';
        break;
      case '4g':
      default:
        // Verificar downlink para 4G
        if (downlink.value < 1.5) {
          qualityLevel.value = 'medium';
        } else {
          qualityLevel.value = 'high';
        }
        break;
    }
  }
  
  // Obtener configuración de imagen según calidad
  function getImageConfig() {
    const configs = {
      low: {
        quality: 60,
        maxWidth: 400,
        format: 'webp',
        progressive: true
      },
      medium: {
        quality: 75,
        maxWidth: 800,
        format: 'webp',
        progressive: true
      },
      high: {
        quality: 85,
        maxWidth: 1200,
        format: 'webp',
        progressive: false
      }
    };
    
    return configs[qualityLevel.value] || configs.high;
  }
  
  // Estimar si se debe usar lazy loading agresivo
  function shouldUseLazyLoading() {
    return qualityLevel.value === 'low' || saveData.value;
  }
  
  // Número recomendado de imágenes a precargar
  function getPreloadCount() {
    const counts = {
      low: 2,
      medium: 4,
      high: 6
    };
    
    return counts[qualityLevel.value] || 4;
  }
  
  onMounted(() => {
    updateConnectionInfo();
    
    // Escuchar cambios en la conexión
    if ('connection' in navigator) {
      navigator.connection.addEventListener('change', updateConnectionInfo);
    }
  });
  
  onUnmounted(() => {
    if ('connection' in navigator) {
      navigator.connection.removeEventListener('change', updateConnectionInfo);
    }
  });
  
  return {
    // Estado reactivo
    connectionType,
    saveData,
    effectiveType,
    downlink,
    rtt,
    qualityLevel,
    
    // Métodos
    getImageConfig,
    shouldUseLazyLoading,
    getPreloadCount,
    updateConnectionInfo
  };
}