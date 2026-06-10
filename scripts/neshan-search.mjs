/**
 * Neshan Search API client — header auth only (never query string).
 *
 * @example
 * import { NeshanSearchClient } from './neshan-search.mjs';
 * const client = new NeshanSearchClient();
 * const results = await client.search('restaurant', 35.6892, 51.3890);
 */

import { fileURLToPath } from 'node:url';

export const NESHAN_SEARCH_URL = 'https://api.neshan.org/v1/search';

export const API_KEY =
  process.env.NESHAN_API_KEY ??
  process.env.NESHAN_LOCATION_API_KEY ??
  'service.4b8e68ce91224ce691fad00b6133b2a5';

const ERROR_MESSAGES = {
  400: 'Invalid request parameters',
  470: 'Invalid coordinates',
  480: 'Missing or invalid API key',
  481: 'Usage limit exceeded',
  482: 'Rate limit exceeded',
  483: 'Invalid API key type',
  484: 'API whitelist restriction',
  485: 'API service restriction',
  500: 'Internal server error',
  503: 'Service unavailable',
};

/**
 * @typedef {Object} Location
 * @property {number} x longitude
 * @property {number} y latitude
 * @property {string} [z]
 */

/**
 * @typedef {Object} SearchItem
 * @property {string} title
 * @property {string} [address]
 * @property {string} [neighbourhood]
 * @property {string} [region]
 * @property {string} [type]
 * @property {string} [category]
 * @property {Location} location
 */

/**
 * @typedef {Object} SearchResponse
 * @property {number} totalCount
 * @property {SearchItem[]} items
 */

export class NeshanSearchError extends Error {
  /**
   * @param {number} code
   * @param {string} message
   * @param {number} httpStatus
   */
  constructor(code, message, httpStatus) {
    super(`Neshan Search error ${code}: ${message}`);
    this.name = 'NeshanSearchError';
    this.code = code;
    this.httpStatus = httpStatus;
  }
}

export class NeshanSearchClient {
  /**
   * @param {{ apiKey?: string, baseUrl?: string, timeoutMs?: number, logger?: Console }} [options]
   */
  constructor(options = {}) {
    this.apiKey = options.apiKey ?? API_KEY;
    this.baseUrl = options.baseUrl ?? NESHAN_SEARCH_URL;
    this.timeoutMs = options.timeoutMs ?? 10_000;
    this.logger = options.logger ?? console;
  }

  /**
   * @param {string} term
   * @param {number} lat
   * @param {number} lng
   * @returns {Promise<SearchResponse>}
   */
  async search(term, lat, lng) {
    validateSearchInput(term, lat, lng);
    const url = buildSearchUrl(this.baseUrl, term, lat, lng);
    this.logger.info?.(`Neshan Search request URL: ${url}`);

    let lastNetworkError;
    for (let attempt = 0; attempt < 2; attempt++) {
      if (attempt > 0)
        this.logger.warn?.(`Retrying Neshan Search after network failure (attempt ${attempt + 1})`);

      const started = Date.now();
      try {
        const response = await fetchWithTimeout(url, {
          method: 'GET',
          headers: {
            'Api-Key': this.apiKey,
            Accept: 'application/json',
          },
        }, this.timeoutMs);

        const elapsed = Date.now() - started;
        this.logger.info?.(`Neshan Search completed in ${elapsed}ms with HTTP ${response.status}`);

        if (!response.ok) {
          throw await createSearchError(response);
        }

        const body = await response.json();
        return normalizeSearchResponse(body);
      } catch (error) {
        if (error instanceof NeshanSearchError) throw error;
        lastNetworkError = error;
      }
    }

    throw new NeshanSearchError(503, lastNetworkError?.message ?? 'Network failure', 503);
  }
}

/**
 * @param {string} term
 * @param {number} lat
 * @param {number} lng
 * @param {{ apiKey?: string }} [options]
 * @returns {Promise<{ count: number, items: SearchItem[] }>}
 */
export async function searchPlaces(term, lat, lng, options = {}) {
  const client = new NeshanSearchClient({ apiKey: options.apiKey });
  const result = await client.search(term, lat, lng);
  return { count: result.totalCount, items: result.items };
}

/**
 * @param {string} baseUrl
 * @param {string} term
 * @param {number} lat
 * @param {number} lng
 */
export function buildSearchUrl(baseUrl, term, lat, lng) {
  const url = new URL(baseUrl);
  url.searchParams.set('term', term);
  url.searchParams.set('lat', String(lat));
  url.searchParams.set('lng', String(lng));
  return url.toString();
}

/**
 * @param {string} term
 * @param {number} lat
 * @param {number} lng
 */
export function validateSearchInput(term, lat, lng) {
  if (!term?.trim()) throw new Error('term is required');
  if (typeof lat !== 'number' || Number.isNaN(lat) || lat < -90 || lat > 90)
    throw new Error('lat must be between -90 and 90');
  if (typeof lng !== 'number' || Number.isNaN(lng) || lng < -180 || lng > 180)
    throw new Error('lng must be between -180 and 180');
}

/**
 * @param {any} body
 * @returns {SearchResponse}
 */
export function normalizeSearchResponse(body) {
  const items = Array.isArray(body?.items) ? body.items : [];
  const totalCount = typeof body?.count === 'number' ? body.count : items.length;
  return { totalCount, items };
}

/**
 * @param {string} url
 * @param {RequestInit} init
 * @param {number} timeoutMs
 */
async function fetchWithTimeout(url, init, timeoutMs) {
  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), timeoutMs);
  try {
    return await fetch(url, { ...init, signal: controller.signal });
  } finally {
    clearTimeout(timer);
  }
}

/**
 * @param {Response} response
 */
async function createSearchError(response) {
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
  }

  if (status >= 400 && status < 500) {
    return new NeshanSearchError(code, message, status);
  }

  return new NeshanSearchError(code, message, status);
}

/**
 * @param {SearchResponse} result
 */
export function logSearchResults(result) {
  console.log('\n--- Neshan Search Results ---');
  console.log(`Count: ${result.totalCount ?? result.items?.length ?? 0}\n`);

  if (!result.items?.length) {
    console.log('No places found.');
    return;
  }

  result.items.forEach((item, index) => {
    console.log(`${index + 1}. ${item.title}`);
    console.log(`   Type: ${item.type ?? '—'} | Category: ${item.category ?? '—'}`);
    console.log(`   Address: ${item.address ?? '—'}`);
    if (item.location?.y != null && item.location?.x != null) {
      console.log(`   Location: ${item.location.y}, ${item.location.x}`);
    }
    console.log('');
  });
}

async function main() {
  try {
    const client = new NeshanSearchClient();
    const result = await client.search('restaurant', 35.6892, 51.3890);
    logSearchResults(result);
  } catch (err) {
    if (err instanceof NeshanSearchError) {
      console.error(err.message);
    } else {
      console.error(err.message ?? err);
    }
    process.exitCode = 1;
  }
}

if (process.argv[1] === fileURLToPath(import.meta.url)) {
  main();
}
