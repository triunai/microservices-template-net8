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
};

export default function () {
  const url = 'https://localhost:60304/api/sales/11111111-1111-1111-1111-111111111111';
  const params = {
    headers: {
      'X-Tenant': '7ELEVEN',
    },
    insecureSkipTLSVerify: true,  // Skip SSL verification for localhost
  };
  
  const res = http.get(url, params);
  
  check(res, {
    'is status 200': (r) => r.status === 200,
    'response time < 200ms': (r) => r.timings.duration < 200,
  });
}