import { defineConfig } from 'vite';
import vue from '@vitejs/plugin-vue';
import path from 'path';
import createExternal from 'vite-plugin-external';
import cssInjectedByJsPlugin from 'vite-plugin-css-injected-by-js';
import UnoCSS from 'unocss/vite';

export default defineConfig({
  plugins: [
    createExternal({
      externals: {
        vue: 'Vue',
        'vue-router': 'VueRouter',
        pinia: 'Pinia',
        'tdesign-vue-next': 'TDesign',
        'mslx-request': 'mslxRequest'
      }
    }),

    vue({
      template: { compilerOptions: { hoistStatic: false } }
    }),
    UnoCSS({
      mode: 'vue-scoped'
    }),
    cssInjectedByJsPlugin(),
  ],

  server: {
    port: 5001,
    cors: true,
  },
  preview: {
    port: 5001,
    cors: true,
  },


  build: {
    lib: {
      entry: path.resolve(__dirname, 'src/pluginEntry.ts'),
      name: 'MslxPlugin',
      formats: ['es'],
      fileName: () => 'mslx-plugin-entry.js'
    },
    rollupOptions: {
      external: ['vue', 'vue-router', 'pinia', 'tdesign-vue-next', 'mslx-request'],
      output: {
        assetFileNames: 'assets/[name]-[hash].[ext]'
      }
    }
  }
});