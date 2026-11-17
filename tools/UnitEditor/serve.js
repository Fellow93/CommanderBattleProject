// Serves the unit editor and reads and writes the unit files for it, so the page needs no folder picker:
//   node tools\UnitEditor\serve.js [ModuleData folder] [port]
// The folder defaults to this repository's Module\ModuleData, the port to 8123.
'use strict';
const http = require('http'), fs = require('fs'), path = require('path');

const dataDir = path.resolve(process.argv[2] || path.join(__dirname, '..', '..', 'Module', 'ModuleData'));
const port = +process.argv[3] || 8123;
// The page asks for ../../../RealisticBattleProject/...; a browser stops the ".." at the root.
const STATIC = {
  '/': path.join(__dirname, 'index.html'),
  '/index.html': path.join(__dirname, 'index.html'),
  '/unit-editor-core.js': path.join(__dirname, 'unit-editor-core.js'),
  '/unit-editor-data.js': path.join(__dirname, 'unit-editor-data.js'),
  '/RealisticBattleProject/tools/TroopLoadoutEditor/troop-loadout-core.js':
    path.join(__dirname, '..', '..', '..', 'RealisticBattleProject', 'tools', 'TroopLoadoutEditor', 'troop-loadout-core.js')
};
// Only the unit files, by plain name: nothing outside the folder can be named.
const UNIT_FILE = /^\/api\/file\/(cb_[a-z0-9_]+\.xml)$/;
const MAX_BODY = 8 * 1024 * 1024;

function send(res, status, body, type) {
  res.writeHead(status, { 'Content-Type': type || 'text/plain; charset=utf-8', 'Cache-Control': 'no-store' });
  res.end(body);
}
function sendFile(res, file, type) {
  fs.readFile(file, function (e, data) { e ? send(res, 404, 'Not found') : send(res, 200, data, type); });
}

http.createServer(function (req, res) {
  // Another site's page must not reach this server through a name that resolves to 127.0.0.1.
  if (['localhost:' + port, '127.0.0.1:' + port].indexOf(req.headers.host) < 0) return send(res, 403, 'Forbidden');
  const url = req.url.split('?')[0];
  const unit = UNIT_FILE.exec(url);

  if (req.method === 'GET' && STATIC[url]) return sendFile(res, STATIC[url], /\.js$/.test(url) ? 'text/javascript; charset=utf-8' : 'text/html; charset=utf-8');
  if (req.method === 'GET' && url === '/api/files') {
    return fs.readdir(dataDir, function (e, names) {
      e ? send(res, 500, e.message) : send(res, 200, JSON.stringify(names), 'application/json');
    });
  }
  if (req.method === 'GET' && unit) return sendFile(res, path.join(dataDir, unit[1]), 'application/octet-stream');
  if (req.method === 'PUT' && unit) {
    const file = path.join(dataDir, unit[1]);
    // The page only saves files it has read: a new name is a mistake.
    if (!fs.existsSync(file)) return send(res, 404, 'Not found');
    const chunks = [];
    let size = 0;
    req.on('data', function (c) {
      size += c.length;
      if (size > MAX_BODY) { send(res, 413, 'Too large'); req.destroy(); } else chunks.push(c);
    });
    req.on('end', function () {
      if (size > MAX_BODY) return;
      fs.writeFile(file, Buffer.concat(chunks), function (e) {
        if (!e) console.log('saved ' + unit[1]);
        e ? send(res, 500, e.message) : send(res, 200, 'ok');
      });
    });
    return;
  }
  send(res, 404, 'Not found');
}).on('error', function (e) {
  console.error(e.code === 'EADDRINUSE' ? 'Port ' + port + ' is in use: is the unit editor already running?' : e.message);
  process.exit(1);
}).listen(port, '127.0.0.1', function () {
  console.log('Unit editor: http://localhost:' + port + '/');
  console.log('Unit files:  ' + dataDir);
});
