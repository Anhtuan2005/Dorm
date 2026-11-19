const fs=require('node:fs'),path=require('node:path'),os=require('node:os'),assert=require('node:assert/strict');
const {chromium}=require('playwright');
const auth=JSON.parse(fs.readFileSync(path.join(os.tmpdir(),'nepktx-ux-session.secrets.json'),'utf8'))[0];
const base='http://localhost:5001';
(async()=>{
 const browser=await chromium.launch({executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',headless:true});const checks=[];const errors=[];
 try{
  const manager=await browser.newContext({viewport:{width:1440,height:1000}});await manager.addCookies(auth.manager);const page=await manager.newPage();page.on('pageerror',e=>errors.push(e.message));
  await page.goto(base+'/Applications?Scope=attention&OverdueOnly=true');
  await page.keyboard.press('Control+k');assert.equal(await page.locator('[data-global-search]').evaluate(n=>n===document.activeElement),true);checks.push('keyboard search shortcut');
  await page.locator('.queue-toolbar input[name=Search]').fill('Nguyễn');
  await Promise.all([page.waitForURL(url=>url.searchParams.has('Search')),page.locator('.queue-toolbar button[type=submit]').click()]);
  assert.equal(new URL(page.url()).searchParams.get('OverdueOnly'),'true');checks.push('application search preserves overdue scope');
  assert.equal(await page.locator('main').count(),1);checks.push('single main landmark');
  assert.match(await page.locator('.decision-rail h2').innerText(),/Xét duyệt|Tiếp nhận/);checks.push('review heading matches unfinished state');
  await page.goto(base+'/Billing');
  await page.locator('select[data-payment-method]').selectOption('Cash');assert.equal(await page.locator('[data-bank-reference]').isVisible(),false);
  await page.locator('select[data-payment-method]').selectOption('BankTransfer');assert.equal(await page.locator('[data-bank-reference] input').getAttribute('required'),'');checks.push('payment method retains reference validation');
  assert.equal(await page.locator('.optional-note').getAttribute('open'),null);await page.locator('.optional-note summary').click();assert.equal(await page.locator('textarea[name=Note]').isVisible(),true);checks.push('optional notes remain accessible');
  await page.locator('.collection-form input[name=Amount]').fill('-1');await page.locator('.collection-form').evaluate(form=>{form.noValidate=true;});
  await Promise.all([page.waitForNavigation({waitUntil:'domcontentloaded'}),page.locator('.collect-button').click()]);
  assert.equal(await page.locator('.workspace-flash.error').isVisible(),true);await page.waitForTimeout(4500);assert.equal(await page.locator('.workspace-flash.error').isVisible(),true);checks.push('validation error persists until dismissed');
  await page.locator('[data-flash-dismiss]').click();assert.equal(await page.locator('.workspace-flash').count(),0);
  await page.setViewportSize({width:430,height:932});await page.goto(base+'/Applications');
  assert.equal(await page.locator('.request-queue-list').isVisible(),false);await page.getByRole('button',{name:'Hiện danh sách'}).click();
  const target=page.locator('.request-queue-item').nth(1);const id=new URL(await target.getAttribute('href'),base).searchParams.get('SelectedId');
  await Promise.all([page.waitForURL(url=>url.searchParams.get('SelectedId')===id),target.click()]);
  assert.equal(await page.locator('.request-queue-list').isVisible(),false);assert.equal(await page.locator('.application-case input[name=Id]').first().inputValue(),id);checks.push('mobile case selection opens the correct record');
  await page.locator('[data-mobile-menu]').click();await page.waitForFunction(()=>document.querySelector('.sidebar').inert===false);
  assert.equal(await page.locator('#main-content').evaluate(n=>n.inert),true);await page.locator('.sidebar .brand').focus();await page.keyboard.press('Shift+Tab');assert.equal(await page.locator('.sidebar .main-nav a').last().evaluate(n=>n===document.activeElement),true);
  await page.keyboard.press('Escape');await page.waitForFunction(()=>document.querySelector('.sidebar').inert);assert.equal(await page.locator('[data-mobile-menu]').evaluate(n=>n===document.activeElement),true);checks.push('mobile navigation traps and restores focus');
  await page.goto(base+'/Billing');assert.equal(await page.locator('.filter-options').getAttribute('open'),null);await page.locator('.filter-options > summary').click();assert.equal(await page.locator('select[name=Status]').isVisible(),true);checks.push('mobile filters disclose on demand');
  await page.goto(base+'/Students');assert.equal(await page.locator('.student-record-scroll').isVisible(),false);await page.getByRole('button',{name:'Hiện danh sách'}).click();const studentRow=page.locator('.student-record').nth(1);const chosenName=await studentRow.locator('strong').innerText();await Promise.all([page.waitForURL(url=>url.searchParams.has('SelectedId')),studentRow.click()]);assert.equal(await page.locator('.student-profile-cover h2').innerText(),chosenName);assert.equal(await page.locator('.student-record-scroll').isVisible(),false);checks.push('mobile student directory opens the chosen profile');
  for(const width of [360,768,1024,1280,1920]){
   await page.setViewportSize({width,height:1000});
   for(const route of ['/','/Applications','/Billing','/Maintenance','/Students','/Reports','/Rooms/Map']){
    await page.goto(base+route,{waitUntil:'domcontentloaded'});
    assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth),false,`Overflow ${width} ${route}`);
    assert.equal(await page.locator('main').count(),1);
    assert.equal(await page.locator('form[method=post]').evaluateAll(forms=>forms.every(f=>f.querySelector('input[name="__RequestVerificationToken"]'))),true);
   }
  }checks.push('35 extra viewport checks and protected forms');
  const student=await browser.newContext({viewport:{width:430,height:932}});await student.addCookies(auth.student);const portal=await student.newPage();portal.on('pageerror',e=>errors.push(e.message));
  await portal.goto(base+'/Student/Dashboard');assert.ok(!(await portal.locator('.dashboard-head').innerText()).includes('09/09/2026'));checks.push('student date is current');
  await portal.locator('[data-student-menu]').click();await portal.waitForFunction(()=>document.querySelector('.student-sidebar').inert===false);await portal.keyboard.press('Escape');assert.equal(await portal.locator('[data-student-menu]').getAttribute('aria-expanded'),'false');checks.push('student drawer keyboard access');
  await portal.goto(base+'/Student/Rooms');assert.equal(await portal.locator('.student-room-card button:disabled').count(),0);assert.equal(await portal.locator('.active-request-banner a').isVisible(),true);checks.push('pending request has one clear next step');
  await portal.route('**/Chatbot/Ask',route=>route.fulfill({status:200,contentType:'application/json',body:JSON.stringify({success:true,text:'Mở Đăng ký của tôi để theo dõi đơn.'})}));
  await portal.locator('.ai-launcher').click();await portal.locator('[data-chat-input]').fill('Theo dõi đơn ở đâu?');await portal.locator('.ai-composer button').click();await portal.getByText('Mở Đăng ký của tôi để theo dõi đơn.',{exact:true}).waitFor();assert.equal(await portal.locator('.ai-message .ai-link').last().getAttribute('href'),'/Student/Applications');await portal.keyboard.press('Escape');checks.push('chat remains usable in the refined shell');
  const anonymous=await browser.newContext({viewport:{width:430,height:932}});const login=await anonymous.newPage();await login.goto(base+'/Account/Login');await login.locator('input[name=Password]').fill('not-a-real-password');await login.locator('[data-password-toggle]').click();assert.equal(await login.locator('input[name=Password]').getAttribute('type'),'text');checks.push('login password visibility');
  assert.deepEqual(errors,[]);fs.writeFileSync(path.join(__dirname,'interaction-checks.json'),JSON.stringify({checks,errors},null,2));console.log(JSON.stringify({checks,errors}));
  await manager.close();await student.close();await anonymous.close();
 }finally{await browser.close();}
})().catch(error=>{console.error(error.message);process.exitCode=1;});
