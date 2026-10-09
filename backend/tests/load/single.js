// 单张接口压测（T-015、设计书第 9 节）。在 backend/tests/load 目录运行：
//   k6 run single.js                                   按固定速率 60 次/秒、最多 50 个并发请求，跑 60 秒（验收用）
//   k6 run -e RATE=150 single.js                       换速率，找 P95 还在 100 ms 以内的最高吞吐
//   k6 run -e MODE=saturate -e VUS=50 single.js        50 个并发不停地发，测最大吞吐（这时排队时间会算进耗时，P95 不设门槛）
// 其他参数：BASE_URL、API_KEY、DURATION、FORMAT（png/pdf/zpl）、REQUEST（请求体文件）。
// 目标（4 核 8 GB）：服务端 P95 ≤ 100 ms、错误率 0、吞吐 ≥ 60 次/秒。服务端耗时取响应头 Server-Timing 的 total（含排队，不含网络）。
import http from 'k6/http';
import { check } from 'k6';
import { Trend } from 'k6/metrics';

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5080';
const API_KEY = __ENV.API_KEY || 'lbl_test_local_dev';
const VUS = Number(__ENV.VUS || 50);
const DURATION = __ENV.DURATION || '60s';
const saturate = __ENV.MODE === 'saturate';
const request = JSON.parse(open(__ENV.REQUEST || '../../samples/requests/box-label.render.json'));
if (__ENV.FORMAT) request.format = __ENV.FORMAT;
const body = JSON.stringify(request);
const serverTotal = new Trend('server_total_ms', true);

export const options = {
  scenarios: {
    single: saturate
      ? { executor: 'constant-vus', vus: VUS, duration: DURATION }
      : {
          executor: 'constant-arrival-rate',
          rate: Number(__ENV.RATE || 60),
          timeUnit: '1s',
          duration: DURATION,
          preAllocatedVUs: VUS,
          maxVUs: VUS,
        },
  },
  thresholds: saturate
    ? { http_req_failed: ['rate==0'] }
    : {
        server_total_ms: ['p(95)<100'],
        http_req_failed: ['rate==0'],
        // 50 个并发都忙不过来时 k6 会丢掉发不出去的请求：说明达不到要求的吞吐。
        dropped_iterations: ['count==0'],
      },
  summaryTrendStats: ['min', 'med', 'avg', 'p(90)', 'p(95)', 'p(99)', 'max'],
};

export default function () {
  const response = http.post(`${BASE_URL}/api/v1/render`, body, {
    headers: { 'Content-Type': 'application/json', 'X-Api-Key': API_KEY },
  });
  check(response, { '200': (r) => r.status === 200 });
  const match = /total;dur=([\d.]+)/.exec(response.headers['Server-Timing'] || '');
  if (match) serverTotal.add(Number(match[1]));
}
