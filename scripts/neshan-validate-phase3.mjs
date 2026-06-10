/**
 * Phase 3 API Integration Validation runner.
 * Run: node scripts/neshan-validate-phase3.mjs
 */

import { createHash } from 'node:crypto';
import { writeFileSync, mkdirSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { searchPlaces } from './neshan-search.mjs';

const __dirname = dirname(fileURLToPath(import.meta.url));
const SAMPLES_DIR = join(__dirname, '..', 'docs', 'samples');
const REPORT_PATH = join(__dirname, '..', 'docs', 'samples', 'phase3-validation-report.json');

const API_KEY = process.env.NESHAN_API_KEY ?? 'service.4b8e68ce91224ce691fad00b6133b2a5';
const STATIC_MAP_KEY = process.env.NESHAN_STATIC_MAP_KEY ?? API_KEY;

const TEHRAN_LOCATIONS = [
  { name: 'center', lat: 35.6892, lng: 51.389 },
  { name: 'north', lat: 35.724, lng: 51.405 },
  { name: 'azadi', lat: 35.6997, lng: 51.3381 },
  { name: 'mirdamad', lat: 35.76, lng: 51.433 },
  { name: 'west', lat: 35.7, lng: 51.365 },
];

const CATEGORIES = [
  { code: 'metro', terms: ['ایستگاه مترو', 'مترو'] },
  { code: 'brt', terms: ['ایستگاه BRT', 'اتوبوس تندرو'] },
  { code: 'bus', terms: ['ایستگاه اتوبوس'] },
  { code: 'school', terms: ['مدرسه', 'دبستان', 'دبیرستان'] },
  { code: 'university', terms: ['دانشگاه', 'دانشکده'] },
  { code: 'hospital', terms: ['بیمارستان'] },
  { code: 'clinic', terms: ['درمانگاه', 'کلینیک'] },
  { code: 'pharmacy', terms: ['داروخانه'] },
  { code: 'shoppingCenter', terms: ['مرکز خرید', 'مجتمع تجاری'] },
  { code: 'supermarket', terms: ['سوپرمارکت', 'هایپرمارکت'] },
  { code: 'park', terms: ['پارک', 'بوستان'] },
  { code: 'gym', terms: ['باشگاه ورزشی', 'سالن ورزشی'] },
  { code: 'bank', terms: ['بانک', 'شعبه بانک'] },
  { code: 'mosque', terms: ['مسجد'] },
  { code: 'governmentOffice', terms: ['اداره', 'دفتر پیشخوان'] },
];

function normalizeText(value) {
  return (value ?? '')
    .normalize('NFC')
    .replace(/[\u200B-\u200D\uFEFF]/g, '')
    .trim()
    .replace(/\s+/g, ' ')
    .toLowerCase();
}

function roundCoord(n) {
  return Math.round(n * 1e6) / 1e6;
}

export function computePoiFingerprint({ title, category, latitude, longitude, address }) {
  const input = [
    normalizeText(title),
    normalizeText(category),
    roundCoord(latitude).toFixed(6),
    roundCoord(longitude).toFixed(6),
    normalizeText(address),
  ].join('|');
  return createHash('sha256').update(input, 'utf8').digest('hex');
}

function auditFields(items) {
  const allKeys = new Set();
  const nestedKeys = new Set();
  for (const item of items) {
    for (const k of Object.keys(item)) allKeys.add(k);
    if (item.location) {
      for (const k of Object.keys(item.location)) nestedKeys.add(`location.${k}`);
    }
  }
  const idLike = [...allKeys, ...nestedKeys].filter((k) =>
    /^(id|place_?id|poi_?id|uuid|guid)$/i.test(k.split('.').pop()),
  );
  return {
    topLevelFields: [...allKeys].sort(),
    locationFields: [...nestedKeys].sort(),
    hasStableId: idLike.length > 0,
    idLikeFields: idLike,
  };
}

function tehranRelevant(item) {
  const region = item.region ?? '';
  return region.includes('تهران');
}

async function testStaticMap() {
  const url = new URL('https://api.neshan.org/v5/static');
  url.searchParams.set('style', 'light');
  url.searchParams.set('zoom', '15');
  url.searchParams.set('latitude', '35.6892');
  url.searchParams.set('longitude', '51.3890');
  url.searchParams.set('width', '400');
  url.searchParams.set('height', '300');
  url.searchParams.set('marker', 'red');

  const headerRes = await fetch(url, {
    headers: { 'Api-Key': STATIC_MAP_KEY, Accept: 'image/png' },
  });

  const queryUrl = new URL(url);
  queryUrl.searchParams.set('key', STATIC_MAP_KEY);
  const queryRes = await fetch(queryUrl, { headers: { Accept: 'image/png' } });

  return {
    headerAuth: {
      status: headerRes.status,
      contentType: headerRes.headers.get('content-type'),
      ok: headerRes.ok,
      bodyPreview: headerRes.ok ? 'image/png binary' : await headerRes.text(),
    },
    queryKeyAuth: {
      status: queryRes.status,
      contentType: queryRes.headers.get('content-type'),
      ok: queryRes.ok,
      bodyPreview: queryRes.ok ? 'image/png binary' : await queryRes.text(),
    },
  };
}

async function rateLimitProbe() {
  const results = [];
  const start = Date.now();
  let errors482 = 0;
  const maxRequests = 25;

  for (let i = 0; i < maxRequests; i++) {
    const t0 = Date.now();
    try {
      await searchPlaces('restaurant', 35.6892, 51.389, { apiKey: API_KEY });
      results.push({ i: i + 1, ms: Date.now() - t0, ok: true });
    } catch (err) {
      results.push({
        i: i + 1,
        ms: Date.now() - t0,
        ok: false,
        code: err.code,
        httpStatus: err.httpStatus,
      });
      if (err.code === 482) errors482++;
      if (err.code === 482 || err.code === 481) break;
    }
  }

  return {
    totalRequests: results.length,
    durationMs: Date.now() - start,
    errors482,
    lastResults: results.slice(-5),
    requestsPerMinuteEstimate: Math.round((results.length / (Date.now() - start)) * 60000),
  };
}

async function categoryMatrix() {
  const matrix = [];
  const loc = TEHRAN_LOCATIONS[0];

  for (const cat of CATEGORIES) {
    for (const term of cat.terms) {
      try {
        const result = await searchPlaces(term, loc.lat, loc.lng, { apiKey: API_KEY });
        const tehranCount = result.items.filter(tehranRelevant).length;
        matrix.push({
          category: cat.code,
          term,
          count: result.count,
          tehranRelevant: tehranCount,
          sampleTypes: [...new Set(result.items.slice(0, 10).map((i) => i.type))],
        });
        await sleep(150);
      } catch (err) {
        matrix.push({
          category: cat.code,
          term,
          error: err.code ?? err.message,
        });
      }
    }
  }
  return matrix;
}

function sleep(ms) {
  return new Promise((r) => setTimeout(r, ms));
}

async function main() {
  mkdirSync(SAMPLES_DIR, { recursive: true });
  const report = { generatedAt: new Date().toISOString() };

  console.log('P3-T1: API key validation (Search, header auth)...');
  const restaurant = await searchPlaces('restaurant', 35.6892, 51.389, { apiKey: API_KEY });
  writeFileSync(join(SAMPLES_DIR, 'search-restaurant-header.json'), JSON.stringify(restaurant, null, 2));
  report.searchKeyValid = true;
  report.searchCount = restaurant.count;

  console.log('P3-T5: Field audit...');
  report.fieldAudit = auditFields(restaurant.items);
  report.fieldAudit.locationZSample = restaurant.items[0]?.location?.z;

  console.log('P3-T4: Category term matrix (center Tehran)...');
  report.categoryMatrix = await categoryMatrix();

  console.log('P3-T6: Fingerprint test vectors...');
  const item = restaurant.items.find((i) => i.type === 'restaurant') ?? restaurant.items[0];
  const fp1 = computePoiFingerprint({
    title: item.title,
    category: item.type,
    latitude: item.location.y,
    longitude: item.location.x,
    address: item.address,
  });
  const fp2 = computePoiFingerprint({
    title: item.title,
    category: item.type,
    latitude: item.location.y,
    longitude: item.location.x,
    address: item.address,
  });
  const fpCollision = computePoiFingerprint({
    title: 'Test POI A',
    category: 'restaurant',
    latitude: 35.6892,
    longitude: 51.389,
    address: 'Addr A',
  });
  const fpDifferent = computePoiFingerprint({
    title: 'Test POI B',
    category: 'restaurant',
    latitude: 35.6892,
    longitude: 51.389,
    address: 'Addr B',
  });
  report.fingerprint = {
    deterministic: fp1 === fp2,
    sample: fp1,
    collisionDifferentAddress: fpCollision !== fpDifferent,
    vectors: [
      { label: 'restaurant sample', fingerprint: fp1 },
      { label: 'collision test A', fingerprint: fpCollision },
      { label: 'collision test B', fingerprint: fpDifferent },
    ],
  };

  console.log('P3-T7: Static Map API...');
  report.staticMap = await testStaticMap();

  console.log('P3-T3: Rate limit probe (25 rapid requests)...');
  report.rateLimit = await rateLimitProbe();

  console.log('P3-T8: Error mapping sample (invalid coords)...');
  try {
    await searchPlaces('x', 999, 999, { apiKey: API_KEY });
    report.invalidCoordError = null;
  } catch (err) {
    report.invalidCoordError = {
      code: err.code,
      httpStatus: err.httpStatus,
      message: err.message,
    };
  }

  writeFileSync(REPORT_PATH, JSON.stringify(report, null, 2));
  console.log(`\nReport saved: ${REPORT_PATH}`);
  console.log(JSON.stringify(report, null, 2));
}

if (process.argv[1] === fileURLToPath(import.meta.url)) {
  main().catch((err) => {
    console.error(err);
    process.exitCode = 1;
  });
}
