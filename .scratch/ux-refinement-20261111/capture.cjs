const fs=require('node:fs');
const path=require('node:path');
const os=require('node:os');
const {chromium}=require('playwright');
const stage=process.argv[2]||'before';
const output=path.join(__dirname,stage);fs.mkdirSync(output,{recursive:true});
const base='http://localhost:5001';
const variants=JSON.parse(fs.readFileSync(path.join(os.tmpdir(),'nepktx-ux-session.secrets.json'),'utf8'));
const routes={manager:[['overview','/'],['rooms','/Rooms/Map'],['applications','/Applications'],['billing','/Billing'],['maintenance','/Maintenance'],['students','/Students'],['reports','/Reports'],['settings','/Settings'],['catalog','/Rooms/Catalog']],student:[['home','/Student/Dashboard'],['rooms','/Student/Rooms'],['applications','/Student/Applications'],['invoices','/Student/Invoices'],['repairs','/Student/Repairs'],['notifications','/Student/Notifications']]};
(async()=>{
 const browser=await chromium.launch({executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',headless:true});
 const findings=[];let chosen;
 try{
  for(const variant of variants){const context=await browser.newContext();await context.addCookies(variant.manager);const page=await context.newPage();const response=await page.goto(base);if(response.status()===200&&!page.url().includes('Login'))chosen=variant;await context.close();if(chosen)break;}
  if(!chosen)throw new Error('Could not establish a local review session.');
  for(const [role,items]of Object.entries(routes))for(const width of [1440,430]){
   const context=await browser.newContext({viewport:{width,height:width===430?932:1000}});await context.addCookies(chosen[role]);const page=await context.newPage();const errors=[];page.on('pageerror',e=>errors.push(e.message));
   for(const[name,url]of items){const response=await page.goto(base+url,{waitUntil:'networkidle'});await page.evaluate(()=>document.fonts.ready);await page.screenshot({path:path.join(output,`${role}-${name}-${width}.png`),fullPage:false});
    const details=await page.evaluate(()=>({title:document.querySelector('h1')?.textContent?.trim(),height:document.documentElement.scrollHeight,overflow:document.documentElement.scrollWidth>innerWidth,smallText:[...document.querySelectorAll('main p,main small,main label,main button,main a')].filter(n=>n.getBoundingClientRect().height>0&&parseFloat(getComputedStyle(n).fontSize)<12).length,badLinks:[...document.querySelectorAll('main a')].filter(n=>!n.getAttribute('href')||n.getAttribute('href')==='#').length,focus:document.querySelectorAll(':focus-visible').length}));
    findings.push({role,name,width,status:response.status(),...details,errors:[...errors]});
   }await context.close();
  }
  const context=await browser.newContext({viewport:{width:1440,height:1000}});const page=await context.newPage();await page.goto(base+'/Account/Login',{waitUntil:'networkidle'});await page.screenshot({path:path.join(output,'login-1440.png')});await context.close();
  fs.writeFileSync(path.join(output,'checks.json'),JSON.stringify(findings,null,2));
  const contact=await browser.newPage({viewport:{width:1440,height:1550}});const names=['manager-overview-1440','manager-applications-1440','manager-billing-1440','manager-maintenance-1440','student-home-1440','student-rooms-1440'];
  await contact.setContent('<body style="margin:0;padding:16px;background:#e7e9e8;display:grid;grid-template-columns:1fr 1fr;gap:14px;font:14px Arial">'+names.map(name=>'<div><p style="margin:0 0 6px">'+name+'</p><img style="display:block;width:100%;border:1px solid #ccd2d0" src="data:image/png;base64,'+fs.readFileSync(path.join(output,name+'.png')).toString('base64')+'"></div>').join('')+'</body>');await contact.screenshot({path:path.join(output,'contact.png'),fullPage:true});await contact.close();
  console.log(JSON.stringify({stage,pages:findings.length,overflow:findings.filter(x=>x.overflow).map(x=>`${x.role}/${x.name}/${x.width}`),badStatus:findings.filter(x=>x.status!==200).map(x=>`${x.role}/${x.name}`),runtimeErrors:findings.flatMap(x=>x.errors),smallText:findings.reduce((a,x)=>a+x.smallText,0)}));
 }finally{await browser.close();}
})().catch(error=>{console.error(error.message);process.exitCode=1;});
