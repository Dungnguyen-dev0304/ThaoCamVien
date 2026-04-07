# API-First UX Spec

## 1) ViewModel State Machine per screen

All screens must implement these states:

- `Loading`: show skeleton content; no blank canvas.
- `Success`: show bound API data.
- `Empty`: no data from API (200 + empty payload).
- `Error`: network/timeout/server error with retry CTA.

### Home
- Data: `HomeFeed`, `Weather`
- Empty condition: no cards in both `HappenNow` and `Recommended`
- Error CTA: retry + open map fallback

### Map
- Data: `PoiClusters`, `NearbyAnimals`
- Empty condition: no POI in viewport
- Error CTA: retry + keep cached map

### StoryAudio
- Data: `Voices`, `LyricsTimeline`
- Empty condition: no script or timeline returned
- Error CTA: retry + switch studio audio/TTS fallback

### QR
- Data: `QrLookup`
- Empty condition: code not found (404 / null)
- Error CTA: retry scan + manual input

### About
- Data: `AboutSections`
- Empty condition: sections list empty
- Error CTA: retry + show contact fallback

## 2) Minimum endpoints

- `GET /api/mobile/home-feed`
- `GET /api/mobile/weather/current`
- `GET /api/mobile/pois/clusters?z={zoom}&bbox={bbox}`
- `GET /api/mobile/pois/nearby?lat={lat}&lng={lng}&radius={m}`
- `GET /api/mobile/tts/voices?lang={lang}`
- `GET /api/mobile/audio/lyrics-timeline?poiId={id}&voiceId={id}`
- `POST /api/mobile/qr/lookup`
- `GET /api/mobile/about/sections`

## 3) Skeleton component catalog

- `SkeletonBlock`: generic animated block for text/cards/chips
- `StateContainer`: wraps success content with loading/empty/error overlays
- Recommended compositions:
  - `HomeHeaderSkeleton`
  - `HomeHorizontalCardSkeleton`
  - `MapNearbyListSkeleton`
  - `StoryAudioSkeleton`
  - `AboutAccordionSkeleton`
