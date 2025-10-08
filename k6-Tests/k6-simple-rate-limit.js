import http from 'k6/http';
import { check } from 'k6';

// Simple rate limit test: 150 requests in 10 seconds
export const options = {
  vus: 10,           // 10 virtual users
  duration: '10s',   // Run for 10 seconds
};

const BASE_URL = 'https://localhost:60304';
const SALE_ID = '11111111-1111-1111-1111-111111111111';

export default function() {
  const params = {
    headers: {
      'X-Tenant': '7ELEVEN',
    },
    insecureSkipTLSVerify: true,  // Skip SSL cert validation for localhost
  };
  
  const response = http.get(`${BASE_URL}/api/v1/sales/${SALE_ID}`, params);
  
  check(response, {
    'status is 200 or 429': (r) => r.status === 200 || r.status === 429,
  });
  
  if (response.status === 429) {
    console.log(`🚦 RATE LIMITED! Response: ${response.body.substring(0, 100)}...`);
  } else if (response.status === 200) {
    console.log(`✅ Success (${response.timings.duration.toFixed(0)}ms)`);
  } else {
    console.log(`❌ Error: ${response.status} - ${response.body.substring(0, 100)}`);
  }
}

