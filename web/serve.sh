#!/bin/sh
# Local test server for web/dist (plain static server: the COI service worker
# supplies COOP/COEP on the first load + reload). http://localhost:8000/
cd "$(dirname "$0")/dist" && exec python3 -m http.server "${1:-8000}"
