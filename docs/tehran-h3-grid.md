# Tehran H3 Crawl Grid



Standard H3 **polyfill** of `tehran.bounds` at the resolution and search radius that:



1. Covers the full bounding box (overlap validation on corners, edges, and random samples).

2. Keeps total Neshan requests between **10,000 and 15,000** per full crawl (all categories).

3. Uses the **minimum** search radius that still passes overlap (less repetition between cells).



## Configuration



| Key | Default | Meaning |

|-----|---------|---------|

| `tehran.bounds` | `35.50–35.88°N, 51.10–51.62°E` | Tehran municipality envelope |

| `crawl.h3_resolution` | `auto` | `auto` picks budget-optimal resolution, or set `6`/`7`/`8` explicitly |

| `search.radius.default_meters` | `2100` | Search radius; auto-updated by planner on reseed |

| `crawl.h3_reseed_on_startup` | `false` | Rebuild grid when resolution/cell count differs from plan |



## Algorithm (`H3GridPlanner`)



1. Polyfill `tehran.bounds` using standard H3 `Fill` (Uber polygon-to-cells).

2. Count enabled category search terms from DB (default **27**).

3. For each resolution **7 → 6 → 8**, find the **minimum** radius (1,500–4,500 m, step 50 m) that passes overlap validation.

4. Pick the candidate whose `cells × search_terms` falls in **10k–15k**, preferring:

   - Closest to 12,500 requests (midpoint)

   - Smallest radius (least overlap)

   - Resolution 7 when tied



## Tehran cell counts (current bounds)



| Resolution | Cells | Min overlap radius | Requests/crawl (27 terms) |

|------------|-------|--------------------|---------------------------|

| 6 | 52 | ~4,500 m | ~1,404 (below budget) |

| **7** | **373** | **2,100 m** | **~10,071 (auto-selected)** |

| 8 | 2,610 | ~1,500 m | ~70,470 (above budget) |



Resolution 7 with a 2,100 m search radius is the sweet spot: full coverage with ~10k API calls and modest overlap between adjacent cell centroids.



## API cost estimate



With 15 categories × 27 search terms:



| Grid | Cells | Requests per full crawl |

|------|-------|-------------------------|

| **Auto (res 7)** | **373** | **~10,071** |

| Legacy loose bbox res 7 | 473 | ~12,771 |

| Legacy res 8 | 3,307 | ~89,289 |



## Reseed after plan change



`auto` mode self-heals on startup when the DB grid does not match the planned grid.



To force a rebuild manually:



```sql

UPDATE system_configuration SET config_value = 'true' WHERE config_key = 'crawl.h3_reseed_on_startup';

```



Restart API/Worker, then set `crawl.h3_reseed_on_startup` back to `false`.



To update bounds on an existing database:



```sql

UPDATE system_configuration

SET config_value = '{"minLat":35.50,"maxLat":35.88,"minLng":51.10,"maxLng":51.62}'

WHERE config_key = 'tehran.bounds';

```

