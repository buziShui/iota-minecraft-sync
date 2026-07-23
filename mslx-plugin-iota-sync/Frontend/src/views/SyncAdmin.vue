<template>
  <div class="iota-page">
    <section class="hero design-card">
      <div>
        <div class="eyebrow">IOTA CLIENT SYNC</div>
        <h1>让每位玩家保持同一套客户端</h1>
        <p>选择实例、扫描分类、确认发布，再把长期同步码交给玩家。</p>
      </div>
      <div class="hero-actions">
        <button class="button ghost" @click="copyText(sourceRoot, '同步源地址已复制')">复制同步源</button>
        <a class="button primary" href="https://github.com/buziShui/iota-minecraft-sync/releases/latest" target="_blank" rel="noreferrer">下载 PCL IO</a>
      </div>
    </section>

    <section class="metrics" aria-label="同步概览">
      <article class="design-card"><span>服务端实例</span><strong>{{ instances.length }}</strong><small>{{ stoppedCount }} 个可操作</small></article>
      <article class="design-card"><span>已扫描文件</span><strong>{{ totalFiles }}</strong><small>所有实例合计</small></article>
      <article class="design-card" :class="{ warning: pendingFiles > 0 }"><span>待人工确认</span><strong>{{ pendingFiles }}</strong><small>{{ pendingFiles ? '发布前必须处理' : '分类已清晰' }}</small></article>
      <article class="design-card"><span>有效同步码</span><strong>{{ activeCodeCount }}</strong><small>长期有效，可随时撤销</small></article>
    </section>

    <nav class="section-tabs design-card" aria-label="功能导航">
      <button :class="{ active: section === 'sync' }" @click="section = 'sync'">实例同步</button>
      <button :class="{ active: section === 'codes' }" @click="section = 'codes'">同步码管理 <b>{{ activeCodeCount }}</b></button>
      <button :class="{ active: section === 'guide' }" @click="section = 'guide'">使用指引</button>
    </nav>

    <t-loading :loading="loading" show-overlay>
      <main v-if="section === 'sync'" class="workspace">
        <aside class="instance-rail design-card">
          <div class="rail-title">
            <div><span>服务端实例</span><small>每个实例独立发布</small></div>
            <button class="icon-button" title="刷新状态" @click="load">↻</button>
          </div>
          <button v-for="item in instances" :key="item.id" class="instance-button" :class="{ active: selectedId === item.id }" @click="selectInstance(item.id)">
            <span class="status-dot" :class="item.running ? 'running' : 'stopped'"></span>
            <span><strong>{{ item.name }}</strong><small>{{ item.running ? '运行中 · 仅查看' : `${item.settings.lastScan.length} 个文件` }}</small></span>
            <i>›</i>
          </button>
          <div v-if="!instances.length" class="empty compact">MSLX 中还没有服务端实例。</div>
        </aside>

        <section v-if="current" class="instance-panel design-card">
          <header class="instance-head">
            <div>
              <div class="head-line"><h2>{{ current.name }}</h2><span class="status-pill" :class="current.running ? 'running' : 'stopped'">{{ current.running ? '正在运行' : '已停止' }}</span></div>
              <p :title="current.base">{{ current.base }}</p>
            </div>
            <div class="release-badge"><small>当前正式版</small><strong>{{ current.settings.releases[0] || '尚未发布' }}</strong></div>
          </header>

          <div class="flow">
            <span class="done"><b>1</b>配置与目录</span><i></i>
            <span :class="{ done: hasScan }"><b>2</b>扫描并分类</span><i></i>
            <span :class="{ done: hasRelease }"><b>3</b>发布版本</span><i></i>
            <span :class="{ done: instanceActiveCodes > 0 }"><b>4</b>分发同步码</span>
          </div>

          <div class="panel-tabs">
            <button :class="{ active: pane === 'config' }" @click="pane = 'config'">基础配置</button>
            <button :class="{ active: pane === 'directories' }" @click="pane = 'directories'">同步目录 <em>{{ enabledDirectoryCount }}</em></button>
            <button :class="{ active: pane === 'files' }" @click="pane = 'files'">Mod 分类 <em v-if="instancePending">{{ instancePending }}</em></button>
            <button :class="{ active: pane === 'releases' }" @click="pane = 'releases'">发布记录</button>
          </div>

          <section v-if="pane === 'config'" class="content-card">
            <div class="card-heading"><div><h3>客户端版本信息</h3><p>PCL IO 会使用这些信息创建或识别对应游戏实例。</p></div></div>
            <div class="form-grid">
              <label><span>Minecraft 版本</span><input v-model.trim="current.settings.minecraftVersion" placeholder="例如 1.21.1" @input="markDirty"></label>
              <label><span>加载器</span><select v-model="current.settings.loader" @change="markDirty"><option value="">请选择</option><option v-for="item in loaders" :key="item" :value="item">{{ item }}</option></select></label>
              <label><span>加载器版本</span><input v-model.trim="current.settings.loaderVersion" placeholder="填写完整版本号" @input="markDirty"></label>
              <label><span>玩家连接地址</span><input v-model.trim="current.settings.serverAddress" placeholder="域名或 IP:端口" @input="markDirty"></label>
            </div>
            <div class="action-row">
              <span v-if="dirty" class="unsaved">● 有未保存修改</span><span v-else>配置已保存</span>
              <div><button class="button ghost" :disabled="saving" @click="saveSettings(true)">保存配置</button><button class="button primary" :disabled="current.running || saving" @click="scanCurrent">保存并扫描</button></div>
            </div>
            <div v-if="current.running" class="notice warning">实例正在运行。为避免读取到写入一半的文件，请先在 MSLX 中停止实例。</div>
          </section>

          <section v-if="pane === 'directories'" class="content-card directory-section">
            <div class="directory-overview">
              <div>
                <span class="eyebrow">SYNC SCOPE</span>
                <h3>需要同步的目录</h3>
                <p>开启的目录会在扫描时纳入客户端版本；关闭的目录只保留在服务端，不会进入下一次发布。</p>
              </div>
              <div class="directory-total"><strong>{{ enabledDirectoryCount }}</strong><span>/ {{ current.settings.directories.length }} 已启用</span></div>
            </div>

            <div class="directory-grid">
              <label v-for="directory in current.settings.directories" :key="directory.path" class="directory-card design-card" :class="{ active: directory.enabled }">
                <input v-model="directory.enabled" type="checkbox" @change="markDirty">
                <span class="directory-icon" aria-hidden="true">{{ directoryMeta(directory.path).icon }}</span>
                <span class="directory-content">
                  <span class="directory-title"><strong>{{ directoryMeta(directory.path).title }}</strong><code>{{ directory.path }}/</code></span>
                  <span class="directory-description">{{ directoryMeta(directory.path).description }}</span>
                  <span class="directory-files">{{ directoryFileCount(directory.path) ? `当前扫描到 ${directoryFileCount(directory.path)} 个文件` : '当前扫描结果中没有文件' }}</span>
                </span>
                <span class="directory-state">{{ directory.enabled ? '已同步' : '未同步' }}</span>
              </label>
            </div>

            <div class="directory-hint">
              <div><strong>推荐设置</strong><p><code>mods</code> 建议始终开启；只有整合包确实依赖配置、脚本、资源包或光影时，再开启对应目录。</p></div>
              <span>目录设置按服务端实例独立保存</span>
            </div>
            <div class="action-row directory-actions">
              <span v-if="dirty" class="unsaved">● 目录选择尚未保存</span><span v-else>同步目录已保存</span>
              <div><button class="button ghost" :disabled="saving" @click="saveSettings(true, '同步目录已保存')">保存目录</button><button class="button primary" :disabled="current.running || saving" @click="scanCurrent">保存并重新扫描</button></div>
            </div>
            <div v-if="current.running" class="notice warning">实例正在运行，目录可以保存，但需要停止实例后才能重新扫描。</div>
          </section>

          <section v-if="pane === 'files'" class="files-section">
            <div class="scan-toolbar">
              <div><h3>服务端 Mod 与同步 Mod</h3><p>左侧只留在服务器，右侧会发给玩家；系统先识别，再由你批量检查。</p></div>
              <div><button class="button ghost" :disabled="current.running || saving" @click="scanCurrent">重新扫描</button><button class="button primary" :disabled="!canPublish || saving" @click="publishCurrent">发布新版本</button></div>
            </div>

            <div v-if="!hasScan" class="empty large"><div>⌁</div><h3>还没有扫描结果</h3><p>停止实例后点击“重新扫描”，系统会读取已启用的同步目录。</p><button class="button primary" :disabled="current.running" @click="scanCurrent">开始扫描</button></div>
            <template v-else>
              <div class="file-summary">
                <span><b>{{ serverFiles.length }}</b> 个服务端文件</span><span><b>{{ syncFiles.length }}</b> 个同步文件</span><span :class="{ danger: instancePending > 0 }"><b>{{ instancePending }}</b> 个待确认</span><span v-if="dirty" class="unsaved">分类有修改，保存后才能发布</span>
              </div>
              <div class="split-lists">
                <section class="file-column server-column design-card">
                  <header><div><h3>服务端 Mod</h3><p>不会下载到玩家客户端</p></div><span>{{ serverFiles.length }}</span></header>
                  <div class="list-tools"><input v-model="serverQuery" placeholder="搜索服务端文件"><button @click="toggleAll('server')">{{ allServerSelected ? '取消全选' : '全选结果' }}</button></div>
                  <div v-if="selectedServer.size" class="batch-bar"><b>已选 {{ selectedServer.size }}</b><button @click="batchSide('server', 2)">确认仅服务端</button><button @click="batchSide('server', 0)">移到同步端 →</button></div>
                  <div class="file-list">
                    <article v-for="file in filteredServerFiles" :key="file.path" class="file-item" :class="{ pending: file.side === 3 }">
                      <input type="checkbox" :checked="selectedServer.has(file.path)" @change="toggleFile('server', file.path)">
                      <div class="file-main"><strong :title="file.path">{{ fileName(file.path) }}</strong><small :title="file.path">{{ file.path }}</small><p>{{ file.reason }} · {{ runtimeSideName(file.runtimeSide) }}</p></div>
                      <div class="file-actions"><span :class="file.side === 3 ? 'pending-tag' : 'server-tag'">{{ sideName(file.side) }}</span><button title="移到同步 Mod" @click="changeSide(file, 0)">→</button></div>
                    </article>
                    <div v-if="!filteredServerFiles.length" class="empty compact">没有符合条件的服务端文件。</div>
                  </div>
                </section>

                <section class="file-column sync-column design-card">
                  <header><div><h3>同步 Mod</h3><p>由 PCL IO 同步给玩家</p></div><span>{{ syncFiles.length }}</span></header>
                  <div class="list-tools"><input v-model="syncQuery" placeholder="搜索同步文件"><button @click="toggleAll('sync')">{{ allSyncSelected ? '取消全选' : '全选结果' }}</button></div>
                  <div v-if="selectedSync.size" class="batch-bar"><b>已选 {{ selectedSync.size }}</b><button @click="batchSide('sync', 0)">设为必需</button><button @click="batchSide('sync', 1)">设为可选</button><button @click="batchSide('sync', 2)">← 移回服务端</button></div>
                  <div class="file-list">
                    <article v-for="file in filteredSyncFiles" :key="file.path" class="file-item">
                      <input type="checkbox" :checked="selectedSync.has(file.path)" @change="toggleFile('sync', file.path)">
                      <button class="move-back" title="移回服务端 Mod" @click="changeSide(file, 2)">←</button>
                      <div class="file-main"><strong :title="file.path">{{ fileName(file.path) }}</strong><small :title="file.path">{{ file.path }}</small><p>{{ formatSize(file.size) }} · {{ file.reason }} · {{ runtimeSideName(file.runtimeSide) }}</p></div>
                      <select class="side-select" :value="file.side" @change="selectSide(file, $event)"><option :value="0">客户端必需</option><option :value="1">玩家可选</option></select>
                    </article>
                    <div v-if="!filteredSyncFiles.length" class="empty compact">没有符合条件的同步文件。</div>
                  </div>
                </section>
              </div>
              <div class="sticky-actions"><span>{{ instancePending ? `还有 ${instancePending} 个文件待确认` : dirty ? '请保存当前分类' : '分类已保存，可以发布' }}</span><div><button class="button ghost" :disabled="!dirty || saving" @click="saveSettings(true)">保存分类</button><button class="button primary" :disabled="!canPublish || saving" @click="publishCurrent">确认并发布</button></div></div>
            </template>
          </section>

          <section v-if="pane === 'releases'" class="content-card">
            <div class="card-heading"><div><h3>最近正式版本</h3><p>每次发布都是固定快照，最多保留最近 5 个。</p></div><button class="button primary" :disabled="!canPublish || saving" @click="publishCurrent">发布当前扫描结果</button></div>
            <div v-if="hasRelease" class="release-list"><article v-for="(release, index) in current.settings.releases" :key="release"><span>{{ index === 0 ? '当前' : `历史 ${index}` }}</span><strong>{{ release }}</strong><small>{{ releaseTime(release) }}</small></article></div>
            <div v-else class="empty large"><div>◇</div><h3>还没有正式版本</h3><p>完成扫描和分类后即可发布。</p></div>
          </section>
        </section>
      </main>

      <main v-if="section === 'codes'" class="single-panel design-card">
        <div class="page-heading"><div><span class="eyebrow">ACCESS CODES</span><h2>同步码管理</h2><p>单实例码仅访问指定服务器；统一同步码可在 PCL IO 中选择任意已发布实例。</p></div><button class="button primary" :disabled="!instances.length" @click="openCodeDialog">＋ 创建同步码</button></div>
        <div class="code-tools"><select v-model="codeInstanceFilter"><option value="all">全部范围</option><option value="global">统一同步码</option><option v-for="item in instances" :key="item.id" :value="String(item.id)">{{ item.name }}</option></select><input v-model="codeQuery" placeholder="搜索玩家备注或适用范围"><span>{{ visibleCodes.length }} 条记录</span></div>
        <div class="code-grid">
          <article v-for="code in visibleCodes" :key="code.id" class="code-card design-card" :class="{ revoked: !code.enabled }">
            <div class="code-icon">#</div><div><div class="code-name"><strong>{{ code.name }}</strong><span :class="code.enabled ? 'enabled' : 'disabled'">{{ code.enabled ? '有效' : '已撤销' }}</span></div><p>{{ instanceName(code.instanceId) }}</p><small>创建于 {{ formatDate(code.createdAt) }} · ID {{ code.id.slice(0, 8) }}</small></div>
            <button v-if="code.enabled" class="danger-button" @click="revokeCode(code)">撤销</button><button v-else class="delete-button" @click="deleteCode(code)">删除记录</button>
          </article>
          <div v-if="!visibleCodes.length" class="empty large full"><div>⌘</div><h3>没有找到同步码</h3><p>创建后原始同步码只显示一次，请立即交给对应玩家。</p></div>
        </div>
      </main>

      <main v-if="section === 'guide'" class="single-panel guide design-card">
        <div class="page-heading"><div><span class="eyebrow">QUICK START</span><h2>四步完成客户端同步</h2><p>管理端只发布服务器文件；PCL IO 程序统一从 GitHub Releases 下载。</p></div><a class="button ghost" href="https://github.com/buziShui/iota-minecraft-sync/releases/latest" target="_blank" rel="noreferrer">打开 GitHub Releases</a></div>
        <div class="guide-grid"><article><b>01</b><h3>配置实例</h3><p>填写游戏版本和连接地址，再到独立的同步目录栏选择发布范围。</p></article><article><b>02</b><h3>停止并扫描</h3><p>停止服务端后扫描，系统会自动给出端侧分类和识别依据。</p></article><article><b>03</b><h3>检查并发布</h3><p>用双列表和批量选择快速整理 Mod，处理全部待确认项后发布。</p></article><article><b>04</b><h3>分发给玩家</h3><p>复制同步源根地址和玩家专属同步码，让玩家添加到 PCL IO。</p></article></div>
        <div class="source-card"><div><span>玩家填写的同步源根地址</span><strong>{{ sourceRoot }}</strong><small>支持 PassNat、FRP、端口映射、反向代理、内网地址及公网域名；不要附加 /api/plugins/... 路径。</small></div><button class="button primary" @click="copyText(sourceRoot, '同步源地址已复制')">复制地址</button></div>
      </main>
    </t-loading>

    <div v-if="codeDialogVisible" class="modal-backdrop" @click.self="codeDialogVisible = false">
      <section class="modal"><button class="modal-close" @click="codeDialogVisible = false">×</button><span class="eyebrow">NEW ACCESS CODE</span><h2>创建玩家同步码</h2><p>统一同步码可访问全部已发布实例；也可以继续创建仅绑定一个实例的同步码。</p><label><span>适用范围</span><select v-model="newCodeInstance"><option value="global">全部实例（统一同步码）</option><option v-for="item in instances" :key="item.id" :value="item.id">{{ item.name }}（单实例）</option></select></label><label><span>玩家备注</span><input v-model.trim="newCodeName" maxlength="64" placeholder="例如：小明" @keyup.enter="createCode"></label><div class="modal-actions"><button class="button ghost" @click="codeDialogVisible = false">取消</button><button class="button primary" :disabled="!newCodeName || saving" @click="createCode">创建</button></div></section>
    </div>

    <div v-if="rawCode" class="modal-backdrop">
      <section class="modal success-modal"><div class="success-mark">✓</div><h2>同步码已创建</h2><p>出于安全考虑，原始同步码只显示这一次。</p><div class="raw-code">{{ rawCode }}</div><div class="modal-actions"><button class="button ghost" @click="rawCode = ''">我已保存</button><button class="button primary" @click="copyText(rawCode, '同步码已复制')">复制同步码</button></div></section>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';
import { MessagePlugin } from 'tdesign-vue-next';
import hostRequest from 'mslx-request';

interface DirectoryRule { path: string; enabled: boolean }
interface ScanFile { path: string; size: number; sha256: string; side: number; reason: string; detectedSide?: number | null; runtimeSide?: number }
interface Settings { instanceId: number; directories: DirectoryRule[]; overrides: Record<string, number>; minecraftVersion: string; loader: string; loaderVersion: string; serverAddress: string; lastScan: ScanFile[]; releases: string[] }
type SettingsUpdate = Pick<Settings, 'instanceId' | 'directories' | 'overrides' | 'minecraftVersion' | 'loader' | 'loaderVersion' | 'serverAddress'>;
interface Instance { id: number; name: string; base: string; running: boolean; settings: Settings }
interface AccessCode { id: string; instanceId: number | null; global: boolean; name: string; enabled: boolean; createdAt: string }
interface DirectoryMeta { title: string; description: string; icon: string }
type ListKind = 'server' | 'sync';

const api = '/api/plugins/mslx-plugin-iota-sync/admin';
const loading = ref(false);
const saving = ref(false);
const instances = ref<Instance[]>([]);
const codes = ref<AccessCode[]>([]);
const selectedId = ref<number | null>(null);
const section = ref<'sync' | 'codes' | 'guide'>('sync');
const pane = ref<'config' | 'directories' | 'files' | 'releases'>('config');
const dirty = ref(false);
const serverQuery = ref('');
const syncQuery = ref('');
const selectedServer = ref(new Set<string>());
const selectedSync = ref(new Set<string>());
const codeQuery = ref('');
const codeInstanceFilter = ref('all');
const codeDialogVisible = ref(false);
const newCodeInstance = ref<number | 'global' | null>('global');
const newCodeName = ref('');
const rawCode = ref('');
const loaders = ['vanilla', 'forge', 'neoforge', 'fabric', 'quilt'];
const sourceRoot = window.location.origin;
const directoryMetadata: Record<string, DirectoryMeta> = {
  mods: { title: '模组目录', description: '客户端需要安装的必需或可选 Mod。', icon: '◆' },
  config: { title: '通用配置', description: '同步整合包约定的客户端配置文件。', icon: '⚙' },
  defaultconfigs: { title: '默认配置', description: '为新世界或新实例提供默认配置。', icon: '≋' },
  kubejs: { title: 'KubeJS 脚本', description: '同步配方、资源与客户端脚本。', icon: 'JS' },
  resourcepacks: { title: '资源包', description: '随整合包分发材质、语言与音效。', icon: '▧' },
  shaderpacks: { title: '光影包', description: '向玩家提供可自行启用的光影文件。', icon: '✦' }
};

const current = computed(() => instances.value.find(item => item.id === selectedId.value) || null);
const hasScan = computed(() => Boolean(current.value?.settings.lastScan.length));
const hasRelease = computed(() => Boolean(current.value?.settings.releases.length));
const enabledDirectoryCount = computed(() => current.value?.settings.directories.filter(directory => directory.enabled).length || 0);
const serverFiles = computed(() => current.value?.settings.lastScan.filter(file => file.side >= 2) || []);
const syncFiles = computed(() => current.value?.settings.lastScan.filter(file => file.side < 2) || []);
const filteredServerFiles = computed(() => filterFiles(serverFiles.value, serverQuery.value));
const filteredSyncFiles = computed(() => filterFiles(syncFiles.value, syncQuery.value));
const allServerSelected = computed(() => filteredServerFiles.value.length > 0 && filteredServerFiles.value.every(file => selectedServer.value.has(file.path)));
const allSyncSelected = computed(() => filteredSyncFiles.value.length > 0 && filteredSyncFiles.value.every(file => selectedSync.value.has(file.path)));
const instancePending = computed(() => current.value?.settings.lastScan.filter(file => file.side === 3).length || 0);
const instanceActiveCodes = computed(() => codes.value.filter(code => (code.instanceId === selectedId.value || code.global) && code.enabled).length);
const canPublish = computed(() => Boolean(current.value && !current.value.running && hasScan.value && !instancePending.value && !dirty.value));
const stoppedCount = computed(() => instances.value.filter(item => !item.running).length);
const totalFiles = computed(() => instances.value.reduce((sum, item) => sum + item.settings.lastScan.length, 0));
const pendingFiles = computed(() => instances.value.reduce((sum, item) => sum + item.settings.lastScan.filter(file => file.side === 3).length, 0));
const activeCodeCount = computed(() => codes.value.filter(code => code.enabled).length);
const visibleCodes = computed(() => {
  const query = codeQuery.value.trim().toLowerCase();
  return codes.value.filter(code => (codeInstanceFilter.value === 'all'
      || (codeInstanceFilter.value === 'global' ? code.global : String(code.instanceId) === codeInstanceFilter.value))
    && (!query || code.name.toLowerCase().includes(query) || instanceName(code.instanceId).toLowerCase().includes(query)));
});

function defaultSettings(id: number): Settings {
  return { instanceId: id, directories: ['mods', 'config', 'defaultconfigs', 'kubejs', 'resourcepacks', 'shaderpacks'].map((path, index) => ({ path, enabled: index === 0 })), overrides: {}, minecraftVersion: '', loader: '', loaderVersion: '', serverAddress: '', lastScan: [], releases: [] };
}

function normalizeInstance(item: Omit<Instance, 'settings'> & { settings?: Partial<Settings> | null }): Instance {
  const defaults = defaultSettings(item.id);
  const directories = normalizeDirectories(item.settings?.directories, defaults.directories);
  const lastScan = normalizeScanFiles(item.settings?.lastScan);
  return { ...item, settings: { ...defaults, ...(item.settings || {}), instanceId: item.id, directories, overrides: item.settings?.overrides || {}, lastScan, releases: [...new Set(item.settings?.releases || [])].slice(0, 5) } };
}

function normalizeDirectories(input: DirectoryRule[] | undefined, defaults = defaultSettings(0).directories): DirectoryRule[] {
  if (!input?.length) return defaults.map(item => ({ ...item }));
  const enabledByPath = new Map<string, boolean>();
  for (const item of input) {
    const path = item.path?.trim().toLowerCase();
    if (path) enabledByPath.set(path, Boolean(item.enabled));
  }
  return defaults.map(item => ({ path: item.path, enabled: enabledByPath.get(item.path.toLowerCase()) || false }));
}

function normalizeScanFiles(input: ScanFile[] | undefined): ScanFile[] {
  const unique = new Map<string, ScanFile>();
  for (const file of input || []) if (file.path?.trim()) unique.set(file.path.toLowerCase(), file);
  return [...unique.values()].sort((left, right) => left.path.localeCompare(right.path));
}

function normalizeSettings(settings: Settings) {
  settings.directories = normalizeDirectories(settings.directories);
  settings.lastScan = normalizeScanFiles(settings.lastScan);
}

async function request<T>(url: string, init?: RequestInit): Promise<T> {
  if (!hostRequest) throw new Error('未检测到 MSLX 宿主请求实例，请确认 MSLX 版本不低于 1.4.7');
  const method = (init?.method || 'GET').toLowerCase();
  const supportedMethods = ['get', 'post', 'put', 'delete', 'patch'];
  if (!supportedMethods.includes(method)) throw new Error(`不支持的请求方法：${method.toUpperCase()}`);

  const config: { url: string; data?: unknown; headers?: HeadersInit } = { url };
  if (init?.headers) config.headers = init.headers;
  if (init?.body !== undefined && init.body !== null) {
    if (typeof init.body === 'string') {
      try { config.data = JSON.parse(init.body); }
      catch { config.data = init.body; }
    } else {
      config.data = init.body;
    }
  }
  return await hostRequest[method](config) as T;
}

async function load() {
  loading.value = true;
  try {
    const [instanceData, codeData] = await Promise.all([request<Array<Omit<Instance, 'settings'> & { settings?: Partial<Settings> | null }>>(`${api}/instances`), request<AccessCode[]>(`${api}/codes`)]);
    instances.value = instanceData.map(normalizeInstance);
    codes.value = codeData;
    if (!instances.value.some(item => item.id === selectedId.value)) selectedId.value = instances.value[0]?.id ?? null;
    if (newCodeInstance.value === null) newCodeInstance.value = 'global';
  } catch (error) { showError(error); }
  finally { loading.value = false; }
}

function selectInstance(id: number) { selectedId.value = id; pane.value = 'config'; dirty.value = false; clearSelections(); }
function markDirty() { dirty.value = true; }
function clearSelections() { selectedServer.value = new Set(); selectedSync.value = new Set(); }
function editableSettings(settings: Settings): SettingsUpdate {
  return {
    instanceId: settings.instanceId,
    directories: settings.directories,
    overrides: settings.overrides,
    minecraftVersion: settings.minecraftVersion,
    loader: settings.loader,
    loaderVersion: settings.loaderVersion,
    serverAddress: settings.serverAddress
  };
}

async function saveSettings(notify: boolean, success = '实例配置已保存') {
  const item = current.value;
  if (!item) return;
  saving.value = true;
  try {
    normalizeSettings(item.settings);
    item.settings.overrides = buildOverrides(item.settings.lastScan);
    const saved = await request<Settings>(`${api}/instances/${item.id}`, { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(editableSettings(item.settings)) });
    item.settings = normalizeInstance({ ...item, settings: saved }).settings;
    dirty.value = false;
    if (notify) MessagePlugin.success(success);
  } catch (error) { showError(error); }
  finally { saving.value = false; }
}

async function scanCurrent() {
  const item = current.value;
  if (!item || item.running) return;
  saving.value = true;
  try {
    normalizeSettings(item.settings);
    item.settings.overrides = buildOverrides(item.settings.lastScan);
    await request(`${api}/instances/${item.id}`, { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(editableSettings(item.settings)) });
    item.settings.lastScan = await request<ScanFile[]>(`${api}/instances/${item.id}/scan`, { method: 'POST' });
    dirty.value = false; clearSelections(); pane.value = 'files';
    MessagePlugin.success(`扫描完成，共发现 ${item.settings.lastScan.length} 个文件`);
  } catch (error) { showError(error); }
  finally { saving.value = false; }
}

async function publishCurrent() {
  const item = current.value;
  if (!item || !canPublish.value) { MessagePlugin.warning(instancePending.value ? '请先处理全部待人工确认文件' : dirty.value ? '请先保存当前分类' : '当前状态不能发布'); return; }
  saving.value = true;
  try {
    const manifest = await request<{ releaseId: string }>(`${api}/instances/${item.id}/publish`, { method: 'POST' });
    MessagePlugin.success(`正式版本 ${manifest.releaseId} 发布成功`);
    await load(); pane.value = 'releases';
  } catch (error) { showError(error); }
  finally { saving.value = false; }
}

function filterFiles(files: ScanFile[], query: string) { const text = query.trim().toLowerCase(); return text ? files.filter(file => file.path.toLowerCase().includes(text) || file.reason.toLowerCase().includes(text)) : files; }
function buildOverrides(files: ScanFile[]) {
  return Object.fromEntries(files
    .filter(file => file.detectedSide == null || file.side !== file.detectedSide)
    .map(file => [file.path, file.side]));
}
function toggleFile(kind: ListKind, path: string) { const source = kind === 'server' ? selectedServer : selectedSync; const next = new Set(source.value); next.has(path) ? next.delete(path) : next.add(path); source.value = next; }
function toggleAll(kind: ListKind) {
  const files = kind === 'server' ? filteredServerFiles.value : filteredSyncFiles.value;
  const source = kind === 'server' ? selectedServer : selectedSync;
  const allSelected = files.length > 0 && files.every(file => source.value.has(file.path));
  const next = new Set(source.value); for (const file of files) allSelected ? next.delete(file.path) : next.add(file.path); source.value = next;
}
function changeSide(file: ScanFile, side: number) { file.side = side; dirty.value = true; selectedServer.value.delete(file.path); selectedSync.value.delete(file.path); selectedServer.value = new Set(selectedServer.value); selectedSync.value = new Set(selectedSync.value); }
function selectSide(file: ScanFile, event: Event) { changeSide(file, Number((event.target as HTMLSelectElement).value)); }
function batchSide(kind: ListKind, side: number) { const selected = kind === 'server' ? selectedServer.value : selectedSync.value; for (const file of current.value?.settings.lastScan || []) if (selected.has(file.path)) file.side = side; dirty.value = true; clearSelections(); }

function openCodeDialog() { newCodeInstance.value = 'global'; newCodeName.value = ''; codeDialogVisible.value = true; }
async function createCode() {
  if (newCodeInstance.value === null || !newCodeName.value.trim()) return;
  saving.value = true;
  try {
    const instanceId = newCodeInstance.value === 'global' ? null : Number(newCodeInstance.value);
    const created = await request<{ code: string }>(`${api}/codes`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ name: newCodeName.value, instanceId }) });
    rawCode.value = created.code; codeDialogVisible.value = false;
    codes.value = await request<AccessCode[]>(`${api}/codes`);
  } catch (error) { showError(error); }
  finally { saving.value = false; }
}
async function revokeCode(code: AccessCode) {
  if (!window.confirm(`确认撤销“${code.name}”的同步码？撤销后无法恢复。`)) return;
  try { await request(`${api}/codes/${code.id}/revoke`, { method: 'POST' }); code.enabled = false; MessagePlugin.success('同步码已撤销'); }
  catch (error) { showError(error); }
}
async function deleteCode(code: AccessCode) {
  if (code.enabled || !window.confirm(`永久删除“${code.name}”的撤销记录？删除后列表中不再保留。`)) return;
  try { await request(`${api}/codes/${code.id}`, { method: 'DELETE' }); codes.value = codes.value.filter(item => item.id !== code.id); MessagePlugin.success('撤销记录已删除'); }
  catch (error) { showError(error); }
}

function sideName(side: number) { return ['客户端必需', '玩家可选', '仅服务端', '待人工确认'][side] || '未知'; }
function runtimeSideName(side?: number) { return ['运行端未知', '仅客户端运行', '仅服务端运行', '客户端与服务端运行'][side ?? 0] || '运行端未知'; }
function directoryMeta(path: string) { return directoryMetadata[path.toLowerCase()] || { title: path, description: '同步此目录中的客户端文件。', icon: '◇' }; }
function directoryFileCount(path: string) { const prefix = `${path.toLowerCase()}/`; return current.value?.settings.lastScan.filter(file => file.path.toLowerCase().startsWith(prefix)).length || 0; }
function fileName(path: string) { return path.split('/').pop() || path; }
function formatSize(size: number) { if (size < 1024) return `${size} B`; if (size < 1024 ** 2) return `${(size / 1024).toFixed(1)} KB`; return `${(size / 1024 ** 2).toFixed(1)} MB`; }
function formatDate(value: string) { return new Intl.DateTimeFormat('zh-CN', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value)); }
function releaseTime(value: string) { const match = /^(\d{4})(\d{2})(\d{2})(\d{2})(\d{2})(\d{2})(?:\d{3})?(?:-[a-f0-9]+)?$/.exec(value); return match ? `${match[1]}-${match[2]}-${match[3]} ${match[4]}:${match[5]}:${match[6]} UTC` : value; }
function instanceName(id: number | null) { return id === null ? '全部实例 · 统一同步码' : instances.value.find(item => item.id === id)?.name || `实例 #${id}`; }
async function copyText(value: string, success: string) {
  try {
    if (window.isSecureContext && navigator.clipboard?.writeText) {
      await navigator.clipboard.writeText(value);
    } else if (!copyTextWithSelection(value)) {
      throw new Error('浏览器拒绝复制');
    }
    MessagePlugin.success(success);
  } catch {
    try {
      if (!copyTextWithSelection(value)) throw new Error('浏览器拒绝复制');
      MessagePlugin.success(success);
    } catch {
      MessagePlugin.error('复制失败，请选中同步码后手动复制');
    }
  }
}

function copyTextWithSelection(value: string) {
  const textarea = document.createElement('textarea');
  const activeElement = document.activeElement instanceof HTMLElement ? document.activeElement : null;
  textarea.value = value;
  textarea.readOnly = true;
  textarea.setAttribute('aria-hidden', 'true');
  Object.assign(textarea.style, {
    position: 'fixed',
    inset: '0 auto auto -9999px',
    width: '1px',
    height: '1px',
    opacity: '0'
  });
  document.body.appendChild(textarea);
  textarea.focus();
  textarea.select();
  textarea.setSelectionRange(0, textarea.value.length);
  let copied = false;
  try { copied = document.execCommand('copy'); }
  finally {
    textarea.remove();
    activeElement?.focus();
  }
  return copied;
}
function showError(error: unknown) { MessagePlugin.error(error instanceof Error ? error.message : '操作失败，请稍后重试'); }

onMounted(load);
</script>

<style scoped>
.iota-page{--brand:var(--td-brand-color,#5b5bd6);--brand-light:var(--td-brand-color-1,#eef0ff);--surface:var(--td-bg-color-container,#fff);--page:var(--td-bg-color-page,#f4f6f9);--text:var(--td-text-color-primary,#1d2433);--muted:var(--td-text-color-secondary,#6b7280);--line:var(--td-component-border,#e7eaf0);min-height:100%;padding:22px;color:var(--text);background:radial-gradient(circle at 90% 0,rgba(91,91,214,.08),transparent 24%)}
button,input,select{font:inherit}.hero{display:flex;justify-content:space-between;gap:24px;align-items:center;padding:28px 30px;border:1px solid rgba(91,91,214,.16);border-radius:20px;background:linear-gradient(125deg,var(--surface),var(--brand-light));box-shadow:0 12px 38px rgba(40,48,90,.07)}.eyebrow{color:var(--brand);font-size:11px;font-weight:800;letter-spacing:.16em}.hero h1,.page-heading h2{margin:7px 0 6px;font-size:26px;line-height:1.2}.hero p,.page-heading p{margin:0;color:var(--muted)}.hero-actions{display:flex;gap:10px;flex-shrink:0}.button{display:inline-flex;align-items:center;justify-content:center;min-height:38px;padding:0 17px;border:1px solid transparent;border-radius:9px;font-weight:650;text-decoration:none;cursor:pointer;transition:.18s}.button:disabled{opacity:.45;cursor:not-allowed}.button.primary{color:#fff;background:var(--brand);box-shadow:0 6px 15px rgba(91,91,214,.2)}.button.primary:hover:not(:disabled){filter:brightness(1.06);transform:translateY(-1px)}.button.ghost{color:var(--text);border-color:var(--line);background:var(--surface)}
.metrics{display:grid;grid-template-columns:repeat(4,1fr);gap:13px;margin:15px 0}.metrics article{position:relative;padding:17px 19px;border:1px solid var(--line);border-radius:14px;background:var(--surface);overflow:hidden}.metrics article::after{content:"";position:absolute;right:-12px;bottom:-22px;width:60px;height:60px;border-radius:50%;background:var(--brand-light)}.metrics span,.metrics small{display:block;color:var(--muted);font-size:12px}.metrics strong{display:block;margin:4px 0 2px;font-size:25px}.metrics .warning strong{color:#e37318}.section-tabs{display:flex;gap:5px;width:max-content;padding:4px;border:1px solid var(--line);border-radius:11px;background:var(--surface)}.section-tabs button,.panel-tabs button{border:0;color:var(--muted);background:transparent;cursor:pointer}.section-tabs button{padding:9px 15px;border-radius:8px}.section-tabs button.active{color:var(--brand);background:var(--brand-light);font-weight:700}.section-tabs b{padding:1px 6px;border-radius:10px;background:rgba(91,91,214,.12);font-size:11px}
.workspace{display:grid;grid-template-columns:238px minmax(0,1fr);gap:14px;margin-top:14px}.instance-rail,.instance-panel,.single-panel{border:1px solid var(--line);border-radius:16px;background:var(--surface)}.instance-rail{align-self:start;padding:10px}.rail-title{display:flex;justify-content:space-between;align-items:center;padding:7px 8px 12px}.rail-title span,.rail-title small{display:block}.rail-title span{font-weight:750}.rail-title small{margin-top:2px;color:var(--muted);font-size:11px}.icon-button{width:31px;height:31px;border:1px solid var(--line);border-radius:8px;color:var(--muted);background:transparent;cursor:pointer}.instance-button{display:grid;grid-template-columns:9px 1fr 12px;gap:10px;align-items:center;width:100%;padding:12px 10px;border:0;border-radius:10px;text-align:left;color:var(--text);background:transparent;cursor:pointer}.instance-button:hover{background:var(--page)}.instance-button.active{background:var(--brand-light)}.instance-button span strong,.instance-button span small{display:block;overflow:hidden;text-overflow:ellipsis;white-space:nowrap}.instance-button span small{margin-top:3px;color:var(--muted);font-size:11px}.instance-button i{color:var(--muted);font-style:normal}.status-dot{width:8px;height:8px;border-radius:50%}.status-dot.stopped{background:#22a06b;box-shadow:0 0 0 3px rgba(34,160,107,.12)}.status-dot.running{background:#e34d59;box-shadow:0 0 0 3px rgba(227,77,89,.12)}
.instance-panel{min-width:0;overflow:hidden}.instance-head{display:flex;justify-content:space-between;align-items:center;padding:20px 23px;border-bottom:1px solid var(--line)}.head-line{display:flex;gap:10px;align-items:center}.head-line h2{margin:0;font-size:21px}.instance-head p{max-width:600px;margin:5px 0 0;overflow:hidden;color:var(--muted);font-size:12px;text-overflow:ellipsis;white-space:nowrap}.status-pill{padding:3px 8px;border-radius:20px;font-size:11px}.status-pill.stopped{color:#087a50;background:#e7f7f1}.status-pill.running{color:#b4232e;background:#fff0f1}.release-badge{text-align:right}.release-badge small,.release-badge strong{display:block}.release-badge small{color:var(--muted);font-size:11px}.release-badge strong{margin-top:4px;font-family:ui-monospace,monospace;font-size:13px}.flow{display:flex;align-items:center;padding:16px 23px;background:var(--page)}.flow span{display:flex;gap:7px;align-items:center;color:var(--muted);font-size:12px;white-space:nowrap}.flow span b{display:grid;width:22px;height:22px;place-items:center;border:1px solid var(--line);border-radius:50%;background:var(--surface);font-size:11px}.flow span.done{color:var(--text)}.flow span.done b{border-color:var(--brand);color:#fff;background:var(--brand)}.flow i{flex:1;height:1px;margin:0 9px;background:var(--line)}.panel-tabs{display:flex;padding:0 22px;border-bottom:1px solid var(--line)}.panel-tabs button{position:relative;padding:15px 17px}.panel-tabs button.active{color:var(--brand);font-weight:700}.panel-tabs button.active::after{content:"";position:absolute;right:15px;bottom:-1px;left:15px;height:2px;background:var(--brand)}.panel-tabs em{margin-left:4px;padding:1px 5px;border-radius:9px;color:#fff;background:#e37318;font-size:10px;font-style:normal}
.content-card,.files-section{padding:22px}.card-heading,.scan-toolbar,.page-heading{display:flex;justify-content:space-between;gap:18px;align-items:center}.card-heading h3,.scan-toolbar h3{margin:0 0 4px;font-size:16px}.card-heading p,.scan-toolbar p{margin:0;color:var(--muted);font-size:12px}.form-grid{display:grid;grid-template-columns:repeat(2,1fr);gap:16px;margin-top:20px}.form-grid label>span,.modal label>span{display:block;margin-bottom:7px;font-size:12px;font-weight:650}.form-grid input,.form-grid select,.modal input,.modal select,.code-tools input,.code-tools select,.list-tools input,.side-select{width:100%;height:38px;padding:0 11px;border:1px solid var(--line);border-radius:8px;outline:none;color:var(--text);background:var(--surface)}.form-grid input:focus,.form-grid select:focus,.modal input:focus,.modal select:focus,.list-tools input:focus{border-color:var(--brand);box-shadow:0 0 0 3px rgba(91,91,214,.1)}.directory-overview{display:flex;justify-content:space-between;gap:24px;align-items:flex-end}.directory-overview h3{margin:7px 0 5px;font-size:18px}.directory-overview p{max-width:720px;margin:0;color:var(--muted);font-size:12px;line-height:1.7}.directory-total{display:flex;gap:6px;align-items:baseline;white-space:nowrap}.directory-total strong{color:var(--brand);font-size:32px}.directory-total span{color:var(--muted);font-size:12px}.directory-grid{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:12px;margin-top:20px}.directory-card{position:relative;display:grid;grid-template-columns:46px minmax(0,1fr) auto;gap:13px;align-items:center;padding:16px;border:1px solid var(--line);border-radius:13px;background:color-mix(in srgb,var(--surface) 94%,transparent);cursor:pointer;transition:.18s}.directory-card:hover{border-color:color-mix(in srgb,var(--brand) 42%,var(--line));transform:translateY(-1px)}.directory-card.active{border-color:color-mix(in srgb,var(--brand) 55%,var(--line));background:color-mix(in srgb,var(--brand-light) 72%,var(--surface))}.directory-card input{position:absolute;width:1px;height:1px;opacity:0;pointer-events:none}.directory-icon{display:grid;width:46px;height:46px;place-items:center;border-radius:12px;color:var(--muted);background:var(--page);font-weight:800}.directory-card.active .directory-icon{color:var(--brand);background:var(--surface);box-shadow:0 5px 14px rgba(91,91,214,.1)}.directory-content,.directory-title,.directory-description,.directory-files{display:block}.directory-title{display:flex;gap:8px;align-items:center}.directory-title strong{font-size:14px}.directory-title code,.directory-hint code{padding:2px 6px;border-radius:5px;color:var(--brand);background:rgba(91,91,214,.09);font-size:10px}.directory-description{margin-top:5px;color:var(--muted);font-size:11px}.directory-files{margin-top:7px;color:var(--muted);font-size:10px}.directory-state{padding:5px 8px;border-radius:7px;color:var(--muted);background:var(--page);font-size:10px;white-space:nowrap}.directory-card.active .directory-state{color:var(--brand);background:var(--surface);font-weight:700}.directory-hint{display:flex;justify-content:space-between;gap:20px;align-items:center;margin-top:14px;padding:14px 16px;border:1px dashed var(--line);border-radius:11px;background:var(--page)}.directory-hint strong,.directory-hint p{display:inline}.directory-hint p,.directory-hint>span{margin:0;color:var(--muted);font-size:11px}.directory-hint>span{white-space:nowrap}.directory-actions{padding-top:2px}.action-row,.sticky-actions{display:flex;justify-content:space-between;align-items:center;margin-top:18px;color:var(--muted);font-size:12px}.action-row>div,.scan-toolbar>div:last-child,.sticky-actions>div{display:flex;gap:9px}.unsaved{color:#d46b08}.notice{margin-top:14px;padding:11px 13px;border-radius:8px;font-size:12px}.notice.warning{color:#9a5700;background:#fff5e5}
.file-summary{display:flex;gap:18px;align-items:center;margin:15px 0;padding:11px 14px;border-radius:10px;color:var(--muted);background:var(--page);font-size:12px}.file-summary b{color:var(--text)}.file-summary .danger{color:#c33}.split-lists{display:grid;grid-template-columns:1fr 1fr;gap:13px}.file-column{min-width:0;border:1px solid var(--line);border-radius:13px;overflow:hidden}.file-column>header{display:flex;justify-content:space-between;align-items:center;padding:14px 15px}.file-column>header h3{margin:0 0 3px;font-size:14px}.file-column>header p{margin:0;color:var(--muted);font-size:11px}.file-column>header>span{display:grid;width:29px;height:29px;place-items:center;border-radius:8px;font-weight:750}.server-column>header{background:#f7f8fa}.server-column>header>span{color:#53606f;background:#e9edf2}.sync-column>header{background:var(--brand-light)}.sync-column>header>span{color:var(--brand);background:rgba(91,91,214,.12)}.list-tools{display:flex;gap:7px;padding:10px;border-top:1px solid var(--line);border-bottom:1px solid var(--line)}.list-tools input{height:33px}.list-tools button,.batch-bar button{border:1px solid var(--line);border-radius:7px;color:var(--text);background:var(--surface);white-space:nowrap;cursor:pointer;font-size:11px}.batch-bar{display:flex;gap:5px;align-items:center;padding:8px 10px;color:var(--brand);background:var(--brand-light);font-size:11px}.batch-bar b{margin-right:auto}.batch-bar button{padding:5px 7px}.file-list{max-height:520px;overflow:auto}.file-item{display:flex;gap:9px;align-items:center;padding:10px;border-bottom:1px solid var(--line)}.file-item:last-child{border-bottom:0}.file-item:hover{background:var(--page)}.file-item.pending{background:#fffaf1}.file-main{min-width:0;flex:1}.file-main strong,.file-main small{display:block;overflow:hidden;text-overflow:ellipsis;white-space:nowrap}.file-main strong{font-size:12px}.file-main small,.file-main p{color:var(--muted);font-size:10px}.file-main p{margin:3px 0 0;overflow:hidden;text-overflow:ellipsis;white-space:nowrap}.file-actions{display:flex;gap:5px;align-items:center}.file-actions span{padding:3px 6px;border-radius:5px;font-size:10px;white-space:nowrap}.server-tag{color:#53606f;background:#e9edf2}.pending-tag{color:#a15c00;background:#fff0d5}.file-actions button,.move-back{width:27px;height:27px;border:1px solid var(--line);border-radius:6px;color:var(--brand);background:var(--surface);cursor:pointer}.move-back{flex:0 0 auto}.side-select{width:105px;height:29px;padding:0 5px;font-size:10px}.sticky-actions{position:sticky;bottom:0;padding:13px 14px;border:1px solid var(--line);border-radius:11px;background:color-mix(in srgb,var(--surface) 92%,transparent);box-shadow:0 8px 24px rgba(30,40,70,.1);backdrop-filter:blur(10px)}
.single-panel{margin-top:14px;padding:24px}.page-heading h2{font-size:23px}.code-tools{display:grid;grid-template-columns:220px 1fr auto;gap:10px;align-items:center;margin:20px 0 13px;padding:12px;border-radius:11px;background:var(--page)}.code-tools input,.code-tools select{height:36px}.code-tools span{color:var(--muted);font-size:12px}.code-grid{display:grid;grid-template-columns:repeat(2,1fr);gap:11px}.code-card{display:grid;grid-template-columns:38px 1fr auto;gap:12px;align-items:center;padding:15px;border:1px solid var(--line);border-radius:12px}.code-card.revoked{opacity:.72}.code-icon{display:grid;width:38px;height:38px;place-items:center;border-radius:10px;color:var(--brand);background:var(--brand-light);font-weight:800}.code-name{display:flex;gap:8px;align-items:center}.code-name span{padding:2px 6px;border-radius:8px;font-size:10px}.code-name .enabled{color:#087a50;background:#e7f7f1}.code-name .disabled{color:#777;background:#eee}.code-card p,.code-card small{margin:3px 0 0;color:var(--muted);font-size:11px}.danger-button,.delete-button{padding:6px 9px;border:1px solid #f1b6ba;border-radius:7px;color:#c62f3a;background:#fff7f7;cursor:pointer}.delete-button{border-color:#d8dce3;color:#596273;background:var(--surface)}.guide-grid{display:grid;grid-template-columns:repeat(4,1fr);gap:12px;margin-top:22px}.guide-grid article{padding:19px;border:1px solid var(--line);border-radius:13px;background:linear-gradient(145deg,var(--surface),var(--page))}.guide-grid b{color:var(--brand);font-size:12px}.guide-grid h3{margin:18px 0 7px;font-size:15px}.guide-grid p{margin:0;color:var(--muted);font-size:12px;line-height:1.7}.source-card{display:flex;justify-content:space-between;align-items:center;margin-top:14px;padding:18px 20px;border-radius:13px;color:#fff;background:linear-gradient(120deg,#4343ad,#6c63e8)}.source-card span,.source-card strong,.source-card small{display:block}.source-card span,.source-card small{opacity:.75;font-size:11px}.source-card strong{margin:5px 0;font-family:ui-monospace,monospace}.source-card .button.primary{color:var(--brand);background:#fff}.empty{text-align:center;color:var(--muted)}.empty.compact{padding:18px;font-size:12px}.empty.large{padding:50px 20px;border:1px dashed var(--line);border-radius:12px}.empty.large>div{font-size:27px}.empty.large h3{margin:8px 0 5px;color:var(--text)}.empty.large p{margin:0 0 14px}.empty.full{grid-column:1/-1}
.modal-backdrop{position:fixed;z-index:9999;inset:0;display:grid;place-items:center;padding:20px;background:rgba(14,20,33,.52);backdrop-filter:blur(4px)}.modal{position:relative;width:min(430px,100%);padding:25px;border-radius:16px;background:var(--surface);box-shadow:0 25px 70px rgba(0,0,0,.25)}.modal h2{margin:8px 0 6px}.modal>p{margin:0 0 18px;color:var(--muted);font-size:12px}.modal label{display:block;margin-top:13px}.modal-close{position:absolute;top:13px;right:13px;width:30px;height:30px;border:0;border-radius:8px;color:var(--muted);background:var(--page);cursor:pointer}.modal-actions{display:flex;justify-content:flex-end;gap:8px;margin-top:21px}.success-modal{text-align:center}.success-mark{display:grid;width:48px;height:48px;margin:0 auto 10px;place-items:center;border-radius:50%;color:#fff;background:#22a06b;font-size:22px}.raw-code{padding:14px;border:1px dashed var(--brand);border-radius:9px;overflow-wrap:anywhere;color:var(--brand);background:var(--brand-light);font-family:ui-monospace,monospace}.success-modal .modal-actions{justify-content:center}
@media(max-width:1100px){.metrics{grid-template-columns:repeat(2,1fr)}.split-lists,.directory-grid{grid-template-columns:1fr}.guide-grid{grid-template-columns:repeat(2,1fr)}}
@media(max-width:760px){.iota-page{padding:12px}.hero,.page-heading,.scan-toolbar,.source-card,.directory-overview,.directory-hint{align-items:flex-start;flex-direction:column}.hero-actions{width:100%}.hero-actions .button{flex:1}.workspace{grid-template-columns:1fr}.instance-rail{display:flex;gap:7px;overflow:auto}.rail-title{min-width:135px}.instance-button{min-width:170px}.metrics{grid-template-columns:1fr 1fr}.flow{overflow:auto}.form-grid,.code-grid,.guide-grid{grid-template-columns:1fr}.code-tools{grid-template-columns:1fr}.instance-head{align-items:flex-start;flex-direction:column;gap:12px}.release-badge{text-align:left}.file-summary{align-items:flex-start;flex-direction:column;gap:5px}.sticky-actions{align-items:flex-start;flex-direction:column;gap:10px}.panel-tabs{overflow:auto}.panel-tabs button{white-space:nowrap}.directory-card{grid-template-columns:40px minmax(0,1fr)}.directory-icon{width:40px;height:40px}.directory-state{grid-column:2;justify-self:start}}
</style>
