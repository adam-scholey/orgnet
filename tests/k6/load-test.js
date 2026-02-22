import http from 'k6/http';
import { check, sleep, group } from 'k6';
import { Rate, Trend } from 'k6/metrics';

// ── Custom Metrics ──
const errorRate = new Rate('errors');
const loginDuration = new Trend('login_duration', true);
const apiDuration = new Trend('api_duration', true);

// ── Configuration ──
const BASE_URL = __ENV.BASE_URL || 'http://localhost:5100';

// ── Load Stages ──
// Simulates multi-tenant load: ramp up to 50 concurrent users, sustain, then ramp down
export const options = {
    stages: [
        { duration: '30s', target: 10 },   // Warm-up: 10 users
        { duration: '1m',  target: 50 },   // Ramp to 50 concurrent users
        { duration: '2m',  target: 50 },   // Sustain 50 users for 2 minutes
        { duration: '30s', target: 100 },  // Spike to 100 users
        { duration: '1m',  target: 100 },  // Sustain spike
        { duration: '30s', target: 0 },    // Ramp down
    ],
    thresholds: {
        http_req_duration: ['p(95)<500', 'p(99)<1000'],  // 95th < 500ms, 99th < 1s
        errors: ['rate<0.05'],                            // Error rate < 5%
        login_duration: ['p(95)<800'],                    // Login < 800ms at p95
        api_duration: ['p(95)<300'],                      // API calls < 300ms at p95
    },
};

// ── Setup: Register orgs and get tokens ──
export function setup() {
    const orgs = [];
    const orgCount = 5; // Simulate 5 different organisations

    for (let i = 0; i < orgCount; i++) {
        const orgName = `LoadTestOrg_${Date.now()}_${i}`;
        const email = `admin${i}_${Date.now()}@loadtest.com`;
        const password = 'LoadTest123!';

        const regRes = http.post(`${BASE_URL}/api/auth/register-org`, JSON.stringify({
            OrganisationName: orgName,
            AdminDisplayName: `Admin ${i}`,
            Email: email,
            Password: password,
        }), { headers: { 'Content-Type': 'application/json' } });

        if (regRes.status === 200) {
            const body = JSON.parse(regRes.body);
            orgs.push({
                tenantId: body.TenantId,
                accessToken: body.AccessToken,
                refreshToken: body.RefreshToken,
                email: email,
                password: password,
                orgName: orgName,
            });
        }
    }

    if (orgs.length === 0) {
        console.error('Failed to register any test organisations');
    }

    return { orgs };
}

// ── Main Test Scenario ──
export default function (data) {
    if (!data.orgs || data.orgs.length === 0) {
        console.error('No test orgs available');
        return;
    }

    // Pick a random org to simulate multi-tenant load
    const org = data.orgs[Math.floor(Math.random() * data.orgs.length)];
    const headers = {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${org.accessToken}`,
        'X-Tenant-Id': org.tenantId,
    };

    // ── Health Check ──
    group('Health Check', () => {
        const res = http.get(`${BASE_URL}/health`);
        check(res, { 'health OK': (r) => r.status === 200 });
        errorRate.add(res.status !== 200);
    });

    // ── Login by Email ──
    group('Login by Email', () => {
        const start = Date.now();
        const res = http.post(`${BASE_URL}/api/auth/login-by-email`, JSON.stringify({
            Email: org.email,
            Password: org.password,
        }), { headers: { 'Content-Type': 'application/json' } });

        loginDuration.add(Date.now() - start);
        check(res, { 'login OK': (r) => r.status === 200 });
        errorRate.add(res.status !== 200);
    });

    // ── Tenant Info ──
    group('Get Tenant', () => {
        const start = Date.now();
        const res = http.get(`${BASE_URL}/api/tenant`, { headers });
        apiDuration.add(Date.now() - start);
        check(res, { 'tenant OK': (r) => r.status === 200 });
        errorRate.add(res.status !== 200);
    });

    // ── Members ──
    group('Get Members', () => {
        const start = Date.now();
        const res = http.get(`${BASE_URL}/api/tenant/members`, { headers });
        apiDuration.add(Date.now() - start);
        check(res, { 'members OK': (r) => r.status === 200 });
        errorRate.add(res.status !== 200);
    });

    // ── Chat: Send & Read Messages ──
    group('Chat', () => {
        const sendStart = Date.now();
        const sendRes = http.post(`${BASE_URL}/api/chat/send`, JSON.stringify({
            Channel: 'general',
            Content: `Load test message from VU ${__VU} iter ${__ITER}`,
        }), { headers });
        apiDuration.add(Date.now() - sendStart);
        check(sendRes, { 'chat send OK': (r) => r.status === 200 });
        errorRate.add(sendRes.status !== 200);

        const readStart = Date.now();
        const readRes = http.get(`${BASE_URL}/api/chat/messages/general?take=20`, { headers });
        apiDuration.add(Date.now() - readStart);
        check(readRes, { 'chat read OK': (r) => r.status === 200 });
        errorRate.add(readRes.status !== 200);
    });

    // ── File Vault: Upload & List ──
    group('File Storage', () => {
        const uploadStart = Date.now();
        const uploadRes = http.post(`${BASE_URL}/api/files/upload`, JSON.stringify({
            FileName: `test_${__VU}_${__ITER}.txt`,
            Base64Content: 'SGVsbG8gZnJvbSBLNiBsb2FkIHRlc3Q=', // "Hello from K6 load test"
            EncryptionPin: '1234',
            IsShared: false,
        }), { headers });
        apiDuration.add(Date.now() - uploadStart);
        check(uploadRes, { 'upload OK': (r) => r.status === 200 });
        errorRate.add(uploadRes.status !== 200);

        const listStart = Date.now();
        const listRes = http.get(`${BASE_URL}/api/files`, { headers });
        apiDuration.add(Date.now() - listStart);
        check(listRes, { 'files list OK': (r) => r.status === 200 });
        errorRate.add(listRes.status !== 200);
    });

    // ── Notes: Create & List ──
    group('Notes', () => {
        const createStart = Date.now();
        const createRes = http.post(`${BASE_URL}/api/notes`, JSON.stringify({
            Title: `Note from VU ${__VU}`,
            Content: `Performance test note created at ${new Date().toISOString()}`,
        }), { headers });
        apiDuration.add(Date.now() - createStart);
        check(createRes, { 'note create OK': (r) => r.status === 200 });
        errorRate.add(createRes.status !== 200);

        const listStart = Date.now();
        const listRes = http.get(`${BASE_URL}/api/notes`, { headers });
        apiDuration.add(Date.now() - listStart);
        check(listRes, { 'notes list OK': (r) => r.status === 200 });
        errorRate.add(listRes.status !== 200);
    });

    // ── Tasks: Create, List, Summary ──
    group('Task Board', () => {
        const createStart = Date.now();
        const createRes = http.post(`${BASE_URL}/api/tasks`, JSON.stringify({
            Title: `Task from VU ${__VU} iter ${__ITER}`,
            Description: 'K6 load test task',
            Priority: 1,
        }), { headers });
        apiDuration.add(Date.now() - createStart);
        check(createRes, { 'task create OK': (r) => r.status === 200 });
        errorRate.add(createRes.status !== 200);

        const listStart = Date.now();
        const listRes = http.get(`${BASE_URL}/api/tasks`, { headers });
        apiDuration.add(Date.now() - listStart);
        check(listRes, { 'tasks list OK': (r) => r.status === 200 });
        errorRate.add(listRes.status !== 200);

        const summaryStart = Date.now();
        const summaryRes = http.get(`${BASE_URL}/api/tasks/summary`, { headers });
        apiDuration.add(Date.now() - summaryStart);
        check(summaryRes, { 'task summary OK': (r) => r.status === 200 });
        errorRate.add(summaryRes.status !== 200);
    });

    // ── Announcements ──
    group('Announcements', () => {
        const listStart = Date.now();
        const listRes = http.get(`${BASE_URL}/api/announcements`, { headers });
        apiDuration.add(Date.now() - listStart);
        check(listRes, { 'announcements OK': (r) => r.status === 200 });
        errorRate.add(listRes.status !== 200);
    });

    // ── Audit Logs ──
    group('Audit', () => {
        const start = Date.now();
        const res = http.get(`${BASE_URL}/api/audit?page=1`, { headers });
        apiDuration.add(Date.now() - start);
        check(res, { 'audit OK': (r) => r.status === 200 });
        errorRate.add(res.status !== 200);
    });

    // ── Devices ──
    group('Devices', () => {
        const start = Date.now();
        const res = http.get(`${BASE_URL}/api/devices`, { headers });
        apiDuration.add(Date.now() - start);
        check(res, { 'devices OK': (r) => r.status === 200 });
        errorRate.add(res.status !== 200);
    });

    sleep(1); // Think time between iterations
}

// ── Teardown ──
export function teardown(data) {
    console.log(`Load test complete. Tested ${data.orgs.length} organisations.`);
}
