// src/api.js
const API_BASE = import.meta.env.VITE_API_BASE || 'http://localhost:5000';
const API_FAST = 'http://localhost:5002'; // Endpoint rápido para propiedades
const API_KEY = import.meta.env.VITE_BACK_API_KEY;

async function j(url, { method = 'GET', headers = {}, body, auth = false, timeout = 5000 } = {}) {
  const ctrl = new AbortController();
  const t = setTimeout(() => ctrl.abort(), timeout);

  const res = await fetch(url, {
    method,
    headers: {
      'Accept': 'application/json',
      ...(body ? { 'Content-Type': 'application/json' } : {}),
      ...(auth ? { Authorization: `Bearer ${API_KEY}` } : {}),
      ...headers
    },
    body: body ? JSON.stringify(body) : undefined,
    signal: ctrl.signal
  }).catch((e) => { clearTimeout(t); throw e; });

  clearTimeout(t);
  if (!res.ok) throw new Error(`HTTP ${res.status}`);
  return res.json();
}

// === Catálogo (público) - Endpoint rápido ===
export async function getProperties(page = 1, limit = 12, afterId = null) {
  try {
    console.log(`⚡ Usando endpoint rápido - Página ${page}, Límite ${limit}`);
    
    // Construir URL con parámetros
    const params = new URLSearchParams({
      limit: limit.toString(),
      estado: 'disponible' // Solo propiedades disponibles
    });
    
    // Para paginación cursor-based
    if (afterId) {
      params.set('afterId', afterId.toString());
    }
    
    const url = `${API_FAST}/api/propiedades/miraiz?${params.toString()}`;
    console.log('🚀 Request URL:', url);
    
    const start = Date.now();
    const r = await j(url);
    const latency = Date.now() - start;
    
    console.log('📡 Respuesta rápida completa:', r);
    
    if (!r?.success) {
      console.warn('⚠️ Respuesta fallida:', r);
      return { 
        success: false, 
        data: [], 
        pagination: {
          total: 0,
          totalPages: 1,
          currentPage: page,
          limit: limit,
          hasNext: false,
          hasPrev: false,
          cursor: null
        }
      };
    }
    
    const properties = r.data || [];
    const cursor = r.cursor; // Para siguiente página
    const hasMore = properties.length === limit; // Si devolvió el límite completo, probablemente hay más
    
    console.log('✅ Propiedades obtenidas:', properties.length);
    console.log(`⚡ Latencia ultra-rápida: ${latency}ms`);
    
    // Simular paginación tradicional para compatibilidad con el componente
    const pagination = {
      total: null, // No conocemos el total con cursor-based
      totalPages: null, // No calculable sin total
      currentPage: page,
      limit: limit,
      hasNext: hasMore,
      hasPrev: page > 1,
      cursor: cursor // Para siguiente request
    };
    
    return { 
      success: true, 
      data: properties,
      nlqAnswer: `Mostrando ${properties.length} propiedades disponibles`, // Mensaje simple
      latency: latency,
      pagination: pagination
    };
    
  } catch (error) {
    console.error('❌ Error en getProperties (endpoint rápido):', error);
    throw error;
  }
}

// === Interacciones protegidas (NLQ) ===
export async function postInteraction({ userId = 'u-demo', pregunta, propiedadId = null }) {
  if (!pregunta) throw new Error('Falta "pregunta" para la interacción');
  const url = `${API_BASE}/interactions`;
  const body = { userId, pregunta };
  if (propiedadId) body.propiedadId = propiedadId;
  return j(url, { method: 'POST', body, auth: true, timeout: 5000 });
}

// NLQ directo (público) -> POST /api/nlq usando el texto para buscar
export async function askNLQ(query, _opts = {}) {
  try {
    const url = `${API_BASE}/api/nlq`;
    const limit = Number(_opts.limit ?? 12);
    const r = await j(url, { method: 'POST', body: { query, limit }, auth: false, timeout: 5000 });
    // Normalizar múltiples posibles formas de respuesta
    const answer = r?.answer || r?.message || null;
    const toolPayload = r?.toolPayload || (Array.isArray(r?.data) ? { data: r.data } : null);
    const success = r?.success !== undefined ? !!r.success : !!(answer || toolPayload);
    return { success, answer, toolPayload };
  } catch (e) {
    console.error('❌ Error en askNLQ /api/nlq:', e);
    return { success: false, answer: null, toolPayload: null, error: e?.message };
  }
}

export default { getProperties, askNLQ, postInteraction };