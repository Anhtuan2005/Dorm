const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const runtime = 'C:/Program Files/WindowsApps/OpenAI.CodexPrimaryRuntime.v26-1110-973-0_26.1110.973.0_x64__3k8sg7r9htsxt/dependencies/node/node_modules/';
const { chromium } = require(runtime + 'playwright');
const { html: fixture } = require('E:/DormPrj/.scratch/font-rendering-20261111/reproduce.cjs');

(async () => {
  const css = fs.readFileSync(path.resolve(__dirname, '../../DormitoryManagementSystem/wwwroot/css/site.css'), 'utf8');
  const served = await (await fetch('http://localhost:5001/css/site.css', {cache:'no-store'})).text();
  assert.equal(served, css, 'Running application CSS must match the edited file');
  const html = fixture.replace(/<style>[\s\S]*?<\/style>/, () => `<style>${css}</style>`);
  const browser = await chromium.launch({executablePath:'C:/Program Files/Google/Chrome/Application/chrome.exe',headless:true});
  const results = [];
  for (const [width, dpr] of [[292,1],[305,1.25],[360,1.25],[420,1],[740,1.25]]) {
    const page = await browser.newPage({viewport:{width:Math.max(320,width+28),height:900},deviceScaleFactor:dpr});
    await page.setContent(html);
    await page.locator('.request-queue').evaluate((n,w) => n.style.width = `${w}px`, width);
    await page.evaluate(() => document.fonts.ready);
    const report = await page.evaluate(() => {
      const selectors = ['.request-queue > header strong','.queue-person strong','.queue-person small','.request-queue-item > .request-state','.queue-route','.request-queue-item time'];
      const queue = document.querySelector('.request-queue');
      return {sizes:selectors.map(s=>({selector:s,px:parseFloat(getComputedStyle(document.querySelector(s)).fontSize)})),overflow:queue.scrollWidth>queue.clientWidth,clipped:[...document.querySelectorAll('.queue-person strong,.queue-person small,.request-state,.queue-route,.request-queue-item time')].filter(n=>n.scrollWidth>n.clientWidth+1).map(n=>n.textContent),overlaps:[...document.querySelectorAll('.request-queue-item')].filter(n=>{const person=n.querySelector('.queue-person').getBoundingClientRect();const state=n.querySelector('.request-state').getBoundingClientRect();return person.right>state.left+1}).length};
    });
    assert.equal(report.overflow,false);
    assert.deepEqual(report.clipped,[]);
    assert.equal(report.overlaps,0);
    await page.locator('.request-queue').screenshot({path:path.join(__dirname,`queue-${width}-${dpr}.png`)});
    if(width===292){
      await page.locator('.queue-person strong').first().evaluate(n=>n.textContent='Nguyễn Trương Anh Tuấn');
      await page.locator('.request-state').first().evaluate(n=>n.textContent='Chờ nhận phòng');
      assert.equal(await page.locator('.request-queue').evaluate(n=>n.scrollWidth>n.clientWidth),false);
      await page.locator('.request-queue').screenshot({path:path.join(__dirname,'long-name-mobile.png')});
    }
    results.push({width,dpr,...report});
    await page.close();
  }
  await browser.close();
  fs.writeFileSync(path.join(__dirname,'visual-checks.json'),JSON.stringify(results,null,2));
  console.log(JSON.stringify({widths:results.map(x=>x.width),fontSizes:results[0].sizes.map(x=>x.px),overflow:false,overlaps:0,servedCssMatches:true}));
})().catch(e=>{console.error(e);process.exitCode=1;});
