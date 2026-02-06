const backendUrl =
  process.env.services__backend__http__0 ||
  process.env.SERVICES__BACKEND__HTTP__0 ||
  'http://localhost:5175';

module.exports = {
  '/api': {
    target: backendUrl,
    secure: false,
    changeOrigin: true,
    logLevel: 'info'
  },
  '/hubs/game': {
    target: backendUrl,
    secure: false,
    changeOrigin: true,
    ws: true,
    logLevel: 'info'
  },
  '/hubs/lobby': {
    target: backendUrl,
    secure: false,
    changeOrigin: true,
    ws: true,
    logLevel: 'info'
  }
};
