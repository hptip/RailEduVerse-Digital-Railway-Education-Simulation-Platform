import { mkdir, readdir, writeFile } from 'node:fs/promises';
import { spawn } from 'node:child_process';
import { resolve } from 'node:path';

const path = resolve(process.env.UTTVR_TEST_OUTPUT ?? 'Documentation/PlatformCompatibility/WebTests');
const url = process.argv[2] ?? 'http://127.0.0.1:8765/';
const chrome = process.env.UTTVR_CHROME ?? 'C:/Program Files/Google/Chrome/Application/chrome.exe';
const port = Number(process.env.UTTVR_CDP_PORT ?? 9877);
const sharedMenu = process.env.UTTVR_SHARED_MENU === '1';
const campusDoorProbe = process.env.UTTVR_CAMPUS_DOOR_PROBE === '1';
const evidence = [];
const logs = [];
const performanceSamples = [];
let action = 'Startup';
const delay = ms => new Promise(done => setTimeout(done, ms));
const browser = spawn(chrome, [
  '--remote-debugging-port=' + port, '--user-data-dir=' + resolve(process.env.UTTVR_CHROME_PROFILE ?? 'Temp/WebDemoChrome-' + Date.now()),
  '--no-first-run', '--no-default-browser-check', '--window-size=1500,900',
  ...(process.env.UTTVR_HEADLESS === '1' ? ['--headless=new', '--enable-unsafe-swiftshader'] : []),
  'about:blank'
], { stdio:['ignore','ignore','pipe'], windowsHide:false });
browser.stderr.setEncoding('utf8');
browser.stderr.on('data', chunk => logs.push({type:'Chrome process',text:chunk.trim()}));
browser.on('error', error => logs.push({type:'Chrome launch',text:String(error)}));
let socket;
const commands = new Map();
let id = 0;
function send(method, params = {}) {
  const request = ++id;
  return new Promise((done, fail) => {
    const timeout = setTimeout(() => { commands.delete(request); fail(new Error(method + ' timed out')); }, method === 'Page.captureScreenshot' ? 90000 : 30000);
    commands.set(request, data => { clearTimeout(timeout); data.error ? fail(new Error(JSON.stringify(data.error))) : done(data.result); });
    socket.send(JSON.stringify({id:request, method, params}));
  });
}
async function evalPage(expression) {
  const result = await send('Runtime.evaluate', {expression, returnByValue:true, awaitPromise:true});
  if (result.exceptionDetails) throw new Error(result.exceptionDetails.text);
  return result.result.value;
}
async function capture(name) {
  const image = await send('Page.captureScreenshot', {format:'png',captureBeyondViewport:false});
  await writeFile(path + '/Evidence/' + name + '.png', Buffer.from(image.data,'base64'));
}
async function samplePerformance(scene) {
  if (!process.env.UTTVR_PERFORMANCE) return;
  const cpuBefore = (await send('Performance.getMetrics')).metrics.find(m => m.name === 'TaskDuration')?.value;
  const reading = await evalPage(`new Promise(resolve => {
    const intervals = [];
    const renderedIntervals = [];
    const draws = {calls:0, triangles:0};
    let lastDrawCount = 0;
    let lastRender;
    let previous;
    const end = performance.now() + 5000;
    window.uttDrawProbe = draws;
    function measure(now) {
      if (previous !== undefined) intervals.push(now - previous);
      previous = now;
      if (lastDrawCount !== draws.calls) {
        if (lastRender !== undefined) renderedIntervals.push(now - lastRender);
        lastRender = now;
        lastDrawCount = draws.calls;
      }
      if (now < end) requestAnimationFrame(measure);
      else {
        window.uttDrawProbe = null;
        resolve({intervals, renderedIntervals, draws, jsHeapBytes: performance.memory?.usedJSHeapSize ?? null});
      }
    }
    requestAnimationFrame(measure);
  })`);
  const cpuAfter = (await send('Performance.getMetrics')).metrics.find(m => m.name === 'TaskDuration')?.value;
  const times = reading.intervals.sort((a,b) => a-b);
  const meanMs = times.reduce((sum,ms) => sum+ms,0) / times.length;
  const rendered = reading.renderedIntervals.sort((a,b) => a-b);
  const renderedMs = rendered.length ? rendered.reduce((sum,ms) => sum+ms,0) / rendered.length : null;
  const percentile = (values, q) => values[Math.ceil(values.length*q)-1] ?? null;
  performanceSamples.push({scene, frames:times.length, meanMs, fps:1000/meanMs,
    p95Ms:percentile(times,.95), p99Ms:percentile(times,.99), jsHeapBytes:reading.jsHeapBytes,
    renderedFrames:rendered.length, renderedFps:renderedMs ? 1000/renderedMs : null, renderedMeanMs:renderedMs,
    renderedP95Ms:percentile(rendered,.95), renderedP99Ms:percentile(rendered,.99),
    glDrawsPerFrame:rendered.length ? reading.draws.calls/(rendered.length+1) : null,
    submittedTrianglesPerFrame:rendered.length ? reading.draws.triangles/(rendered.length+1) : null,
    browserMainThreadMsPerRaf:cpuBefore === undefined || cpuAfter === undefined ? null : (cpuAfter-cpuBefore)*1000/times.length});
}
async function mouse(x,y) {
  await send('Input.dispatchMouseEvent',{type:'mouseMoved',x,y});
  await delay(150);
  await send('Input.dispatchMouseEvent',{type:'mousePressed',button:'left',buttons:1,x,y,clickCount:1});
  await delay(200);
  await send('Input.dispatchMouseEvent',{type:'mouseReleased',button:'left',buttons:0,x,y,clickCount:1});
}
async function key(name, code) {
  const virtualKey = ({Tab:9, Escape:27, F1:112})[name] ?? name.toUpperCase().charCodeAt(0);
  await send('Input.dispatchKeyEvent',{type:'keyDown',key:name,code,windowsVirtualKeyCode:virtualKey});
  await delay(70);
  await send('Input.dispatchKeyEvent',{type:'keyUp',key:name,code});
}
async function holdKey(name, ms) {
  const code = 'Key' + name.toUpperCase();
  await send('Input.dispatchKeyEvent',{type:'keyDown',key:name,code,windowsVirtualKeyCode:name.toUpperCase().charCodeAt(0)});
  await delay(ms);
  await send('Input.dispatchKeyEvent',{type:'keyUp',key:name,code});
}
async function followRoute(route) {
  for (const [name, ms] of route) {
    if (name === 'space') await key(' ', 'Space');
    else if (name === 'wait') await delay(ms);
    else await holdKey(name, ms);
    await delay(100);
  }
}
async function look(dx, dy) {
  const x = 610, y = 350;
  await send('Input.dispatchMouseEvent',{type:'mouseMoved',buttons:0,x,y});
  await send('Input.dispatchMouseEvent',{type:'mousePressed',button:'right',buttons:2,x,y,clickCount:1});
  await delay(150);
  for (let step=1; step<=5; step++) {
    await send('Input.dispatchMouseEvent',{type:'mouseMoved',buttons:2,x:x+dx*step/5,y:y+dy*step/5});
    await delay(90);
  }
  await send('Input.dispatchMouseEvent',{type:'mouseReleased',button:'right',buttons:0,x:x+dx,y:y+dy,clickCount:1});
}
async function typeText(value) {
  for (const ch of value) {
    const code = /[A-Za-z]/.test(ch) ? 'Key'+ch.toUpperCase() : /\d/.test(ch) ? 'Digit'+ch : 'Minus';
    const params={key:ch,code,windowsVirtualKeyCode:ch.toUpperCase().charCodeAt(0)};
    await send('Input.dispatchKeyEvent',{type:'keyDown',text:ch,unmodifiedText:ch,...params});
    await delay(60);
    await send('Input.dispatchKeyEvent',{type:'keyUp',...params});
  }
}
try {
  await mkdir(path + '/Evidence',{recursive:true});
  let tabs;
  for(let attempt=0; attempt<25 && !tabs; attempt++) {
    try {tabs=await (await fetch('http://127.0.0.1:'+port+'/json/list')).json();} catch { await delay(400); }
  }
  if(!tabs) throw new Error('Chrome CDP endpoint did not start.');
  socket = new WebSocket(tabs.find(t=>t.type==='page').webSocketDebuggerUrl);
  await new Promise((done,fail)=> {socket.onopen=done;socket.onerror=fail;});
  socket.onmessage=event=>{
    const m=JSON.parse(event.data);
    if(m.id){commands.get(m.id)?.(m);commands.delete(m.id);return;}
    if(m.method==='Runtime.consoleAPICalled') {
      logs.push({type:m.params.type,text:m.params.args.map(x=>x.value??x.description).join(' ')});
      if (m.params.type === 'error')
        logs.push({type:'Console error context',text:JSON.stringify({action,stack:m.params.stackTrace?.callFrames})});
    }
    if(m.method==='Runtime.exceptionThrown'||m.method==='Log.entryAdded'||m.method==='Network.loadingFailed') logs.push({type:m.method,text:JSON.stringify(m.params)});
  };
  await Promise.all(['Runtime.enable','Page.enable','Log.enable','Network.enable','Performance.enable'].map(name=>send(name)));
  await send('Page.addScriptToEvaluateOnNewDocument',{source:`
    const getContext = HTMLCanvasElement.prototype.getContext;
    HTMLCanvasElement.prototype.getContext = function(type, ...args) {
      const context = getContext.call(this, type, ...args);
      if (context && /webgl/i.test(type)) {
        const debug = context.getExtension('WEBGL_debug_renderer_info');
        window.uttWebGL = {type, version: context.getParameter(context.VERSION), renderer: debug ? context.getParameter(debug.UNMASKED_RENDERER_WEBGL) : null};
        this.addEventListener('webglcontextlost', () => console.error('UTTVR WebGL context lost'), {once:true});
        if (${Boolean(process.env.UTTVR_PERFORMANCE)} && !context.uttDrawTracked) {
          context.uttDrawTracked = true;
          const commands = {drawArrays:[2,-1], drawElements:[1,-1], drawRangeElements:[3,-1],
            drawArraysInstanced:[2,3], drawElementsInstanced:[1,4]};
          for (const [name, [countIndex, instanceIndex]] of Object.entries(commands)) {
            const draw = context[name];
            if (!draw) continue;
            context[name] = function(...args) {
              const probe = window.uttDrawProbe;
              if (probe) {
                const count = args[countIndex];
                const instances = instanceIndex < 0 ? 1 : args[instanceIndex];
                const faces = args[0] === 4 ? Math.floor(count/3) : (args[0] === 5 || args[0] === 6) ? Math.max(0,count-2) : 0;
                probe.calls++;
                probe.triangles += faces * instances;
              }
              return draw.apply(this,args);
            };
          }
        }
      }
      return context;
    };
  `});
  await send('Emulation.setDeviceMetricsOverride',{width:1280,height:720,deviceScaleFactor:1,mobile:false});
  const started=Date.now();
  await send('Page.navigate',{url});
  await send('Page.bringToFront');
  for(let i=0;i<240;i++){
    const loaded=await evalPage("typeof unityInstance !== 'undefined' && !!unityInstance?.Module || !!window.uttUnity");
    const bar=await evalPage("document.querySelector('#unity-loading-bar')?.style.display");
    if(loaded || bar==='none'){evidence.push({Scene:'Startup',Action:'HTTP load',Observed:'Unity startup after '+((Date.now()-started)/1000).toFixed(1)+' s',Status:'OBSERVED'});break;}
    if(i===239) throw new Error('Unity startup not confirmed within 240 seconds');
    await delay(1000);
  }
  const focusCanvas = () => evalPage(`(() => {
    const canvas = document.querySelector('#unity-canvas');
    const container = document.querySelector('#unity-container');
    if (container) container.style.cssText = 'position:fixed;inset:0;transform:none';
    if (canvas) {
      canvas.style.cssText = 'width:100vw;height:100vh;display:block';
      canvas.tabIndex = 0;
      canvas.focus();
    }
  })()`);
  await delay(2500);
  logs.push({type:'WebGL context',text:JSON.stringify(await evalPage('window.uttWebGL ?? null'))});
  await focusCanvas();
  await delay(700);
  if (sharedMenu) {
    await capture('Login_at_start');
    await samplePerformance('Login');
    if (process.env.UTTVR_LOGIN_USERNAME) {
      await mouse(620,190);
      await send('Input.dispatchKeyEvent',{type:'keyDown',key:'a',code:'KeyA',modifiers:2,windowsVirtualKeyCode:65});
      await send('Input.dispatchKeyEvent',{type:'keyUp',key:'a',code:'KeyA',modifiers:2,windowsVirtualKeyCode:65});
      await typeText(process.env.UTTVR_LOGIN_USERNAME);
    }
    if (process.env.UTTVR_LOGIN_ROLE_PROBE === '1') {
      await mouse(905,255);
      await delay(450);
      await capture('Login_lecturer_selection');
      await mouse(643,590);
      await delay(800);
      await capture('Login_mismatched_role');
    }
    await mouse(905,process.env.UTTVR_LOGIN_ROLE === 'teacher' ? 255 : 192);
    await delay(500);
    await capture('Login_student_role');
    await mouse(643,590);
    await delay(8000);
    await capture('Campus_after_login');
    await samplePerformance('UTTCampus');
    if (campusDoorProbe) {
      const route = process.env.UTTVR_CAMPUS_ROUTE ? JSON.parse(process.env.UTTVR_CAMPUS_ROUTE) : [
        ['w', Number(process.env.UTTVR_CAMPUS_WALK_MS ?? 2300)], ['d', Number(process.env.UTTVR_CAMPUS_RIGHT_MS ?? 540)]];
      await followRoute(route);
      if (process.env.UTTVR_CAMPUS_YAW_PIXELS) await look(Number(process.env.UTTVR_CAMPUS_YAW_PIXELS), 0);
      await capture('Campus_door_approach');
      action = 'Campus door click';
      const beforeDoor = logs.length;
      await mouse(Number(process.env.UTTVR_CAMPUS_DOOR_X ?? 875),Number(process.env.UTTVR_CAMPUS_DOOR_Y ?? 385));
      await delay(5000);
      await capture('Campus_after_door_click');
      const hit = logs.slice(beforeDoor).find(entry => entry.text.includes('Door_Trigger_'));
      evidence.push({Scene:'UTTCampus',Action:'Click physical door',Observed:hit?.text ?? 'No door interaction logged',Status:hit?'OBSERVED':'UNVERIFIED'});
    }
    if (!campusDoorProbe && process.env.UTTVR_UI_PROBES === '1') {
      const otherTab = await send('Target.createTarget',{url:'about:blank',background:false});
      await delay(350);
      await send('Target.closeTarget',{targetId:otherTab.targetId});
      await send('Page.bringToFront');
      await focusCanvas();
      evidence.push({Scene:'UTTCampus',Action:'Switch browser tab and return',Observed:'Returned focus to game canvas',Status:'OBSERVED'});
    }
    if (!campusDoorProbe) {
      await key('m','KeyM');
      await delay(500);
      await capture('Shared_menu_1280x720');
      await send('Emulation.setDeviceMetricsOverride',{width:1920,height:1080,deviceScaleFactor:1,mobile:false});
      await delay(800);
      await capture('Shared_menu_1920x1080');
      await send('Emulation.setDeviceMetricsOverride',{width:1280,height:720,deviceScaleFactor:1,mobile:false});
      await delay(650);
    }
  } else if (process.env.UTTVR_STARTS_LOGIN === '1') {
    await capture('Login_at_start');
    await samplePerformance('Login');
    if (process.env.UTTVR_UI_PROBES === '1') {
      await mouse(620,190);
      await send('Input.dispatchKeyEvent',{type:'keyDown',key:'a',code:'KeyA',modifiers:2,windowsVirtualKeyCode:65});
      await send('Input.dispatchKeyEvent',{type:'keyUp',key:'a',code:'KeyA',modifiers:2,windowsVirtualKeyCode:65});
      await typeText('web-demo');
      await mouse(845,192);
      await capture('Login_input_and_role');
    }
    await key('m','KeyM');
    await delay(400);
    await mouse(640,265);
    await delay(3500);
  }
  if (!sharedMenu) await capture('Classroom_1280x720');
  if (!sharedMenu && process.env.UTTVR_SKIP_CLASSROOM_SMOKE !== '1') {
  await key('m','KeyM');
  await delay(1000);
  await capture('Classroom_menu_closed');
  await samplePerformance('Classroom');
  if(process.env.UTTVR_UI_PROBES === '1'){
    const lectureKey = process.env.UTTVR_LECTURE_KEY ?? 'l';
    const lectureCode = lectureKey === 'Tab' ? 'Tab' : 'Key' + lectureKey.toUpperCase();
    await key(lectureKey, lectureCode);
    await delay(700);
    await capture('Classroom_student_panel');
    if (process.env.UTTVR_CLASSROOM_PROBE === '1') {
      await mouse(390,120);
      await delay(900);
      await capture('Classroom_lesson_next');
      await mouse(802,586);
      await delay(700);
      await capture('Classroom_slide_next');
      await mouse(680,586);
      await delay(700);
      await capture('Classroom_slide_previous');
      await mouse(150,120);
      await delay(700);
      await capture('Classroom_lesson_previous');
      await mouse(111,60);
      await delay(500);
      await capture('Classroom_lesson_dropdown');
      await mouse(65,92);
      await delay(700);
      await capture('Classroom_lesson_dropdown_selected');
      await mouse(80,24);
      await delay(500);
      await capture('Classroom_panel_close');
    } else {
      await key(lectureKey, lectureCode);
      await delay(400);
    }
  }
  await send('Emulation.setDeviceMetricsOverride',{width:1920,height:1080,deviceScaleFactor:1,mobile:false});
  await delay(800);
  await capture('Classroom_1920x1080');
  await key('m','KeyM');
  await delay(400);
  await capture('Classroom_menu_1920x1080');
  await send('Emulation.setDeviceMetricsOverride',{width:1280,height:720,deviceScaleFactor:1,mobile:false});
  await delay(400);
  }
  const menu = ['UTTCampus','Classroom','Showroom','YenTuHaLongXanh','QuanLyVongDoiTaiSan','ChiTietSaBan','Login'];
  const geometry=await evalPage("JSON.stringify(document.querySelector('canvas')?.getBoundingClientRect().toJSON())");
  const canvas=JSON.parse(geometry);
  const defaultScenes = campusDoorProbe ? '' : sharedMenu ? '1,2,3,4,0' : '5,4,3,0,2,6';
  const order=(process.env.UTTVR_SCENES ?? defaultScenes).split(',').filter(Boolean).map(Number);
  for (const [step,index] of order.entries()) {
    action = 'Open ' + menu[index];
    const beforeNavigation = logs.length;
    if(step>0) await key('m','KeyM');
    await delay(400);
    if (sharedMenu) {
      await capture('Shared_menu_before_' + menu[index]);
      const y = Number(process.env.UTTVR_MENU_FIRST_Y ?? 200) + Number(process.env.UTTVR_MENU_ROW_HEIGHT ?? 88)*index;
      if (process.env.UTTVR_MENU_KEYBOARD === '1') await key(String(index+1), 'Digit'+(index+1));
      else await mouse(canvas.x+canvas.width/2,canvas.y+canvas.height*y/720);
    } else await mouse(canvas.x+canvas.width/2,canvas.y+canvas.height*(200+65*index)/720);
    await delay(index===2?12000:6500);
    if (index===4 && process.env.UTTVR_LIFECYCLE_STAGE_PROBE === '1') {
      for (let i=0;i<120;i++) {
        if (logs.slice(beforeNavigation).some(entry => entry.text.includes('[RailwayBim] Ready'))) break;
        if (i===119) throw new Error('Lifecycle database did not report ready');
        await delay(1000);
      }
      await delay(800);
    }
    try { await capture(menu[index]+'_after_click'); }
    catch (error) { logs.push({type:'capture',text:menu[index]+': '+error.message}); continue; }
    await samplePerformance(menu[index]);
    evidence.push({Scene:menu[index],Action:'Mouse click menu destination',Observed:'Screenshot captured; inspect scene and errors',Status:'UNVERIFIED'});
    if (sharedMenu && index===1 && process.env.UTTVR_CLASSROOM_PROBE === '1') {
      await key('l','KeyL');
      await delay(800);
      await capture('Classroom_student_panel');
      await mouse(390,120);
      await delay(700);
      await capture('Classroom_lesson_next');
      await mouse(802,586);
      await delay(700);
      await capture('Classroom_slide_next');
      await mouse(680,586);
      await delay(700);
      await capture('Classroom_slide_previous');
      await mouse(150,120);
      await delay(700);
      await capture('Classroom_lesson_previous');
      await mouse(112,60);
      await delay(450);
      await capture('Classroom_lesson_dropdown');
      await mouse(65,92);
      await delay(600);
      await capture('Classroom_lesson_dropdown_selected');
      await mouse(80,24);
      await delay(400);
    }
    if (index===2 && process.env.UTTVR_SHOWROOM_PROBE === '1') {
      await mouse(599,337);
      await delay(2300);
      await capture('Showroom_train_play');
      await mouse(619,343);
      await delay(1800);
      await capture('Showroom_train_speed4');
      await mouse(599,337);
      await delay(600);
      await capture('Showroom_train_pause');
      await mouse(599,349);
      await delay(650);
      await capture('Showroom_train_reset');
      for (const [id,x,y] of [['train',585,358],['terrain',613,358],['nature',585,364],['rail',613,364],
        ['bridge',585,370],['tunnel',613,370],['station',585,375],['other',613,375]]) {
        await mouse(x,y);
        await delay(470);
        await capture('Showroom_layer_' + id + '_off');
        await mouse(x,y);
        await delay(470);
      }
      await capture('Showroom_layers_restored');
    }
    if (index===3 && process.env.UTTVR_YENTU_PROBE === '1') {
      const route = process.env.UTTVR_YENTU_ROUTE ? JSON.parse(process.env.UTTVR_YENTU_ROUTE) : [
        ['w', Number(process.env.UTTVR_YENTU_WALK_MS ?? 1800)]];
      await followRoute(route);
      await look(Number(process.env.UTTVR_YENTU_YAW_PIXELS ?? 0), Number(process.env.UTTVR_YENTU_PITCH_DELTA ?? 22));
      const aim = [Number(process.env.UTTVR_YENTU_AIM_X ?? 640), Number(process.env.UTTVR_YENTU_AIM_Y ?? 360)];
      await send('Input.dispatchMouseEvent',{type:'mouseMoved',buttons:0,x:aim[0],y:aim[1]});
      await capture('YenTu_driver_approach');
      const beforeSeat = logs.length;
      if (process.env.UTTVR_YENTU_SEAT_CLICK === '1') await mouse(...aim);
      else await key('f','KeyF');
      await delay(1200);
      await capture('YenTu_driver_seat_probe');
      if (process.env.UTTVR_YENTU_DRIVE_PROBE === '1') {
        const seated = logs.slice(beforeSeat).some(entry => entry.text.includes('[TrainSeat] Đã vào ghế lái.'));
        evidence.push({Scene:menu[index],Action:'Enter driver seat',Observed:seated?'Driver seat activation logged':'Driver seat activation not observed',Status:seated?'PASS':'UNVERIFIED'});
        if (seated) {
          await holdKey('w', 4000);
          await capture('YenTu_applied_throttle');
          await delay(Number(process.env.UTTVR_YENTU_DRIVE_MS ?? 22000));
          await capture('YenTu_after_travel');
          await holdKey('s', 4200);
          await capture('YenTu_applied_brake');
        }
      }
    }
    if(process.env.UTTVR_UI_PROBES === '1' && index===6){
      await mouse(620,190);
      await send('Input.dispatchKeyEvent',{type:'keyDown',key:'a',code:'KeyA',modifiers:2,windowsVirtualKeyCode:65});
      await send('Input.dispatchKeyEvent',{type:'keyUp',key:'a',code:'KeyA',modifiers:2,windowsVirtualKeyCode:65});
      await typeText('web-demo');
      await mouse(845,192);
      await capture('Login_input_and_role');
    }
    if(process.env.UTTVR_UI_PROBES === '1' && index===5){
      await mouse(642,412);
      await delay(1200);
      await capture('ChiTietSaBan_model_click');
      await send('Page.setInterceptFileChooserDialog',{enabled:true});
      const count=logs.length;
      const previous=socket.onmessage;
      socket.onmessage=event=>{
        const message=JSON.parse(event.data);
        if(message.method==='Page.fileChooserOpened') logs.push({type:'File chooser',text:'ChiTietSaBan Excel picker opened'});
        previous(event);
      };
      await mouse(1209,460);
      await delay(500);
      evidence.push({Scene:menu[index],Action:'Click Excel import in Unity UI',Observed:logs.slice(count).some(x=>x.type==='File chooser')?'Browser file picker event observed':'No browser file picker event',Status:logs.slice(count).some(x=>x.type==='File chooser')?'PASS':'NOT TESTED'});
      socket.onmessage=previous;
      await send('Page.setInterceptFileChooserDialog',{enabled:false});
    }
    if(process.env.UTTVR_UI_PROBES === '1' && index===4){
      action = 'Lifecycle search';
      await mouse(112,170);
      await typeText('s002');
      await delay(400);
      await capture('Lifecycle_search');
      action = 'Lifecycle select result';
      await mouse(96,235);
      await delay(700);
      await capture('Lifecycle_selected');
      if (process.env.UTTVR_LIFECYCLE_READ_ONLY === '1') {
        await mouse(1036,168);
        await delay(500);
        await capture('Lifecycle_after_reload');
        break;
      }
      action = 'Lifecycle open stage';
      const beforeStage = logs.length;
      await mouse(1180,170);
      await delay(400);
      await capture('Lifecycle_tab');
      if (process.env.UTTVR_LIFECYCLE_STAGE_PROBE === '1') {
        action = 'Lifecycle Operation stage';
        await mouse(900,268);
        await delay(650);
        await capture('Lifecycle_operation_stage');
        action = 'Lifecycle Maintenance stage';
        await mouse(1040,268);
        await delay(650);
        await capture('Lifecycle_maintenance_stage');
        action = 'Lifecycle return to Planning';
        await mouse(901,222);
        await delay(650);
        await capture('Lifecycle_planning_stage');
      }
      const stageError = logs.slice(beforeStage).find(entry => entry.type==='error');
      evidence.push({Scene:menu[index],Action:'Open lifecycle tab or stages through UI',Observed:stageError?.text ?? 'Captured stage UI without a console error',Status:stageError?'FAIL':'OBSERVED'});
      action = 'Lifecycle scroll over input field';
      await send('Input.dispatchMouseEvent',{type:'mouseWheel',x:1100,y:480,deltaX:0,deltaY:350});
      await delay(800);
      await capture('Lifecycle_tab_scrolled');
      const downloads=resolve('Temp/WebDemoDownloads-'+Date.now());
      await mkdir(downloads,{recursive:true});
      await send('Browser.setDownloadBehavior',{behavior:'allow',downloadPath:downloads});
      action = 'Lifecycle export XLSX';
      await mouse(398,183);
      await delay(4500);
      const exported=(await readdir(downloads)).find(name=>name.toLowerCase().endsWith('.xlsx'));
      evidence.push({Scene:menu[index],Action:'Click Xuất bảng tính',Observed:exported??'No downloaded XLSX',Status:exported?'PASS':'FAIL'});
      await send('Page.setInterceptFileChooserDialog',{enabled:true});
      let chooser;
      const previous=socket.onmessage;
      socket.onmessage=event=>{
        const message=JSON.parse(event.data);
        if(message.method==='Page.fileChooserOpened') chooser=message.params;
        previous(event);
      };
      action = 'Lifecycle import picker';
      await mouse(555,184);
      await delay(500);
      evidence.push({Scene:menu[index],Action:'Click Xem trước tệp',Observed:chooser?'Browser file picker opened':'No browser file picker event',Status:chooser?'PASS':'FAIL'});
      if(chooser?.backendNodeId && process.env.UTTVR_TEST_WORKBOOK){
        await send('DOM.setFileInputFiles',{backendNodeId:chooser.backendNodeId,files:[resolve(process.env.UTTVR_TEST_WORKBOOK)]});
        await delay(1400);
        await capture('Lifecycle_import_preview');
        if (process.env.UTTVR_APPLY_PROBE === '1' && process.env.UTTVR_TEST_WORKBOOK.endsWith('Valid_Sandbox_Update.xlsx')) {
          await mouse(717,182);
          await delay(1400);
          await capture('Lifecycle_import_applied');
          await mouse(1036,168);
          await delay(400);
          await capture('Lifecycle_import_technical_fields');
        }
      }
      socket.onmessage=previous;
      await send('Page.setInterceptFileChooserDialog',{enabled:false});
      if (chooser?.backendNodeId && process.env.UTTVR_RELOAD_PROBE === '1' && process.env.UTTVR_APPLY_PROBE === '1' && process.env.UTTVR_TEST_WORKBOOK?.endsWith('Valid_Sandbox_Update.xlsx')) {
        await delay(8500);
        logs.push({type:'IndexedDB before reload',text:JSON.stringify(await evalPage('indexedDB.databases()'))});
        await send('Page.reload');
        await delay(4000);
        for (let i=0; i<120; i++) {
          if (await evalPage("!!window.uttWebGL && document.querySelector('#unity-loading-bar')?.style.display === 'none'")) break;
          if (i===119) throw new Error('Unity startup after reload could not be confirmed');
          await delay(1000);
        }
        await focusCanvas();
        await delay(2100);
        if (sharedMenu) {
          await mouse(905,192);
          await mouse(643,590);
          await delay(8000);
          await key('m','KeyM');
          await delay(400);
          await mouse(640,552);
        } else if (process.env.UTTVR_STARTS_LOGIN === '1') {
          await key('m','KeyM');
          await delay(400);
          await mouse(640,460);
        } else {
          await mouse(640,460);
        }
        await delay(6500);
        await mouse(112,170);
        await typeText('s002');
        await delay(700);
        await mouse(96,235);
        await delay(700);
        await mouse(1036,168);
        await delay(500);
        await capture('Lifecycle_after_reload');
        break;
      }
    }
    if(index===0){
      await send('Input.dispatchKeyEvent',{type:'keyDown',key:'w',code:'KeyW',windowsVirtualKeyCode:87});
      await delay(900);
      await send('Input.dispatchKeyEvent',{type:'keyUp',key:'w',code:'KeyW'});
      await capture('UTTCampus_after_W');
      await send('Input.dispatchMouseEvent',{type:'mousePressed',button:'right',buttons:2,x:630,y:340,clickCount:1});
      await delay(180);
      for(let n=1;n<=5;n++){
        await send('Input.dispatchMouseEvent',{type:'mouseMoved',buttons:2,x:630+n*12,y:340});
        await delay(90);
      }
      await send('Input.dispatchMouseEvent',{type:'mouseReleased',button:'right',buttons:0,x:690,y:340,clickCount:1});
      await capture('UTTCampus_mouse_look');
    }
  }
} catch(error){ logs.push({type:'test harness',text:String(error)}); process.exitCode=1; }
finally {
  await writeFile(path+'/Browser_Automation_Run.json',JSON.stringify(evidence,null,2));
  await writeFile(path+'/Browser_Console_Errors.txt',logs.map(x=>x.type+': '+x.text).join('\n'));
  if (performanceSamples.length) await writeFile(path+'/Browser_Performance.json',JSON.stringify(performanceSamples,null,2));
  socket?.close();
  if (process.env.UTTVR_KEEP_BROWSER === '1') {
    await new Promise(resolve => {
      const timer = setTimeout(resolve, 180000);
      browser.once('exit', () => { clearTimeout(timer); resolve(); });
    });
  }
  browser.kill();
}
