/**
 * Neshan Search API client (header auth only).
 *
 * Usage:
 *   NESHAN_API_KEY=your-key node scripts/neshan-search.mjs
 *   node scripts/neshan-search.mjs   # uses default dev key below if env unset
 */

import { fileURLToPath } from 'node:url';

const NESHAN_SEARCH_URL = 'https://api.neshan.org/v1/search';

const DEFAULT_API_KEY = 'service.4b8e68ce91224ce691fad00b6133b2a5';

/**
 * @param {string} term
 * @param {number} lat
 * @param {number} lng
 * @param {{ apiKey?: string }} [options]
 * @returns {Promise<{ count: number, items: object[] }>}
 */
export async function searchPlaces(term, lat, lng, options = {}) {
  const apiKey =
    options.apiKey ??
    process.env.NESHAN_API_KEY ??
    process.env.NESHAN_LOCATION_API_KEY ??
    DEFAULT_API_KEY;

  if (!term?.trim()) {
    throw new Error('term is required');
  }
  if (typeof lat !== 'number' || Number.isNaN(lat) || lat < -90 || lat > 90) {
    throw new Error('lat must be a number between -90 and 90');
  }
  if (typeof lng !== 'number' || Number.isNaN(lng) || lng < -180 || lng > 180) {
    throw new Error('lng must be a number between -180 and 180');
  }

  const url = new URL(NESHAN_SEARCH_URL);
  url.searchParams.set('term', term);
  url.searchParams.set('lat', String(lat));
  url.searchParams.set('lng', String(lng));

  const response = await fetch(url, {
    method: 'GET',
    headers: {
      'Api-Key': apiKey,
      Accept: 'application/json',
    },
  });

  let body;
  const contentType = response.headers.get('content-type') ?? '';
  if (contentType.includes('application/json')) {
    body = await response.json();
  } else {
    const text = await response.text();
    throw new Error(`Unexpected response (${response.status}): ${text.slice(0, 200)}`);
  }

  if (!response.ok || body.status === 'ERROR') {
    const error = new Error(
      body.message ?? `Neshan Search API failed with HTTP ${response.status}`,
    );
    error.name = 'NeshanSearchError';
    error.httpStatus = response.status;
    error.code = body.code;
    error.response = body;
    throw error;
  }

  return body;
}

/**
 * @param {{ count: number, items: object[] }} result
 */
export function logSearchResults(result) {
  console.log('\n--- Neshan Search Results ---');
  console.log(`Count: ${result.count ?? result.items?.length ?? 0}\n`);

  if (!result.items?.length) {
    console.log('No places found.');
    return;
  }

  result.items.forEach((item, index) => {
    const lat = item.location?.y;
    const lng = item.location?.x;
    console.log(`${index + 1}. ${item.title}`);
    console.log(`   Type: ${item.type ?? '—'} | Category: ${item.category ?? '—'}`);
    console.log(`   Address: ${item.address ?? '—'}`);
    if (lat != null && lng != null) {
      console.log(`   Location: ${lat}, ${lng}`);
    }
    console.log('');
  });
}

async function main() {
  try {
    const result = await searchPlaces('restaurant', 35.6892, 51.3890);
    logSearchResults(result);
  } catch (err) {
    if (err.name === 'NeshanSearchError') {
      console.error(`Neshan error ${err.code} (HTTP ${err.httpStatus}): ${err.message}`);
    } else {
      console.error(err.message ?? err);
    }
    process.exitCode = 1;
  }
}

if (process.argv[1] === fileURLToPath(import.meta.url)) {
  main();
}
