const { env } = require('process');
const target = env.ASPNETCORE_HTTPS_PORT ? `https://localhost:${env.ASPNETCORE_HTTPS_PORT}` : env.ASPNETCORE_URLS ? env.ASPNETCORE_URLS.split(';')[0] : 'https://localhost:7131';
// ws: the real-time hub (P14) upgrades /api/v1/realtime to a WebSocket.
const PROXY_CONFIG = [{ context: ['/api', '/health'], target, secure: false, changeOrigin: true, ws: true }];
module.exports = PROXY_CONFIG;