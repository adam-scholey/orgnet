# OrgNet K6 Load & Performance Tests

## Prerequisites

Install K6: https://k6.io/docs/getting-started/installation/

```powershell
winget install k6 --source winget
```

## Test Suites

### 1. Load Test (`load-test.js`)
Simulates realistic multi-tenant usage with 5 organisations and up to 100 concurrent users.

**What it tests:**
- Health check endpoint
- Login by email (authentication throughput)
- Tenant info & member listing
- Chat: send messages + read history
- File Storage: upload encrypted files + list
- Notes: create + list
- Task Board: create + list + summary
- Announcements: list
- Audit logs: paginated read
- Device listing

**Thresholds:**
- 95th percentile response time < 500ms
- 99th percentile response time < 1000ms
- Error rate < 5%
- Login p95 < 800ms

```powershell
k6 run tests/k6/load-test.js
```

### 2. Spike Test (`spike-test.js`)
Simulates sudden burst from 5 to 200 users (e.g. company-wide login at 9am).

**Thresholds:**
- 95th percentile < 2000ms (allows degradation under spike)
- Error rate < 15%

```powershell
k6 run tests/k6/spike-test.js
```

### 3. Stress Test (`stress-test.js`)
Gradually increases load from 20 to 250 concurrent users across 10 organisations to find the breaking point.

**Workload mix:** 70% reads, 30% writes

**Thresholds:**
- 95th percentile < 3000ms
- Error rate < 30% (expected to degrade at high load)

```powershell
k6 run tests/k6/stress-test.js
```

## Custom API URL

All tests default to `http://localhost:5100`. Override with:

```powershell
k6 run -e BASE_URL=http://your-server:5100 tests/k6/load-test.js
```

## Interpreting Results

| Metric | Good | Warning | Critical |
|--------|------|---------|----------|
| `http_req_duration p(95)` | < 200ms | 200-500ms | > 500ms |
| `errors` rate | < 1% | 1-5% | > 5% |
| `login_duration p(95)` | < 300ms | 300-800ms | > 800ms |
| `api_duration p(95)` | < 100ms | 100-300ms | > 300ms |

## Architecture Notes

- Each test creates its own test organisations during `setup()` — no pre-existing data needed
- Multi-tenant isolation is verified by using different tenant tokens per VU
- SignalR real-time features are not tested by K6 (use dedicated WebSocket tools for that)
- File uploads use small base64 payloads to focus on API throughput, not bandwidth
