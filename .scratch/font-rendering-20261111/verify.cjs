const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const runtime = 'C:/Program Files/WindowsApps/OpenAI.CodexPrimaryRuntime.v26-1110-973-0_26.1110.973.0_x64__3k8sg7r9htsxt/dependencies/node/node_modules/';
const {chromium} = require(runtime + 'playwright');
const sharp = require(runtime + 'sharp');
const {html:fixture} = require('E:/DormPrj/.scratch/font-rendering-20261111/reproduce.cjs');
const root = path.resolve(__dirname, '../..');
const localCss = fs.readFileSync(path.join(root,'DormitoryManagementSystem/wwwroot/css/site.css'),'utf8');
const flag = process.argv[2] || 'before';
const browsers = [
  ['edge','C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe'],
  ['chrome','C:/Program Files/Google/Chrome/Application/chrome.exe']
];

async function measure(page, buffer) {
  const texts = await page.locator('.queue-person strong, .queue-person small, .request-state, .request-queue-item time').evaluateAll(nodes=>nodes.map(n=>{
    const r=n.getBoundingClientRect();
    const q=n.closest('.request-queue').getBoundingClientRect();
    const range=document.createRange();
    range.selectNodeContents(n);
    const ink=range.getBoundingClientRect();
    const computed=getComputedStyle(n);
    return {text:n.textContent,rect:{x:r.x-q.x,y:r.y-q.y,width:r.width,height:r.height},ink:{x:ink.x-q.x,y:ink.y-q.y,width:ink.width,height:ink.height},foreground:computed.color.match(/\d+/g).slice(0,3).map(Number),background:n.classList.contains('request-state') ? computed.backgroundColor.match(/\d+/g).slice(0,3).map(Number) : (n.closest('.selected') ? [237,248,246] : [255,255,255])};
  }));
  const {data,info}=await sharp(buffer).removeAlpha().raw().toBuffer({resolveWithObject:true});
  const dpr=await page.evaluate(()=>devicePixelRatio);
  let chromaticEdges=0,edgePixels=0;
  for(const item of texts){
    const fg=item.foreground,bg=item.background;
    const v=fg.map((x,i)=>x-bg[i]),norm=v.reduce((s,x)=>s+x*x,0);
    const box={left:Math.ceil(item.ink.x*dpr),top:Math.ceil(item.ink.y*dpr),right:Math.floor((item.ink.x+item.ink.width)*dpr),bottom:Math.floor((item.ink.y+item.ink.height)*dpr)};
    for(let y=box.top;y<box.bottom;y++)for(let x=box.left;x<box.right;x++){
      const p=(y*info.width+x)*info.channels;
      const c=[data[p],data[p+1],data[p+2]];
      const a=c.reduce((s,val,i)=>s+(val-bg[i])*v[i],0)/norm;
      if(a<.02||a>.98)continue;
      const residual=Math.max(...c.map((val,i)=>Math.abs(val-(bg[i]+a*v[i]))));
      edgePixels++;
      if(residual>4)chromaticEdges++;
    }
  }
  return {edgePixels,chromaticEdges,text: texts.map(x=>x.text),rects:texts.map(x=>x.rect)};
}

(async()=>{
  const results=[];
  let css=localCss;
  if(flag==='after'){
    const response=await fetch('http://localhost:5001/css/site.css',{cache:'no-store'});
    assert.equal(response.status,200);
    const served=await response.text();
    assert.equal(served,localCss,'The running website must serve the reviewed CSS');
    css=served;
  }
  const html=fixture.replace(/<style>[\s\S]*?<\/style>/,()=>`<style>${css}</style>`);
  for(const [name,executablePath] of browsers){
    const browser=await chromium.launch({executablePath,headless:true});
    for(const dpr of [1,1.25,1.5]){
      const page=await browser.newPage({viewport:{width:1100,height:900},deviceScaleFactor:dpr});
      const errors=[];
      page.on('pageerror',e=>errors.push(e.message));
      await page.setContent(html);
      await page.evaluate(()=>document.fonts.ready);
      const image=await page.locator('.request-queue').screenshot({path:path.join(__dirname,`${flag}-${name}-${dpr}.png`)});
      const measured=await measure(page,image);
      assert.equal(errors.length,0);
      assert.equal(measured.text.some(x=>x.includes('\uFFFD')),false);
      results.push({browser:name,dpr,...measured});
      await page.close();
    }
    await browser.close();
  }
  fs.writeFileSync(path.join(__dirname,`${flag}-results.json`),JSON.stringify(results,null,2));
  console.log(JSON.stringify(results.map(({browser,dpr,chromaticEdges,edgePixels})=>({browser,dpr,chromaticEdges,edgePixels})),null,2));
  if(flag==='after'){
    const before=JSON.parse(fs.readFileSync(path.join(__dirname,'before-results.json'),'utf8'));
    for(let i=0;i<results.length;i++){
      assert.deepEqual(results[i].text,before[i].text,'Vietnamese text must remain unchanged');
      assert.deepEqual(results[i].rects,before[i].rects,'The correction must preserve layout');
    }
  }
  for(const result of results)assert.equal(result.chromaticEdges,0,`${result.browser} DPR ${result.dpr}: color fringes remain around queue text`);
})().catch(e=>{console.error(e.message);process.exitCode=1});
