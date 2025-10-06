import { htmlReport } from "https://raw.githubusercontent.com/benc-uk/k6-reporter/main/dist/bundle.js";
import { textSummary } from "https://jslib.k6.io/k6-summary/0.0.1/index.js";
import http from 'k6/http';
import { check, sleep } from 'k6';
import { uuidv4 } from 'https://jslib.k6.io/k6-utils/1.1.0/index.js';

const backend_HostAddress = __ENV.BACKEND_URL || "http://localhost:5164";
const movie_HostAddress = __ENV.MOVIE_URL || "http://localhost:5185";
const rating_HostAddress = __ENV.RATING_URL || "http://localhost:5272";

function generateTracingHeaders() {
    const traceId = uuidv4().replace(/-/g, "").substring(0, 16); // 16-char hex
    const spanId = uuidv4().replace(/-/g, "").substring(0, 16);

    return {
        Accept: 'application/json',
        'x-app-source' : 'k6',
        'x-app-version' : '1.1.1',
        'x-request-id': uuidv4(),
        'x-b3-traceid': traceId,
        'x-b3-spanid': spanId,
        'x-b3-sampled': '1',
        'x-b3-flags': '0',
        'x-ot-span-context': uuidv4()
    };
}

function makeRequests() {
    const headers = generateTracingHeaders();
    const randomNumber = Math.floor(Math.random() * 4) + 1;

    let res = http.get(`${backend_HostAddress}/`, { headers });
    check(res, { 'status code is 200': (r) => r.status === 200 });

    res = http.get(`${backend_HostAddress}/login/`, { headers });
    check(res, { 'status code is 200': (r) => r.status === 200 });

    res = http.get(`${backend_HostAddress}/error/`, { headers });
    check(res, { 'status code is 500': (r) => r.status === 500 });

    res = http.get(`${backend_HostAddress}/movies/`, { headers });
    check(res, { 'status code is 200': (r) => r.status === 200 });

    res = http.get(`${backend_HostAddress}/movies/${randomNumber}`, { headers });
    check(res, { 'status code is 200': (r) => r.status === 200 });

    res = http.get(`${backend_HostAddress}/movies/${randomNumber}/ratings`, { headers });
    check(res, { 'status code is 200': (r) => r.status === 200 });

    res = http.get(`${movie_HostAddress}/${randomNumber}`, { headers });
    check(res, { 'status code is 200': (r) => r.status === 200 });

    res = http.get(`${movie_HostAddress}/${randomNumber}/ratings`, { headers });
    check(res, { 'status code is 200': (r) => r.status === 200 });

    res = http.get(`${rating_HostAddress}/${randomNumber}`, { headers });
    check(res, { 'status code is 200': (r) => r.status === 200 });

    res = http.get(`${rating_HostAddress}/${randomNumber}`, { headers });
    check(res, { 'status code is 200': (r) => r.status === 200 });
}

export default function () {
    makeRequests();
    sleep(1);
}

export const options = {
    stages: [
        { duration: '1m', target: 100 },
        { duration: '1m', target: 200 },
        { duration: '1m', target: 300 },
        { duration: '3m', target: 300 },
        { duration: '30s', target: 50 }
    ],
    summaryTrendStats: ['avg', 'min', 'med', 'max', 'p(90)', 'p(95)', 'p(99)'],
};

export function handleSummary(data) {
    return {
        "/results/result.html": htmlReport(data),
        stdout: textSummary(data, { indent: " ", enableColors: true }),
    };
}