import http from 'k6/http';
import { check } from 'k6';

export const options = {
  scenarios: {
    spike: {
      executor: 'constant-arrival-rate',
      rate: 200,        // 200 requests per second
      timeUnit: '1s',
      duration: '10s',  // Run for 10 seconds
      preAllocatedVUs: 50,
      maxVUs: 100,
    },
  },
  
  thresholds: {
    // Target: 95% of requests complete in <200ms (POS requirement)
    'http_req_duration': ['p(95)<200'],
    // Target: 90%+ success rate under stampede load
    'http_reqs{status:200}': ['rate>0.9'],
    // Target: Most requests should be <100ms (cache hit)
    'http_req_duration{expected_response:true}': ['p(90)<100'],
  },
  
  summaryTrendStats: ['avg', 'min', 'med', 'max', 'p(90)', 'p(95)', 'p(99)'],
};

// Tenants to test multi-tenant isolation and per-tenant caching
const TENANTS = ['7ELEVEN', 'BURGERKING'];

// Test sale ID (both tenants have this seeded)
const SALE_ID = '11111111-1111-1111-1111-111111111111';

export default function () {
  // Randomly select a tenant for this request (tests multi-tenant isolation)
  const tenant = TENANTS[Math.floor(Math.random() * TENANTS.length)];
  
  // Note: In real POS, different cashiers query different sales.
  // Here we're testing cache stampede protection, so we intentionally
  // hammer the SAME sale ID to stress-test IMemoryCache.
  // For realistic testing, you'd query different sale IDs.
  const url = `https://localhost:60304/api/v1/sales/${SALE_ID}`;
  
  const params = {
    headers: {
      'X-Tenant': tenant,
    },
    insecureSkipTLSVerify: true,  // Skip SSL verification for localhost
  };
  
  const res = http.get(url, params);
  
  check(res, {
    'is status 200': (r) => r.status === 200,
    'response time < 200ms': (r) => r.timings.duration < 200,
    'has correlation id': (r) => r.headers['X-Correlation-Id'] !== undefined,
    'has rate limit headers': (r) => r.headers['X-Ratelimit-Limit'] !== undefined,
  });
}

// Custom summary - clean, POS-focused output
export function handleSummary(data) {
  // Extract key metrics
  const totalRequests = data.metrics.http_reqs?.values?.count || 0;
  const successCount = data.metrics['http_reqs{status:200}']?.values?.count || 0;
  const failedCount = totalRequests - successCount;
  const droppedCount = data.metrics.dropped_iterations?.values?.count || 0;
  const checksPass = data.root_group.checks.reduce((sum, c) => sum + c.passes, 0);
  const checksFail = data.root_group.checks.reduce((sum, c) => sum + c.fails, 0);
  
  // Latency metrics (all requests)
  const duration = data.metrics.http_req_duration?.values || {};
  // Latency metrics (success only - 200 responses)
  const durationSuccess = data.metrics['http_req_duration{expected_response:true}']?.values || {};
  
  // Calculate percentages
  const successPercent = totalRequests > 0 ? (successCount / totalRequests * 100).toFixed(1) : 0;
  const failedPercent = totalRequests > 0 ? (failedCount / totalRequests * 100).toFixed(1) : 0;
  const droppedPercent = ((droppedCount / (totalRequests + droppedCount)) * 100).toFixed(1);
  
  // Test duration
  const testDuration = (data.state.testRunDurationMs / 1000).toFixed(1);
  const targetRPS = 200;
  const actualRPS = (totalRequests / (data.state.testRunDurationMs / 1000)).toFixed(1);
  
  // Estimate cache hits (requests <100ms are likely cache hits)
  const cacheHitPercent = durationSuccess['p(90)'] < 100 ? '~90%+ (p90 < 100ms)' : 
                          durationSuccess['p(50)'] < 100 ? '~50%+ (p50 < 100ms)' : 
                          'Low (<50%)';
  
  // Pass/Fail indicators
  const p95Pass = duration['p(95)'] < 200;
  const successRatePass = parseFloat(successPercent) >= 90;
  const p90CachePass = durationSuccess['p(90)'] < 100;
  
  // Build clean text summary
  const summary = `
╔════════════════════════════════════════════════════════════════╗
║              🚀 STAMPEDE TEST - POS CACHE PERFORMANCE           ║
╚════════════════════════════════════════════════════════════════╝

⏱️  TEST DURATION: ${testDuration}s  |  Target: ${targetRPS} req/s  |  Actual: ${actualRPS} req/s

📊 REQUEST RESULTS
┌────────────────────────────────────────────────────────────────┐
│ Total Completed:   ${totalRequests.toString().padStart(6)}                              │
│ ✅ Success (200):   ${successCount.toString().padStart(6)}  (${successPercent.padEnd(5)}%) ${successRatePass ? '✅ PASS      ' : '❌ FAIL      '}│
│ ❌ Failed:          ${failedCount.toString().padStart(6)}  (${failedPercent.padEnd(5)}%)                    │
│ ⚠️  Dropped (k6):    ${droppedCount.toString().padStart(6)}  (${droppedPercent.padEnd(5)}% of total sent)     │
└────────────────────────────────────────────────────────────────┘

⚡ LATENCY - ALL REQUESTS (cache miss + cache hit)
┌────────────────────────────────────────────────────────────────┐
│ Min:           ${duration.min?.toFixed(2).padStart(8)}ms                          │
│ Average:       ${duration.avg?.toFixed(2).padStart(8)}ms                          │
│ Median (p50):  ${duration.med?.toFixed(2).padStart(8)}ms                          │
│ p(90):         ${duration['p(90)']?.toFixed(2).padStart(8)}ms                          │
│ p(95):         ${duration['p(95)']?.toFixed(2).padStart(8)}ms ${p95Pass ? '✅ <200ms PASS ' : '❌ >200ms FAIL '}│
│ p(99):         ${duration['p(99)']?.toFixed(2).padStart(8)}ms (worst 1%)              │
│ Max:           ${duration.max?.toFixed(2).padStart(8)}ms                          │
└────────────────────────────────────────────────────────────────┘

⚡ LATENCY - SUCCESS ONLY (200 responses, cache performance)
┌────────────────────────────────────────────────────────────────┐
│ Min:           ${durationSuccess.min?.toFixed(2).padStart(8)}ms (fastest cache hit)   │
│ Average:       ${durationSuccess.avg?.toFixed(2).padStart(8)}ms                          │
│ Median (p50):  ${durationSuccess.med?.toFixed(2).padStart(8)}ms                          │
│ p(90):         ${durationSuccess['p(90)']?.toFixed(2).padStart(8)}ms ${p90CachePass ? '✅ <100ms      ' : '⚠️  >100ms      '}│
│ p(95):         ${durationSuccess['p(95)']?.toFixed(2).padStart(8)}ms                          │
│ p(99):         ${durationSuccess['p(99)']?.toFixed(2).padStart(8)}ms (TAIL)                   │
│ Max:           ${durationSuccess.max?.toFixed(2).padStart(8)}ms                          │
└────────────────────────────────────────────────────────────────┘

💾 CACHE EFFECTIVENESS (Estimated)
┌────────────────────────────────────────────────────────────────┐
│ Cache Hit Rate:    ${cacheHitPercent.padEnd(48)}│
│ Note: Requests <100ms = likely IMemoryCache hit               │
│       Requests >100ms = likely DB query (first hit per tenant)│
└────────────────────────────────────────────────────────────────┘

✅ CHECKS
┌────────────────────────────────────────────────────────────────┐
│ Passed:        ${checksPass.toString().padStart(6)} / ${(checksPass + checksFail).toString().padEnd(6)} ${checksFail === 0 ? '✅ All passed!' : '❌           '}   │
└────────────────────────────────────────────────────────────────┘

🎯 POS PERFORMANCE VERDICT
┌────────────────────────────────────────────────────────────────┐
│ ${(successRatePass ? '✅ SUCCESS: 90%+ requests succeeded!' : '❌ FAIL: <90% success rate').padEnd(62)}│
│ ${(p95Pass ? '✅ SUCCESS: p95 < 200ms (POS requirement met)' : '❌ FAIL: p95 > 200ms (too slow for POS)').padEnd(62)}│
│ ${(p90CachePass ? '✅ SUCCESS: p90 < 100ms (cache is FAST!)' : '⚠️  WARNING: p90 > 100ms (cache may be slow)').padEnd(62)}│
│ ${(droppedCount < 100 ? '✅ GOOD: <100 dropped iterations' : '⚠️  WARNING: Many dropped iterations (add VUs)').padEnd(62)}│
└────────────────────────────────────────────────────────────────┘

📝 TIPS:
   • p95 < 200ms = POS cashier never waits (critical!)
   • p90 < 100ms = IMemoryCache is working great
   • p50 < 50ms  = Most requests are instant (cache hits)
   • Dropped iterations = k6 couldn't send fast enough (normal if <10%)

🔍 TROUBLESHOOTING:
   ${failedCount > 0 ? '⚠️  Some requests failed - check API logs for errors!' : ''}
   ${!p95Pass ? '⚠️  High p95 latency - check SQL connection pool size!' : ''}
   ${!p90CachePass ? '⚠️  Slow cache - IMemoryCache should be <10ms typically!' : ''}
   ${droppedCount > totalRequests * 0.1 ? '⚠️  High drop rate - increase maxVUs or reduce target rate!' : ''}

`;

  return {
    'stdout': summary,
  };
}