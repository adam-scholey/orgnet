import http from 'k6/http';
import { check, sleep, group } from 'k6';
import { Rate, Trend, Counter } from 'k6/metrics';

// ── Stress Test ──
// Gradually increases load beyond normal capacity to find breaking point.
// Identifies the maximum number of concurrent users the system can handle.

const errorRate = new Rate('errors');
const apiDuration = new Trend('api_duration', true);
const requestCount = new Counter('total_requests');

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5100';

export const options = {
    stages: [
        { duration: '30s', target: 20 },    // Normal load
        { duration: '1m',  target: 50 },    // Moderate load
        { duration: '1m',  target: 100 },   // High load
        { duration: '1m',  target: 150 },   // Very high load
        { duration: '1m',  target: 200 },   // Breaking point test
        { duration: '2m',  target: 250 },   // Beyond capacity
        { duration: '30s', target: 0 },     // Recovery
    ],
    thresholds: {
        http_req_duration: ['p(95)<3000'],   // Track but allow higher latency
        errors: ['rate<0.30'],                // Track error rate under stress
    },
};

export function setup() {
    const orgs = [];
    for (let i = 0; i < 10; i++) {
        const email = `stress${i}_${Date.now()}@loadtest.com`;
        const res = http.post(`${BASE_URL}/api/auth/register-org`, JSON.stringify({
            OrganisationName: `StressOrg_${Date.now()}_${i}`,
            AdminDisplayName: `Stress Admin ${i}`,
            Email: email,
            Password: 'StressTest123!',
        }), { headers: { 'Content-Type': 'application/json' } });

        if (res.status === 200) {
            const body = JSON.parse(res.body);
            orgs.push({
                token: body.AccessToken,
                tenantId: body.TenantId,
                email: email,
            });
        }
    }
    return { orgs };
}

export default function (data) {
    if (!data.orgs || data.orgs.length === 0) return;

    const org = data.orgs[Math.floor(Math.random() * data.orgs.length)];
    const headers = {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${org.token}`,
        'X-Tenant-Id': org.tenantId,
    };

    // Mixed workload: 70% reads, 30% writes
    const isWrite = Math.random() < 0.3;

    if (isWrite) {
        group('Stress: Writes', () => {
            const writeOps = [
                () => http.post(`${BASE_URL}/api/chat/send`, JSON.stringify({
                    Channel: 'general',
                    Content: `Stress VU${__VU} iter${__ITER}`,
                }), { headers }),
                () => http.post(`${BASE_URL}/api/notes`, JSON.stringify({
                    Title: `Stress note ${__VU}`,
                    Content: 'Stress test content',
                }), { headers }),
                () => http.post(`${BASE_URL}/api/tasks`, JSON.stringify({
                    Title: `Stress task ${__VU}`,
                    Priority: 1,
                }), { headers }),
            ];

            const op = writeOps[Math.floor(Math.random() * writeOps.length)];
            const start = Date.now();
            const res = op();
            apiDuration.add(Date.now() - start);
            requestCount.add(1);
            check(res, { 'write OK': (r) => r.status === 200 });
            errorRate.add(res.status !== 200);
        });
    } else {
        group('Stress: Reads', () => {
            const readEndpoints = [
                '/api/tenant',
                '/api/tenant/members',
                '/api/chat/messages/general?take=20',
                '/api/files',
                '/api/notes',
                '/api/tasks',
                '/api/tasks/summary',
                '/api/announcements',
                '/api/devices',
                '/api/audit?page=1',
            ];

            const endpoint = readEndpoints[Math.floor(Math.random() * readEndpoints.length)];
            const start = Date.now();
            const res = http.get(`${BASE_URL}${endpoint}`, { headers });
            apiDuration.add(Date.now() - start);
            requestCount.add(1);
            check(res, { 'read OK': (r) => r.status === 200 });
            errorRate.add(res.status !== 200);
        });
    }

    sleep(0.3);
}

export function teardown(data) {
    console.log(`Stress test complete. Tested ${data.orgs.length} organisations.`);
    console.log('Review the results to identify the breaking point and bottlenecks.');
}
