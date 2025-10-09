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
    
    // Scenario 2: Multi-tenant fairness test (verify per-tenant isolation)
    multi_tenant_fairness: {
      executor: 'constant-arrival-rate',
      rate: 15,              // 15 requests per second (split between tenants)
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
    'http_req_duration': ['p(95)<500', 'p(99)<2000'],  // 95% <500ms, 99% <2s
    'http_reqs{status:200}': ['rate>0.3'],             // At least 30% should succeed (some will be rate limited)
    'http_reqs{status:429}': ['rate>0'],               // Should see some rate limit responses
    'rate_limit_hits': ['rate>0.1'],                   // Should hit rate limits
  },
  
  // Include p99 in summary statistics
  summaryTrendStats: ['avg', 'min', 'med', 'max', 'p(90)', 'p(95)', 'p(99)'],
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

// Scenario 2: Multi-tenant fairness (verify tenant isolation)
export function multiTenantFairness() {
  // Rotate between tenants to verify per-tenant rate limiting works
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

// Custom summary - clean, readable output
export function handleSummary(data) {
  // Extract key metrics
  const totalRequests = data.metrics.http_reqs?.values?.count || 0;
  const successCount = data.metrics['http_reqs{status:200}']?.values?.count || 0;
  const rateLimitCount = data.metrics['http_reqs{status:429}']?.values?.count || 0;
  const droppedCount = data.metrics.dropped_iterations?.values?.count || 0;
  const checksPass = data.root_group.checks.reduce((sum, c) => sum + c.passes, 0);
  const checksFail = data.root_group.checks.reduce((sum, c) => sum + c.fails, 0);
  
  // Latency metrics
  const duration = data.metrics.http_req_duration?.values || {};
  const durationSuccess = data.metrics['http_req_duration{expected_response:true}']?.values || {};
  
  // Calculate percentages
  const successPercent = totalRequests > 0 ? (successCount / totalRequests * 100).toFixed(1) : 0;
  const rateLimitPercent = totalRequests > 0 ? (rateLimitCount / totalRequests * 100).toFixed(1) : 0;
  
  // Test duration
  const testDuration = (data.state.testRunDurationMs / 1000).toFixed(1);
  
  // Build clean text summary with proper alignment
  const summary = `
╔════════════════════════════════════════════════════════════════╗
║                  📊 RATE LIMITING TEST RESULTS                 ║
╚════════════════════════════════════════════════════════════════╝

⏱️  TEST DURATION: ${testDuration}s

📈 REQUEST SUMMARY
┌────────────────────────────────────────────────────────────────┐
│ Total Requests:        ${totalRequests.toString().padStart(6)}                            │
│ ✅ Success (200):       ${successCount.toString().padStart(6)}  (${successPercent.padEnd(5)}%)                   │
│ 🚦 Rate Limited (429):  ${rateLimitCount.toString().padStart(6)}  (${rateLimitPercent.padEnd(5)}%)                    │
│ ⚠️  Dropped (k6):        ${droppedCount.toString().padStart(6)}                            │
└────────────────────────────────────────────────────────────────┘

⚡ LATENCY (All Requests)
┌────────────────────────────────────────────────────────────────┐
│ Average:       ${duration.avg?.toFixed(2).padStart(8)}ms                          │
│ Median (p50):  ${duration.med?.toFixed(2).padStart(8)}ms                          │
│ p(90):         ${duration['p(90)']?.toFixed(2).padStart(8)}ms                          │
│ p(95):         ${duration['p(95)']?.toFixed(2).padStart(8)}ms ${duration['p(95)'] > 500 ? '⚠️  (>500ms)      ' : '✅              '}│
│ p(99):         ${duration['p(99)']?.toFixed(2).padStart(8)}ms ${duration['p(99)'] > 2000 ? '⚠️  (>2s)        ' : '✅              '}│
│ Max:           ${duration.max?.toFixed(2).padStart(8)}ms                          │
└────────────────────────────────────────────────────────────────┘

⚡ LATENCY (Success Only - 200 responses)
┌────────────────────────────────────────────────────────────────┐
│ Average:       ${durationSuccess.avg?.toFixed(2).padStart(8)}ms                          │
│ Median (p50):  ${durationSuccess.med?.toFixed(2).padStart(8)}ms                          │
│ p(90):         ${durationSuccess['p(90)']?.toFixed(2).padStart(8)}ms                          │
│ p(95):         ${durationSuccess['p(95)']?.toFixed(2).padStart(8)}ms                          │
│ p(99):         ${durationSuccess['p(99)']?.toFixed(2).padStart(8)}ms (TAIL)                   │
└────────────────────────────────────────────────────────────────┘

✅ CHECKS
┌────────────────────────────────────────────────────────────────┐
│ Passed:        ${checksPass.toString().padStart(6)} / ${(checksPass + checksFail).toString().padEnd(6)} ${checksFail === 0 ? '✅ All passed!' : '❌           '}   │
└────────────────────────────────────────────────────────────────┘

🎯 VERDICT
┌────────────────────────────────────────────────────────────────┐
│ ${(rateLimitCount > 0 ? '✅ Rate limiting is working!' : '⚠️  No rate limits hit (increase load)').padEnd(62)}│
│ ${(checksFail === 0 ? '✅ All checks passed!' : '❌ Some checks failed').padEnd(62)}│
│ ${(successPercent >= 50 ? '✅ Good success rate under load' : '⚠️  Low success rate').padEnd(62)}│
│ ${(duration['p(95)'] < 500 ? '✅ Latency within target (<500ms)' : '⚠️  Latency above target (queue/backpressure)').padEnd(62)}│
└────────────────────────────────────────────────────────────────┘

📝 TIP: p(95) = 95% of requests faster, p(99) = worst 1% (the TAIL).
       High tail latency under 2x load is expected - rate limiter queuing
       requests before rejecting them (graceful degradation).

`;

  // Return ONLY the clean text summary to stdout
  return {
    'stdout': summary,
  };
}

