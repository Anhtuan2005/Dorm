const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const { chromium } = require('C:/Program Files/WindowsApps/OpenAI.CodexPrimaryRuntime.v26-1110-973-0_26.1110.973.0_x64__3k8sg7r9htsxt/dependencies/node/node_modules/playwright');
const css = fs.readFileSync(path.resolve(__dirname, '../../DormitoryManagementSystem/wwwroot/css/site.css'), 'utf8');
const statuses = [['Đang xét duyệt','reviewing'],['Đã gửi','submitted'],['Cần bổ sung','needs-info'],['Chờ nhận phòng','awaiting-movein']];
const html = `<!doctype html><html lang="vi"><meta charset="utf-8"><style>${css}</style><body class="app-body" style="padding:20px"><div style="display:grid;gap:12px">${statuses.map(([text,state])=>`<div class="request-queue-item selected" style="min-height:80px"><span class="queue-avatar">VH</span><span class="queue-person"><strong>Nguyễn Văn Hùng</strong><small>Chuyển phòng</small></span><span class="request-state ${state}">${text}</span></div>`).join('')}</div></body></html>`;
(async()=>{
  const browser=await chromium.launch({executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',headless:true});
  const results=[];
  try {
    for(const dpr of [1,1.25,1.5]) {
      const page=await browser.newPage({viewport:{width:430,height:620},deviceScaleFactor:dpr});
      await page.setContent(html);
      const badges=await page.locator('.request-state').evaluateAll(nodes=>nodes.map(node=>{
        const box=node.getBoundingClientRect(); const range=document.createRange();range.selectNodeContents(node);const text=range.getBoundingClientRect(); const style=getComputedStyle(node);
        return {text:node.textContent,display:style.display,align:style.alignItems,justify:style.justifyContent,height:box.height,offsetX:(text.x+text.width/2)-(box.x+box.width/2),offsetY:(text.y+text.height/2)-(box.y+box.height/2)};
      }));
      for(const badge of badges){assert.equal(badge.align,'center');assert.equal(badge.justify,'center');assert.ok(Math.abs(badge.offsetX)<=1);assert.ok(Math.abs(badge.offsetY)<=1.5);}
      await page.screenshot({path:path.join(__dirname,`badges-dpr-${dpr}.png`)});
      results.push({dpr,badges}); await page.close();
    }
    fs.writeFileSync(path.join(__dirname,'badge-checks.json'),JSON.stringify(results,null,2));
    console.log(JSON.stringify({checked:results.length*statuses.length,maxVerticalOffset:Math.max(...results.flatMap(x=>x.badges.map(b=>Math.abs(b.offsetY))))}));
  } finally {await browser.close();}
})().catch(error=>{console.error(error.message);process.exitCode=1;});
