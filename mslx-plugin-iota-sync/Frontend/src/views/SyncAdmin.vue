<template>
  <div class="page">
    <t-alert theme="info" title="发布流程">实例必须停止运行。先配置客户端版本并刷新扫描，确认端侧分类，再发布。</t-alert>
    <t-card title="PCL IO 正式版" class="card">
      <t-space><t-input v-model="launcherVersion" placeholder="版本号，如 0.2.0"/><t-input v-model="launcherNotes" placeholder="更新说明"/><input type="file" accept=".exe" @change="chooseLauncher"/><t-button theme="primary" :disabled="!launcherFile" @click="uploadLauncher">发布启动器</t-button></t-space>
    </t-card>
    <t-loading :loading="loading" show-overlay>
      <t-card v-for="item in instances" :key="item.id" :title="item.name" class="card">
        <template #actions><t-tag :theme="item.running?'danger':'success'">{{item.running?'运行中':'已停止'}}</t-tag></template>
        <t-space direction="vertical" style="width:100%">
          <div>目录：{{item.base}}</div>
          <t-form v-if="item.settings" layout="inline">
            <t-form-item label="MC 版本"><t-input v-model="item.settings.minecraftVersion" placeholder="1.21.1"/></t-form-item>
            <t-form-item label="加载器"><t-select v-model="item.settings.loader" :options="loaders" style="width:140px"/></t-form-item>
            <t-form-item label="加载器版本"><t-input v-model="item.settings.loaderVersion"/></t-form-item>
            <t-form-item label="服务器地址"><t-input v-model="item.settings.serverAddress" placeholder="地址:端口"/></t-form-item>
          </t-form>
          <t-space v-if="item.settings"><span>同步目录：</span><t-checkbox v-for="d in item.settings.directories" :key="d.path" v-model="d.enabled">{{d.path}}</t-checkbox></t-space>
          <t-space><t-button :disabled="item.running" @click="scan(item)">刷新扫描</t-button><t-button theme="primary" :disabled="item.running||!item.settings?.lastScan?.length" @click="publish(item)">发布新版本</t-button><t-button variant="outline" @click="createCode(item)">创建同步码</t-button></t-space>
          <t-table v-if="item.settings?.lastScan?.length" :data="item.settings.lastScan" :columns="columns" row-key="path"><template #side="{row}"><t-select v-model="row.side" :options="sides" @change="save(item)"/></template></t-table>
        </t-space>
      </t-card>
    </t-loading>
  </div>
</template>
<script setup lang="ts">
import {onMounted,ref} from 'vue'; import {MessagePlugin,DialogPlugin} from 'tdesign-vue-next';
const api='/api/plugins/mslx-plugin-iota-sync/admin',loading=ref(false),instances=ref<any[]>([]);
const launcherVersion=ref(''),launcherNotes=ref(''),launcherFile=ref<File|null>(null);
const sides=[{label:'客户端必需',value:0},{label:'可选（默认安装）',value:1},{label:'仅服务端',value:2},{label:'待人工确认',value:3}];
const loaders=['vanilla','forge','neoforge','fabric','quilt'].map(x=>({label:x,value:x}));
const columns=[{colKey:'path',title:'文件',ellipsis:true},{colKey:'size',title:'大小'},{colKey:'side',title:'端侧',cell:'side'},{colKey:'reason',title:'识别依据',ellipsis:true}];
async function req(url:string,init?:RequestInit){const r=await fetch(url,init);if(!r.ok)throw new Error(await r.text());return r.status===204?null:r.json()}
async function load(){loading.value=true;try{instances.value=await req(`${api}/instances`);for(const i of instances.value)i.settings||={instanceId:i.id,directories:['mods','config','defaultconfigs','kubejs','resourcepacks','shaderpacks'].map((path,n)=>({path,enabled:n===0})),overrides:{},minecraftVersion:'',loader:'',loaderVersion:'',serverAddress:'',lastScan:[],releases:[]}}catch(e:any){MessagePlugin.error(e.message)}finally{loading.value=false}}
async function scan(i:any){loading.value=true;try{await save(i);i.settings.lastScan=await req(`${api}/instances/${i.id}/scan`,{method:'POST'});MessagePlugin.success('扫描完成，请检查端侧分类')}catch(e:any){MessagePlugin.error(e.message)}finally{loading.value=false}}
async function save(i:any){i.settings.overrides=Object.fromEntries((i.settings.lastScan||[]).map((x:any)=>[x.path,x.side]));await req(`${api}/instances/${i.id}`,{method:'PUT',headers:{'Content-Type':'application/json'},body:JSON.stringify(i.settings)})}
async function publish(i:any){try{await save(i);await req(`${api}/instances/${i.id}/publish`,{method:'POST'});MessagePlugin.success('发布成功');await load()}catch(e:any){MessagePlugin.error(e.message)}}
async function createCode(i:any){const name=prompt('同步码备注（例如玩家昵称）');if(!name)return;try{const x=await req(`${api}/instances/${i.id}/codes`,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({name})});DialogPlugin.alert({header:'同步码只显示一次',body:`${x.code}\n\n请立即安全保存。`})}catch(e:any){MessagePlugin.error(e.message)}}
function chooseLauncher(e:Event){launcherFile.value=(e.target as HTMLInputElement).files?.[0]||null}
async function uploadLauncher(){if(!launcherFile.value)return;const f=new FormData();f.append('version',launcherVersion.value);f.append('notes',launcherNotes.value);f.append('file',launcherFile.value);try{await req(`${api}/launcher`,{method:'POST',body:f});MessagePlugin.success('启动器正式版发布成功')}catch(e:any){MessagePlugin.error(e.message)}}
onMounted(load);
</script>
<style scoped>.page{padding:20px}.card{margin-top:16px}</style>
