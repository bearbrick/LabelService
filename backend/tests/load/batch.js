// 批量接口压测（T-015）：每次 100 张示例箱标合并成一个 PDF，默认 4 个并发连续请求 60 秒。
// 用法（在 backend/tests/load 目录）：
//   k6 run batch.js
//   k6 run -e BASE_URL=http://服务器:8080 -e API_KEY=lbl_xxx -e VUS=4 -e ITEMS=100 -e OUTPUT=merge batch.js
// 目标：100 张一批 ≤ 3 秒（服务端 total），错误率 0。
import http from 'k6/http';
import { check } from 'k6';
import { Trend } from 'k6/metrics';

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5080';
const API_KEY = __ENV.API_KEY || 'lbl_test_local_dev';
const single = JSON.parse(open('../../samples/requests/box-label.render.json'));
const items = Array.from({ length: Number(__ENV.ITEMS || 100) }, (_, index) => ({
  data: { ...single.data, boxNo: `C26100${String(index + 1).padStart(5, '0')}` },
}));
const body = JSON.stringify({ templateCode: single.templateCode, format: __ENV.FORMAT || 'pdf', output: __ENV.OUTPUT || 'merge', items });
const serverTotal = new Trend('server_total_ms', true);

export const options = {
  scenarios: {
    batch: {
      executor: 'constant-vus',
      vus: Number(__ENV.VUS || 4),
      duration: __ENV.DURATION || '60s',
    },
  },
  thresholds: {
    server_total_ms: ['p(95)<3000'],
    http_req_failed: ['rate==0'],
  },
  summaryTrendStats: ['min', 'med', 'avg', 'p(90)', 'p(95)', 'p(99)', 'max'],
};

export default function () {
  const response = http.post(`${BASE_URL}/api/v1/render/batch`, body, {
    headers: { 'Content-Type': 'application/json', 'X-Api-Key': API_KEY },
    timeout: '30s',
  });
  check(response, { '200': (r) => r.status === 200 });
  const match = /total;dur=([\d.]+)/.exec(response.headers['Server-Timing'] || '');
  if (match) serverTotal.add(Number(match[1]));
}
