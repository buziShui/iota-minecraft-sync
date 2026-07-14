<template>
  <div class="design-card flex flex-col bg-[var(--td-bg-color-container)]/80 rounded-xl border border-[var(--td-component-border)] shadow-sm p-5">

    <div class="flex justify-between items-center mb-4 pb-4 border-b border-zinc-200/60 dark:border-zinc-700/60">
      <div class="flex items-center gap-1.5 font-bold text-sm text-[var(--td-text-color-primary)] m-0">
        <span class="text-[var(--td-text-color-secondary)] text-base leading-none">🧩</span>
        插件扩充面板

        <span class="relative inline-flex px-1.5 py-0.5 rounded text-xs font-medium text-[var(--color-primary)] overflow-hidden">
    <span class="absolute inset-0 bg-[var(--color-primary)] opacity-10"></span>
    <span class="relative z-10">Plus</span>
  </span>
      </div>

      <button
          class="text-xs font-medium text-[var(--color-primary)] hover:bg-[var(--color-primary)]/10 bg-transparent border-none px-2 py-1 rounded transition-colors cursor-pointer"
          @click="handleAction"
      >
        测试按钮
      </button>
    </div>

    <div class="flex-1 min-h-[40px]">
      <template v-if="status === 2">
        <div class="flex flex-col gap-2">
          <div class="text-sm font-medium text-[var(--td-text-color-secondary)]">
            当前绑定实例：<span class="font-mono font-bold text-[var(--td-text-color-primary)]">{{ serverId }}</span>
          </div>

          <div class="p-3 bg-zinc-100 dark:bg-zinc-800 border border-zinc-200 dark:border-zinc-700 rounded-md text-xs text-zinc-600 dark:text-zinc-400 leading-relaxed shadow-sm">
            这是通过万能插槽 <code class="text-[var(--color-primary)] font-mono">PluginSlot</code> 动态渲染出来的外部卡片！
            它继承了宿主传来的 <span class="font-bold">status ({{ status }})</span> 和样式上下文。
          </div>
        </div>
      </template>

      <div v-else class="py-4 flex flex-col items-center justify-center gap-2 text-[var(--td-text-color-secondary)]">
        <span class="text-2xl opacity-50">💤</span>
        <span class="text-xs font-medium">实例休眠中，插件统计已暂停</span>
      </div>
    </div>

  </div>
</template>

<script setup>
import {MessagePlugin} from "tdesign-vue-next";

const props = defineProps({
  serverId: {
    type: Number,
    required: true
  },
  status: {
    type: Number,
    default: 0
  }
});

const handleAction = () => {
  MessagePlugin.info(`我是插件内部的逻辑！当前操作的服务器 ID 是: ${props.serverId}`);
};
</script>

<style scoped>
</style>