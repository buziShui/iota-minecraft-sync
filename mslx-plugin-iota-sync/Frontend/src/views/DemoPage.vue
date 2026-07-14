<script setup lang="ts">
import { ref, computed } from 'vue';
import { MessagePlugin, DialogPlugin, NotifyPlugin } from 'tdesign-vue-next';
import {
  UserCircleIcon,
  CodeIcon,
  PlayCircleIcon,
  SlashIcon,
  LayersIcon,
  ApiIcon,
  ChatIcon,
  NotificationIcon,
  AppIcon,
  DashboardIcon
} from 'tdesign-icons-vue-next';

import request from 'mslx-request';

// 宿主状态透传
const stores = (window as any).MSLX_Stores;
const userStore = stores?.getUserStore?.();

const userInfo = computed(() => userStore?.userInfo || { name: '未登录', avatar: '' });
const loading = ref(false);
const jsonResult = ref<string>('// 点击上方按钮发起请求\n// 结果将在此处以 JSON 格式打印...');

// 接口透传测试逻辑
const fetchInstances = async () => {
  if (!request) {
    MessagePlugin.error('未检测到宿主的请求实例');
    jsonResult.value = JSON.stringify({ error: "mslx-request not found on window" }, null, 2);
    return;
  }

  try {
    loading.value = true;
    jsonResult.value = '// 正在通过宿主 Axios 拦截器拉取数据...\n// 自动携带 Token 中...';

    const res = await request.get({
      url: '/api/plugins/mslx-plugin-demo/demo'
    });

    jsonResult.value = JSON.stringify(res, null, 2);
    MessagePlugin.success('数据流拉取成功！');
  } catch (err: any) {
    console.error('[Plugin Request Error]', err);
    jsonResult.value = JSON.stringify({ error: err.message || '请求失败，请检查网络' }, null, 2);
  } finally {
    loading.value = false;
  }
};

// 宿主组件唤醒示例
const triggerMessage = () => {
  MessagePlugin.success('这是一条来自插件的 Message！完全复用了主项目的样式和层级。');
};

const triggerNotify = () => {
  NotifyPlugin.info({
    title: '原生状态通知',
    content: '通过共享 TDesign 上下文，插件发出的通知能与主项目完美队列，绝不遮挡。',
    duration: 3000,
    closeBtn: true,
  });
};

const triggerDialog = () => {
  const dialogIns = DialogPlugin.confirm({
    header: '危险操作预警 (模拟)',
    body: '此弹窗由外部 ESM 插件强行唤起。即便插件的 DOM 被销毁，该弹窗依然能稳定存在于 Body 顶层。',
    theme: 'danger',
    confirmBtn: '确认销毁',
    cancelBtn: '取消',
    onConfirm: () => {
      MessagePlugin.success('操作已执行');
      dialogIns.destroy();
    },
    onClose: () => {
      dialogIns.destroy();
    },
    onCancel: () => {
      dialogIns.destroy();
    }
  });
};
</script>

<template>
  <div class="mx-auto flex flex-col gap-6 text-[var(--td-text-color-primary)] pb-8 pt-6">

    <div class="design-card rounded-2xl glass-card border border-[var(--td-component-border)] shadow-sm p-8 flex flex-col md:flex-row items-center md:items-start">
      <t-avatar :image="userInfo.avatar" size="84px" class="shrink-0 mr-6! mb-4! md:mb-0 border-4 border-[var(--color-primary)] shadow-md">
        <template #icon><user-circle-icon /></template>
      </t-avatar>

      <div class="flex-grow text-center md:text-left flex flex-col justify-center ml-5!">
        <div class="flex flex-col md:flex-row md:items-center gap-3 mb-2">
          <h1 class="text-2xl font-extrabold m-0 tracking-wide flex items-center justify-center md:justify-start gap-2">
            欢迎回来，{{ userInfo.name }}
          </h1>
          <t-tag v-if="userStore" theme="success" variant="light-outline" size="small" shape="round">
            <template #icon><div class="w-1.5 h-1.5 rounded-full bg-[var(--color-success)] mr-1"></div></template>
            状态已同步
          </t-tag>
          <t-tag v-else theme="danger" variant="light" size="small" shape="round">未连接状态库</t-tag>
        </div>

        <div class="flex items-center justify-center md:justify-start gap-2 mb-3 text-[var(--color-primary)] font-bold tracking-widest text-xs uppercase">
          <dashboard-icon /> MSLX Plugin SDK v1.0 • 开发者工作台
        </div>

        <p class="text-[var(--td-text-color-secondary)] max-w-3xl m-0 leading-relaxed text-sm">
          本页面完全独立于主项目构建，通过 <span class="text-[var(--color-primary)] font-bold">Native ESM</span> 动态注入。在这里，你可以测试 API 连通性、全局 UI 调用以及宿主状态共享。
        </p>
      </div>
    </div>

    <div class="grid grid-cols-1 md:grid-cols-3 gap-4">
      <div class="list-item-anim design-card p-5 rounded-2xl glass-card border border-[var(--td-component-border)] flex items-start gap-4 transition-all hover:-translate-y-1 hover:shadow-md" style="animation-delay: 0s">
        <div class="w-12 h-12 shrink-0 flex items-center justify-center rounded-xl bg-primary-light text-[var(--color-primary)]">
          <slash-icon size="24px" />
        </div>
        <div>
          <h3 class="font-bold text-base m-0 mb-1">极致轻量</h3>
          <p class="text-xs text-[var(--td-text-color-secondary)] m-0 leading-normal">
            打包体积近乎为 0KB。插件内剔除了所有 Vue、TDesign 和 Axios 的源码，实现极速加载。
          </p>
        </div>
      </div>

      <div class="list-item-anim design-card p-5 rounded-2xl glass-card border border-[var(--td-component-border)] flex items-start gap-4 transition-all hover:-translate-y-1 hover:shadow-md" style="animation-delay: 0.1s">
        <div class="w-12 h-12 shrink-0 flex items-center justify-center rounded-xl bg-success-light text-[var(--color-success)]">
          <layers-icon size="24px" />
        </div>
        <div>
          <h3 class="font-bold text-base m-0 mb-1">生态共享</h3>
          <p class="text-xs text-[var(--td-text-color-secondary)] m-0 leading-normal">
            透传 <code>window.MSLX_Stores</code>。插件能实时响应主项目的主题切换以及权限变更。
          </p>
        </div>
      </div>

      <div class="list-item-anim design-card p-5 rounded-2xl glass-card border border-[var(--td-component-border)] flex items-start gap-4 transition-all hover:-translate-y-1 hover:shadow-md" style="animation-delay: 0.2s">
        <div class="w-12 h-12 shrink-0 flex items-center justify-center rounded-xl bg-warning-light text-[var(--color-warning)]">
          <api-icon size="24px" />
        </div>
        <div>
          <h3 class="font-bold text-base m-0 mb-1">无缝鉴权</h3>
          <p class="text-xs text-[var(--td-text-color-secondary)] m-0 leading-normal">
            共享宿主 <code>mslxRequest</code> 实例。插件发起的请求自动携带 Token 并享受报错拦截。
          </p>
        </div>
      </div>
    </div>

    <div class="grid grid-cols-1 lg:grid-cols-5 gap-6 mt-2">
      <div class="lg:col-span-3 flex flex-col design-card list-item-anim glass-card rounded-2xl border border-[var(--td-component-border)] shadow-sm p-6" style="animation-delay: 0.3s">
        <div class="mb-5 pb-4 border-b border-dashed border-[var(--td-border-level-2-color)] flex items-center justify-between">
          <div class="flex items-center gap-3">
            <div class="w-1.5 h-6 bg-[var(--color-primary)] rounded-full shadow-[0_0_8px_var(--color-primary-light)] opacity-90"></div>
            <div class="flex flex-col">
              <h2 class="text-lg font-bold text-[var(--td-text-color-primary)] m-0">宿主接口透传测试</h2>
              <span class="text-xs text-[var(--td-text-color-secondary)] mt-1 font-medium">验证 request.get() 的拦截器响应</span>
            </div>
          </div>
          <t-button theme="primary" @click="fetchInstances" :loading="loading" shape="round">
            <template #icon><play-circle-icon /></template>
            发起请求
          </t-button>
        </div>

        <div class="mb-3 flex items-center gap-2 text-sm text-[var(--td-text-color-primary)] font-bold">
          <code-icon class="text-[var(--color-primary)]" />
          <span class="bg-primary-light text-[var(--color-primary)] px-2 py-0.5 rounded text-xs font-mono">GET</span>
          /api/plugins/mslx-plugin-demo/demo 原始响应
        </div>

        <div class="relative w-full flex-grow rounded-xl border border-[var(--td-component-border)] bg-[#1e1e1e] overflow-hidden shadow-inner flex flex-col min-h-[260px]">
          <div class="flex items-center px-4 py-2.5 bg-[#2d2d2d] border-b border-[#404040]">
            <div class="flex gap-1.5">
              <div class="w-3 h-3 rounded-full bg-[#ff5f56]"></div>
              <div class="w-3 h-3 rounded-full bg-[#ffbd2e]"></div>
              <div class="w-3 h-3 rounded-full bg-[#27c93f]"></div>
            </div>
            <span class="ml-4 text-xs font-mono text-[#858585]">response-debugger.json</span>
            <span v-if="loading" class="ml-auto flex items-center gap-2 text-xs text-[var(--color-primary)]">
              <t-loading size="14px" /> 接收中...
            </span>
          </div>
          <div class="p-5 overflow-x-auto h-full flex-grow custom-scrollbar">
            <pre class="m-0 text-[13px] font-mono leading-relaxed text-[#56b6c2]" v-if="jsonResult.startsWith('//')"><code>{{ jsonResult }}</code></pre>
            <pre class="m-0 text-[13px] font-mono leading-relaxed text-[#d4d4d4]" v-else><code>{{ jsonResult }}</code></pre>
          </div>
        </div>
      </div>

      <div class="lg:col-span-2 flex flex-col design-card list-item-anim glass-card rounded-2xl border border-[var(--td-component-border)] shadow-sm p-6" style="animation-delay: 0.4s">
        <div class="mb-5 pb-4 border-b border-dashed border-[var(--td-border-level-2-color)] flex items-center gap-3">
          <div class="w-1.5 h-6 bg-[var(--color-warning)] rounded-full shadow-[0_0_8px_var(--color-warning-light)] opacity-90"></div>
          <div class="flex flex-col">
            <h2 class="text-lg font-bold text-[var(--td-text-color-primary)] m-0">宿主组件唤醒</h2>
            <span class="text-xs text-[var(--td-text-color-secondary)] mt-1 font-medium">无需导入组件库，直接调用挂载实例</span>
          </div>
        </div>

        <div class="flex flex-col gap-4 flex-grow justify-center">
          <div class="group relative bg-secondary-light border border-[var(--td-component-border)] p-4 rounded-xl hover:bg-[var(--td-bg-color-container)] transition-colors">
            <div class="flex justify-between items-center mb-3">
              <span class="font-bold flex items-center gap-2 text-sm"><chat-icon class="text-[var(--color-primary)]" /> Message 提示</span>
              <t-button size="small" variant="outline" @click="triggerMessage">触发</t-button>
            </div>
            <p class="text-xs text-[var(--td-text-color-secondary)] m-0 leading-relaxed">轻量级操作反馈，出现在页面顶部居中。</p>
          </div>

          <div class="group relative bg-secondary-light border border-[var(--td-component-border)] p-4 rounded-xl hover:bg-[var(--td-bg-color-container)] transition-colors">
            <div class="flex justify-between items-center mb-3">
              <span class="font-bold flex items-center gap-2 text-sm"><notification-icon class="text-[var(--color-success)]" /> Notification 通知</span>
              <t-button size="small" variant="outline" @click="triggerNotify">触发</t-button>
            </div>
            <p class="text-xs text-[var(--td-text-color-secondary)] m-0 leading-relaxed">携带标题和内容详情的侧边通知框。</p>
          </div>

          <div class="group relative bg-secondary-light border border-[var(--td-component-border)] p-4 rounded-xl hover:bg-[var(--td-bg-color-container)] transition-colors">
            <div class="flex justify-between items-center mb-3">
              <span class="font-bold flex items-center gap-2 text-sm"><layers-icon class="text-[var(--color-danger)]" /> Dialog 弹窗</span>
              <t-button size="small" variant="outline" theme="danger" @click="triggerDialog">触发</t-button>
            </div>
            <p class="text-xs text-[var(--td-text-color-secondary)] m-0 leading-relaxed">打断式高优弹窗，拥有最高级 Z-Index。</p>
          </div>
        </div>
      </div>
    </div>

    <div class="mt-8 flex justify-center opacity-60">
      <div class="flex items-center gap-2 text-xs font-mono text-[var(--td-text-color-secondary)] uppercase tracking-widest hover:text-[var(--color-primary)] transition-colors cursor-pointer">
        <app-icon /> MSLX Plugin Architecture v1.0
      </div>
    </div>

  </div>
</template>

<style scoped>
@unocss;
.bg-primary-light { background-color: color-mix(in srgb, var(--td-brand-color) 10%, transparent); }
.bg-success-light { background-color: color-mix(in srgb, var(--td-success-color) 10%, transparent); }
.bg-warning-light { background-color: color-mix(in srgb, var(--td-warning-color) 10%, transparent); }
.bg-secondary-light { background-color: color-mix(in srgb, var(--td-bg-color-secondarycontainer) 50%, transparent); }

/* 动画与滚动条优化 */
.list-item-anim {
  animation: slideUp 0.6s cubic-bezier(0.2, 0.8, 0.2, 1) backwards;
}

@keyframes slideUp {
  from { opacity: 0; transform: translateY(20px); }
  to { opacity: 1; transform: translateY(0); }
}

.custom-scrollbar::-webkit-scrollbar {
  width: 8px;
  height: 8px;
}
.custom-scrollbar::-webkit-scrollbar-track {
  background: rgba(0,0,0,0.1);
  border-radius: 4px;
}
.custom-scrollbar::-webkit-scrollbar-thumb {
  background: #4a4a4a;
  border-radius: 4px;
}
.custom-scrollbar::-webkit-scrollbar-thumb:hover {
  background: #666;
}
</style>