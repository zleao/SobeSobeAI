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
  '/sobesobe.game.GameEvents': {
    target: backendUrl,
    secure: false,
    changeOrigin: true,
    logLevel: 'info'
  },
  '/sobesobe.game.LobbyEvents': {
    target: backendUrl,
    secure: false,
    changeOrigin: true,
    logLevel: 'info'
  }
};
