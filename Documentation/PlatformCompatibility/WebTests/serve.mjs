import { createServer, request as httpRequest } from 'node:http';
import { createReadStream, statSync } from 'node:fs';
import { extname, resolve, sep } from 'node:path';

const root = resolve(process.argv[2] ?? 'Builds/UTTVR_WebDemo');
const port = Number(process.argv[3] ?? 8765);
// /api/... được chuyển tới backend Flask (Backend/server.py) giống nginx trên máy chủ thật.
const apiTarget = new URL(process.env.UTT_API ?? 'http://127.0.0.1:5000');
const mime = { '.html':'text/html; charset=utf-8', '.js':'text/javascript', '.wasm':'application/wasm', '.css':'text/css', '.json':'application/json', '.png':'image/png', '.data':'application/octet-stream' };
createServer((request, response) => {
  if (request.url.startsWith('/api/')) {
    const upstream = httpRequest({ hostname: apiTarget.hostname, port: apiTarget.port, path: request.url,
      method: request.method, headers: { ...request.headers, host: apiTarget.host } }, (res) => {
      response.writeHead(res.statusCode ?? 502, res.headers);
      res.pipe(response);
    });
    upstream.on('error', () => { if (!response.headersSent) response.writeHead(502); response.end('Backend chưa chạy'); });
    request.pipe(upstream);
    return;
  }
  try {
    const name = decodeURIComponent(new URL(request.url, 'http://localhost').pathname);
    const file = resolve(root, '.' + (name === '/' ? '/index.html' : name));
    if (!file.startsWith(root + sep)) throw new Error('Not found');
    const info = statSync(file, {bigint:true});
    if (!info.isFile()) throw new Error('Not found');
    const encoding = file.endsWith('.br') ? 'br' : file.endsWith('.gz') ? 'gzip' : '';
    const original = encoding ? file.slice(0, -3) : file;
    const etag = `W/"${info.size.toString(16)}-${info.mtimeNs.toString(16)}"`;
    response.setHeader('Cache-Control', 'no-cache');
    response.setHeader('ETag', etag);
    response.setHeader('Last-Modified', new Date(Number(info.mtimeMs)).toUTCString());
    if (request.headers['if-none-match'] === etag) {
      response.writeHead(304).end();
      return;
    }
    response.setHeader('Content-Type', mime[extname(original)] || 'application/octet-stream');
    response.setHeader('Content-Length', info.size.toString());
    if (encoding) response.setHeader('Content-Encoding', encoding);
    if (request.method === 'HEAD') response.end();
    else createReadStream(file).pipe(response);
  } catch { response.writeHead(404).end('Not found'); }
}).listen(port, '127.0.0.1', () => console.log('http://127.0.0.1:' + port + '/'));
