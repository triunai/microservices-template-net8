import http from 'k6/http';
import { check, sleep } from 'k6';
import { Rate } from 'k6/metrics';

// Custom metrics
const rateLimitHitRate = new Rate('rate_limit_hits');

// Test configuration
export const options = {
  scenarios: {
    // Scenario 1: Single tenant stress test (trigger rate limiting)
    single_tenant_burst: {
      executor: 'constant-arrival-rate',
      rate: 20,              // 20 requests per second
      timeUnit: '1s',
      duration: '15s',       // Run for 15 seconds
      preAllocatedVUs: 5,
      maxVUs: 20,
      tags: { scenario: 'single_tenant_burst', tenant: '7ELEVEN' },
      exec: 'singleTenantBurst',
    },
    
    // Scenario 2: Multi-tenant fairness test (separate tenants shouldn't affect each other)
    multi_tenant_fairness: {
      executor: 'constant-arrival-rate',
      rate: 15,              // 15 requests per second per tenant
      timeUnit: '1s',
      duration: '15s',
      preAllocatedVUs: 5,
      maxVUs: 20,
      startTime: '20s',      // Start after single tenant test
      tags: { scenario: 'multi_tenant_fairness' },
      exec: 'multiTenantFairness',
    },
  },
  
  thresholds: {
    // Success thresholds
    'http_req_duration': ['p(95)<500'],           // 95% of requests should be <500ms
    'http_reqs{status:200}': ['rate>0.3'],       // At least 30% should succeed (some will be rate limited)
    'http_reqs{status:429}': ['rate>0'],         // Should see some rate limit responses
    'rate_limit_hits': ['rate>0.1'],             // Should hit rate limits
  },
};

// Test data
const BASE_URL = 'https://localhost:60304';
const SALE_ID = '11111111-1111-1111-1111-111111111111';

// Scenario 1: Single tenant burst (should trigger rate limiting)
export function singleTenantBurst() {
  const tenant = '7ELEVEN';
  
  const params = {
    headers: {
      'X-Tenant': tenant,
      'Content-Type': 'application/json',
    },
    tags: { tenant: tenant },
    insecureSkipTLSVerify: true,  // Skip SSL cert validation for localhost
  };
  
  const response = http.get(`${BASE_URL}/api/v1/sales/${SALE_ID}`, params);
  
  // Check response
  const success = check(response, {
    'status is 200 or 429': (r) => r.status === 200 || r.status === 429,
    'has correlation id': (r) => r.headers['X-Correlation-Id'] !== undefined,
    'has rate limit headers': (r) => 
      r.headers['X-Ratelimit-Limit'] !== undefined || 
      r.headers['X-Ratelimit-Window'] !== undefined,
  });
  
  // Track rate limit hits
  if (response.status === 429) {
    rateLimitHitRate.add(1);
    console.log(`[${tenant}] 🚦 Rate limited! Retry-After: ${response.headers['Retry-After'] || 'N/A'}s`);
  } else if (response.status === 200) {
    rateLimitHitRate.add(0);
    console.log(`[${tenant}] ✅ Success (${response.timings.duration.toFixed(0)}ms)`);
  } else {
    console.log(`[${tenant}] ❌ Error: ${response.status}`);
  }
}

// Scenario 2: Multi-tenant fairness (different tenants shouldn't affect each other)
export function multiTenantFairness() {
  // Rotate between different tenants
  const tenants = ['7ELEVEN', 'BURGERKING'];
  const tenant = tenants[Math.floor(Math.random() * tenants.length)];
  
  const params = {
    headers: {
      'X-Tenant': tenant,
      'Content-Type': 'application/json',
    },
    tags: { tenant: tenant },
    insecureSkipTLSVerify: true,  // Skip SSL cert validation for localhost
  };
  
  const response = http.get(`${BASE_URL}/api/v1/sales/${SALE_ID}`, params);
  
  // Check response
  check(response, {
    'status is 200 or 429': (r) => r.status === 200 || r.status === 429,
    'has tenant in response': (r) => {
      if (r.status === 429) {
        const body = JSON.parse(r.body);
        return body.tenantId === tenant;
      }
      return true;
    },
  });
  
  if (response.status === 429) {
    rateLimitHitRate.add(1);
    console.log(`[${tenant}] 🚦 Rate limited (should be independent per tenant)`);
  } else if (response.status === 200) {
    rateLimitHitRate.add(0);
    console.log(`[${tenant}] ✅ Success`);
  }
}

// Summary at the end
export function handleSummary(data) {
  const rateLimitHits = data.metrics.rate_limit_hits?.values?.rate || 0;
  const totalRequests = data.metrics.http_reqs?.values?.count || 0;
  const successRate = (data.metrics['http_reqs{status:200}']?.values?.rate || 0);
  const rateLimitRate = (data.metrics['http_reqs{status:429}']?.values?.rate || 0);
  
  console.log('\n========================================');
  console.log('📊 RATE LIMITING TEST SUMMARY');
  console.log('========================================');
  console.log(`Total Requests: ${totalRequests}`);
  console.log(`Success Rate: ${(successRate * 100).toFixed(1)}%`);
  console.log(`Rate Limited: ${(rateLimitRate * 100).toFixed(1)}%`);
  console.log(`Rate Limit Hit Rate: ${(rateLimitHits * 100).toFixed(1)}%`);
  console.log('========================================\n');
  
  return {
    'stdout': JSON.stringify(data, null, 2),
  };
}

