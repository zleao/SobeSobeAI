const { spawn } = require('node:child_process');
const path = require('node:path');

const port = process.env.PORT || '4200';

const ngCommand = process.platform === 'win32' ? 'ng.cmd' : 'ng';
const ngArgs = ['serve', '--port', port, '--proxy-config', 'proxy.conf.cjs'];

const child = spawn(ngCommand, ngArgs, {
  stdio: 'inherit',
  shell: true,
  cwd: path.resolve(__dirname, '..')
});

child.on('exit', (code) => {
  process.exit(code ?? 1);
});
