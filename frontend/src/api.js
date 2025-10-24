// src/api.js
const API_BASE = import.meta.env.VITE_API_BASE || 'http://localhost:5000';
const API_KEY = import.meta.env.VITE_BACK_API_KEY;

async function j(url, { method = 'GET', headers = {}, body, auth = false, timeout = 5000 } = {}) {
  const ctrl = new AbortController();
  const t = setTimeout(() => ctrl.abort(), timeout);

  // Compose auth headers (support both Authorization and x-api-key just in case)
  const authHeaders = (auth && API_KEY)
    ? { Authorization: `Bearer ${API_KEY}`, 'x-api-key': API_KEY }
    : {};
  if (auth && !API_KEY) {
    console.warn('Falta VITE_BACK_API_KEY: la petición autenticada podría fallar con 401');
  }

  const res = await fetch(url, {
    method,
    headers: {
      'Accept': 'application/json',
      ...(body ? { 'Content-Type': 'application/json' } : {}),
      ...authHeaders,
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
export async function getProperties(page = 1, limit = 25, afterId = null) {
  try {
  console.log(`Usando endpoint rápido - Página ${page}, Límite ${limit}`);
    
    // Construir URL con parámetros (sin 'estado')
    const params = new URLSearchParams({
      limit: limit.toString()
    });
    
    // Para paginación cursor-based
    if (afterId) {
      params.set('afterId', afterId.toString());
    }
    
  const base = API_BASE.replace(/\/$/, '');
  const url = `${base}/api/propiedades/miraiz?${params.toString()}`;
    console.log('Request URL:', url);
    
    const start = Date.now();
    const r = await j(url);
    const latency = Date.now() - start;
    
  console.log('Respuesta rápida completa:', r);
    
    if (!r?.success) {
  console.warn('Respuesta fallida:', r);
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
    const serverCursor = r.cursor; // cursor que devuelva el backend (puede ser null)
    // Fallback de cursor si el backend no lo envía: usamos el id del último elemento
    const fallbackCursor = (!serverCursor && Array.isArray(properties) && properties.length === limit)
      ? properties[properties.length - 1]?.id ?? null
      : null;
    const effectiveCursor = serverCursor ?? fallbackCursor;
    // Hay más si existe cursor de servidor o si completó el lote exacto
    const hasMore = (effectiveCursor !== null && effectiveCursor !== undefined) || (properties.length === limit);
    
  console.log('Propiedades obtenidas:', properties.length);
  console.log(`Latencia: ${latency}ms`);
    
    // Simular paginación tradicional para compatibilidad con el componente
    const pagination = {
      total: null, // No conocemos el total con cursor-based
      totalPages: null, // No calculable sin total
      currentPage: page,
      limit: limit,
      hasNext: hasMore,
      hasPrev: page > 1,
      cursor: effectiveCursor // Para siguiente request (server o fallback por último id)
    };
    
    return { 
      success: true, 
      data: properties,
      nlqAnswer: `Mostrando ${properties.length} propiedades disponibles`, // Mensaje simple
      latency: latency,
      pagination: pagination
    };
    
  } catch (error) {
  console.error('Error en getProperties (endpoint rápido):', error);
    throw error;
  }
}

// === Catálogo completo (cliente pagina) ===
export async function getAllProperties(batchLimit = 200) {
  // Descarga todo el catálogo en lotes y lo devuelve en un solo arreglo
  // Paginará el front; útil cuando queremos todas las imágenes/datos desde inicio
  const all = [];
  let cursor = null;
  let page = 1;
  while (true) {
    const r = await getProperties(page, batchLimit, cursor);
    if (!r?.success) break;
    const data = Array.isArray(r.data) ? r.data : [];
    all.push(...data);
    cursor = r?.pagination?.cursor ?? null;
    const hasNext = !!cursor || !!r?.pagination?.hasNext;
    page += 1;
    if (!hasNext) break;
  }
  return { success: true, data: all };
}

// === Interacciones (NLQ) - sin autenticación y sin userId ===
export async function postInteraction({ pregunta, propiedadId = null }) {
  if (!pregunta) throw new Error('Falta "pregunta" para la interacción');
  const base = (import.meta.env.VITE_API_BASE || API_BASE).replace(/\/$/, '');
  const url = `${base}/interactions`;
  const body = { pregunta };
  if (propiedadId) body.propiedadId = propiedadId;
  return j(url, { method: 'POST', body, auth: false, timeout: 5000 });
}

// Normalizador para NLQ en frontend
export async function askNLQ(query, opts = {}) {
  try {
    // No se envía userId ni API Key
    const r = await postInteraction({ pregunta: query });
    // Se espera estructura: { success, answer, toolPayload? }
    return {
      success: !!r?.success,
      answer: r?.answer || null,
      toolPayload: r?.toolPayload || null
    };
  } catch (e) {
  console.error('Error en askNLQ:', e);
    return { success: false, answer: null, toolPayload: null, error: e?.message };
  }
}

export default { getProperties, askNLQ, postInteraction };