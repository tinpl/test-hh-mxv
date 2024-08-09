curl --insecure -X 'POST' \
  'https://localhost:7260/SearchRoutes/Search' \
  -H 'accept: text/plain' \
  -H 'Content-Type: application/json' \
  -d '{
  "origin": "location-from",
  "destination": "location-to",
  "originDateTime": "2024-05-05T13:43:43.131Z",
  "filters": {
    "destinationDateTime": "2024-05-15T13:43:43.131Z",
    "maxPrice": 1000,
    "minTimeLimit": "2024-05-20T13:43:43.131Z",
    "onlyCached": false
  }
}'

curl --insecure -X 'POST' \
  'https://localhost:7260/SearchRoutes/Search' \
  -H 'accept: text/plain' \
  -H 'Content-Type: application/json' \
  -d '{
  "origin": "location-from",
  "destination": "location-to",
  "originDateTime": "2024-05-065T13:43:43.131Z",
  "filters": {
    "destinationDateTime": "2024-05-15T13:43:43.131Z",
    "maxPrice": 1000,
    "minTimeLimit": "2024-05-20T13:43:43.131Z",
    "onlyCached": false
  }
}'

curl --insecure -X 'POST' \
  'https://localhost:7260/SearchRoutes/Search' \
  -H 'accept: text/plain' \
  -H 'Content-Type: application/json' \
  -d '{
  "origin": "location-from",
  "destination": "location-to",
  "originDateTime": "2024-06-05T13:43:43.131Z",
  "filters": {
    "destinationDateTime": "2024-06-15T13:43:43.131Z",
    "maxPrice": 1000,
    "minTimeLimit": "2024-05-20T13:43:43.131Z",
    "onlyCached": false
  }
}'

# cached
curl --insecure -X 'POST' \
  'https://localhost:7260/SearchRoutes/Search' \
  -H 'accept: text/plain' \
  -H 'Content-Type: application/json' \
  -d '{
  "origin": "location-from",
  "destination": "location-to",
  "originDateTime": "2024-05-05T13:43:43.131Z",
  "filters": {
    "destinationDateTime": "2024-05-15T13:43:43.131Z",
    "maxPrice": 8888,
    "minTimeLimit": "2024-05-20T13:43:43.131Z",
    "onlyCached": true
  }
}'