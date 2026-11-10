#!/usr/bin/env node
// Validate actual deployed bytes and headers against the staged Unity build.
const https = require('node:https');
const fs = require('node:fs');
const crypto = require('node:crypto');
const [base, manifestPath, output] = process.argv.slice(2);
if (!base || !manifestPath || !output) throw new Error('Usage: node tools/check_web_headers.cjs HTTPS_URL payload.json report.json');
const manifest = JSON.parse(fs.readFileSync(manifestPath));
const get = path => new Promise((resolve,reject) => {
 https.get(new URL(path,base),{headers:{'accept-encoding':'identity'}},r=>{
  const chunks=[]; r.on('data',b=>chunks.push(b));
  r.on('end',()=>resolve({status:r.statusCode,headers:r.headers,body:Buffer.concat(chunks)}));
 }).on('error',reject);
});
(async()=>{
 const files = await Promise.all(manifest.files.filter(f=>f.path!=='vercel.json').map(async f=>{
  const r=await get('/'+f.path); const digest=crypto.createHash('sha256').update(r.body).digest('hex');
  const expectedType=f.path.includes('.wasm')?'application/wasm':f.path.includes('.js')?'application/javascript':f.path.includes('.data')?'application/octet-stream':null;
  return {path:f.path,status:r.status,bytes:r.body.length,sha256Matches:digest===f.sha256,
   contentType:r.headers['content-type'],contentEncoding:r.headers['content-encoding']||null,
   pass:r.status===200&&digest===f.sha256&&(!expectedType||r.headers['content-type']?.startsWith(expectedType))&&(!f.path.endsWith('.br')||r.headers['content-encoding']==='br')};
 }));
 const forbidden=await Promise.all(['/.env.local','/AGENTS.md','/.agents/skills/','/.vercel/project.json','/Assets/'].map(async path=>({path,status:(await get(path)).status})));
 const report={url:base,utc:new Date().toISOString(),files,forbidden,pass:files.every(f=>f.pass)&&forbidden.every(f=>f.status===404)};
 fs.writeFileSync(output,JSON.stringify(report,null,2)+'\n');
 console.log(JSON.stringify({url:base,pass:report.pass,resources:files.length,forbidden:forbidden.map(f=>f.status)}));
 if(!report.pass)process.exitCode=1;
})();
