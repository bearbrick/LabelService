// 单张接口压测（T-015、设计书第 9 节“50 并发时 P95 ≤ 100 ms、错误率为 0”）。在 backend/tests/load 目录运行：
//   k6 run single.js                                   验收：50 个并发持续发 60 秒（constant-vus），P95 ≤ 100 ms、错误率 0
//   k6 run -e MODE=rate -e RATE=150 single.js          按固定速率发（最多 50 个并发），找 P95 还在 100 ms 以内的最高速率
// 其他参数：BASE_URL、API_KEY、VUS、DURATION、FORMAT（png/pdf/zpl）、REQUEST（请求体文件）、
//   REPEAT=1（每次发完全相同的请求；默认每次换箱号，条码、二维码的编码缓存命中不了，接近真实打印）、
//   BUDGET（P95 门槛毫秒，默认 100；k6 不在服务器本机、要按“局域网调用方实测 ≤ 150 ms”验时用 -e HTTP_BUDGET=150）。
// 服务端耗时：响应头 Server-Timing 的 total 只到写响应头为止（T-021）；到写完最后一个字节的分位数用 log-report.cs 从调用日志算。
import http from 'k6/http';
import { check } from 'k6';
import { Rate, Trend } from 'k6/metrics';

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5080';
const API_KEY = __ENV.API_KEY || 'lbl_test_local_dev';
const VUS = Number(__ENV.VUS || 50);
const DURATION = __ENV.DURATION || '60s';
const BUDGET = Number(__ENV.BUDGET || 100);
const HTTP_BUDGET = Number(__ENV.HTTP_BUDGET || BUDGET);
const rate = __ENV.MODE === 'rate';
const repeat = __ENV.REPEAT === '1';
const request = JSON.parse(open(__ENV.REQUEST || '../../samples/requests/box-label.render.json'));
if (__ENV.FORMAT) request.format = __ENV.FORMAT;
const fixedBody = JSON.stringify(request);
const serverTotal = new Trend('server_total_ms', true);
const missingTiming = new Rate('missing_timing');

export const options = {
  scenarios: {
    single: rate
      ? {
          executor: 'constant-arrival-rate',
          rate: Number(__ENV.RATE || 60),
          timeUnit: '1s',
          duration: DURATION,
          preAllocatedVUs: VUS,
          maxVUs: VUS,
        }
      : { executor: 'constant-vus', vus: VUS, duration: DURATION },
  },
  thresholds: {
    server_total_ms: [`p(95)<=${BUDGET}`],
    http_req_duration: [`p(95)<=${HTTP_BUDGET}`],
    http_req_failed: ['rate==0'],
    checks: ['rate==1'],
    missing_timing: ['rate==0'],
    // 固定速率时 50 个并发都忙不过来，k6 会丢掉发不出去的请求：说明达不到这个吞吐。
    ...(rate ? { dropped_iterations: ['count==0'] } : {}),
  },
  summaryTrendStats: ['min', 'med', 'avg', 'p(90)', 'p(95)', 'p(99)', 'max'],
};

export default function () {
  let body = fixedBody;
  if (!repeat && request.data) {
    request.data.boxNo = 'C' + String(1000000000 + __VU * 10000000 + __ITER);
    body = JSON.stringify(request);
  }

  const response = http.post(`${BASE_URL}/api/v1/render`, body, {
    headers: { 'Content-Type': 'application/json', 'X-Api-Key': API_KEY },
  });
  check(response, { '200': (r) => r.status === 200 });
  const match = /total;dur=([\d.]+)/.exec(response.headers['Server-Timing'] || '');
  missingTiming.add(!match);
  if (match) serverTotal.add(Number(match[1]));
}
