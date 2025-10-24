// src/utils/images.js
// Base para imágenes. Si no está configurado, usaremos VITE_API_BASE como respaldo.
const IMAGE_BASE = (import.meta.env.VITE_IMAGE_BASE || '').replace(/\/$/, '');
const API_BASE_FALLBACK = (import.meta.env.VITE_API_BASE || '').replace(/\/$/, '');

// Cache para imágenes precargadas
const imageCache = new Map();
const preloadQueue = new Set();

/**
 * Precarga una imagen y la guarda en cache
 * @param {string} url - URL de la imagen
 * @returns {Promise<boolean>} - true si se cargó correctamente
 */
export function preloadImage(url) {
  if (imageCache.has(url)) {
    return Promise.resolve(true);
  }
  
  if (preloadQueue.has(url)) {
    return imageCache.get(url);
  }
  
  preloadQueue.add(url);
  
  const promise = new Promise((resolve) => {
    const img = new Image();
    
    const cleanup = () => {
      preloadQueue.delete(url);
    };
    
    img.onload = () => {
      imageCache.set(url, true);
      cleanup();
      resolve(true);
    };
    
    img.onerror = () => {
      imageCache.set(url, false);
      cleanup();
      resolve(false);
    };
    
    // Timeout después de 3 segundos
    setTimeout(() => {
      cleanup();
      resolve(false);
    }, 3000);
    
    img.src = url;
  });
  
  imageCache.set(url, promise);
  return promise;
}

/**
 * Verifica si una imagen está en cache
 * @param {string} url - URL de la imagen
 * @returns {boolean|Promise} - true/false si está cargada, Promise si está cargando
 */
export function isImageCached(url) {
  const entry = imageCache.get(url);
  return entry === true; // solo true explícito indica que ya cargó
}

/**
 * Obtiene la URL final de una imagen, manejando rutas relativas y absolutas
 */
export function getImageUrl(url) {
  if (!url) return '';
  // Si ya es absoluta (http/https), úsala tal cual
  if (/^https?:\/\//i.test(url)) return url;
  // Normalizar para asegurar un solo '/'
  const path = url.startsWith('/') ? url : `/${url}`;
  // Si es relativa ("/mvp-props/images/..."), prepender base conocida
  if (IMAGE_BASE) return `${IMAGE_BASE}${path}`;
  if (API_BASE_FALLBACK) return `${API_BASE_FALLBACK}${path}`;
  // Último recurso: devolver relativa al origen actual (puede 404 si el host no sirve imágenes)
  return path;
}