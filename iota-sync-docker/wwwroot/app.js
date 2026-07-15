let instances=[],codes=[],launcher=null;
let selectedInstanceId=null,currentView='overview',instancePane='config',fileSideFilter='all',codeFilter='active',fileQuery='';
let selectedFiles=new Set(),dirty=false;
const dirs=['mods','config','defaultconfigs','kubejs','resourcepacks','shaderpacks'];
const sideNames=['客户端必需','玩家可选','仅服务端','待人工确认'];
const $=id=>document.getElementById(id);
const esc=s=>String(s??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));

class ApiError extends Error{constructor(status,message){super(message);this.status=status}}
async function api(url,opt={}){
  opt.headers={...(opt.headers||{}),'X-Iota-Admin-Token':localStorage.getItem('iotaAdmin')||''};
  const response=await fetch(url,opt),contentType=response.headers.get('content-type')||'';
  const value=response.status===204?null:contentType.includes('json')?await response.json():await response.text();
  if(!response.ok)throw new ApiError(response.status,typeof value==='string'?value:(value?.title||'请求失败'));
  return value;
}
function toast(message,error=false){const el=$('toast');el.querySelector('p').textContent=message;el.querySelector('span').style.background=error?'var(--red)':'var(--green)';el.hidden=false;clearTimeout(window.toastTimer);window.toastTimer=setTimeout(()=>el.hidden=true,4000)}
function toggleToken(button){const input=$('token'),show=input.type==='password';input.type=show?'text':'password';button.textContent=show?'隐藏':'查看'}
async function login(useSaved=false){
  const entered=$('token').value.trim();
  if(!useSaved){if(!entered)return toast('请输入管理员令牌',true);localStorage.setItem('iotaAdmin',entered)}
  if(!localStorage.getItem('iotaAdmin'))return;
  try{await loadAll();$('login').hidden=true;$('app').hidden=false}
  catch(error){if(error.status===401)localStorage.removeItem('iotaAdmin');$('login').hidden=false;$('app').hidden=true;toast(error.message,true)}
}
$('logout').onclick=()=>{localStorage.removeItem('iotaAdmin');location.reload()};

async function loadAll(button){
  if(button)button.disabled=true;
  try{
    [instances,codes,launcher]=await Promise.all([api('/api/admin/instances'),api('/api/admin/codes'),api('/api/admin/launcher')]);
    if(selectedInstanceId===null||!instances.some(x=>x.instanceId===selectedInstanceId))selectedInstanceId=instances[0]?.instanceId??null;
    renderAll();$('lastUpdated').textContent=`更新于 ${new Date().toLocaleTimeString('zh-CN',{hour:'2-digit',minute:'2-digit'})}`;
  }catch(error){toast(error.message,true);throw error}finally{if(button)button.disabled=false}
}
function renderAll(){renderMetrics();renderOverview();renderInstancePicker();renderInstanceWorkspace();renderCodes();renderLauncher();$('sourceUrl').textContent=syncSource()}
function syncSource(){return `${location.origin}/api/plugins/mslx-plugin-iota-sync/sync`}
function renderMetrics(){
  const releases=instances.reduce((n,x)=>n+(x.releases?.length||0),0),files=instances.reduce((n,x)=>n+(x.lastScan?.length||0),0),review=instances.reduce((n,x)=>n+(x.lastScan||[]).filter(f=>f.side===3).length,0),active=codes.filter(x=>x.enabled).length,revoked=codes.length-active;
  $('metricInstances').textContent=instances.length;$('metricFiles').textContent=files;$('metricReleases').textContent=releases;$('metricCodes').textContent=active;
  $('metricReview').textContent=`${review} 待审核`;$('metricRevoked').textContent=`${revoked} 已撤销`;$('navInstances').textContent=instances.length;$('navCodes').textContent=active;$('activeCodeCount').textContent=active;$('revokedCodeCount').textContent=revoked;
  $('welcomeText').textContent=instances.length?`已连接 ${instances.length} 个服务端实例，${review?`还有 ${review} 个文件需要人工确认。`:'当前没有待审核文件。'}`:'添加第一个服务端实例，开始建立同步工作流。';
}
function showView(view){
  const titles={overview:['WORKSPACE','运行总览'],instances:['SERVER MANAGEMENT','服务端实例'],codes:['ACCESS CONTROL','同步码管理'],launcher:['CLIENT DELIVERY','PCL IO 客户端']};
  currentView=view;document.querySelectorAll('.view').forEach(x=>x.classList.toggle('active',x.id===`view-${view}`));document.querySelectorAll('.nav-item').forEach(x=>x.classList.toggle('active',x.dataset.view===view));
  $('pageKicker').textContent=titles[view][0];$('pageTitle').textContent=titles[view][1];history.replaceState(null,'',`#${view}`);toggleSidebar(false);
}
function toggleSidebar(force){const sidebar=document.querySelector('.sidebar'),open=force??!sidebar.classList.contains('open');sidebar.classList.toggle('open',open);$('sidebarMask').hidden=!open}
function toggleTheme(){const dark=document.documentElement.dataset.theme!=='dark';document.documentElement.dataset.theme=dark?'dark':'';localStorage.setItem('iotaTheme',dark?'dark':'light')}
async function copyText(text,message='已复制'){try{await navigator.clipboard.writeText(text)}catch{const input=document.createElement('textarea');input.value=text;document.body.appendChild(input);input.select();document.execCommand('copy');input.remove()}toast(message)}
function copySource(){copyText(syncSource(),'同步源地址已复制')}

function renderOverview(){
  $('overviewInstances').innerHTML=instances.length?instances.slice(0,5).map(i=>{const pending=(i.lastScan||[]).filter(f=>f.side===3).length;return `<div class="overview-server" onclick="openInstance(${i.instanceId})"><span class="server-avatar">${esc(i.name.slice(0,1).toUpperCase())}</span><div><b>${esc(i.name)}</b><small>${esc(i.minecraftVersion||'未设置版本')} · ${esc(i.loader||'未设置加载器')}</small></div><span>${pending?`${pending} 待审核`:`${i.releases?.length||0} 个版本`} →</span></div>`}).join(''):'<div class="empty-inline">尚未添加服务端实例</div>';
  const brief=$('launcherBrief');if(launcher){brief.innerHTML=`<div class="release-brief"><span>↓</span><div><b>PCL IO ${esc(launcher.version)}</b><small>${formatDate(launcher.publishedAt)} · ${size(launcher.size)}</small></div></div>`;$('launcherState').textContent='已发布';$('launcherState').className='pill success'}else{brief.textContent='尚未上传客户端正式版';$('launcherState').textContent='未发布';$('launcherState').className='pill neutral'}
}
function renderInstancePicker(){
  $('instancePicker').innerHTML=instances.length?instances.map(i=>`<button class="instance-tab ${i.instanceId===selectedInstanceId?'active':''}" onclick="selectInstance(${i.instanceId})"><span class="server-avatar">${esc(i.name.slice(0,1).toUpperCase())}</span><span><b>${esc(i.name)}</b><small>${esc(i.minecraftVersion||'版本未设置')} · ${esc(i.loader||'加载器未设置')}</small></span><em>${i.releases?.length||0} 版本</em></button>`).join(''):'';
}
function openInstance(id){selectedInstanceId=id;instancePane='config';showView('instances');renderInstancePicker();renderInstanceWorkspace()}
function selectInstance(id){captureInstance();selectedInstanceId=id;instancePane='config';fileQuery='';fileSideFilter='all';selectedFiles.clear();renderInstancePicker();renderInstanceWorkspace()}
function currentInstance(){return instances.find(x=>x.instanceId===selectedInstanceId)}
function renderInstanceWorkspace(){
  const root=$('instanceWorkspace'),i=currentInstance();if(!i){root.innerHTML='<article class="card empty-state"><span>＋</span><b>还没有服务端实例</b><p>添加实例后即可配置同步目录、扫描文件并发布。</p></article>';return}
  const pending=(i.lastScan||[]).filter(f=>f.side===3).length;
  root.innerHTML=`<article class="card workspace-card" data-instance-editor="${i.instanceId}">
    <header class="instance-hero"><div class="instance-name"><span class="server-avatar">${esc(i.name.slice(0,1).toUpperCase())}</span><div><h2>${esc(i.name)}</h2><p>${esc(i.rootPath)} · INSTANCE ${i.instanceId}</p></div></div><span class="pill ${pending?'warning':'success'}">${pending?`${pending} 个文件待确认`:'可以发布'}</span></header>
    <nav class="instance-tabs"><button class="${instancePane==='config'?'active':''}" onclick="setInstancePane('config')">基础配置</button><button class="${instancePane==='files'?'active':''}" onclick="setInstancePane('files')">扫描与文件 <b>${i.lastScan?.length||0}</b></button><button class="${instancePane==='releases'?'active':''}" onclick="setInstancePane('releases')">正式版本 <b>${i.releases?.length||0}</b></button></nav>
    <section class="instance-pane ${instancePane==='config'?'active':''}">${configPane(i)}</section>
    <section class="instance-pane ${instancePane==='files'?'active':''}"><div id="fileArea">${filesPane(i)}</div></section>
    <section class="instance-pane ${instancePane==='releases'?'active':''}">${releasesPane(i)}</section>
  </article>`;
}
function setInstancePane(pane){captureInstance();instancePane=pane;renderInstanceWorkspace()}
function configPane(i){return `<div class="form-section"><div class="section-label"><b>实例信息</b><small>用于生成客户端版本和服务器入口</small></div><div class="form-grid">
  <label class="field"><span>实例名称</span><input data-k="name" value="${esc(i.name)}" oninput="markDirty()"></label><label class="field"><span>容器内目录</span><input data-k="rootPath" value="${esc(i.rootPath)}" oninput="markDirty()"></label><label class="field"><span>玩家连接地址</span><input data-k="serverAddress" value="${esc(i.serverAddress)}" placeholder="mc.example.com:25565" oninput="markDirty()"></label>
  <label class="field"><span>Minecraft 版本</span><input data-k="minecraftVersion" value="${esc(i.minecraftVersion)}" placeholder="1.21.1" oninput="markDirty()"></label><label class="field"><span>模组加载器</span><select data-k="loader" onchange="markDirty()">${['vanilla','forge','neoforge','fabric','quilt'].map(x=>`<option value="${x}" ${i.loader===x?'selected':''}>${x}</option>`).join('')}</select></label><label class="field"><span>加载器版本</span><input data-k="loaderVersion" value="${esc(i.loaderVersion)}" placeholder="21.1.219" oninput="markDirty()"></label></div></div>
  <div class="form-section"><div class="section-label"><b>同步目录</b><small>建议仅启用确实需要向客户端分发的目录</small></div><div class="dir-grid">${dirs.map(path=>`<label class="dir-toggle"><input type="checkbox" data-dir="${path}" ${i.directories?.find(x=>x.path===path)?.enabled?'checked':''} onchange="markDirty()"><span>${path}</span></label>`).join('')}</div></div>
  <div class="save-row"><small id="saveHint">${dirty?'有尚未保存的更改':'所有配置均已保存'}</small><button class="btn primary" onclick="saveInstance(${i.instanceId})">保存配置</button></div><div class="danger-zone"><button class="btn danger" onclick="confirmRemoveInstance(${i.instanceId})">删除此实例</button></div>`}
function captureInstance(){const i=currentInstance(),el=document.querySelector('[data-instance-editor]');if(!i||!el)return;i.name=el.querySelector('[data-k="name"]')?.value??i.name;i.rootPath=el.querySelector('[data-k="rootPath"]')?.value??i.rootPath;i.serverAddress=el.querySelector('[data-k="serverAddress"]')?.value??i.serverAddress;i.minecraftVersion=el.querySelector('[data-k="minecraftVersion"]')?.value??i.minecraftVersion;i.loader=el.querySelector('[data-k="loader"]')?.value??i.loader;i.loaderVersion=el.querySelector('[data-k="loaderVersion"]')?.value??i.loaderVersion;const checks=el.querySelectorAll('[data-dir]');if(checks.length)i.directories=dirs.map(path=>({path,enabled:el.querySelector(`[data-dir="${path}"]`)?.checked??false}))}
function markDirty(){dirty=true;const hint=$('saveHint');if(hint)hint.textContent='有尚未保存的更改'}
function payload(i){i.overrides=Object.fromEntries((i.lastScan||[]).map(x=>[x.path,x.side]));return i}
async function saveInstance(id,quiet=false){captureInstance();const i=instances.find(x=>x.instanceId===id);try{await api(`/api/admin/instances/${id}`,{method:'PUT',headers:{'Content-Type':'application/json'},body:JSON.stringify(payload(i))});dirty=false;if(!quiet)toast('实例配置已保存');renderInstancePicker();return i}catch(error){toast(error.message,true);throw error}}

function filesPane(i){
  if(!i.lastScan?.length)return `<div class="scan-callout"><div><b>先停止服务端，再进行文件扫描</b><small>扫描完成后，系统会自动判断每个 Mod 的端侧分类。</small></div><label class="check-control"><input id="scanStopped" type="checkbox"> 我确认服务端已停止</label></div><div class="empty-state"><span>⌕</span><b>暂无扫描结果</b><p>确认停服状态后，点击下方按钮开始。</p><button class="btn primary" style="margin-top:16px" onclick="scanInstance(${i.instanceId})">开始扫描</button></div>`;
  const stats=[0,1,2,3].map(side=>i.lastScan.filter(x=>x.side===side).length);
  return `<div class="summary-grid">${sideNames.map((name,n)=>`<div class="summary-item"><strong>${stats[n]}</strong><small>${name}</small></div>`).join('')}<div class="summary-item"><strong>${size(i.lastScan.reduce((n,f)=>n+f.size,0))}</strong><small>扫描总大小</small></div></div>
    <div class="scan-callout"><div><b>需要重新扫描？</b><small>先保存当前分类规则，再停止服务端并确认。</small></div><div style="display:flex;align-items:center;gap:10px"><label class="check-control"><input id="scanStopped" type="checkbox"> 已停止服务端</label><button class="btn secondary" onclick="scanInstance(${i.instanceId})">重新扫描</button></div></div>
    ${fileTable(i)}
    <div class="publish-panel"><div><b>${stats[3]?'完成审核后才能发布':'审核完成，可以生成正式版本'}</b><small>${stats[3]?`还有 ${stats[3]} 个文件处于待人工确认状态。`:'发布前请再次停止 Minecraft 服务端。'}</small></div><div style="display:flex;align-items:center;gap:10px"><label class="check-control"><input id="publishStopped" type="checkbox"> 已停止服务端</label><button class="btn primary" ${stats[3]?'disabled':''} onclick="confirmPublish(${i.instanceId})">发布正式版本</button></div></div>`;
}
function filteredFiles(i){const q=fileQuery.trim().toLowerCase();return (i.lastScan||[]).map((file,index)=>({file,index})).filter(x=>(fileSideFilter==='all'||x.file.side===+fileSideFilter)&&(!q||x.file.path.toLowerCase().includes(q)||x.file.reason.toLowerCase().includes(q)))}
function fileTable(i){const list=filteredFiles(i);return `<div class="file-toolbar"><label class="search-box"><svg viewBox="0 0 24 24"><circle cx="11" cy="11" r="7"/><path d="m20 20-4-4"/></svg><input value="${esc(fileQuery)}" placeholder="搜索文件名或识别依据" oninput="updateFileQuery(this.value)"></label><div class="segmented">${[['all','全部'],['0','必需'],['1','可选'],['2','服务端'],['3','待确认']].map(([v,n])=>`<button class="${fileSideFilter===v?'active':''}" onclick="setFileFilter('${v}')">${n}</button>`).join('')}</div><button class="btn secondary" onclick="selectVisibleFiles(${i.instanceId})">选择当前结果</button></div>
  <div class="selection-bar"><strong>已选 ${selectedFiles.size} 个</strong><span>批量设置：</span>${sideNames.map((name,side)=>`<button onclick="bulkSide(${i.instanceId},${side})">${name}</button>`).join('')}<button onclick="clearSelection()">清除选择</button></div>
  <div class="file-table-wrap"><table class="file-table"><thead><tr><th></th><th>文件路径</th><th>大小</th><th>端侧分类</th><th>识别依据</th></tr></thead><tbody>${list.length?list.map(({file,index})=>`<tr><td><input type="checkbox" ${selectedFiles.has(index)?'checked':''} onchange="selectFile(${index},this.checked)"></td><td class="path-cell">${esc(file.path)}</td><td>${size(file.size)}</td><td><select onchange="setFileSide(${index},this.value)">${sideNames.map((name,side)=>`<option value="${side}" ${file.side===side?'selected':''}>${name}</option>`).join('')}</select></td><td class="reason-cell">${esc(file.reason)}</td></tr>`).join(''):`<tr><td colspan="5" class="empty-inline">没有符合筛选条件的文件</td></tr>`}</tbody></table></div><div class="file-footer"><span>显示 ${list.length} / ${i.lastScan.length} 个文件</span><span>修改分类后请保存配置</span></div>`}
function renderFiles(){const area=$('fileArea'),i=currentInstance();if(area&&i)area.innerHTML=filesPane(i)}
function updateFileQuery(value){fileQuery=value;renderFiles();const input=$('fileArea')?.querySelector('.search-box input');if(input){input.focus();input.setSelectionRange(input.value.length,input.value.length)}}
function setFileFilter(value){fileSideFilter=value;renderFiles()}
function selectFile(index,checked){checked?selectedFiles.add(index):selectedFiles.delete(index);renderFiles()}
function selectVisibleFiles(){filteredFiles(currentInstance()).forEach(x=>selectedFiles.add(x.index));renderFiles()}
function clearSelection(){selectedFiles.clear();renderFiles()}
function setFileSide(index,value){const i=currentInstance();i.lastScan[index].side=+value;dirty=true;renderFiles()}
function bulkSide(id,side){if(!selectedFiles.size)return toast('请先选择文件',true);const i=instances.find(x=>x.instanceId===id);selectedFiles.forEach(index=>{if(i.lastScan[index])i.lastScan[index].side=side});dirty=true;toast(`已修改 ${selectedFiles.size} 个文件`);renderFiles()}
async function scanInstance(id){const stopped=$('scanStopped')?.checked;if(!stopped)return toast('请先确认 Minecraft 服务端已停止',true);try{await saveInstance(id,true);const i=instances.find(x=>x.instanceId===id);i.stopConfirmed=true;await api(`/api/admin/instances/${id}`,{method:'PUT',headers:{'Content-Type':'application/json'},body:JSON.stringify(payload(i))});await api(`/api/admin/instances/${id}/scan`,{method:'POST'});selectedFiles.clear();toast('扫描完成，请审核文件分类');await loadAll()}catch(error){toast(error.message,true)}}
function confirmPublish(id){if(!$('publishStopped')?.checked)return toast('请先确认 Minecraft 服务端已停止',true);openModal('PUBLISH RELEASE','发布正式版本',`<p class="modal-message">即将为 <b>${esc(currentInstance().name)}</b> 生成新的客户端快照。发布后，玩家手动刷新即可获取更新。</p><div class="warning-box">发布期间不要启动 Minecraft 服务端，也不要修改同步目录中的文件。</div>`,`<button class="btn secondary" onclick="closeModal()">取消</button><button class="btn primary" onclick="publishInstance(${id})">确认发布</button>`)}
async function publishInstance(id){closeModal();try{await saveInstance(id,true);const i=instances.find(x=>x.instanceId===id);i.stopConfirmed=true;await api(`/api/admin/instances/${id}`,{method:'PUT',headers:{'Content-Type':'application/json'},body:JSON.stringify(payload(i))});await api(`/api/admin/instances/${id}/publish`,{method:'POST'});instancePane='releases';toast('正式版本发布成功');await loadAll()}catch(error){toast(error.message,true)}}
function releasesPane(i){return i.releases?.length?`<div class="section-label"><b>最近发布记录</b><small>系统最多保留最近 5 个正式版本</small></div><div class="release-list">${i.releases.map((release,n)=>`<div class="release-item"><span>${n===0?'✓':'↓'}</span><div><b>${esc(release)}</b><small>${releaseDate(release)}${n===0?' · 当前客户端版本':''}</small></div><span class="pill ${n===0?'success':'neutral'}">${n===0?'当前':'历史'}</span></div>`).join('')}</div>`:'<div class="empty-state"><span>↓</span><b>尚未发布正式版本</b><p>完成扫描和人工审核后即可发布。</p></div>'}

function openAddInstance(){openModal('NEW SERVER','添加服务端实例',`<div class="form-grid one"><label class="field"><span>实例名称</span><input id="newName" placeholder="例如：机械动力生存服"></label><label class="field"><span>容器内目录</span><input id="newPath" placeholder="例如：/servers/5"></label></div><div class="warning-box">这里填写 Docker 容器内的挂载路径，不是 NAS 宿主机路径。</div>`,`<button class="btn secondary" onclick="closeModal()">取消</button><button class="btn primary" onclick="addInstance()">确认添加</button>`);setTimeout(()=>$('newName')?.focus(),0)}
async function addInstance(){const name=$('newName').value.trim(),rootPath=$('newPath').value.trim();if(!name||!rootPath)return toast('请填写实例名称和容器内目录',true);try{const item=await api('/api/admin/instances',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({name,rootPath})});closeModal();selectedInstanceId=item.instanceId;toast('服务端实例已添加');await loadAll();showView('instances')}catch(error){toast(error.message,true)}}
function confirmRemoveInstance(id){openModal('DANGER ZONE','删除服务端实例',`<p class="modal-message">确定删除 <b>${esc(currentInstance().name)}</b> 的实例配置吗？关联同步码会失效，已发布快照不会自动删除。</p>`,`<button class="btn secondary" onclick="closeModal()">取消</button><button class="btn danger" onclick="removeInstance(${id})">确认删除</button>`)}
async function removeInstance(id){try{await api(`/api/admin/instances/${id}`,{method:'DELETE'});closeModal();selectedInstanceId=null;toast('实例配置已删除');await loadAll()}catch(error){toast(error.message,true)}}

function setCodeFilter(value){codeFilter=value;document.querySelectorAll('[data-code-filter]').forEach(x=>x.classList.toggle('active',x.dataset.codeFilter===value));renderCodes()}
function renderCodes(){
  const query=($('codeSearch')?.value||'').trim().toLowerCase(),instanceName=id=>instances.find(x=>x.instanceId===id)?.name||`实例 #${id}`;
  const list=codes.filter(c=>(codeFilter==='all'||(codeFilter==='active'?c.enabled:!c.enabled))&&(!query||c.name.toLowerCase().includes(query)||instanceName(c.instanceId).toLowerCase().includes(query)));
  const root=$('codesTable');if(!root)return;root.innerHTML=list.length?`<div style="overflow:auto"><table class="data-table"><thead><tr><th>备注</th><th>服务端实例</th><th>创建时间</th><th>状态</th><th></th></tr></thead><tbody>${list.map(c=>`<tr><td><div class="user-cell"><span class="user-avatar">${esc((c.name||'?').slice(0,1).toUpperCase())}</span><div><b>${esc(c.name||'未命名')}</b><small>ID ${esc(c.id.slice(0,8))}</small></div></div></td><td>${esc(instanceName(c.instanceId))}</td><td>${formatDate(c.createdAt)}</td><td><span class="pill ${c.enabled?'success':'neutral'}">${c.enabled?'有效':'已撤销'}</span></td><td>${c.enabled?`<button class="table-action" onclick="confirmRevoke('${c.id}')">撤销</button>`:''}</td></tr>`).join('')}</tbody></table></div>`:'<div class="empty-state small"><span>⌁</span><b>没有符合条件的同步码</b><p>创建后即可授权玩家连接同步源。</p></div>';
}
function openCodeModal(instanceId=selectedInstanceId){if(!instances.length)return toast('请先添加服务端实例',true);openModal('NEW ACCESS CODE','创建长期同步码',`<div class="form-grid one"><label class="field"><span>授权服务端</span><select id="codeInstance">${instances.map(i=>`<option value="${i.instanceId}" ${i.instanceId===instanceId?'selected':''}>${esc(i.name)}</option>`).join('')}</select></label><label class="field"><span>玩家备注</span><input id="codeName" placeholder="例如：Fengoo 的电脑"></label></div><div class="warning-box">同步码长期有效，撤销前可重复使用；原始同步码只显示一次。</div>`,`<button class="btn secondary" onclick="closeModal()">取消</button><button class="btn primary" onclick="createCode()">生成同步码</button>`);setTimeout(()=>$('codeName')?.focus(),0)}
async function createCode(){const id=+$('codeInstance').value,name=$('codeName').value.trim();if(!name)return toast('请填写玩家备注',true);try{const result=await api(`/api/admin/instances/${id}/codes`,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({name})});await loadAll();openModal('CODE CREATED','同步码已生成',`<p class="modal-message">请立即复制并发送给对应玩家。关闭后无法再次查看原始同步码。</p><div class="modal-copy"><input id="newCodeValue" readonly value="${esc(result.code)}"><button class="btn primary" onclick="copyText($('newCodeValue').value,'同步码已复制')">复制</button></div><div class="warning-box">客户端同步源：<br><code>${esc(syncSource())}</code></div>`,`<button class="btn primary" onclick="closeModal()">我已保存</button>`)}catch(error){toast(error.message,true)}}
function confirmRevoke(id){const c=codes.find(x=>x.id===id);openModal('REVOKE ACCESS','撤销同步码',`<p class="modal-message">撤销 <b>${esc(c?.name||'此玩家')}</b> 的同步码后，使用该码的客户端将无法继续获取更新。</p>`,`<button class="btn secondary" onclick="closeModal()">取消</button><button class="btn danger" onclick="revokeCode('${id}')">确认撤销</button>`)}
async function revokeCode(id){try{await api(`/api/admin/codes/${id}`,{method:'DELETE'});closeModal();toast('同步码已撤销');await loadAll()}catch(error){toast(error.message,true)}}

function renderLauncher(){const current=$('launcherCurrent');if(!current)return;if(!launcher){current.className='empty-state small';current.innerHTML='<span>↓</span><b>还没有客户端版本</b><p>在右侧上传首个 PCL IO 正式版。</p>';$('launcherBadge').textContent='未发布';$('launcherBadge').className='pill neutral';return}current.className='launcher-details';current.innerHTML=`<div class="version-hero"><span>↓</span><div><h3>PCL IO ${esc(launcher.version)}</h3><p>${formatDate(launcher.publishedAt)} 发布</p></div></div><div class="detail-list"><div><span>文件大小</span><b>${size(launcher.size)}</b></div><div><span>文件名</span><b>${esc(launcher.fileName)}</b></div><div style="grid-column:1/-1"><span>SHA-256</span><b>${esc(launcher.sha256)}</b></div></div><div class="release-notes">${esc(launcher.notes||'本次发布未填写更新说明')}</div>`;$('launcherBadge').textContent='正式版';$('launcherBadge').className='pill success'}
function fileChosen(){const file=$('launcherFile').files[0];if(!file){$('fileName').textContent='选择 PCL IO EXE 文件';$('fileMeta').textContent='点击选择，最大 200 MB';return}$('fileName').textContent=file.name;$('fileMeta').textContent=`${size(file.size)} · 已准备上传`}
async function uploadLauncher(){const file=$('launcherFile').files[0],version=$('launcherVersion').value.trim();if(!version)return toast('请填写客户端版本号',true);if(!file)return toast('请选择 EXE 文件',true);const data=new FormData();data.append('version',version);data.append('notes',$('launcherNotes').value.trim());data.append('file',file);const progress=$('uploadProgress');progress.hidden=false;progress.querySelector('span').style.width='0';try{launcher=await xhrUpload('/api/admin/launcher',data,p=>progress.querySelector('span').style.width=`${p}%`);progress.querySelector('span').style.width='100%';toast('PCL IO 正式版发布成功');$('launcherVersion').value=$('launcherNotes').value='';$('launcherFile').value='';fileChosen();renderLauncher();renderOverview()}catch(error){toast(error.message,true)}finally{setTimeout(()=>progress.hidden=true,700)}}
function xhrUpload(url,data,onProgress){return new Promise((resolve,reject)=>{const xhr=new XMLHttpRequest();xhr.open('POST',url);xhr.setRequestHeader('X-Iota-Admin-Token',localStorage.getItem('iotaAdmin')||'');xhr.upload.onprogress=e=>{if(e.lengthComputable)onProgress(Math.round(e.loaded/e.total*100))};xhr.onload=()=>{let value;try{value=JSON.parse(xhr.responseText)}catch{value=xhr.responseText}xhr.status>=200&&xhr.status<300?resolve(value):reject(new ApiError(xhr.status,typeof value==='string'?value:'上传失败'))};xhr.onerror=()=>reject(new Error('网络连接中断'));xhr.send(data)})}

function openModal(kicker,title,body,actions){$('modalKicker').textContent=kicker;$('modalTitle').textContent=title;$('modalBody').innerHTML=body;$('modalActions').innerHTML=actions;$('modal').hidden=false}
function closeModal(){$('modal').hidden=true}
function size(n){if(!Number.isFinite(n))return'—';if(n<1024)return`${n} B`;if(n<1048576)return`${(n/1024).toFixed(1)} KiB`;if(n<1073741824)return`${(n/1048576).toFixed(1)} MiB`;return`${(n/1073741824).toFixed(2)} GiB`}
function formatDate(value){return new Date(value).toLocaleString('zh-CN',{year:'numeric',month:'2-digit',day:'2-digit',hour:'2-digit',minute:'2-digit'})}
function releaseDate(id){if(!/^\d{14}$/.test(id))return id;return `${id.slice(0,4)}-${id.slice(4,6)}-${id.slice(6,8)} ${id.slice(8,10)}:${id.slice(10,12)}:${id.slice(12,14)} UTC`}

document.documentElement.dataset.theme=localStorage.getItem('iotaTheme')||'';
const initialView=['overview','instances','codes','launcher'].includes(location.hash.slice(1))?location.hash.slice(1):'overview';showView(initialView);
if(localStorage.getItem('iotaAdmin'))login(true);
