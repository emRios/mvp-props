// src/utils/imageOptimization.js

/**
 * Optimizaciones para mejorar la carga de imágenes
 */

// Configuración de tamaños optimizados
const OPTIMIZED_SIZES = {
  thumbnail: { width: 300, height: 200, quality: 0.8 },
  card: { width: 400, height: 300, quality: 0.85 },
  carousel: { width: 800, height: 600, quality: 0.9 }
};

/**
 * Genera URL optimizada con parámetros de redimensionamiento
 * @param {string} originalUrl - URL original de la imagen
 * @param {string} size - Tamaño deseado ('thumbnail', 'card', 'carousel')
 * @returns {string} URL optimizada
 */
export function getOptimizedImageUrl(originalUrl, size = 'card') {
  if (!originalUrl) return '';
  
  // Si ya es una URL absoluta, la retornamos tal como está por ahora
  if (originalUrl.startsWith('http://') || originalUrl.startsWith('https://')) {
    return originalUrl;
  }
  
  const config = OPTIMIZED_SIZES[size] || OPTIMIZED_SIZES.card;
  
  // Para el proxy local, podemos añadir parámetros de query
  // En un entorno real, esto se conectaría con un servicio de optimización
  const params = new URLSearchParams({
    w: config.width.toString(),
    h: config.height.toString(),
    q: Math.round(config.quality * 100).toString(),
    fit: 'cover'
  });
  
  // Si la URL empieza con /mvp-props, mantenemos el formato del proxy
  if (originalUrl.startsWith('/mvp-props/')) {
    return `${originalUrl}?${params.toString()}`;
  }
  
  return originalUrl;
}

/**
 * Crea una imagen WebP fallback para mejor compresión
 * @param {string} url - URL de la imagen
 * @returns {object} Objeto con URLs WebP y fallback
 */
export function createWebPFallback(url) {
  if (!url) return { webp: '', fallback: '' };
  
  // Para desarrollo, solo retornamos la URL original
  // En producción, esto se conectaría con un servicio que genere WebP
  return {
    webp: url,
    fallback: url
  };
}

/**
 * Detecta si el navegador soporta WebP
 * @returns {Promise<boolean>}
 */
export function supportsWebP() {
  return new Promise((resolve) => {
    const webp = new Image();
    webp.onload = webp.onerror = () => {
      resolve(webp.height === 2);
    };
    webp.src = 'data:image/webp;base64,UklGRjoAAABXRUJQVlA4IC4AAACyAgCdASoCAAIALmk0mk0iIiIiIgBoSygABc6WWgAA/veff/0PP8bA//LwYAAA';
  });
}

/**
 * Aplica lazy loading inteligente
 * @param {HTMLImageElement} img - Elemento de imagen
 * @param {string} src - URL de la imagen
 * @param {object} options - Opciones de lazy loading
 */
export function applySmartLazyLoading(img, src, options = {}) {
  const {
    rootMargin = '50px',
    threshold = 0.1,
    placeholder = 'data:image/svg+xml;base64,PHN2ZyB3aWR0aD0iMzAwIiBoZWlnaHQ9IjIwMCIgeG1sbnM9Imh0dHA6Ly93d3cudzMub3JnLzIwMDAvc3ZnIj48cmVjdCB3aWR0aD0iMTAwJSIgaGVpZ2h0PSIxMDAlIiBmaWxsPSIjZjBmMGYwIi8+PC9zdmc+'
  } = options;
  
  // Mostrar placeholder inicialmente
  img.src = placeholder;
  
  // Solo aplicar lazy loading si IntersectionObserver está disponible
  if ('IntersectionObserver' in window) {
    const observer = new IntersectionObserver((entries) => {
      entries.forEach(entry => {
        if (entry.isIntersecting) {
          img.src = src;
          observer.unobserve(img);
        }
      });
    }, {
      rootMargin,
      threshold
    });
    
    observer.observe(img);
  } else {
    // Fallback para navegadores que no soportan IntersectionObserver
    img.src = src;
  }
}

/**
 * Reduce la calidad de imagen según la conexión de red
 * @param {string} url - URL original
 * @returns {string} URL optimizada según la conexión
 */
export function adaptToNetworkSpeed(url) {
  if (!url) return '';
  
  // Detectar velocidad de conexión si está disponible
  if ('connection' in navigator) {
    const connection = navigator.connection;
    const effectiveType = connection.effectiveType;
    
    // Para conexiones lentas, usar menor calidad
    if (effectiveType === 'slow-2g' || effectiveType === '2g') {
      return getOptimizedImageUrl(url, 'thumbnail');
    } else if (effectiveType === '3g') {
      return getOptimizedImageUrl(url, 'card');
    }
  }
  
  // Por defecto, usar calidad estándar
  return getOptimizedImageUrl(url, 'card');
}

/**
 * Genera una URL optimizada parametrizada por ancho/calidad/formato.
 * Mantener en utils para reutilizarla en componentes y precargas.
 */
export function generateOptimizedUrl(src, width, quality, format = null) {
  if (!src) return '';
  // Si es absoluta, devolver tal cual (el servidor remoto debería manejar cache)
  if (src.startsWith('http://') || src.startsWith('https://')) return src;

  const url = new URL(src, window.location.origin);
  if (width) url.searchParams.set('w', String(width));
  if (quality) url.searchParams.set('q', String(quality));
  if (format) url.searchParams.set('f', format);
  return url.toString();
}