<template>
  <div>
    <t-dialog
        v-model:visible="visible"
        header="测试插件弹窗"
        attach="body"
        :footer="false"
    >
      <div class="flex flex-col gap-4">

        <div class="text-lg font-bold text-blue-500">
          🎉 恭喜！这是一个插件弹窗
        </div>

        <div class="bg-gray-100 p-3 rounded-md text-sm text-gray-700">
          当前接收到的 Server ID:
          <span class="text-red-500 font-mono font-bold">{{ serverId }}</span>
        </div>

        <button
            class="bg-blue-500 hover:bg-blue-600 text-white font-bold py-2 px-4 rounded transition-colors cursor-pointer"
            @click="doSomething"
        >
          点击执行测试逻辑
        </button>

      </div>
    </t-dialog>
  </div>
</template>

<script setup>
import { ref } from 'vue';
import {MessagePlugin} from "tdesign-vue-next";

const props = defineProps({
  serverId: Number
});

// 控制弹窗显示隐藏的变量
const visible = ref(false);

const doSomething = () => {
  MessagePlugin.info(`逻辑执行成功！正在处理实例: ${props.serverId}`);

  // 关掉弹窗
  visible.value = false;
};

// 暴露接口给宿主
defineExpose({
  open: () => {
    console.log('👉 [插件内部] open 方法被成功触发！');
    visible.value = true;
  }
});
</script>