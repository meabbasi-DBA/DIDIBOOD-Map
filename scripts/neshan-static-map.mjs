/**
 * Neshan Static Map API client (v5/static).
 * Auth: key query parameter only — never headers.
 *
 * Usage:
 *   node scripts/neshan-static-map.mjs
 *   NESHAN_API_KEY=your-key node scripts/neshan-static-map.mjs
 */

import { writeFile } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';

export const STATIC_MAP_BASE_URL = 'https://api.neshan.org/v5/static';

export const API_KEY =
  process.env.NESHAN_API_KEY ??
  process.env.NESHAN_STATIC_MAP_API_KEY ??
  'service.4b8e68ce91224ce691fad00b6133b2a5';

const ERROR_MESSAGES = {
  400: 'Invalid parameters',
  470: 'Invalid coordinates',
  480: 'Missing or invalid API key',
  481: 'Quota exceeded',
  482: 'Rate limit exceeded',
  483: 'Invalid key type',
  484: 'Whitelist error',
  485: 'Service mismatch',
  500: 'Generic server error',
  503: 'Timeout or service overload',
};

/**
 * @typedef {Object} StaticMapParams
 * @property {'light'|'dark'} style
 * @property {number} zoom
 * @property {number} latitude
 * @property {number} longitude
 * @property {number} width
 * @property {number} height
 * @property {string} [marker]
 * @property {string} [apiKey]
 */

/**
 * @param {StaticMapParams} params
 * @returns {string}
 */
export function getStaticMapUrl(params) {
  validateStaticMapParams(params);

  const apiKey = params.apiKey ?? API_KEY;
  const url = new URL(STATIC_MAP_BASE_URL);

  url.searchParams.set('key', apiKey);
  url.searchParams.set('style', params.style);
  url.searchParams.set('zoom', String(params.zoom));
  url.searchParams.set('latitude', String(params.latitude));
  url.searchParams.set('longitude', String(params.longitude));
  url.searchParams.set('width', String(params.width));
  url.searchParams.set('height', String(params.height));

  if (params.marker) {
    url.searchParams.set('marker', params.marker);
  }

  return url.toString();
}

/**
 * @param {StaticMapParams} params
 * @param {RequestInit} [fetchOptions]
 * @returns {Promise<Buffer>}
 */
export async function fetchStaticMap(params, fetchOptions = {}) {
  const url = getStaticMapUrl(params);
  const response = await fetch(url, {
    method: 'GET',
    ...fetchOptions,
  });

  if (!response.ok) {
    throw await createStaticMapError(response);
  }

  const contentType = response.headers.get('content-type') ?? '';
  if (!contentType.startsWith('image/')) {
    const text = await response.text();
    throw new Error(`Expected image response, got ${contentType}: ${text.slice(0, 200)}`);
  }

  const arrayBuffer = await response.arrayBuffer();
  return Buffer.from(arrayBuffer);
}

/**
 * @param {StaticMapParams} params
 */
export function validateStaticMapParams(params) {
  if (!params || typeof params !== 'object') {
    throw new Error('params object is required');
  }

  const required = ['style', 'zoom', 'latitude', 'longitude', 'width', 'height'];
  for (const field of required) {
    if (params[field] === undefined || params[field] === null || params[field] === '') {
      throw new Error(`${field} is required`);
    }
  }

  if (params.style !== 'light' && params.style !== 'dark') {
    throw new Error("style must be 'light' or 'dark'");
  }

  const zoom = Number(params.zoom);
  if (!Number.isFinite(zoom) || zoom < 5 || zoom > 19) {
    throw new Error('zoom must be between 5 and 19');
  }

  const latitude = Number(params.latitude);
  if (!Number.isFinite(latitude) || latitude < -90 || latitude > 90) {
    throw new Error('latitude must be between -90 and 90');
  }

  const longitude = Number(params.longitude);
  if (!Number.isFinite(longitude) || longitude < -180 || longitude > 180) {
    throw new Error('longitude must be between -180 and 180');
  }

  const width = Number(params.width);
  if (!Number.isInteger(width) || width < 1 || width > 2048) {
    throw new Error('width must be an integer between 1 and 2048');
  }

  const height = Number(params.height);
  if (!Number.isInteger(height) || height < 1 || height > 2048) {
    throw new Error('height must be an integer between 1 and 2048');
  }
}

/**
 * @param {Response} response
 * @returns {Promise<Error>}
 */
async function createStaticMapError(response) {
  const status = response.status;
  let code = status;
  let message = ERROR_MESSAGES[status] ?? `HTTP ${status}`;

  const contentType = response.headers.get('content-type') ?? '';
  if (contentType.includes('application/json')) {
    try {
      const body = await response.json();
      if (body?.code) code = body.code;
      if (body?.message) message = body.message;
      else if (ERROR_MESSAGES[code]) message = ERROR_MESSAGES[code];
    } catch {
      // keep defaults
    }
  } else {
    const text = await response.text();
    if (text) message = `${message}: ${text.slice(0, 200)}`;
  }

  const err = new Error(`Neshan Static Map error ${code}: ${message}`);
  err.code = code;
  err.httpStatus = status;
  return err;
}

async function main() {
  const sample = {
    style: 'light',
    zoom: 15,
    latitude: 32.657307,
    longitude: 51.677579,
    width: 500,
    height: 500,
    marker: 'red',
  };

  const url = getStaticMapUrl(sample);
  console.log('URL:', url);

  const buffer = await fetchStaticMap(sample);
  console.log('Downloaded bytes:', buffer.length);

  if (process.argv[1] === fileURLToPath(import.meta.url)) {
    const out = 'docs/samples/static-map-sample.png';
    await writeFile(out, buffer);
    console.log('Saved:', out);
  }
}

if (process.argv[1] === fileURLToPath(import.meta.url)) {
  main().catch((err) => {
    console.error(err.message ?? err);
    process.exit(1);
  });
}
