import http from 'k6/http';
import { check, sleep, group } from 'k6';
import { Rate, Trend } from 'k6/metrics';

// ── Spike Test ──
// Simulates sudden burst of users (e.g. company-wide login at 9am)
// Tests system resilience under sudden load

const errorRate = new Rate('errors');
const apiDuration = new Trend('api_duration', true);

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5100';

export const options = {
    stages: [
        { duration: '10s', target: 5 },    // Baseline
        { duration: '10s', target: 200 },   // Spike to 200 users instantly
        { duration: '30s', target: 200 },   // Hold spike
        { duration: '10s', target: 5 },     // Drop back to baseline
        { duration: '30s', target: 5 },     // Recovery period
        { duration: '10s', target: 0 },     // Ramp down
    ],
    thresholds: {
        http_req_duration: ['p(95)<2000'],   // Allow up to 2s during spike
        errors: ['rate<0.15'],                // Allow up to 15% errors during spike
    },
};

export function setup() {
    const email = `spike_${Date.now()}@loadtest.com`;
    const res = http.post(`${BASE_URL}/api/auth/register-org`, JSON.stringify({
        OrganisationName: `SpikeTestOrg_${Date.now()}`,
        AdminDisplayName: 'Spike Admin',
        Email: email,
        Password: 'SpikeTest123!',
    }), { headers: { 'Content-Type': 'application/json' } });

    if (res.status !== 200) {
        console.error('Setup failed:', res.body);
        return { token: '', tenantId: '', email: '' };
    }

    const body = JSON.parse(res.body);
    return {
        token: body.AccessToken,
        tenantId: body.TenantId,
        email: email,
        password: 'SpikeTest123!',
    };
}

export default function (data) {
    if (!data.token) return;

    const headers = {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${data.token}`,
        'X-Tenant-Id': data.tenantId,
    };

    // Rapid-fire read operations (most common under spike)
    group('Spike: Read Operations', () => {
        const endpoints = [
            '/api/tenant',
            '/api/tenant/members',
            '/api/chat/channels',
            '/api/chat/messages/general?take=20',
            '/api/files',
            '/api/notes',
            '/api/tasks',
            '/api/tasks/summary',
            '/api/announcements',
            '/api/audit?page=1',
        ];

        for (const endpoint of endpoints) {
            const start = Date.now();
            const res = http.get(`${BASE_URL}${endpoint}`, { headers });
            apiDuration.add(Date.now() - start);
            check(res, { [`${endpoint} OK`]: (r) => r.status === 200 });
            errorRate.add(res.status !== 200);
        }
    });

    // Some write operations mixed in
    group('Spike: Write Operations', () => {
        const msgRes = http.post(`${BASE_URL}/api/chat/send`, JSON.stringify({
            Channel: 'general',
            Content: `Spike msg VU${__VU}`,
        }), { headers });
        check(msgRes, { 'spike chat OK': (r) => r.status === 200 });
        errorRate.add(msgRes.status !== 200);
    });

    sleep(0.5);
}
